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
        services.AddScoped<GovernmentFormDirectory>();
        services.AddScoped<CalendarDirectory>();
        services.AddScoped<CalendarRegistry>();
        services.AddScoped<AbsenceDirectory>();
        services.AddScoped<AbsenceRegistry>();
        services.AddScoped<WorkTimeDirectory>();
        services.AddScoped<WorkTimeRegistry>();
        services.AddScoped<OvertimeSheetRegistry>();
        services.AddScoped<LeaveDirectory>();
        services.AddScoped<LeaveRegistry>();
        services.AddScoped<PayPeriodDirectory>();
        services.AddScoped<PayPeriodRegistry>();
        services.AddScoped<SlipSignerRegistry>();
        services.AddScoped<HrDocumentDirectory>();
        services.AddScoped<ResignationRegistry>();
        services.AddScoped<OnboardingDirectory>();
        services.AddScoped<WarningDirectory>();
        services.AddScoped<WarningRegistry>();
        services.AddScoped<LeaveReportDirectory>();
        services.AddScoped<AttendanceReportDirectory>();
        return services;
    }

    /// <summary>Brings schema <c>hr</c> up to date (called by the host after <c>org</c>, which its foreign keys point to).</summary>
    public static async Task MigrateAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<HrDbContext>().Database.MigrateAsync();
    }

    /// <summary>For tools that fill the database but leave migrating to the host.</summary>
    public static async Task<bool> IsUpToDateAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        return !(await scope.ServiceProvider.GetRequiredService<HrDbContext>().Database.GetPendingMigrationsAsync()).Any();
    }
}

/// <summary>
/// ลบข้อมูลทั้งหมดของ module ก่อนการย้ายข้อมูลรอบใหม่ (#47: ลบแล้วโหลดใหม่ทั้งชุด). For the data-migration tool only:
/// a bulk delete, so it writes no history. Tables added later that point to an employee are deleted here first.
/// </summary>
public sealed class HrDataReset(HrDbContext db)
{
    public async Task DeleteAllAsync()
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.WeeklyHolidays.ExecuteDeleteAsync();
        await db.PublicHolidays.ExecuteDeleteAsync();
        await db.Warnings.ExecuteDeleteAsync();
        await db.Leaves.ExecuteDeleteAsync();
        await db.WorkTimes.ExecuteDeleteAsync();
        await db.Absences.ExecuteDeleteAsync();
        await db.PayResults.ExecuteDeleteAsync();
        await db.PayPeriodTimeTotals.ExecuteDeleteAsync();
        await db.PayPeriods.ExecuteDeleteAsync();
        await db.Employees.ExecuteDeleteAsync();
        await transaction.CommitAsync();
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
