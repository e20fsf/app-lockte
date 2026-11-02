using Lockte.Cash.Data;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;

namespace Lockte.Cash;

public static class CashModule
{
    public static IServiceCollection AddCashModule(this IServiceCollection services, string connectionString)
    {
        services.AddModuleDbContext<CashDbContext>(connectionString, CashDbContext.Schema);
        services.AddScoped<CashSettingsDirectory>();
        services.AddScoped<CashSettingsRegistry>();
        services.AddScoped<ExpenseTypeDirectory>();
        services.AddScoped<ExpenseTypeRegistry>();
        services.AddScoped<ExpenseLineDirectory>();
        services.AddScoped<ExpenseLineRegistry>();
        services.AddScoped<ScrapReceiptDirectory>();
        services.AddScoped<ScrapReceiptRegistry>();
        services.AddScoped<ScrapRemittanceDirectory>();
        services.AddScoped<ScrapRemittanceRegistry>();
        services.AddScoped<ScrapBuyerDirectory>();
        services.AddScoped<ScrapBuyerRegistry>();
        services.AddScoped<ScrapItemDirectory>();
        services.AddScoped<ScrapItemRegistry>();
        return services;
    }

    /// <summary>Brings schema <c>cash</c> up to date (called by the host after <c>audit</c>).</summary>
    public static async Task MigrateAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<CashDbContext>().Database.MigrateAsync();
    }

    /// <summary>For tools that fill the database but leave migrating to the host.</summary>
    public static async Task<bool> IsUpToDateAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        return !(await scope.ServiceProvider.GetRequiredService<CashDbContext>().Database.GetPendingMigrationsAsync()).Any();
    }
}

/// <summary>For <c>dotnet ef migrations add … -p src/Lockte.Cash</c> only (no database connection is made).</summary>
internal sealed class CashDbContextFactory : IDesignTimeDbContextFactory<CashDbContext>
{
    public CashDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<CashDbContext>();
        options.UseLockteSqlServer(@"Server=(localdb)\MSSQLLocalDB;Database=Lockte;Integrated Security=true",
            CashDbContext.Schema, new AuditInterceptor(new SystemUser(), TimeProvider.System, new AuditScope()));
        return new CashDbContext(options.Options);
    }
}
