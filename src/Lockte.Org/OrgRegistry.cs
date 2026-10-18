using Lockte.Org.Data;
using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Org;

/// <summary>
/// เพิ่ม/แก้ ทะเบียนองค์กร: SCR-05 (แผนก กับกะที่ทำ) and the ฝ่าย / กะ registers (ฝ่าย filled by the data-migration
/// tool; กะ A–G seeded by the database migration, times edited from SCR-11). Each save carries the rowversion the screen loaded; if the row
/// changed since, it throws <see cref="ConcurrencyConflictException"/>.
/// </summary>
public sealed class OrgRegistry(OrgDbContext db)
{
    public const int DepartmentCodeLength = 10;

    /// <summary>
    /// Saves one SCR-05 row with the shifts the department works, in one transaction. A shift it keeps keeps its
    /// หัวหน้ากะ. The name is required (decided 2026-10-18 instead of BR-051's "blank row is deleted"): clearing it
    /// is an error, never a delete, and a blank new row is not saved.
    /// </summary>
    public async Task<ServiceResult> SaveDepartmentAsync(DepartmentInput input)
    {
        var name = Clean(input.Name);
        var code = Code(input.Code);
        var prefix = Code(input.EmployeeIdPrefix);
        var shiftIds = input.Shifts.Select(s => s.ShiftId).ToHashSet();
        var errors = new List<string>();
        if (name is null) errors.Add("กรุณากรอกชื่อแผนก");
        if (code is null) errors.Add("กรุณากรอกรหัสแผนก");
        else if (code.Length > DepartmentCodeLength) errors.Add($"รหัสแผนกยาวได้ไม่เกิน {DepartmentCodeLength} ตัว");
        if (!IsOneLetter(prefix)) errors.Add("ตัวอักษรนำหน้ารหัสพนักงานต้องเป็นตัวอักษรอังกฤษ 1 ตัว");
        if (input.SectionId is not { } sectionId || !await db.Sections.AnyAsync(s => s.Id == sectionId))
            errors.Add("กรุณาเลือกฝ่าย");
        if (code is not null && await db.Departments.AnyAsync(d => d.Code == code && d.Id != input.Id))
            errors.Add($"มีรหัสแผนก '{code}' อยู่แล้ว");
        if (name is not null && await db.Departments.AnyAsync(d => d.Name == name && d.Id != input.Id))
            errors.Add($"มีแผนก '{name}' อยู่แล้ว");
        if (shiftIds.Count != input.Shifts.Count) errors.Add("เลือกกะซ้ำ");
        var shifts = await db.Shifts.Where(s => shiftIds.Contains(s.Id)).ToListAsync();
        if (shifts.Count != shiftIds.Count) errors.Add("ไม่พบกะที่เลือก");
        if (errors.Count > 0) return new ServiceResult(false, errors);

        var department = input.Id is { } existingId
            ? await LoadAsync(DepartmentsWithShifts(), existingId, input.RowVersion)
            : db.Departments.Add(new Department()).Entity;
        if (department is null) return ServiceResult.Fail("ไม่พบแผนกนี้");
        department.Code = code!;
        department.Name = name!;
        department.EmployeeIdPrefix = prefix!;
        department.SortOrder = input.SortOrder;
        department.SectionId = input.SectionId!.Value;
        department.HeadEmployeeId = input.HeadEmployeeId;
        foreach (var dropped in department.Shifts.Where(h => !shiftIds.Contains(h.ShiftId)).ToList())
            db.Remove(dropped);
        foreach (var wanted in input.Shifts)
        {
            var row = department.Shifts.FirstOrDefault(h => h.ShiftId == wanted.ShiftId);
            if (row is null)
            {
                row = new DepartmentShift { Department = department, Shift = shifts.Single(s => s.Id == wanted.ShiftId) };
                department.Shifts.Add(row);
            }
            row.SupervisorName = Clean(wanted.SupervisorName);
            row.ManagerName = Clean(wanted.ManagerName);
            row.FactoryManagerName = Clean(wanted.FactoryManagerName);
        }
        await db.SaveChangesAsync();
        return ServiceResult.Ok(department.Id);
    }

