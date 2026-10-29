using Lockte.Purch.Data;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;

namespace Lockte.Purch;

public static class PurchModule
{
    public static IServiceCollection AddPurchModule(this IServiceCollection services, string connectionString)
    {
        services.AddModuleDbContext<PurchDbContext>(connectionString, PurchDbContext.Schema);
        services.AddScoped<PurchSettingsDirectory>();
        services.AddScoped<PurchSettingsRegistry>();
        services.AddScoped<SupplierDirectory>();
        services.AddScoped<SupplierRegistry>();
        services.AddScoped<MaterialDirectory>();
        services.AddScoped<MaterialRegistry>();
        services.AddScoped<RequestSetupDirectory>();
        services.AddScoped<RequestSetupRegistry>();
        services.AddScoped<DocumentNumbers>();
        services.AddScoped<PurchaseRequestRegistry>();
        services.AddScoped<PurchaseRequestDirectory>();
        services.AddScoped<PurchaseOrderRegistry>();
        services.AddScoped<PurchaseOrderDirectory>();
        services.AddScoped<StockLedger>();
        services.AddScoped<StockDirectory>();
        services.AddScoped<StockIssueRegistry>();
        services.AddScoped<StockIssueDirectory>();
        services.AddScoped<GoodsReceiptRegistry>();
        services.AddScoped<GoodsReceiptDirectory>();
        services.AddScoped<StockClosingRegistry>();
        services.AddScoped<StockClosingDirectory>();
        services.AddScoped<PurchaseOrderCalculator>();
        return services;
    }

    /// <summary>Brings schema <c>purch</c> up to date (called by the host after <c>audit</c> and <c>org</c>).</summary>
    public static async Task MigrateAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<PurchDbContext>().Database.MigrateAsync();
    }

    /// <summary>For tools that fill the database but leave migrating to the host.</summary>
    public static async Task<bool> IsUpToDateAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        return !(await scope.ServiceProvider.GetRequiredService<PurchDbContext>().Database.GetPendingMigrationsAsync()).Any();
    }
}

/// <summary>For <c>dotnet ef migrations add … -p src/Lockte.Purch</c> only (no database connection is made).</summary>
internal sealed class PurchDbContextFactory : IDesignTimeDbContextFactory<PurchDbContext>
{
    public PurchDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PurchDbContext>();
        options.UseLockteSqlServer(@"Server=(localdb)\MSSQLLocalDB;Database=Lockte;Integrated Security=true",
            PurchDbContext.Schema, new AuditInterceptor(new SystemUser(), TimeProvider.System, new AuditScope()));
        return new PurchDbContext(options.Options);
    }
}
