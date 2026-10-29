using Lockte.Calib.Data;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;

namespace Lockte.Calib;

public static class CalibModule
{
    public static IServiceCollection AddCalibModule(this IServiceCollection services, string connectionString)
    {
        services.AddModuleDbContext<CalibDbContext>(connectionString, CalibDbContext.Schema);
        services.AddScoped<CalibSettingsDirectory>();
        services.AddScoped<CalibSettingsRegistry>();
        services.AddScoped<InstrumentDirectory>();
        services.AddScoped<InstrumentRegistry>();
        return services;
    }

    /// <summary>Brings schema <c>calib</c> up to date (called by the host after <c>audit</c> and <c>org</c>).</summary>
    public static async Task MigrateAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<CalibDbContext>().Database.MigrateAsync();
    }

    /// <summary>For tools that fill the database but leave migrating to the host.</summary>
    public static async Task<bool> IsUpToDateAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        return !(await scope.ServiceProvider.GetRequiredService<CalibDbContext>().Database.GetPendingMigrationsAsync()).Any();
    }
}

/// <summary>For <c>dotnet ef migrations add … -p src/Lockte.Calib</c> only (no database connection is made).</summary>
internal sealed class CalibDbContextFactory : IDesignTimeDbContextFactory<CalibDbContext>
{
    public CalibDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<CalibDbContext>();
        options.UseLockteSqlServer(@"Server=(localdb)\MSSQLLocalDB;Database=Lockte;Integrated Security=true",
            CalibDbContext.Schema, new AuditInterceptor(new SystemUser(), TimeProvider.System, new AuditScope()));
        return new CalibDbContext(options.Options);
    }
}