    /// <summary>
    /// หัวหน้ากะ of a shift the department already works; <paramref name="employeeId"/> null = blank.
    /// Linked from the employee register (#45).
    /// </summary>
    public async Task<ServiceResult> SetShiftSupervisorAsync(int departmentId, int shiftId, int? employeeId, byte[] loadedRowVersion)
    {
        var row = await LoadAsync(db.DepartmentShifts.Include(h => h.Department).Include(h => h.Shift)
            .Where(h => h.DepartmentId == departmentId && h.ShiftId == shiftId), null, loadedRowVersion);
        if (row is null) return ServiceResult.Fail("แผนกนี้ไม่ได้ทำงานกะนี้");
        row.SupervisorEmployeeId = employeeId;
        await db.SaveChangesAsync();
        return ServiceResult.Ok(row.Id);
    }

    public async Task<ServiceResult> SaveSectionAsync(SectionInput input)
    {
        var name = Clean(input.Name);
        if (name is null) return ServiceResult.Fail("กรุณากรอกชื่อฝ่าย");
        if (await db.Sections.AnyAsync(s => s.Name == name && s.Id != input.Id)) return ServiceResult.Fail($"มีฝ่าย '{name}' อยู่แล้ว");

        var section = input.Id is { } id ? await LoadAsync(db.Sections, id, input.RowVersion) : db.Sections.Add(new Section()).Entity;
        if (section is null) return ServiceResult.Fail("ไม่พบฝ่ายนี้ อาจถูกลบไปแล้ว");
        section.Name = name;
        section.SortOrder = input.SortOrder;
        await db.SaveChangesAsync();
        return ServiceResult.Ok(section.Id);
    }

    /// <summary>กะ A–G (T-SHIFT); นาทีพัก from the legacy list T-เวลาพัก.</summary>
    public async Task<ServiceResult> SaveShiftAsync(ShiftInput input)
    {
        var code = Code(input.Code);
        var errors = new List<string>();
        if (code is not { Length: 1 } || code[0] is < 'A' or > 'G') errors.Add("รหัสกะต้องเป็น A ถึง G");
        else if (await db.Shifts.AnyAsync(s => s.Code == code && s.Id != input.Id)) errors.Add($"มีกะ '{code}' อยู่แล้ว");
        if (!ShiftDto.RestMinuteChoices.Contains(input.RestMinutes))
            errors.Add($"นาทีพักต้องเป็น {string.Join(", ", ShiftDto.RestMinuteChoices)}");
        if (errors.Count > 0) return new ServiceResult(false, errors);

        var shift = input.Id is { } id ? await LoadAsync(db.Shifts, id, input.RowVersion) : db.Shifts.Add(new Shift()).Entity;
        if (shift is null) return ServiceResult.Fail("ไม่พบกะนี้ อาจถูกลบไปแล้ว");
        shift.Code = code!;
        shift.WorkIn = input.WorkIn;
        shift.WorkOut = input.WorkOut;
        shift.BreakStart = input.BreakStart;
        shift.BreakEnd = input.BreakEnd;
        shift.OvertimeIn = input.OvertimeIn;
        shift.OvertimeOut = input.OvertimeOut;
        shift.RestMinutes = input.RestMinutes;
        await db.SaveChangesAsync();
        return ServiceResult.Ok(shift.Id);
    }

    /// <summary>
    /// The row to change, or null if it is gone. Throws a conflict if it changed since the screen loaded it —
    /// checked here too, because a save that changes nothing on the row itself (only its shifts) sends no UPDATE.
    /// </summary>
    private async Task<T?> LoadAsync<T>(IQueryable<T> query, int? id, byte[]? loadedRowVersion) where T : class, IAudited
    {
        var row = id is null ? await query.FirstOrDefaultAsync()
            : await query.FirstOrDefaultAsync(e => EF.Property<int>(e, "Id") == id);
        if (row is null || loadedRowVersion is null) return row;
        row.ExpectRowVersion(loadedRowVersion);
        return row;
    }

    private IQueryable<Department> DepartmentsWithShifts()
        => db.Departments.Include(d => d.Shifts).ThenInclude(h => h.Shift);

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    /// <summary>Codes are compared and stored upper-case.</summary>
    internal static string? Code(string? text) => Clean(text)?.ToUpperInvariant();

    private static bool IsOneLetter(string? text) => text is { Length: 1 } && char.IsAsciiLetter(text[0]);
}
