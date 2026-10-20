using Lockte.Hr.Data;
using Lockte.Org;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Hr;

/// <summary>
/// The rows of the leave reports of เมนู 17 (BR-064): RPT-31 ลางานทั้งแผนก (and its พิมพ์ลางานตามวันที่), RPT-32 ลางานรายคน,
/// RPT-33 สรุปการลางาน and RPT-34 ขาดงาน — the legacy queries' filters, joins and sums, ⚠ quirks included. The legacy read
/// its current LEAVE table; with one table for every year (#41) the dates the user types choose the rows.
/// </summary>
public sealed class LeaveReportDirectory(HrDbContext db, OrgDirectory org)
{
    /// <summary>ขาดงาน (SR 8).</summary>
    public const string Absent = "8";

    /// <summary>SR 1–15: รวม of RPT-31 (Expr1) and the columns of RPT-33 — SR 16 is left out of both.</summary>
    public static IReadOnlyList<string> TotalTypes { get; } = [.. LeaveTypes.Codes.Take(15)];

    /// <summary>
    /// RPT-31 (คิวรี พิมพ์ลางานทั้งแผนก1 → …10 → พิมพ์ลางานทั้งแผนก → A): every working employee of the แผนก — those with no
    /// leave print 0 (คิวรี 12 / 1111 add them) — with Σ SUM_D by SR of their leaves with FROM_D ≥ from and TO_D ≤ to
    /// (a leave across a date is left out), by รหัส (the report's sorting wins over the query's รวม มาก→น้อย).
    /// "ทุกแผนก" (DEP "*") is everyone with a แผนก: LIKE "*" is not true of a blank DEPARTMENT.
    /// </summary>
    /// <param name="employees">จำนวนคน typed on the form; null = the แผนก's working employees (DEP_AfterUpdate).</param>
    public async Task<DepartmentLeaveReport> GetDepartmentReportAsync(DateOnly from, DateOnly to, int? departmentId, int? employees, int? workDays)
    {
        var people = await Working(departmentId).Select(e => new { e.Id, e.Code, e.Name, e.StartDate }).ToListAsync();
        var leaves = await Within(from, to).Where(l => l.Employee.ResignDate == null && l.Employee.DepartmentId != null
                && (departmentId == null || l.Employee.DepartmentId == departmentId))
            .GroupBy(l => new { l.EmployeeId, l.Type })
            .Select(g => new { g.Key.EmployeeId, g.Key.Type, Days = g.Sum(l => l.Days) })
            .ToListAsync();
        var byEmployee = leaves.ToLookup(l => l.EmployeeId);
        var lines = people.OrderBy(p => p.Code, StringComparer.Ordinal)
            .Select(p => new DepartmentLeaveLine(p.Code, p.Name, p.StartDate,
                byEmployee[p.Id].Where(l => LeaveTypes.Names.ContainsKey(l.Type)).ToDictionary(l => l.Type, l => l.Days)))
            .ToList();
        return new DepartmentLeaveReport(from, to, await DepartmentNameAsync(departmentId), employees ?? people.Count, workDays, lines);
    }

    /// <summary>
    /// RPT-31 "พิมพ์ลางานตามวันที่" (คิวรี พิมพ์ลางานทั้งแผนก100): the leave rows of the same choice, by รหัส then วันที่.
    /// The macro's append to the สรุปการลา table (Q-45) changes nothing on this report.
    /// </summary>
    public async Task<LeaveListReport> GetListReportAsync(DateOnly from, DateOnly to, int? departmentId)
    {
        var rows = await Within(from, to).Where(l => l.Employee.ResignDate == null && l.Employee.DepartmentId != null
                && (departmentId == null || l.Employee.DepartmentId == departmentId))
            .Select(l => new { l.Id, l.FromDate, l.Employee.Code, l.Employee.Name, l.Days, l.Type, l.Employee.DepartmentId })
            .ToListAsync();
        var departments = await DepartmentNamesAsync();
        var lines = rows.OrderBy(r => r.Code, StringComparer.Ordinal).ThenBy(r => r.FromDate).ThenBy(r => r.Id)
            .Select(r => new LeaveListLine(r.Code, r.Name, r.Days, r.Type, r.DepartmentId is { } d ? departments.GetValueOrDefault(d) : null))
            .ToList();
        return new LeaveListReport(from, to, await DepartmentNameAsync(departmentId), lines);
    }

    /// <summary>
    /// RPT-32 (คิวรี ลางานรายคน): the employee's leaves with FROM_D within the dates, by FROM_D then TO_D. ⚠ kept: the query
    /// joins T-ประเภทการลางาน and T-YES,NO, so a leave with ได้ค่าแรง blank is not printed. Null when there is no such employee.
    /// </summary>
    public async Task<EmployeeLeaveReport?> GetEmployeeReportAsync(int employeeId, DateOnly from, DateOnly to)
    {
        var employee = await db.Employees.AsNoTracking().Where(e => e.Id == employeeId)
            .Select(e => new { e.Code, e.Name, e.StartDate, e.DailyWage }).FirstOrDefaultAsync();
        if (employee is null) return null;
        var rows = await db.Leaves.AsNoTracking()
            .Where(l => l.EmployeeId == employeeId && l.FromDate >= from && l.FromDate <= to && l.Paid != null)
            .OrderBy(l => l.FromDate).ThenBy(l => l.ToDate).ThenBy(l => l.Id)
            .Select(l => new { l.FromDate, l.ToDate, l.FromTime, l.ToTime, l.Days, l.Type, Paid = l.Paid!.Value, l.Pay, l.Remark })
            .ToListAsync();
        var lines = rows.Where(r => LeaveTypes.Names.ContainsKey(r.Type))
            .Select(r => new EmployeeLeaveLine(r.FromDate, r.ToDate, r.FromTime, r.ToTime, r.Days, LeaveTypes.Names[r.Type], r.Paid, r.Pay, r.Remark))
            .ToList();
        return new EmployeeLeaveReport(from, to, employee.Code, employee.Name, employee.StartDate, employee.DailyWage, lines);
    }

