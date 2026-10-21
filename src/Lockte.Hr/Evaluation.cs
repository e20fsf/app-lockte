using Lockte.Hr.Data;
using Lockte.Org;
using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Hr;

/// <summary>
/// SCR-53 บันทึกการประเมินผล (BR-055): writes the evaluation fields onto the employee, as the legacy subform did. A blank
/// field clears it. Each save carries the rowversion the screen loaded (<see cref="ConcurrencyConflictException"/>).
/// </summary>
public sealed class EvaluationRegistry(HrDbContext db)
{
    public const int GradeLength = 50;
    public const int MemoLength = 255;

    public async Task<ServiceResult> SaveAsync(int employeeId, EvaluationInput input, byte[] rowVersion)
    {
        var (grade1, grade2, memo) = (Clean(input.Evaluation1Grade), Clean(input.Evaluation2Grade), Clean(input.Memo));
        var errors = new List<string>();
        if (grade1?.Length > GradeLength || grade2?.Length > GradeLength) errors.Add($"เกรดยาวได้ไม่เกิน {GradeLength} ตัวอักษร");
        if (memo?.Length > MemoLength) errors.Add($"หมายเหตุยาวได้ไม่เกิน {MemoLength} ตัวอักษร");
        if (errors.Count > 0) return new ServiceResult(false, errors);
        var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == employeeId);
        if (employee is null) return ServiceResult.Fail("ไม่พบพนักงานคนนี้");
        employee.ExpectRowVersion(rowVersion);
        employee.Evaluation1Date = input.Evaluation1Date;
        employee.Evaluation1Score = input.Evaluation1Score;
        employee.Evaluation1Grade = grade1;
        employee.Evaluation2Date = input.Evaluation2Date;
        employee.Evaluation2Score = input.Evaluation2Score;
        employee.Evaluation2Grade = grade2;
        employee.ProbationPassedDate = input.ProbationPassedDate;
        employee.Memo = memo;
        await db.SaveChangesAsync();
        return ServiceResult.Ok(employee.Id);
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}

/// <summary>What RPT-43 prints (#67): รายงานการประเมินผลพนักงาน (BR-055) and ใบประเมินประจำปี (BR-067). Read-only.</summary>
public sealed class EvaluationDirectory(HrDbContext db, OrgDirectory org)
{
    /// <summary>รายงานการประเมินผลพนักงาน (คิวรี การประเมินผลพนักงาน): DATE_1 set and not resigned, by รหัส.</summary>
    public async Task<IReadOnlyList<EvaluationListLine>> GetEvaluatedAsync()
    {
        var rows = await db.Employees.AsNoTracking()
            .Where(e => e.Evaluation1Date != null && e.ResignDate == null)
            .OrderBy(e => e.Code)
            .Select(e => new
            {
                e.Code, e.Name, e.DepartmentId, e.Position, e.StartDate, e.EmployeeType, e.Education, e.ShiftId, e.Evaluation1Date,
                e.Evaluation1Score, e.Evaluation1Grade, e.Evaluation2Date, e.Evaluation2Score, e.Evaluation2Grade, e.ProbationPassedDate, e.Memo,
            })
            .ToListAsync();
        var departments = (await org.GetDepartmentsAsync()).ToDictionary(d => d.Id, d => d.Name);
        var shifts = (await org.GetShiftsAsync()).ToDictionary(s => s.Id, s => s.Code);
        return rows.Select(e => new EvaluationListLine(e.Code, e.Name, e.DepartmentId is { } d ? departments.GetValueOrDefault(d) : null,
            e.Position, e.StartDate, e.EmployeeType, e.Education, e.ShiftId is { } s ? shifts.GetValueOrDefault(s) : null, e.Evaluation1Date,
            e.Evaluation1Score, e.Evaluation1Grade, e.Evaluation2Date, e.Evaluation2Score, e.Evaluation2Grade, e.ProbationPassedDate, e.Memo)).ToList();
    }

