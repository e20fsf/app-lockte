using Lockte.SharedKernel.Persistence;
using Lockte.Web.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Lockte.Web.Tests;

/// <summary>
/// A fresh database on SQL Server LocalDB for the test run, built by the real migrations and dropped afterwards.
/// Override the server with env <c>LOCKTE_TEST_SQL</c> (a connection string without Database).
/// </summary>
public sealed class LocalDbFixture : IAsyncLifetime
{
    private const string DefaultServer = @"Server=(localdb)\MSSQLLocalDB;Integrated Security=true;TrustServerCertificate=true";

    private ServiceProvider services = null!;

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
        services = collection.BuildServiceProvider(validateScopes: true);

        await using var scope = Scope(SystemUser.Name);
        await scope.Get<AccountsDbContext>().Database.MigrateAsync();
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
