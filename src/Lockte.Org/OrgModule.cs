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