    /// <summary>
    /// ใบประเมินประจำปี (มาโคร พิมพ์ใบประเมินผลประจำปีรายวัน / …รายเดือน): the employees not resigned in the แผนก (all when
    /// none — the legacy kept LT* here), ตำแหน่ง Like / Not Like "พนักงาน*" (no ตำแหน่ง = on neither), by รหัส. Leaves of
    /// FROM_D ≥ from and TO_D ≤ to, every row summed; มาสาย counts the เวลาทำงาน rows of the range with LATE (each kind of
    /// work again, as คิวรี L1–L3). The legacy read only E_WORK, which holds the latest weeks; เวลาทำงาน here is every year.
    /// </summary>
    public async Task<YearlyEvaluationReport> GetYearlyAsync(YearlyEvaluationKind kind, DateOnly from, DateOnly to, int? departmentId)
    {
        const string staff = "พนักงาน";
        var query = db.Employees.AsNoTracking()
            .Where(e => e.ResignDate == null && e.Position != null)
            .Where(e => departmentId == null || e.DepartmentId == departmentId);
        query = kind == YearlyEvaluationKind.Staff ? query.Where(e => e.Position!.StartsWith(staff)) : query.Where(e => !e.Position!.StartsWith(staff));
        var people = await query.OrderBy(e => e.Code)
            .Select(e => new { e.Id, e.Code, e.Name, e.SectionId, e.DepartmentId })
            .ToListAsync();
        var ids = people.Select(p => p.Id).ToList();
        var leave = (await db.Leaves.AsNoTracking()
                .Where(l => ids.Contains(l.EmployeeId) && l.FromDate >= from && l.ToDate <= to)
                .GroupBy(l => new { l.EmployeeId, l.Type }).Select(g => new { g.Key.EmployeeId, g.Key.Type, Days = g.Sum(l => l.Days) })
                .ToListAsync())
            .ToDictionary(l => (l.EmployeeId, l.Type), l => l.Days);
        var late = await db.WorkTimes.AsNoTracking()
            .Where(w => ids.Contains(w.EmployeeId) && w.Date >= from && w.Date <= to && w.Late != null)
            .GroupBy(w => w.EmployeeId).Select(g => new { EmployeeId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.EmployeeId, g => g.Count);
        var warnings = departmentId is null
            ? new Dictionary<(int, string), int>()
            : await db.Warnings.AsNoTracking()
                .Where(w => ids.Contains(w.EmployeeId) && w.Date >= from && w.Date <= to && w.DepartmentId == departmentId
                            && (w.Level == WarningLevels.Verbal || w.Level == WarningLevels.Written))
                .GroupBy(w => new { w.EmployeeId, w.Level }).Select(g => new { g.Key.EmployeeId, g.Key.Level, Count = g.Count() })
                .ToDictionaryAsync(g => (g.EmployeeId, g.Level!), g => g.Count);

        var sections = (await org.GetSectionsAsync()).ToDictionary(s => s.Id, s => s.Name);
        var departments = await org.GetDepartmentsAsync();
        var departmentSections = departments.ToDictionary(d => d.Id, d => d.SectionName);
        string? Section(int? section, int? department)
            => section is { } s ? sections.GetValueOrDefault(s) : department is { } d ? departmentSections.GetValueOrDefault(d) : null;
        var sheets = people.Select(p =>
        {
            double Days(string type) => leave.GetValueOrDefault((p.Id, type));
            return new YearlyEvaluationSheet(p.Code, p.Name, Section(p.SectionId, p.DepartmentId), Days("1"), Days("2"), Days("3"),
                late.GetValueOrDefault(p.Id), Days("8"), warnings.GetValueOrDefault((p.Id, WarningLevels.Verbal)),
                warnings.GetValueOrDefault((p.Id, WarningLevels.Written)));
        }).ToList();
        var name = departmentId is { } dep ? departments.FirstOrDefault(d => d.Id == dep)?.Name : null;
        return new YearlyEvaluationReport(kind, from, to, name, sheets);
    }
}
