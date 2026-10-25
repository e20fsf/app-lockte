using Lockte.Hr.Data;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Hr;

/// <summary>
/// ลูกจ้างในแบบ คร.2 (RPT-57, BR-072, INT-05): the employee's own fields as the legacy query <c>แบบคร2</c> read them
/// from EMPLO, and the number of employees still working (the legacy form printed a fixed "200").
/// </summary>
public sealed record Kor2Employee(
    int Id, string Code, string Name, DateOnly? BirthDate, string? NationalId, string? RegisteredAddress, DateOnly? StartDate,
    string? Position, string? DepartmentName, double DailyWage, int WorkingEmployees);

/// <summary>
/// หนึ่งแถวของทะเบียนผู้ประกันตน สปส.6-07 (RPT-51, BR-073, INT-04). <see cref="Wage"/> = เงินเดือน ถ้าไม่เป็น 0
/// ไม่อย่างนั้น ค่าแรง/วัน.
/// </summary>
public sealed record InsuredPersonLine(
    string? NationalId, string Name, string Nationality, DateOnly? BirthDate, DateOnly? StartDate, string? Position, double Wage,
    DateOnly? ResignDate);

/// <summary>What the government forms of ทะเบียนพนักงาน print (#64): read-only, like <see cref="EmployeeDirectory"/>.</summary>
public sealed class GovernmentFormDirectory(HrDbContext db, EmployeeDirectory employees)
{
    /// <summary>The employee picked for แบบ คร.2, or null when there is none with this id.</summary>
    public async Task<Kor2Employee?> GetKor2Async(int employeeId)
    {
        if (await employees.GetAsync(employeeId) is not { } e) return null;
        var working = await db.Employees.CountAsync(x => x.ResignDate == null);
        return new Kor2Employee(e.Id, e.Code, e.Name, e.BirthDate, e.NationalId, e.RegisteredAddress, e.StartDate, e.Position,
            e.DepartmentName, e.DailyWage, working);
    }

    /// <summary>
    /// ทะเบียนผู้ประกันตน as the legacy query <c>ทะเบียนผู้ประกันตน</c>: every employee row including those who resigned
    /// (one person who came back has a row per employment), DISTINCT over the columns it read, ordered by เลขบัตร then
    /// วันเริ่มงาน — rows without a เลขบัตร first, as Access sorts Null first.
    /// </summary>
    public async Task<IReadOnlyList<InsuredPersonLine>> ListInsuredPersonsAsync()
    {
        var rows = await db.Employees.AsNoTracking().Where(e => !e.IsMigrationStub)
            .Select(e => new { e.NationalId, e.Name, e.Nationality, e.BirthDate, e.StartDate, e.Position, e.Salary, e.DailyWage, e.ResignDate })
            .ToListAsync();
        return rows.Distinct()
            .OrderBy(r => r.NationalId, StringComparer.Ordinal).ThenBy(r => r.StartDate)
            .Select(r => new InsuredPersonLine(r.NationalId, r.Name, r.Nationality, r.BirthDate, r.StartDate, r.Position,
                r.Salary != 0 ? r.Salary : r.DailyWage, r.ResignDate))
            .ToList();
    }
}
