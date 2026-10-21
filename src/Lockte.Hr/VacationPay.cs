using Lockte.Hr.Data;
using Lockte.Org;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Hr;

/// <summary>
/// BR-031 ค่าพักร้อน (SCR-51, มาโคร คำนวณพักร้อน → คิวรี คำนวณพักร้อน1–8), with the legacy's ⚠ kept (Q-30):
/// <list type="bullet">
/// <item>ใช้ไป = Σ SUM_D of the employee's leaves of <b>every</b> SR with FROM_D ≥ from and TO_D ≤ to — คิวรี 1 groups by
/// (ID, FROM_D, TO_D, SUM_D), so rows equal in these count once whatever their SR.</item>
/// <item>who: not resigned, รหัส not LT* (Like "lt*" ignores case), ใช้ไป &lt; 6 or no leave at all, and
/// Year(วันนี้) − Year(START) &gt; 1 (คิวรี 3–4: years only, no START = no พักร้อน).</item>
/// <item>ค่าพักร้อน = (6 − ใช้ไป) × SA_DAY of today, unrounded (คิวรี 8).</item>
/// </list>
/// The legacy also wrote the lines into EMPLOYEEค่าพักร้อน (with ACC); nothing reads that table, so it is not kept — the
/// report is computed each time it is printed. คิวรี 8 had no ORDER BY (the legacy printed the rows in no set order);
/// here they are by รหัส.
/// </summary>
public sealed class VacationPayDirectory(HrDbContext db, OrgDirectory org, TimeProvider clock)
{
    public async Task<VacationPayReport> GetReportAsync(DateOnly from, DateOnly to)
    {
        var thisYear = clock.GetLocalNow().Year;
        var people = await db.Employees.AsNoTracking()
            .Where(e => e.ResignDate == null && !e.Code.StartsWith("LT") && e.StartDate != null)
            .Select(e => new { e.Id, e.Code, e.Name, e.DepartmentId, e.DailyWage, StartDate = e.StartDate!.Value })
            .ToListAsync();
        var leaves = await db.Leaves.AsNoTracking()
            .Where(l => l.FromDate >= from && l.ToDate <= to && l.Employee.ResignDate == null)
            .Select(l => new { l.EmployeeId, l.FromDate, l.ToDate, l.Days })
            .Distinct()
            .ToListAsync();
        var used = leaves.GroupBy(l => l.EmployeeId).ToDictionary(g => g.Key, g => g.Sum(l => l.Days));
        var departments = (await org.GetDepartmentsAsync()).ToDictionary(d => d.Id, d => d.Name);

        var lines = people
            .Where(p => VacationPay.IsEntitled(p.StartDate, thisYear) && used.GetValueOrDefault(p.Id) < VacationPay.Entitlement)
            .OrderBy(p => p.Code, StringComparer.Ordinal)
            .Select(p => new VacationPayLine(p.Code, p.Name, p.DepartmentId is { } d ? departments.GetValueOrDefault(d) : null, p.DailyWage,
                used.GetValueOrDefault(p.Id)))
            .ToList();
        return new VacationPayReport(from, to, lines);
    }
}

/// <summary>The constants of BR-031.</summary>
public static class VacationPay
{
    /// <summary>สิทธิพักร้อน 6 วัน (คิวรี 3 "&lt; 6", คิวรี 8 "6 − ใช้ไป").</summary>
    public const double Entitlement = 6;

    /// <summary>Format(Now(),'yyyy') − Format(START,'yyyy') &gt; 1: the calendar years only (started ธ.ค. 2024 → entitled in ม.ค. 2026).</summary>
    public static bool IsEntitled(DateOnly startDate, int thisYear) => thisYear - startDate.Year > 1;
}
