using Lockte.Hr.Data;
using Lockte.Org;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Hr;

/// <summary>The read side of ใบเตือน: SCR-32's list of one employee and RPT-30. Writes go through <see cref="WarningRegistry"/>.</summary>
public sealed class WarningDirectory(HrDbContext db, OrgDirectory org)
{
    /// <summary>One employee's warnings by date, newest last (ฟอร์ม ใบเตือน2 → subform ใบเตือน1).</summary>
    /// <remarks>UAT 10: a warning moved by ย้ายข้อมูลใบเตือน is not shown (the legacy's ใบเตือนADD), see <see cref="CountMovedOfEmployeeAsync"/>.</remarks>
    public async Task<IReadOnlyList<WarningDto>> ListOfEmployeeAsync(int employeeId)
    {
        var rows = await db.Warnings.AsNoTracking().Where(w => w.EmployeeId == employeeId && !w.Moved)
            .OrderBy(w => w.Date).ThenBy(w => w.Id)
            .Select(w => new { w.Id, w.Date, w.EmployeeId, w.Employee.Code, w.Employee.Name, w.DepartmentId, w.Level, w.Remark, w.RowVersion })
            .ToListAsync();
        var departments = await DepartmentNamesAsync();
        return rows.Select(w => new WarningDto(w.Id, w.Date, w.EmployeeId, w.Code, w.Name, w.DepartmentId,
            w.DepartmentId is { } d ? departments.GetValueOrDefault(d) : null, w.Level, w.Remark, w.RowVersion)).ToList();
    }

    /// <summary>The warnings of the employee that ย้ายข้อมูลใบเตือน moved out of SCR-32.</summary>
    public Task<int> CountMovedOfEmployeeAsync(int employeeId) => db.Warnings.AsNoTracking().CountAsync(w => w.EmployeeId == employeeId && w.Moved);

    /// <summary>ย้ายข้อมูลใบเตือน (SCR-60, UAT 10): how many warnings dated in the range are not moved yet / moved.</summary>
    public async Task<MoveCount> CountForMoveAsync(DateOnly from, DateOnly to)
    {
        var counts = await db.Warnings.AsNoTracking().Where(w => w.Date >= from && w.Date <= to)
            .GroupBy(w => w.Moved).Select(g => new { g.Key, Count = g.Count() }).ToListAsync();
        return new(counts.FirstOrDefault(c => !c.Key)?.Count ?? 0, counts.FirstOrDefault(c => c.Key)?.Count ?? 0);
    }

    /// <summary>
    /// RPT-30 (คิวรี ใบเตือน1, BR-064): the warnings of employees who have not resigned, by รหัส then วันที่ (the report's
    /// sorting). The legacy printed its current table — the warnings since the last ย้ายข้อมูลใบเตือน (BR-040); with one
    /// table for every year (#41) the dates choose them instead.
    /// </summary>
    /// <remarks>
    /// <paramref name="employeeId"/> (UAT7-6, #467) prints one person's warnings instead of everyone's; the person is chosen
    /// on purpose, so a resigned employee is not left out.
    /// </remarks>
    public async Task<WarningReport> GetReportAsync(DateOnly from, DateOnly to, int? employeeId = null)
    {
        var rows = await db.Warnings.AsNoTracking()
            .Where(w => w.Date >= from && w.Date <= to
                && (employeeId == null ? w.Employee.ResignDate == null : w.EmployeeId == employeeId))
            .Select(w => new { w.Id, w.Date, w.Employee.Code, w.Employee.Name, w.DepartmentId, w.Level, w.Remark })
            .ToListAsync();
        var departments = await DepartmentNamesAsync();
        var lines = rows.OrderBy(w => w.Code, StringComparer.Ordinal).ThenBy(w => w.Date).ThenBy(w => w.Id)
            .Select(w => new WarningReportLine(w.Date, w.Code, w.Name, w.DepartmentId is { } d ? departments.GetValueOrDefault(d) : null,
                WarningLevels.NameOf(w.Level), w.Remark))
            .ToList();
        return new WarningReport(from, to, lines);
    }

    private async Task<Dictionary<int, string>> DepartmentNamesAsync()
        => (await org.GetDepartmentsAsync()).ToDictionary(d => d.Id, d => d.Name);
}
