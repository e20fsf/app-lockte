using Lockte.Hr.Data;
using Lockte.Org;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Hr;

/// <summary>
/// What the onboarding and probation documents (RPT-56, RPT-22) and the employee lists (RPT-21) print (#66): read-only,
/// like <see cref="EmployeeDirectory"/>. พนักงานยังไม่ผ่านทดลองงาน (RPT-20) is <see cref="EmployeeDirectory.ListOnProbationAsync"/>.
/// </summary>
public sealed class OnboardingDirectory(HrDbContext db, EmployeeDirectory employees, OrgDirectory org, TimeProvider clock)
{
    private DateOnly Today => DateOnly.FromDateTime(clock.GetLocalNow().DateTime);

    /// <summary>
    /// The employee with the names of their แผนก in their กะ (BR-072), or null when there is none with this id. ฝ่าย is the
    /// employee's own, else their แผนก's.
    /// </summary>
    public async Task<OnboardingEmployee?> GetEmployeeAsync(int employeeId)
    {
        if (await employees.GetAsync(employeeId) is not { } e) return null;
        var department = e.DepartmentId is { } d ? await org.GetDepartmentAsync(d) : null;
        var signers = department?.Shifts.FirstOrDefault(s => s.ShiftId == e.ShiftId);
        return new OnboardingEmployee
        {
            Id = e.Id, Code = e.Code, Name = e.Name, BirthDate = e.BirthDate, NationalId = e.NationalId, IdCardPlace = e.IdCardPlace,
            IdCardIssued = e.IdCardIssued, RegisteredAddress = e.RegisteredAddress, StartDate = e.StartDate, EmployeeType = e.EmployeeType,
            Salary = e.Salary, DailyWage = e.DailyWage, Position = e.Position, DepartmentName = e.DepartmentName,
            SectionName = e.SectionName ?? department?.SectionName, ShiftCode = e.ShiftCode, SupervisorName = signers?.SupervisorName,
            ManagerName = signers?.ManagerName, FactoryManagerName = signers?.FactoryManagerName,
        };
    }

    /// <summary>
    /// สถิติสำหรับใบประเมินทดลองงาน (BR-071): Sum(LEAVE.SUM_D) by SR and Count(E_WORK rows with LATE) of the employee — all
    /// their rows, as the legacy macro read its tables (repeated เวลาทำงาน rows count again, as there).
    /// </summary>
    public async Task<ProbationStatistics> GetProbationStatisticsAsync(int employeeId)
    {
        var leave = await db.Leaves.AsNoTracking().Where(l => l.EmployeeId == employeeId)
            .GroupBy(l => l.Type).Select(g => new { Type = g.Key, Days = g.Sum(l => l.Days) })
            .ToDictionaryAsync(g => g.Type, g => g.Days);
        var late = await db.WorkTimes.AsNoTracking().CountAsync(w => w.EmployeeId == employeeId && w.Late != null);
        var warnings = await db.Warnings.AsNoTracking()
            .Where(w => w.EmployeeId == employeeId && (w.Level == WarningLevels.Verbal || w.Level == WarningLevels.Written))
            .GroupBy(w => w.Level).Select(g => new { Level = g.Key!, Count = g.Count() })
            .ToDictionaryAsync(g => g.Level, g => g.Count);
        double Days(string type) => leave.GetValueOrDefault(type);
        return new ProbationStatistics(Days("1"), Days("2"), Days("3"), Days("4"), Days("5"), late, Days("8"),
            warnings.GetValueOrDefault(WarningLevels.Verbal), warnings.GetValueOrDefault(WarningLevels.Written));
    }

