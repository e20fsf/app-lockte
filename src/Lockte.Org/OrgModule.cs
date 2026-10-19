using Lockte.Org.Data;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;

namespace Lockte.Org;

public static class OrgModule
{
    public static IServiceCollection AddOrgModule(this IServiceCollection services, string connectionString)
    {
        services.AddModuleDbContext<OrgDbContext>(connectionString, OrgDbContext.Schema);
        services.AddScoped<OrgDirectory>();
        services.AddScoped<OrgRegistry>();
        return services;
    }

    /// <summary>Brings schema <c>org</c> up to date (called by the host after <c>audit</c>).</summary>
    public static async Task MigrateAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<OrgDbContext>().Database.MigrateAsync();
    }

    /// <summary>For tools that fill the database but leave migrating to the host.</summary>
    public static async Task<bool> IsUpToDateAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        return !(await scope.ServiceProvider.GetRequiredService<OrgDbContext>().Database.GetPendingMigrationsAsync()).Any();
    }
}

/// <summary>
/// ลบข้อมูลทั้งหมดของ module ก่อนการย้ายข้อมูลรอบใหม่ (#47: ลบแล้วโหลดใหม่ทั้งชุด), including the กะ that the database
/// migration seeded — the tool loads them again from T-SHIFT. For the data-migration tool only: a bulk delete, so it
/// writes no history.
/// </summary>
public sealed class OrgDataReset(OrgDbContext db)
{
    public async Task DeleteAllAsync()
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.DepartmentShifts.ExecuteDeleteAsync();
        await db.Departments.ExecuteDeleteAsync();
        await db.Shifts.ExecuteDeleteAsync();
        await db.Sections.ExecuteDeleteAsync();
        await transaction.CommitAsync();
    }
}

/// <summary>For <c>dotnet ef migrations add … -p src/Lockte.Org</c> only (no database connection is made).</summary>
internal sealed class OrgDbContextFactory : IDesignTimeDbContextFactory<OrgDbContext>
{
    public OrgDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<OrgDbContext>();
        options.UseLockteSqlServer(@"Server=(localdb)\MSSQLLocalDB;Database=Lockte;Integrated Security=true",
            OrgDbContext.Schema, new AuditInterceptor(new SystemUser(), TimeProvider.System, new AuditScope()));
        return new OrgDbContext(options.Options);
    }
}
