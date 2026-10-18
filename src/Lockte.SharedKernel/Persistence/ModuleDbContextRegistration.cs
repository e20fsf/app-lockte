using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Lockte.SharedKernel.Persistence;

public static class ModuleDbContextRegistration
{
    /// <summary>
    /// One database, one schema per module (ADR 0002): each module keeps its own migrations history in its schema
    /// and every save goes through <see cref="AuditInterceptor"/>.
    /// Needs <see cref="ICurrentUser"/> and <see cref="TimeProvider"/> registered by the host.
    /// </summary>
    public static IServiceCollection AddModuleDbContext<TContext>(
        this IServiceCollection services, string connectionString, string schema) where TContext : DbContext
    {
        services.TryAddScoped<AuditScope>();
        services.AddDbContext<TContext>((sp, options) => options.UseLockteSqlServer(connectionString, schema, sp));
        return services;
    }

    public static DbContextOptionsBuilder UseLockteSqlServer(
        this DbContextOptionsBuilder options, string connectionString, string schema, IServiceProvider sp)
        => UseLockteSqlServer(options, connectionString, schema,
            new AuditInterceptor(sp.GetRequiredService<ICurrentUser>(), sp.GetRequiredService<TimeProvider>(),
                sp.GetRequiredService<AuditScope>()));

    public static DbContextOptionsBuilder UseLockteSqlServer(
        this DbContextOptionsBuilder options, string connectionString, string schema, AuditInterceptor interceptor)
        => options
            .UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", schema))
            .AddInterceptors(interceptor);
}
