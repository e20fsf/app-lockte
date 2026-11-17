using Lockte.Hr.Data;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Hr;

/// <summary>The read side of การลา (SCR-30). Writes go through <see cref="LeaveRegistry"/>.</summary>
public sealed class LeaveDirectory(HrDbContext db, TimeProvider clock, IPayAccess pay)
{
    /// <summary>
    /// The จำนวนวัน offered on the leave screen (<see cref="LeaveDayCounter"/>) from the dates, the times and the calendar's
    /// วันหยุดประจำสัปดาห์ and นักขัตฤกษ์ between them; null while the dates are missing or in the wrong order.
    /// </summary>
    public async Task<double?> CalculateDaysAsync(DateOnly? from, DateOnly? to, TimeOnly? fromTime, TimeOnly? toTime)
    {
        if (from is not { } start || to is not { } end || start > end) return null;
        var weekly = await db.WeeklyHolidays.AsNoTracking().Where(h => h.Date >= start && h.Date <= end).Select(h => h.Date).ToListAsync();
        var holidays = await db.PublicHolidays.AsNoTracking().Where(h => h.Date >= start && h.Date <= end).Select(h => h.Date).ToListAsync();
        return LeaveDayCounter.Count(start, end, fromTime, toTime, weekly.Concat(holidays).ToHashSet());
    }

    /// <summary>
    /// One employee's rows whose FROM_D is in <paramref name="year"/>, by FROM_D then entry — subform LEAVE1 of SCR-30
    /// (OrderBy FROM_D, id), which showed the legacy LEAVE table of the current year (one table for every year, #41).
    /// </summary>
    public async Task<IReadOnlyList<LeaveDto>> ListOfEmployeeAsync(int employeeId, int year)
    {
        var canViewPay = pay.CanViewPay; // #277: ค่าแรง/วัน and จำนวนเงิน are ข้อมูลค่าจ้าง
        return await OfYear(db.Leaves.AsNoTracking(), employeeId, year)
            .OrderBy(l => l.FromDate).ThenBy(l => l.Id)
            .Select(l => new LeaveDto(l.Id, l.EmployeeId, l.Employee.Code, l.RecordedOn, l.FromDate, l.ToDate, l.FromTime, l.ToTime,
                l.Days, l.Type, l.Paid, l.Remark, canViewPay ? l.DailyWage : 0, canViewPay ? l.Pay : 0, l.RowVersion))
            .ToListAsync();
    }

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
        var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
        var quotas = LeaveQuota.Limits.Select(l => new LeaveQuotaLine(l.Name, l.Used(days), l.Types[0] switch
        {
            "1" => LeaveQuota.PersonalLimit(employee.StartDate, today), // 3 until a year is worked (#347)
            "6" => LeaveQuota.VacationEntitlement(employee.StartDate, today),
            _ => l.Days,
        }, l.Types[0] switch
        {
            "6" when !hasVacation => "ยังไม่มีพักร้อน",
            "4" when employee.Sex != "F" => "เพศชายลาคลอดไม่ได้",
            _ => null,
        })).ToList();
        return new LeaveSummary(year, days, byType.Sum(t => t.Days), pay.CanViewPay ? byType.Sum(t => t.Pay) : 0, quotas);
    }

    private static IQueryable<Leave> OfYear(IQueryable<Leave> leaves, int employeeId, int year)
        => leaves.Where(l => l.EmployeeId == employeeId && l.FromDate >= new DateOnly(year, 1, 1) && l.FromDate <= new DateOnly(year, 12, 31));

    /// <summary>The rows that use the BR-020 quota of a year: ได้ค่าแรง (SAL = Y), FROM_D in the calendar year (Q-27).</summary>
    internal static IQueryable<Leave> PaidOfYear(IQueryable<Leave> leaves, int employeeId, int year)
        => OfYear(leaves, employeeId, year).Where(l => l.Paid == true);
}
