using Lockte.Hr.Data;
using Lockte.Org;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Hr;

/// <summary>The read side of ใบเตือน: SCR-32's list of one employee and RPT-30. Writes go through <see cref="WarningRegistry"/>.</summary>
public sealed class WarningDirectory(HrDbContext db, OrgDirectory org)
{
    /// <summary>One employee's warnings by date, newest last (ฟอร์ม ใบเตือน2 → subform ใบเตือน1).</summary>
    public async Task<IReadOnlyList<WarningDto>> ListOfEmployeeAsync(int employeeId)
    {
        var rows = await db.Warnings.AsNoTracking().Where(w => w.EmployeeId == employeeId)
            .OrderBy(w => w.Date).ThenBy(w => w.Id)
            .Select(w => new { w.Id, w.Date, w.EmployeeId, w.Employee.Code, w.Employee.Name, w.DepartmentId, w.Level, w.Remark, w.RowVersion })
            .ToListAsync();
        var departments = await DepartmentNamesAsync();
        return rows.Select(w => new WarningDto(w.Id, w.Date, w.EmployeeId, w.Code, w.Name, w.DepartmentId,
            w.DepartmentId is { } d ? departments.GetValueOrDefault(d) : null, w.Level, w.Remark, w.RowVersion)).ToList();
    }

    /// <summary>
    /// RPT-30 (คิวรี ใบเตือน1, BR-064): the warnings of employees who have not resigned, by รหัส then วันที่ (the report's
    /// sorting). The legacy printed its current table — the warnings since the last ย้ายข้อมูลใบเตือน (BR-040); with one
    /// table for every year (#41) the dates choose them instead.
    /// </summary>
    public async Task<WarningReport> GetReportAsync(DateOnly from, DateOnly to)
    {
        var rows = await db.Warnings.AsNoTracking()
            .Where(w => w.Employee.ResignDate == null && w.Date >= from && w.Date <= to)
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
