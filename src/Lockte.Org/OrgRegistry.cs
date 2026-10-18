using Lockte.Org.Data;
using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Org;

/// <summary>
/// เพิ่ม/แก้ ทะเบียนองค์กร: SCR-05 (แผนก) plus the ฝ่าย and กะ registers. Each save carries the rowversion the
/// screen loaded; a newer save by someone else throws <see cref="ConcurrencyConflictException"/>.
/// </summary>
public sealed class OrgRegistry(OrgDbContext db)
{
    /// <summary>
    /// Saves one SCR-05 row. As in the legacy screen (BR-051, macro <c>ลบแผนก</c>), a row whose name is blank is
    /// not kept: a new one is ignored, an existing one is deleted together with its หัวหน้ากะ rows.
    /// </summary>
    public async Task<ServiceResult> SaveDepartmentAsync(DepartmentInput input)
    {
        var name = Clean(input.Name);
        if (name is null) return input.Id is { } id ? await DeleteDepartmentAsync(id, input.RowVersion) : ServiceResult.Ok();

        var code = Clean(input.Code)?.ToUpperInvariant();
        var prefix = Clean(input.EmployeeIdPrefix)?.ToUpperInvariant();
        var errors = new List<string>();
        if (code is null) errors.Add("กรุณากรอกรหัสแผนก");
        if (prefix is not { Length: 1 } || !char.IsAsciiLetter(prefix[0]))
            errors.Add("ตัวอักษรนำหน้ารหัสพนักงานต้องเป็นตัวอักษรอังกฤษ 1 ตัว");
        if (input.SectionId is not { } sectionId || !await db.Sections.AnyAsync(s => s.Id == sectionId))
            errors.Add("กรุณาเลือกฝ่าย");
        if (code is not null && await db.Departments.AnyAsync(d => d.Code == code && d.Id != input.Id))
            errors.Add($"มีรหัสแผนก '{code}' อยู่แล้ว");
        if (await db.Departments.AnyAsync(d => d.Name == name && d.Id != input.Id))
            errors.Add($"มีแผนก '{name}' อยู่แล้ว");
        if (errors.Count > 0) return new ServiceResult(false, errors);

        var department = await LoadOrCreateAsync(db.Departments, input.Id, input.RowVersion);
        if (department is null) return ServiceResult.Fail("ไม่พบแผนกนี้ อาจถูกลบไปแล้ว");
        department.Code = code!;
        department.Name = name;
        department.EmployeeIdPrefix = prefix!;
        department.SortOrder = input.SortOrder;
        department.SectionId = input.SectionId!.Value;
        department.HeadEmployeeId = input.HeadEmployeeId;
        department.ManagerName = Clean(input.ManagerName);
        department.FactoryManagerName = Clean(input.FactoryManagerName);
        await db.SaveChangesAsync();
        return ServiceResult.Ok(department.Id);
    }

    private async Task<ServiceResult> DeleteDepartmentAsync(int id, byte[]? rowVersion)
    {
        var department = await db.Departments.Include(d => d.ShiftSupervisors).ThenInclude(h => h.Shift)
            .FirstOrDefaultAsync(d => d.Id == id);
        if (department is null) return ServiceResult.Ok(); // already gone
        if (rowVersion is not null) db.ExpectRowVersion(department, rowVersion);
        db.Remove(department); // its หัวหน้ากะ rows go with it (and into the history)
        await db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    /// <summary>
    /// The department works <paramref name="shiftId"/>, with หัวหน้ากะ <paramref name="employeeId"/> (may be blank).
    /// <paramref name="works"/> false removes the shift from the department.
    /// </summary>
    public async Task<ServiceResult> SetShiftSupervisorAsync(int departmentId, int shiftId, bool works, int? employeeId = null)
    {
        var department = await db.Departments.Include(d => d.ShiftSupervisors).ThenInclude(h => h.Shift)
            .FirstOrDefaultAsync(d => d.Id == departmentId);
        if (department is null) return ServiceResult.Fail("ไม่พบแผนกนี้ อาจถูกลบไปแล้ว");
        var shift = await db.Shifts.FirstOrDefaultAsync(s => s.Id == shiftId);
        if (shift is null) return ServiceResult.Fail("ไม่พบกะนี้");

        var row = department.ShiftSupervisors.FirstOrDefault(h => h.ShiftId == shiftId);
        if (!works)
        {
            if (row is not null) db.Remove(row);
        }
        else if (row is null)
        {
            department.ShiftSupervisors.Add(new ShiftSupervisor { Department = department, Shift = shift, EmployeeId = employeeId });
        }
        else
        {
            row.EmployeeId = employeeId;
        }
        await db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> SaveSectionAsync(SectionInput input)
    {
        var name = Clean(input.Name);
        if (name is null) return ServiceResult.Fail("กรุณากรอกชื่อฝ่าย");
        if (await db.Sections.AnyAsync(s => s.Name == name && s.Id != input.Id)) return ServiceResult.Fail($"มีฝ่าย '{name}' อยู่แล้ว");

        var section = await LoadOrCreateAsync(db.Sections, input.Id, input.RowVersion);
        if (section is null) return ServiceResult.Fail("ไม่พบฝ่ายนี้ อาจถูกลบไปแล้ว");
        section.Name = name;
        section.SortOrder = input.SortOrder;
        await db.SaveChangesAsync();
        return ServiceResult.Ok(section.Id);
    }

    public async Task<ServiceResult> SaveShiftAsync(ShiftInput input)
    {
        var code = Clean(input.Code)?.ToUpperInvariant();
        var errors = new List<string>();
        if (code is not { Length: 1 } || !char.IsAsciiLetter(code[0])) errors.Add("รหัสกะต้องเป็นตัวอักษรอังกฤษ 1 ตัว เช่น A");
        else if (await db.Shifts.AnyAsync(s => s.Code == code && s.Id != input.Id)) errors.Add($"มีกะ '{code}' อยู่แล้ว");
        if (input.WorkIn == input.WorkOut) errors.Add("เวลาเข้าและเวลาออกต้องไม่เท่ากัน");
        if (input.BreakStart is null != input.BreakEnd is null) errors.Add("กรอกเวลาเริ่มพักและเลิกพักให้ครบทั้งคู่");
        if (input.OvertimeIn is null != input.OvertimeOut is null) errors.Add("กรอกเวลาเริ่ม OT และเลิก OT ให้ครบทั้งคู่");
        if (input.RestMinutes is < 0 or > 60 || input.RestMinutes % 10 != 0) errors.Add("นาทีพักต้องเป็น 0, 10, 20 … 60");
        if (errors.Count > 0) return new ServiceResult(false, errors);

        var shift = await LoadOrCreateAsync(db.Shifts, input.Id, input.RowVersion);
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

    /// <summary>The row to edit (checked against the rowversion the screen loaded), a new tracked row, or null if gone.</summary>
    private async Task<T?> LoadOrCreateAsync<T>(DbSet<T> set, int? id, byte[]? rowVersion) where T : class, IAudited, new()
    {
        if (id is null)
        {
            var created = new T();
            set.Add(created);
            return created;
        }
        var existing = await set.FindAsync(id.Value);
        if (existing is not null && rowVersion is not null) db.ExpectRowVersion(existing, rowVersion);
        return existing;
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
