using Lockte.Hr.Data;
using Lockte.Org;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Hr;

/// <summary>
/// The public, read-only face of ทะเบียนพนักงาน: how screens and other modules read employees (ADR 0002).
/// Department, section and shift names come from module องค์กร. Writes go through <see cref="EmployeeRegistry"/>.
/// </summary>
public sealed class EmployeeDirectory(HrDbContext db, OrgDirectory org, TimeProvider clock, IPayAccess pay)
{
    private DateOnly Today => DateOnly.FromDateTime(clock.GetLocalNow().DateTime);

    public async Task<EmployeeDto?> GetAsync(int id)
        => await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id && !e.IsMigrationStub) is { } e ? (await ToDtosAsync([e])).Single() : null;

    public async Task<EmployeeDto?> FindByCodeAsync(string code)
    {
        var normalized = EmployeeFormRules.NormalizeCode(code);
        return await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Code == normalized && !e.IsMigrationStub) is { } e
            ? (await ToDtosAsync([e])).Single()
            : null;
    }

    /// <summary>ค้นหาพนักงาน (SCR-03): by code or part of the name; those still working first, then by code.</summary>
    public async Task<IReadOnlyList<EmployeeListItem>> SearchAsync(string? text = null)
    {
        var query = db.Employees.AsNoTracking().Where(e => !e.IsMigrationStub);
        if (!string.IsNullOrWhiteSpace(text))
        {
            var term = text.Trim();
            query = query.Where(e => e.Code.StartsWith(term) || e.Name.Contains(term));
        }
        var rows = await query.OrderBy(e => e.ResignDate != null).ThenBy(e => e.Code)
            .Select(e => new { e.Id, e.Code, e.Name, e.DepartmentId, e.ResignDate })
            .ToListAsync();
        var departments = await DepartmentNamesAsync();
        return rows.Select(e => new EmployeeListItem(e.Id, e.Code, e.Name,
            e.DepartmentId is { } d ? departments.GetValueOrDefault(d) : null, e.ResignDate is null)).ToList();
    }

    /// <summary>พนักงานยังไม่ผ่านทดลองงาน (RPT-20, BR-053), by start date.</summary>
    public async Task<IReadOnlyList<EmployeeDto>> ListOnProbationAsync()
    {
        var earliestStart = Probation.EarliestStartOnProbation(Today);
        var rows = await db.Employees.AsNoTracking()
            .Where(e => e.ResignDate == null && e.StartDate >= earliestStart)
            .OrderBy(e => e.StartDate).ThenBy(e => e.Code)
            .ToListAsync();
        return await ToDtosAsync(rows);
    }

    /// <summary>ค่าที่เคยกรอก for หน้าที่ (all employees) and โรงพยาบาล (employees still working), as the legacy combos.</summary>
    public async Task<EmployeeChoices> GetChoicesAsync()
    {
        var duties = await db.Employees.AsNoTracking().Where(e => e.Duty != null)
            .Select(e => e.Duty!).Distinct().OrderBy(d => d).ToListAsync();
        var hospitals = await db.Employees.AsNoTracking().Where(e => e.Hospital != null && e.ResignDate == null)
            .Select(e => e.Hospital!).Distinct().OrderBy(h => h).ToListAsync();
        return new EmployeeChoices(duties, hospitals);
    }

    /// <summary>สถิติประกอบหน้าแก้ไข (BR-054): employees still working, recomputed on every call.</summary>
    public async Task<EmployeeStatistics> GetStatisticsAsync()
    {
        var working = await db.Employees.AsNoTracking().Where(e => e.ResignDate == null)
            .Select(e => new { e.DepartmentId, e.Sex, e.Education, e.Hospital })
            .ToListAsync();
        var departments = await org.GetDepartmentsAsync();

        var byDepartment = departments.Select(d =>
        {
            var people = working.Where(e => e.DepartmentId == d.Id).ToList();
            return new DepartmentHeadcount(d.Id, d.Name, people.Count(e => e.Sex == "M"), people.Count(e => e.Sex == "F"),
                people.Count, d.StaffingTarget);
        }).ToList();
        var byEducation = departments.Select(d => new DepartmentEducation(d.Id, d.Name,
            EmployeeStatistics.EducationLevels.ToDictionary(level => level,
                level => working.Count(e => e.DepartmentId == d.Id && e.Education == level)))).ToList();
        var byHospital = working.Where(e => e.Hospital != null)
            .GroupBy(e => e.Hospital!)
            .Select(g => new HospitalHeadcount(g.Key, g.Count()))
            .OrderByDescending(h => h.Count).ThenBy(h => h.Hospital)
            .ToList();
        return new EmployeeStatistics(byDepartment, byEducation, byHospital);
    }

    private async Task<Dictionary<int, string>> DepartmentNamesAsync()
        => (await org.GetDepartmentsAsync()).ToDictionary(d => d.Id, d => d.Name);

    private async Task<IReadOnlyList<EmployeeDto>> ToDtosAsync(IReadOnlyList<Employee> rows)
    {
        var departments = (await org.GetDepartmentsAsync()).ToDictionary(d => d.Id);
        var sections = (await org.GetSectionsAsync()).ToDictionary(s => s.Id, s => s.Name);
        var shifts = (await org.GetShiftsAsync()).ToDictionary(s => s.Id);
        var today = Today;
        var canViewPay = pay.CanViewPay; // #277: hidden fields never leave this service
        return rows.Select(e =>
        {
            var department = e.DepartmentId is { } d ? departments.GetValueOrDefault(d) : null;
            return new EmployeeDto
            {
                Id = e.Id, Code = e.Code, Name = e.Name, EnglishTitle = e.EnglishTitle, EnglishName = e.EnglishName,
                Sex = e.Sex, BirthDate = e.BirthDate, NationalId = e.NationalId, SocialSecurityNo = e.SocialSecurityNo,
                IdCardPlace = e.IdCardPlace, IdCardIssued = e.IdCardIssued, RegisteredAddress = e.RegisteredAddress,
                CurrentAddress = e.CurrentAddress, Telephone = e.Telephone, Nationality = e.Nationality,
                StartDate = e.StartDate, ListNo = e.ListNo, Education = e.Education,
                DepartmentId = e.DepartmentId, DepartmentName = department?.Name,
                SectionId = e.SectionId, SectionName = e.SectionId is { } sec ? sections.GetValueOrDefault(sec) : null,
                Position = e.Position, Duty = e.Duty, EmployeeType = e.EmployeeType, Salary = canViewPay ? e.Salary : 0, DailyWage = canViewPay ? e.DailyWage : 0,
                ShiftId = e.ShiftId, ShiftCode = e.ShiftId is { } s ? shifts.GetValueOrDefault(s)?.Code : null,
                HotAllowance = e.HotAllowance, BankAccountTtb = canViewPay ? e.BankAccountTtb : null, BankAccountKbank = canViewPay ? e.BankAccountKbank : null,
                Hospital = e.Hospital, ResignDate = e.ResignDate, Memo = e.Memo,
                Evaluation1Date = e.Evaluation1Date, Evaluation1Score = e.Evaluation1Score, Evaluation1Grade = e.Evaluation1Grade,
                Evaluation2Date = e.Evaluation2Date, Evaluation2Score = e.Evaluation2Score, Evaluation2Grade = e.Evaluation2Grade,
                ProbationPassedDate = e.ProbationPassedDate, RowVersion = e.RowVersion,
                OnProbation = Probation.IsOnProbation(e.StartDate, e.ResignDate, today),
            };
        }).ToList();
    }
}

