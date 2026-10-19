using Lockte.Hr;
using Lockte.Org;
using Lockte.Sales;
using Lockte.Web.Accounts;
using Lockte.Web.Audit;
using Lockte.Web.Reports;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Web;

public static class LockteDatabase
{
    /// <summary>
    /// Brings the database up to date: <c>audit</c> first, because every other schema writes its history there.
    /// Modules add their DbContext here once they have migrations.
    /// </summary>
    public static async Task MigrateAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AuditDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<AccountsDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<ReportsDbContext>().Database.MigrateAsync();
        await OrgModule.MigrateAsync(services);
        await HrModule.MigrateAsync(services); // after org: its foreign keys point there
        await SalesModule.MigrateAsync(services);
    }
}
