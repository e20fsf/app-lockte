using Lockte.Hr.Data;
using Lockte.Org;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Hr;

/// <summary>
/// The rows of the attendance reports (BR-066, BR-062): RPT-12 มาสาย, RPT-13 สรุปชั่วโมงการทำงาน, RPT-14 ทำล่วงเวลาจริง and
/// RPT-15 การมาทำงาน — the legacy queries' filters and sums. The legacy read E_WORK only, so rows already moved to ADD_EW
/// were not printed (⚠ BR-066); with one table for every year (#41) the dates choose the rows.
/// </summary>
public sealed class AttendanceReportDirectory(HrDbContext db, OrgDirectory org)
{
    /// <summary>
    /// RPT-12 (คิวรี มาสาย): rows with LATE set, DATE within the dates, of one employee or everyone (resigned too), by
    /// วันที่ then รหัส.
    /// </summary>
    public async Task<LateReport> GetLateReportAsync(DateOnly from, DateOnly to, int? employeeId)
    {
        var rows = await db.WorkTimes.AsNoTracking()
            .Where(w => w.Late != null && w.Date >= from && w.Date <= to && (employeeId == null || w.EmployeeId == employeeId))
            .Select(w => new { w.Id, w.Date, w.Employee.Code, w.Employee.Name, Late = w.Late!.Value })
            .ToListAsync();
        var lines = rows.OrderBy(r => r.Date).ThenBy(r => r.Code, StringComparer.Ordinal).ThenBy(r => r.Id)
            .Select(r => new LateReportLine(r.Date, r.Code, r.Name, r.Late)).ToList();
        return new LateReport(from, to, await CodeOfAsync(employeeId), lines);
    }

    /// <summary>
    /// RPT-13 (คิวรี สรุปชั่วโมงการทำงาน → 2): per working employee of the แผนก, over the งวดค่าแรง with FROM ≥ from and
    /// TO ≤ to, Σ W and Σ (TT_OT + OT) as the legacy history สำเนาของ ค่าแรง kept them: W and OT in Long columns, so each
    /// period's value rounded half to even (12.5 → 12, 13.5 → 14; checked on the legacy data), TT_OT = RND-A(S+O2+H+O3).
    /// Read from the time totals of each calculated period (BR-002): an employee BR-005 did not calculate has no row, as
    /// before. By รหัส.
    /// </summary>
    /// <param name="employees">จำนวนพนักงาน typed on the form; null = the แผนก's working employees.</param>
    public async Task<WorkHoursReport?> GetWorkHoursReportAsync(DateOnly from, DateOnly to, int departmentId, int? employees, int? workDays)
    {
        if (await org.GetDepartmentAsync(departmentId) is not { } department) return null;
        var totals = await db.PayPeriodTimeTotals.AsNoTracking()
            .Where(t => t.PayPeriod.FromDate >= from && t.PayPeriod.ToDate <= to
                && t.Employee.ResignDate == null && t.Employee.DepartmentId == departmentId)
            .Select(t => new { t.Employee.Code, t.Employee.Name, t.Normal, t.Overtime, t.Sunday, t.SundayOvertime, t.Holiday, t.HolidayOvertime })
            .ToListAsync();
        var lines = totals.GroupBy(t => (t.Code, t.Name))
            .OrderBy(g => g.Key.Code, StringComparer.Ordinal)
            .Select(g => new WorkHoursLine(g.Key.Code, g.Key.Name,
                g.Sum(t => Long(t.Normal)),
                g.Sum(t => RndA(t.Sunday + t.SundayOvertime + t.Holiday + t.HolidayOvertime) + Long(t.Overtime))))
            .ToList();
        var working = employees ?? await db.Employees.CountAsync(e => e.ResignDate == null && e.DepartmentId == departmentId);
        return new WorkHoursReport(from, to, department.Name, working, workDays, lines);
    }

    /// <summary>RPT-14 (คิวรี ทำล่วงเวลาจริง): the rows of <paramref name="date"/> whose TYPE is set and not W, by รหัส.</summary>
    public async Task<OvertimeReport> GetOvertimeReportAsync(DateOnly date)
    {
        var rows = await db.WorkTimes.AsNoTracking()
            .Where(w => w.Date == date && w.Type != null && w.Type != WorkTypes.Normal)
            .Select(w => new { w.Id, w.Date, w.Employee.Code, w.Employee.Name, w.Employee.DepartmentId, w.WorkIn, w.WorkOut })
            .ToListAsync();
        var departments = await DepartmentNamesAsync();
        var lines = rows.OrderBy(r => r.Code, StringComparer.Ordinal).ThenBy(r => r.Id)
            .Select(r => new OvertimeReportLine(r.Date, r.Code, r.Name, Name(departments, r.DepartmentId), r.WorkIn, r.WorkOut)).ToList();
        return new OvertimeReport(date, lines);
    }

    /// <summary>
    /// RPT-15 (คิวรี รายงานการมาทำงาน): the W rows within the dates, of one employee or everyone, by รหัส then วันที่.
    /// จำนวนวัน is WORK_T of BR-001 (the legacy printed the WORK_T stored by the last period calculation).
    /// </summary>
    public async Task<AttendanceReport> GetAttendanceReportAsync(DateOnly from, DateOnly to, int? employeeId)
    {
        var rows = await db.WorkTimes.AsNoTracking()
            .Where(w => w.Type == WorkTypes.Normal && w.Date >= from && w.Date <= to && (employeeId == null || w.EmployeeId == employeeId))
            .Select(w => new { w.Id, w.Date, w.Employee.Code, w.Employee.Name, w.Employee.DepartmentId, w.WorkIn, w.WorkOut, w.RestMinutes })
            .ToListAsync();
        var departments = await DepartmentNamesAsync();
        var lines = rows.OrderBy(r => r.Code, StringComparer.Ordinal).ThenBy(r => r.Date).ThenBy(r => r.Id)
            .Select(r => new AttendanceReportLine(r.Date, r.Code, r.Name, r.WorkIn, r.WorkOut,
                r.WorkIn is { } i && r.WorkOut is { } o ? WorkDuration.WorkT(WorkTypes.Normal, i, o, r.RestMinutes) : null,
                Name(departments, r.DepartmentId)))
            .ToList();
        return new AttendanceReport(from, to, await CodeOfAsync(employeeId), lines);
    }

    /// <summary>A Double stored in an Access Long column: rounded half to even.</summary>
    private static double Long(double value) => Math.Round(value, MidpointRounding.ToEven);

    /// <summary>RND-A <c>Val(Format(x,"#"))</c>: half away from zero.</summary>
    private static double RndA(double value) => Math.Round(value, MidpointRounding.AwayFromZero);

    private async Task<string?> CodeOfAsync(int? employeeId)
        => employeeId is { } id ? await db.Employees.Where(e => e.Id == id).Select(e => e.Code).FirstOrDefaultAsync() : null;

    private static string? Name(Dictionary<int, string> departments, int? id) => id is { } d ? departments.GetValueOrDefault(d) : null;

    private async Task<Dictionary<int, string>> DepartmentNamesAsync()
        => (await org.GetDepartmentsAsync()).ToDictionary(d => d.Id, d => d.Name);
}
