using Lockte.Hr.Data;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Hr;

/// <summary>A migrated ผลค่าแรง row (ค่าแรง / สำเนาของ ค่าแรง) of one employee, already checked by the migration tool.</summary>
public sealed record PayResultImport(int EmployeeId, double Rate, double Normal, double PaidLeave, double Overtime, int HolidayHours,
    decimal PayNormal, decimal PayOvertime, decimal Hot, int PublicHolidays, int WeeklyHolidays);

/// <summary>One migrated งวดค่าแรง with its results.</summary>
public sealed record PayPeriodImport(DateOnly From, DateOnly To, DateOnly PayDate, IReadOnlyList<PayResultImport> Results);

/// <summary>The results of one period in the table: rows and the sums RPT-03 prints.</summary>
public sealed record PayPeriodTotal(DateOnly From, int Rows, decimal PayNormal, decimal PayOvertime, decimal Hot);

/// <summary>
/// ย้ายข้อมูลผลค่าแรง (#51) into งวดค่าแรง and ค่าแรงของงวด. For the data-migration tool only, never registered in the web
/// app. A period that is already there (the open one, from ตั้งวันที่ตัดWEEK) keeps its step and gets the results; any
/// other is a closed period, created at the last step of เมนู 6. แผนก and ประเภท are the employee's today (the legacy
/// history keeps neither; its reports joined EMPLO when printing). No row-by-row history (#41).
/// </summary>
public sealed class HrPayHistoryImport(HrDbContext db, AuditScope audit)
{
    public async Task AddAsync(IEnumerable<PayPeriodImport> periods)
    {
        var employees = await db.Employees.AsNoTracking().ToDictionaryAsync(e => e.Id, e => new { e.DepartmentId, e.EmployeeType });
        using (audit.SuppressRowChanges())
        {
            foreach (var import in periods)
            {
                var period = await db.PayPeriods.FirstOrDefaultAsync(p => p.FromDate == import.From)
                    ?? db.PayPeriods.Add(new PayPeriod
                    {
                        FromDate = import.From, ToDate = import.To, PayDate = import.PayDate, CompletedStep = PayPeriodStep.SlipPrinted,
                    }).Entity;
                db.PayResults.AddRange(import.Results.Select(r => new PayResult
                {
                    PayPeriod = period, EmployeeId = r.EmployeeId, DepartmentId = employees[r.EmployeeId].DepartmentId,
                    EmployeeType = employees[r.EmployeeId].EmployeeType ?? 0, Rate = r.Rate, Normal = r.Normal, PaidLeave = r.PaidLeave,
                    Overtime = r.Overtime, HolidayHours = r.HolidayHours, PayNormal = r.PayNormal, PayOvertime = r.PayOvertime, Hot = r.Hot,
                    PublicHolidays = r.PublicHolidays, WeeklyHolidays = r.WeeklyHolidays,
                }));
                await db.SaveChangesAsync();
                db.ChangeTracker.Clear();
            }
        }
    }

    public Task<int> CountPayResultsAsync() => db.PayResults.CountAsync();

    /// <summary>The periods that have results, counted back for the row counts.</summary>
    public Task<int> CountPeriodsWithPayAsync() => db.PayPeriods.CountAsync(p => db.PayResults.Any(r => r.PayPeriodId == p.Id));

    /// <summary>Rows and sums of every period that has results, by จากวันที่.</summary>
    public async Task<IReadOnlyList<PayPeriodTotal>> GetPeriodTotalsAsync()
        => (await db.PayResults.AsNoTracking()
                .GroupBy(r => r.PayPeriod.FromDate)
                .Select(g => new PayPeriodTotal(g.Key, g.Count(), g.Sum(r => r.PayNormal), g.Sum(r => r.PayOvertime), g.Sum(r => r.Hot)))
                .ToListAsync())
            .OrderBy(t => t.From).ToList();
}
