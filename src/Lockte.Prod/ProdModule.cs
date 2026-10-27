using Lockte.Prod.Data;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;

namespace Lockte.Prod;

public static class ProdModule
{
    public static IServiceCollection AddProdModule(this IServiceCollection services, string connectionString)
    {
        services.AddModuleDbContext<ProdDbContext>(connectionString, ProdDbContext.Schema);
        services.AddScoped<MoldDirectory>();
        services.AddScoped<MoldRegistry>();
        services.AddScoped<ProductDirectory>();
        services.AddScoped<ProductRegistry>();
        services.AddScoped<ProductListRegistry>();
        services.AddScoped<ProductionReportRegistry>();
        services.AddScoped<ProductionReportDirectory>();
        services.AddScoped<MoldUsageRegistry>();
        services.AddScoped<MoldUsageDirectory>();
        services.AddScoped<StockRegistry>();
        services.AddScoped<StockDirectory>();
        services.AddScoped<StockClosingRegistry>();
        services.AddScoped<StockClosingDirectory>();
        services.AddScoped<StockWithdrawalRegistry>();
        services.AddScoped<StockWithdrawalDirectory>();
        services.AddScoped<QaSpecRegistry>();
        services.AddScoped<QaSpecDirectory>();
        services.AddScoped<Lockte.Sales.IProductLookup, SalesProductLookup>();
        return services;
    }

    /// <summary>Brings schema <c>prod</c> up to date (called by the host after <c>audit</c>).</summary>
    public static async Task MigrateAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ProdDbContext>().Database.MigrateAsync();
    }

    /// <summary>For tools that fill the database but leave migrating to the host.</summary>
    public static async Task<bool> IsUpToDateAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        return !(await scope.ServiceProvider.GetRequiredService<ProdDbContext>().Database.GetPendingMigrationsAsync()).Any();
    }
}

/// <summary>
/// ลบข้อมูลหลักของ module ผลิตก่อนการย้ายข้อมูลรอบใหม่ (#107: ลบแล้วโหลดใหม่) — สินค้า with its lists, then แม่พิมพ์. For the
/// data-migration tool only: a bulk delete, so it writes no history. Tables that refer to these (ใบรายงานผลผลิต, สต็อก…)
/// are deleted before them by the tickets that add those tables.
/// </summary>
public sealed class ProdDataReset(ProdDbContext db)
{
    public async Task DeleteAllAsync()
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.MoldCutoffs.ExecuteDeleteAsync();
        await db.MoldUsages.ExecuteDeleteAsync();
        await db.StockEntries.ExecuteDeleteAsync();
        await db.ProductionReportLines.ExecuteDeleteAsync();
        await db.ProductionReports.ExecuteDeleteAsync();
        await db.Products.ExecuteDeleteAsync();
        await db.ProductTypes.ExecuteDeleteAsync();
        await db.BrakeTypes.ExecuteDeleteAsync();
        await db.ProductModels.ExecuteDeleteAsync();
        await db.ProductGroups.ExecuteDeleteAsync();
        await db.Molds.ExecuteDeleteAsync();
        await transaction.CommitAsync();
    }
}

/// <summary>For <c>dotnet ef migrations add … -p src/Lockte.Prod</c> only (no database connection is made).</summary>
internal sealed class ProdDbContextFactory : IDesignTimeDbContextFactory<ProdDbContext>
{
    public ProdDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ProdDbContext>();
        options.UseLockteSqlServer(@"Server=(localdb)\MSSQLLocalDB;Database=Lockte;Integrated Security=true",
            ProdDbContext.Schema, new AuditInterceptor(new SystemUser(), TimeProvider.System, new AuditScope()));
        return new ProdDbContext(options.Options);
    }
}
