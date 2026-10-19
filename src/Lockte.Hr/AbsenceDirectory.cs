using Lockte.Hr.Data;
using Lockte.Org;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Hr;

/// <summary>The read side of ไม่มาทำงาน: SCR-10's rows of a day and รายงานไม่มาทำงานประจำวัน (RPT-17, BR-061).</summary>
public sealed class AbsenceDirectory(HrDbContext db, OrgDirectory org)
{
    /// <summary>BR-061 splits the shifts at 17:00: W_IN before = day shift, after = night shift (exactly 17:00 = neither ⚠).</summary>
    public static readonly TimeOnly NightShiftAfter = new(17, 0);

    public async Task<IReadOnlyList<AbsenceDto>> ListAsync(DateOnly date)
        => await db.Absences.AsNoTracking().Where(a => a.Date == date)
            .OrderBy(a => a.Employee.Code)
            .Select(a => new AbsenceDto(a.Id, a.Date, a.EmployeeId, a.Employee.Code, a.Employee.Name, a.WorkIn, a.Remark, a.RowVersion))
            .ToListAsync();

    /// <summary>
    /// BR-061 (มาโคร ไม่มาทำงาน): absences of <paramref name="date"/> with W_IN before 17:00, and of the day before with
    /// W_IN after 17:00, by date then code; <see cref="AbsenceReport.WorkingEmployees"/> counts everyone with RESIGN blank.
    /// </summary>
    public async Task<AbsenceReport> GetDailyReportAsync(DateOnly date)
    {
        var dayBefore = date.AddDays(-1);
        var rows = await db.Absences.AsNoTracking()
            .Where(a => (a.Date == date && a.WorkIn < NightShiftAfter) || (a.Date == dayBefore && a.WorkIn > NightShiftAfter))
            .OrderBy(a => a.Date).ThenBy(a => a.Employee.Code)
            .Select(a => new { a.Date, a.Employee.Code, a.Employee.Name, a.Employee.DepartmentId, a.Remark })
            .ToListAsync();
        var working = await db.Employees.CountAsync(e => e.ResignDate == null);
        var departments = (await org.GetDepartmentsAsync()).ToDictionary(d => d.Id, d => d.Name);
        var lines = rows.Select(r => new AbsenceReportLine(r.Date, r.Code, r.Name,
            r.DepartmentId is { } d ? departments.GetValueOrDefault(d) : null, r.Remark)).ToList();
        return new AbsenceReport(date, lines, working);
    }
}