    /// <summary>
    /// RPT-33 (คิวรี พิมพ์สรุปการลางาน1 → 50–91 → 66–78 → 11), with FROM_D ≥ from and TO_D ≤ to:
    /// <list type="bullet">
    /// <item>แผนก = the employee's แผนก now, every leave counted whether the employee resigned or not; only the แผนก that
    /// have working employees are printed (inner join with คิวรี 67), each with its จำนวนพนักงาน.</item>
    /// <item>วันลา of an SR: Σ SUM_D of the distinct (ID, FROM_D, TO_D, SUM_D, SR, PAY) — ⚠ the GROUP BY of คิวรี 50…90 counts
    /// rows equal in these once (FROM_T is not in it).</item>
    /// <item>รวมจ่ายค่าแรง of a แผนก: Σ PAY of all its leaves (every SR, no distinct, คิวรี 68); of an SR (ท้ายรายงาน):
    /// Σ PAY of the distinct rows of every employee.</item>
    /// <item>by รวม SR 1–15 มาก→น้อย (the report's sorting), then แผนก.</item>
    /// </list>
    /// </summary>
    public async Task<LeaveSummaryReport> GetSummaryReportAsync(DateOnly from, DateOnly to, int? workDays)
    {
        var rows = await Within(from, to)
            .Select(l => new { l.EmployeeId, l.FromDate, l.ToDate, l.Days, l.Type, l.Pay, l.Employee.DepartmentId })
            .ToListAsync();
        var working = await db.Employees.AsNoTracking().Where(e => e.ResignDate == null && e.DepartmentId != null)
            .GroupBy(e => e.DepartmentId!.Value).Select(g => new { DepartmentId = g.Key, Count = g.Count() }).ToListAsync();
        var departments = await DepartmentNamesAsync();

        var distinct = rows.Where(r => TotalTypes.Contains(r.Type))
            .DistinctBy(r => (r.EmployeeId, r.FromDate, r.ToDate, r.Days, r.Type, r.Pay)).ToList();
        var lines = working.Where(w => departments.ContainsKey(w.DepartmentId))
            .Select(w => new LeaveSummaryLine(departments[w.DepartmentId], w.Count,
                distinct.Where(r => r.DepartmentId == w.DepartmentId).GroupBy(r => r.Type).ToDictionary(g => g.Key, g => g.Sum(r => r.Days)),
                rows.Where(r => r.DepartmentId == w.DepartmentId).Sum(r => r.Pay)))
            .OrderByDescending(l => l.Total).ThenBy(l => l.DepartmentName, StringComparer.Ordinal)
            .ToList();
        var payByType = distinct.GroupBy(r => r.Type).ToDictionary(g => g.Key, g => g.Sum(r => r.Pay));
        return new LeaveSummaryReport(from, to, workDays, lines, payByType);
    }

    /// <summary>
    /// RPT-34 (คิวรี ขาดงาน2): การลา SR 8 with FROM_D within the dates, of one employee or everyone (resigned too), by
    /// FROM_D then รหัส.
    /// </summary>
    public async Task<AbsentLeaveReport> GetAbsentReportAsync(DateOnly from, DateOnly to, int? employeeId)
    {
        var rows = await db.Leaves.AsNoTracking()
            .Where(l => l.Type == Absent && l.FromDate >= from && l.FromDate <= to && (employeeId == null || l.EmployeeId == employeeId))
            .Select(l => new { l.Id, l.FromDate, l.ToDate, l.Days, l.Employee.Code, l.Employee.Name })
            .ToListAsync();
        var code = employeeId is { } id ? await db.Employees.Where(e => e.Id == id).Select(e => e.Code).FirstOrDefaultAsync() : null;
        var lines = rows.OrderBy(r => r.FromDate).ThenBy(r => r.Code, StringComparer.Ordinal).ThenBy(r => r.Id)
            .Select(r => new AbsentLeaveLine(r.FromDate, r.ToDate, r.Days, r.Code, r.Name)).ToList();
        return new AbsentLeaveReport(from, to, code, lines);
    }

    /// <summary>FROM_D ≥ from and TO_D ≤ to (คิวรี พิมพ์ลางานทั้งแผนก1, พิมพ์สรุปการลางาน1).</summary>
    private IQueryable<Leave> Within(DateOnly from, DateOnly to)
        => db.Leaves.AsNoTracking().Where(l => l.FromDate >= from && l.ToDate <= to);

    /// <summary>Working employees of the แผนก; null = everyone with a แผนก.</summary>
    private IQueryable<Employee> Working(int? departmentId)
        => db.Employees.AsNoTracking().Where(e => e.ResignDate == null && e.DepartmentId != null
            && (departmentId == null || e.DepartmentId == departmentId));

    private async Task<string?> DepartmentNameAsync(int? departmentId)
        => departmentId is { } id ? (await org.GetDepartmentAsync(id))?.Name : null;

    private async Task<Dictionary<int, string>> DepartmentNamesAsync()
        => (await org.GetDepartmentsAsync()).ToDictionary(d => d.Id, d => d.Name);
}
