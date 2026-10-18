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

    public async Task<IReadOnlyList<ShiftDto>> GetShiftsAsync()
        => await db.Shifts.AsNoTracking().OrderBy(s => s.Code).Select(s => ToDto(s)).ToListAsync();

    public async Task<ShiftDto?> FindShiftByCodeAsync(string code)
    {
        var normalized = OrgRegistry.Code(code);
        return await db.Shifts.AsNoTracking().Where(s => s.Code == normalized).Select(s => ToDto(s)).FirstOrDefaultAsync();
    }

    private IQueryable<Department> Departments()
        => db.Departments.AsNoTracking().Include(d => d.Section).Include(d => d.ShiftSupervisors).ThenInclude(h => h.Shift);

    private static DepartmentDto ToDto(Department d) => new(
        d.Id, d.Code, d.Name, d.EmployeeIdPrefix, d.SortOrder, d.SectionId, d.Section.Name, d.HeadEmployeeId,
        d.ManagerName, d.FactoryManagerName,
        d.ShiftSupervisors.OrderBy(h => h.Shift.Code)
            .Select(h => new ShiftSupervisorDto(h.ShiftId, h.Shift.Code, h.EmployeeId, h.RowVersion)).ToList(),
        d.RowVersion);

    private static ShiftDto ToDto(Shift s) => new(
        s.Id, s.Code, s.WorkIn, s.WorkOut, s.BreakStart, s.BreakEnd, s.OvertimeIn, s.OvertimeOut, s.RestMinutes, s.RowVersion);
}
