using Lockte.SharedKernel.Persistence;
using Lockte.Web.Accounts;
using Lockte.Web.Audit;
using Lockte.Web.Reports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Lockte.Web;

/// <summary>For <c>dotnet ef migrations add</c> only (no database connection is made).</summary>
internal static class DesignTime
{
    public static DbContextOptions<T> Options<T>(string schema) where T : DbContext
    {
        var options = new DbContextOptionsBuilder<T>();
        options.UseLockteSqlServer(@"Server=(localdb)\MSSQLLocalDB;Database=Lockte;Integrated Security=true",
            schema, new AuditInterceptor(new SystemUser(), TimeProvider.System, new AuditScope()));
        return options.Options;
    }
}

internal sealed class AccountsDbContextFactory : IDesignTimeDbContextFactory<AccountsDbContext>
{
    public AccountsDbContext CreateDbContext(string[] args) => new(DesignTime.Options<AccountsDbContext>(AccountsDbContext.Schema));
}

internal sealed class AuditDbContextFactory : IDesignTimeDbContextFactory<AuditDbContext>
{
    public AuditDbContext CreateDbContext(string[] args) => new(DesignTime.Options<AuditDbContext>(AuditDbContext.Schema));
}

internal sealed class ReportsDbContextFactory : IDesignTimeDbContextFactory<ReportsDbContext>
{
    public ReportsDbContext CreateDbContext(string[] args) => new(DesignTime.Options<ReportsDbContext>(ReportsDbContext.Schema));
}
