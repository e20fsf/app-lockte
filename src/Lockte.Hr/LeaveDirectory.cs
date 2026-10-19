using Lockte.Hr.Data;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Hr;

/// <summary>The read side of การลา (SCR-30). Writes go through <see cref="LeaveRegistry"/>.</summary>
public sealed class LeaveDirectory(HrDbContext db, TimeProvider clock)
{
    /// <summary>
    /// One employee's rows whose FROM_D is in <paramref name="year"/>, by FROM_D then entry — subform LEAVE1 of SCR-30
    /// (OrderBy FROM_D, id), which showed the legacy LEAVE table of the current year (one table for every year, #41).
    /// </summary>
    public async Task<IReadOnlyList<LeaveDto>> ListOfEmployeeAsync(int employeeId, int year)
        => await OfYear(db.Leaves.AsNoTracking(), employeeId, year)
            .OrderBy(l => l.FromDate).ThenBy(l => l.Id)
            .Select(l => new LeaveDto(l.Id, l.EmployeeId, l.Employee.Code, l.RecordedOn, l.FromDate, l.ToDate, l.FromTime, l.ToTime,
                l.Days, l.Type, l.Paid, l.Remark, l.DailyWage, l.Pay, l.RowVersion))
            .ToListAsync();

    /// <summary>
    /// สรุปการลา (คิวรี สรุปการลา6 / subform สรุปการลา subform1): the ได้ค่าแรง days by SR of the calendar year, their
    /// total and Σ PAY, and each BR-020 limit with what is used and why there is no right at all (อายุงาน to today, SEX).
    /// </summary>
    public async Task<LeaveSummary> GetSummaryAsync(int employeeId, int year)
    {
        var byType = await PaidOfYear(db.Leaves.AsNoTracking(), employeeId, year)
            .GroupBy(l => l.Type)
            .Select(g => new { g.Key, Days = g.Sum(l => l.Days), Pay = g.Sum(l => l.Pay) })
            .ToListAsync();
        var employee = await db.Employees.AsNoTracking().Where(e => e.Id == employeeId)
            .Select(e => new { e.Sex, e.StartDate }).FirstAsync();
        var days = byType.ToDictionary(t => t.Key, t => t.Days);
        var hasVacation = LeaveQuota.HasVacation(employee.StartDate, DateOnly.FromDateTime(clock.GetLocalNow().DateTime));
        var quotas = LeaveQuota.Limits.Select(l => new LeaveQuotaLine(l.Name, l.Used(days), l.Days, l.Types[0] switch
        {
            "6" when !hasVacation => "ยังไม่มีพักร้อน",
            "4" when employee.Sex != "F" => "เพศชายลาคลอดไม่ได้",
            _ => null,
        })).ToList();
        return new LeaveSummary(year, days, byType.Sum(t => t.Days), byType.Sum(t => t.Pay), quotas);
    }

    private static IQueryable<Leave> OfYear(IQueryable<Leave> leaves, int employeeId, int year)
        => leaves.Where(l => l.EmployeeId == employeeId && l.FromDate >= new DateOnly(year, 1, 1) && l.FromDate <= new DateOnly(year, 12, 31));

    /// <summary>The rows that use the BR-020 quota of a year: ได้ค่าแรง (SAL = Y), FROM_D in the calendar year (Q-27).</summary>
    internal static IQueryable<Leave> PaidOfYear(IQueryable<Leave> leaves, int employeeId, int year)
        => OfYear(leaves, employeeId, year).Where(l => l.Paid == true);
}