    /// <summary>
    /// รายชื่อพนักงานแผนก… (BR-073, query <c>รายชื่อเฉพาะแผนก7</c>): ยังไม่ลาออก in the แผนก and กะ (null = all, the legacy
    /// "*"), by รหัส. Title "รายชื่อพนักงานแผนก&lt;แผนก&gt;" or "รายชื่อพนักงานทั้งหมด", then "  กะ&lt;กะ&gt;" when one กะ.
    /// </summary>
    public async Task<DepartmentRoster> GetDepartmentRosterAsync(int? departmentId, int? shiftId)
    {
        var rows = await db.Employees.AsNoTracking()
            .Where(e => e.ResignDate == null)
            .Where(e => departmentId == null || e.DepartmentId == departmentId)
            .Where(e => shiftId == null || e.ShiftId == shiftId)
            .OrderBy(e => e.Code)
            .Select(e => new { e.Code, e.Name, e.StartDate, e.Position, e.BirthDate, e.Salary, e.DailyWage, e.Sex })
            .ToListAsync();
        var today = Today;
        var department = departmentId is { } d ? (await org.GetDepartmentAsync(d))?.Name : null;
        var shift = shiftId is { } s ? (await org.GetShiftsAsync()).FirstOrDefault(x => x.Id == s)?.Code : null;
        var title = (department is null ? "รายชื่อพนักงานทั้งหมด" : "รายชื่อพนักงานแผนก" + department) + (shift is null ? "" : "  กะ" + shift);
        var lines = rows.Select(e =>
        {
            int? service = e.StartDate is { } start ? MonthsBetween(start, today) : null;
            return new DepartmentRosterLine(e.Code, e.Name, e.StartDate, e.Position, service / 12, service % 12,
                e.BirthDate is { } born ? MonthsBetween(born, today) / 12 : null, e.Salary, e.DailyWage);
        }).ToList();
        return new DepartmentRoster(title, lines, rows.Count(e => e.Sex == "M"), rows.Count(e => e.Sex == "F"));
    }

    /// <summary>รายชื่อพนักงานทั้งหมด (report <c>EMPLO</c>, BR-073): ยังไม่ลาออก, by รหัส.</summary>
    public async Task<WorkingEmployeeList> GetWorkingEmployeesAsync()
    {
        var rows = await db.Employees.AsNoTracking().Where(e => e.ResignDate == null).OrderBy(e => e.Code)
            .Select(e => new { e.Code, e.Name, e.DepartmentId, e.Position, e.BirthDate, e.StartDate, e.EmployeeType, e.Hospital, e.Sex })
            .ToListAsync();
        var departments = (await org.GetDepartmentsAsync()).ToDictionary(d => d.Id, d => d.Name);
        var today = Today;
        var lines = rows.Select(e => new WorkingEmployeeLine(e.Code, e.Name, e.DepartmentId is { } d ? departments.GetValueOrDefault(d) : null,
            e.Position, e.BirthDate, e.StartDate, e.EmployeeType,
            e.BirthDate is { } born ? (today.DayNumber - born.DayNumber) / 365.0 : null, e.Hospital)).ToList();
        return new WorkingEmployeeList(lines, rows.Count(e => e.Sex == "M"), rows.Count(e => e.Sex == "F"));
    }

    /// <summary>ทะเบียนประวัติพนักงาน (BR-073): every employee, those who resigned too, by รหัส.</summary>
    public async Task<IReadOnlyList<EmployeeRegisterLine>> GetRegisterAsync()
    {
        var rows = await db.Employees.AsNoTracking().Where(e => !e.IsMigrationStub).OrderBy(e => e.Code).ThenBy(e => e.Id).ToListAsync();
        var departments = (await org.GetDepartmentsAsync()).ToDictionary(d => d.Id, d => d.Name);
        return rows.Select(e => new EmployeeRegisterLine(e.Code, e.Name, e.BirthDate,
            e.Sex switch { "F" => "หญิง", "M" => "ชาย", _ => null }, e.Nationality, e.RegisteredAddress, e.CurrentAddress,
            e.Education, e.StartDate, e.Position, e.DepartmentId is { } d ? departments.GetValueOrDefault(d) : null,
            e.Salary != 0 ? e.Salary : e.DailyWage, e.ResignDate)).ToList();
    }

    /// <summary>Access <c>DateDiff("m", from, to)</c>: month boundaries crossed, the days ignored.</summary>
    public static int MonthsBetween(DateOnly from, DateOnly to) => (to.Year - from.Year) * 12 + to.Month - from.Month;
}
