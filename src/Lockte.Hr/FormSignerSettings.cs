using Lockte.Hr.Data;
using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Hr;

/// <summary>The saved names; a never-saved setting is all blank and <see cref="RowVersion"/> null.</summary>
public sealed record FormSignersDto(string? ManagerTitle, string? ManagerFirstName, string? ManagerLastName, string? FactoryManagerName,
    string? PersonnelOfficerName, byte[]? RowVersion);

/// <summary>What HR saves; <see cref="RowVersion"/> is the one the page loaded (null while nothing has been saved).</summary>
public sealed record FormSignersInput(string? ManagerTitle, string? ManagerFirstName, string? ManagerLastName, string? FactoryManagerName,
    string? PersonnelOfficerName, byte[]? RowVersion);

/// <summary>ผู้ลงนามบนแบบฟอร์ม (UAT-29): read for printing and saved from the settings page. A newer save by someone else throws <see cref="ConcurrencyConflictException"/>.</summary>
public sealed class FormSignerSettings(HrDbContext db)
{
    public const int NameLength = 100;
    public const int TitleLength = 30;

    public async Task<FormSignersDto> GetAsync()
    {
        var row = await db.FormSigners.AsNoTracking().FirstOrDefaultAsync();
        return row is null
            ? new FormSignersDto(null, null, null, null, null, null)
            : new FormSignersDto(row.ManagerTitle, row.ManagerFirstName, row.ManagerLastName, row.FactoryManagerName, row.PersonnelOfficerName, row.RowVersion);
    }

    public async Task<ServiceResult> SaveAsync(FormSignersInput input)
    {
        var title = Clean(input.ManagerTitle);
        var first = Clean(input.ManagerFirstName);
        var last = Clean(input.ManagerLastName);
        var factory = Clean(input.FactoryManagerName);
        var officer = Clean(input.PersonnelOfficerName);
        var errors = new List<string>();
        if (title?.Length > TitleLength) errors.Add($"คำนำหน้ายาวได้ไม่เกิน {TitleLength} ตัว");
        foreach (var (label, value) in new[] { ("ชื่อผู้จัดการ", first), ("นามสกุลผู้จัดการ", last), ("ชื่อผู้จัดการโรงงาน", factory), ("ชื่อเจ้าหน้าที่บุคคล", officer) })
            if (value?.Length > NameLength) errors.Add($"{label}ยาวได้ไม่เกิน {NameLength} ตัว");
        if (errors.Count > 0) return new ServiceResult(false, errors);

        var row = await db.FormSigners.FirstOrDefaultAsync();
        if (row is null)
        {
            if (input.RowVersion is not null) throw new ConcurrencyConflictException();
            row = db.FormSigners.Add(new FormSigners()).Entity;
        }
        else row.ExpectRowVersion(input.RowVersion ?? throw new ConcurrencyConflictException());
        row.ManagerTitle = title;
        row.ManagerFirstName = first;
        row.ManagerLastName = last;
        row.FactoryManagerName = factory;
        row.PersonnelOfficerName = officer;
        await db.SaveChangesAsync();
        return ServiceResult.Ok(row.Id);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