/// <summary>
/// The employee register as module ขายและส่งของ reads it for พนักงานขาย (<see cref="Lockte.Sales.IEmployeeLookup"/>) —
/// the interface lives there because this module already depends on it.
/// </summary>
internal sealed class SalesEmployeeLookup(EmployeeDirectory employees) : Lockte.Sales.IEmployeeLookup
{
    public async Task<Lockte.Sales.SalesRepresentativeRef?> FindAsync(int employeeId)
        => await employees.GetAsync(employeeId) is { } e ? new Lockte.Sales.SalesRepresentativeRef(e.Id, e.Code, e.Name) : null;
}

/// <summary>The employee register as module ผลิต reads it for พนักงาน of ใบรายงานผลผลิต (<see cref="Lockte.Prod.IProductionEmployeeLookup"/>).</summary>
internal sealed class ProductionEmployeeLookup(EmployeeDirectory employees) : Lockte.Prod.IProductionEmployeeLookup
{
    public async Task<Lockte.Prod.ProductionEmployeeRef?> FindAsync(int employeeId)
        => await employees.GetAsync(employeeId) is { } e ? new Lockte.Prod.ProductionEmployeeRef(e.Id, e.Code, e.Name) : null;

    public async Task<IReadOnlyList<Lockte.Prod.ProductionEmployeeRef>> SearchAsync(string? text, int take)
        => (await employees.SearchAsync(text)).Where(e => e.IsWorking).Take(take).Select(e => new Lockte.Prod.ProductionEmployeeRef(e.Id, e.Code, e.Name)).ToList();
}
