using Lockte.Hr;
using Lockte.Migration;
using Lockte.Org;
using Lockte.Prod;
using Lockte.Sales;
using Lockte.SharedKernel.Persistence;
using Lockte.Web.Accounts;
using Lockte.Web.Reports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Lockte.Web.Tests;

/// <summary>
/// A fresh database on SQL Server LocalDB for the test run, built by the real migrations and dropped afterwards.
/// Override the server with env <c>LOCKTE_TEST_SQL</c> (a connection string without Database).
/// </summary>
public class LocalDbFixture : IAsyncLifetime
{
    private const string DefaultServer = @"Server=(localdb)\MSSQLLocalDB;Integrated Security=true;TrustServerCertificate=true";

    private ServiceProvider services = null!;

    public IServiceProvider Services => services;

    public async Task InitializeAsync()
    {
        var server = Environment.GetEnvironmentVariable("LOCKTE_TEST_SQL") ?? DefaultServer;
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"ConnectionStrings:{AccountsRegistration.ConnectionStringName}"] = $"{server};Database=LockteTests_{Guid.NewGuid():N}",
        }).Build();

        var collection = new ServiceCollection();
        collection.AddLogging();
        collection.AddScoped<TestCurrentUser>();
        collection.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<TestCurrentUser>());
        collection.AddLockteAccounts(config);
        collection.AddOrgModule(AccountsRegistration.ConnectionString(config));
        collection.AddHrModule(AccountsRegistration.ConnectionString(config));
        collection.AddSalesModule(AccountsRegistration.ConnectionString(config));
        collection.AddProdModule(AccountsRegistration.ConnectionString(config));
        collection.AddReportData(AccountsRegistration.ConnectionString(config));
        collection.AddMasterDataMigration();
        collection.AddModuleDbContext<WidgetDbContext>(AccountsRegistration.ConnectionString(config), WidgetDbContext.Schema);
        services = collection.BuildServiceProvider(validateScopes: true);

        await LockteDatabase.MigrateAsync(services);
        await using var scope = Scope(SystemUser.Name);
        await scope.Get<WidgetDbContext>().CreateTableAsync();
    }

    public async Task DisposeAsync()
    {
        await using (var scope = Scope(SystemUser.Name))
            await scope.Get<AccountsDbContext>().Database.EnsureDeletedAsync();
        await services.DisposeAsync();
    }

    /// <summary>One request / circuit acting as <paramref name="actingUser"/>.</summary>
    public TestScope Scope(string actingUser = "tester")
    {
        var scope = services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TestCurrentUser>().UserName = actingUser;
        return new TestScope(scope);
    }
}

public sealed class TestCurrentUser : ICurrentUser
{
    public string UserName { get; set; } = "tester";
}

public sealed class TestScope(AsyncServiceScope scope) : IAsyncDisposable
{
    public T Get<T>() where T : notnull => scope.ServiceProvider.GetRequiredService<T>();
    public ValueTask DisposeAsync() => scope.DisposeAsync();
}

[CollectionDefinition(Name)]
public sealed class LocalDbCollection : ICollectionFixture<LocalDbFixture>
{
    public const string Name = "LocalDB";
}

/// <summary>
/// The data-migration tool deletes everything it loads, so its tests get a database of their own (run one after
/// another, apart from the tests of <see cref="LocalDbCollection"/>).
/// </summary>
public sealed class MigrationDbFixture : LocalDbFixture;

[CollectionDefinition(Name)]
public sealed class MigrationDbCollection : ICollectionFixture<MigrationDbFixture>
{
    public const string Name = "Migration DB";
}
