using Lockte.Org.Data;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Org;

/// <summary>
/// The public, read-only face of module องค์กร: how other modules (พนักงานและค่าแรง, ผลิต, …) and screens
/// read ฝ่าย, แผนก, กะ and หัวหน้า (ADR 0002). Writes go through <see cref="OrgRegistry"/>.
/// </summary>
public sealed class OrgDirectory(OrgDbContext db)
{
    public async Task<IReadOnlyList<SectionDto>> GetSectionsAsync()
        => await db.Sections.AsNoTracking()
            .OrderBy(s => s.SortOrder).ThenBy(s => s.Name)
            .Select(s => new SectionDto(s.Id, s.Name, s.SortOrder, s.RowVersion))
            .ToListAsync();

    /// <summary>In SCR-05 order (ลำดับ).</summary>
    public async Task<IReadOnlyList<DepartmentDto>> GetDepartmentsAsync()
        => (await Departments().OrderBy(d => d.SortOrder).ThenBy(d => d.Name).ToListAsync()).Select(ToDto).ToList();

    public async Task<DepartmentDto?> GetDepartmentAsync(int id)
        => await Departments().FirstOrDefaultAsync(d => d.Id == id) is { } d ? ToDto(d) : null;

    public async Task<DepartmentDto?> FindDepartmentByCodeAsync(string code)
    {
        var normalized = OrgRegistry.Code(code);
        return await Departments().FirstOrDefaultAsync(d => d.Code == normalized) is { } d ? ToDto(d) : null;
    }

    /// <summary>
    /// By ชื่อสะกดมาตรฐาน, ignoring surrounding spaces. Legacy records (EMPLO.DEPARTMENT, T-แผนก2) refer to a
    /// department by name (DQ-5).
    /// </summary>
    public async Task<DepartmentDto?> FindDepartmentByNameAsync(string name)
    {
        var trimmed = name.Trim();
        return await Departments().FirstOrDefaultAsync(d => d.Name == trimmed) is { } d ? ToDto(d) : null;
    }

    /// <summary>
    /// Departments whose employee IDs start with <paramref name="prefix"/> (the letter, or a whole ID like "D385").
    /// More than one is possible: อัดผ้าเบรก and ประกอบ share "A" in the legacy data (DQ-8).
    /// </summary>
    public async Task<IReadOnlyList<DepartmentDto>> FindDepartmentsByEmployeeIdPrefixAsync(string prefix)
    {
        var letter = OrgRegistry.Code(prefix)?[..1];
        if (letter is null) return [];
        return (await Departments().Where(d => d.EmployeeIdPrefix == letter).OrderBy(d => d.SortOrder).ToListAsync())
            .Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<ShiftDto>> GetShiftsAsync()
        => await db.Shifts.AsNoTracking().OrderBy(s => s.Code).Select(s => ToDto(s)).ToListAsync();

    public async Task<ShiftDto?> FindShiftByCodeAsync(string code)
    {
        var normalized = OrgRegistry.Code(code);
        return await db.Shifts.AsNoTracking().Where(s => s.Code == normalized).Select(s => ToDto(s)).FirstOrDefaultAsync();
    }

    private IQueryable<Department> Departments()
        => db.Departments.AsNoTracking().Include(d => d.Section).Include(d => d.Shifts).ThenInclude(h => h.Shift);

    private static DepartmentDto ToDto(Department d) => new(
        d.Id, d.Code, d.Name, d.EmployeeIdPrefix, d.SortOrder, d.SectionId, d.Section.Name, d.HeadEmployeeId,
        d.Shifts.OrderBy(h => h.Shift.Code)
            .Select(h => new DepartmentShiftDto(h.ShiftId, h.Shift.Code, h.SupervisorEmployeeId, h.SupervisorName,
                h.ManagerName, h.FactoryManagerName, h.RowVersion)).ToList(),
        d.RowVersion, d.StaffingTarget);

    private static ShiftDto ToDto(Shift s) => new(
        s.Id, s.Code, s.WorkIn, s.WorkOut, s.BreakStart, s.BreakEnd, s.OvertimeIn, s.OvertimeOut, s.RestMinutes, s.RowVersion);
}
