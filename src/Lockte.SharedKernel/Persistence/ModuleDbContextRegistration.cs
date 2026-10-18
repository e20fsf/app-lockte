using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lockte.SharedKernel.Persistence;

public static class ModuleDbContextRegistration
{
    /// <summary>
    /// One database, one schema per module (ADR 0002): each module keeps its own migrations history in its schema
    /// and every save goes through <see cref="AuditStampInterceptor"/>.
    /// Needs <see cref="ICurrentUser"/> and <see cref="TimeProvider"/> registered by the host.
    /// </summary>
    public static IServiceCollection AddModuleDbContext<TContext>(
        this IServiceCollection services, string connectionString, string schema) where TContext : DbContext
    {
        services.AddDbContext<TContext>((sp, options) => options.UseLockteSqlServer(connectionString, schema, sp));
        return services;
    }

    public static DbContextOptionsBuilder UseLockteSqlServer(
        this DbContextOptionsBuilder options, string connectionString, string schema, IServiceProvider sp)
        => UseLockteSqlServer(options, connectionString, schema,
            new AuditStampInterceptor(sp.GetRequiredService<ICurrentUser>(), sp.GetRequiredService<TimeProvider>()));

    public static DbContextOptionsBuilder UseLockteSqlServer(
        this DbContextOptionsBuilder options, string connectionString, string schema, AuditStampInterceptor interceptor)
        => options
            .UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", schema))
            .AddInterceptors(interceptor);
}
