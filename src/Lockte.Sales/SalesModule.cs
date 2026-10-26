using Lockte.Sales.Data;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;

namespace Lockte.Sales;

public static class SalesModule
{
    public static IServiceCollection AddSalesModule(this IServiceCollection services, string connectionString)
    {
        services.AddModuleDbContext<SalesDbContext>(connectionString, SalesDbContext.Schema);
        services.AddScoped<CustomerDirectory>();
        services.AddScoped<CustomerRegistry>();
        services.AddScoped<SalesTerritoryDirectory>();
        services.AddScoped<SalesTerritoryRegistry>();
        services.AddScoped<SalesOrderDirectory>();
        services.AddScoped<SalesDeliveryReportDirectory>();
        services.AddScoped<SalesOrderRegistry>();
        services.AddScoped<SalesOrderEditor>();
        services.AddScoped<SalesDeliveryDirectory>();
        services.AddScoped<SalesDeliveryRegistry>();
        services.AddScoped<SalesDeliveryEditor>();
        return services;
    }

    /// <summary>Brings schema <c>sales</c> up to date (called by the host after <c>audit</c>).</summary>
    public static async Task MigrateAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<SalesDbContext>().Database.MigrateAsync();
    }

    /// <summary>For tools that fill the database but leave migrating to the host.</summary>
    public static async Task<bool> IsUpToDateAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        return !(await scope.ServiceProvider.GetRequiredService<SalesDbContext>().Database.GetPendingMigrationsAsync()).Any();
    }
}

/// <summary>
/// ลบข้อมูลทั้งหมดของ module ก่อนการย้ายข้อมูลรอบใหม่ (#47: ลบแล้วโหลดใหม่ทั้งชุด). For the data-migration tool only —
/// a bulk delete, so it writes no history.
/// </summary>
public sealed class SalesDataReset(SalesDbContext db)
{
    public async Task DeleteAllAsync()
    {
        // ORDER and deliveries refer to the customers: they go first.
        await db.Deliveries.ExecuteDeleteAsync();
        await db.SalesOrderLines.ExecuteDeleteAsync();
        await db.SalesOrders.ExecuteDeleteAsync();
        await db.Customers.ExecuteDeleteAsync();
    }
}

/// <summary>For <c>dotnet ef migrations add … -p src/Lockte.Sales</c> only (no database connection is made).</summary>
internal sealed class SalesDbContextFactory : IDesignTimeDbContextFactory<SalesDbContext>
{
    public SalesDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SalesDbContext>();
        options.UseLockteSqlServer(@"Server=(localdb)\MSSQLLocalDB;Database=Lockte;Integrated Security=true",
            SalesDbContext.Schema, new AuditInterceptor(new SystemUser(), TimeProvider.System, new AuditScope()));
        return new SalesDbContext(options.Options);
    }
}
