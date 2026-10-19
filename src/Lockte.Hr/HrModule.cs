using Lockte.Hr.Data;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;

namespace Lockte.Hr;

public static class HrModule
{
    /// <summary>Needs module องค์กร registered too (แผนก and กะ come from its <c>OrgDirectory</c>).</summary>
    public static IServiceCollection AddHrModule(this IServiceCollection services, string connectionString)
    {
        services.AddModuleDbContext<HrDbContext>(connectionString, HrDbContext.Schema);
        services.AddScoped<EmployeeDirectory>();
        services.AddScoped<EmployeeRegistry>();
        services.AddScoped<CalendarDirectory>();
        services.AddScoped<CalendarRegistry>();
        services.AddScoped<AbsenceDirectory>();
        services.AddScoped<AbsenceRegistry>();
        services.AddScoped<WorkTimeDirectory>();
        services.AddScoped<WorkTimeRegistry>();
        return services;
    }

    /// <summary>Brings schema <c>hr</c> up to date (called by the host after <c>org</c>, which its foreign keys point to).</summary>
    public static async Task MigrateAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<HrDbContext>().Database.MigrateAsync();
    }
}

/// <summary>For <c>dotnet ef migrations add … -p src/Lockte.Hr</c> only (no database connection is made).</summary>
internal sealed class HrDbContextFactory : IDesignTimeDbContextFactory<HrDbContext>
{
    public HrDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<HrDbContext>();
        options.UseLockteSqlServer(@"Server=(localdb)\MSSQLLocalDB;Database=Lockte;Integrated Security=true",
            HrDbContext.Schema, new AuditInterceptor(new SystemUser(), TimeProvider.System, new AuditScope()));
        return new HrDbContext(options.Options);
    }
}
