using Lockte.Prod.Data;
using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Prod;

public sealed record MoldDto(int Id, string Code, string Name, byte[] RowVersion);

/// <summary><see cref="Id"/> null = new mold; editing needs <see cref="RowVersion"/> as loaded.</summary>
public sealed record MoldInput(int? Id, string Code, string Name, byte[]? RowVersion);

/// <summary>
/// เพิ่ม/แก้ ทะเบียนแม่พิมพ์ขั้นต่ำ (รหัส + ชื่อ); SCR-17 comes with #104.
/// A newer save by someone else throws <see cref="ConcurrencyConflictException"/>.
/// </summary>
public sealed class MoldRegistry(ProdDbContext db)
{
    /// <summary>T-รหัสพิมพ์ ID is Text(50).</summary>
    public const int CodeLength = 50;
    /// <summary>T-รหัสพิมพ์ NAME is Text(100).</summary>
    public const int NameLength = 100;

    public async Task<ServiceResult> SaveAsync(MoldInput input)
    {
        var code = Code(input.Code);
        var name = Clean(input.Name);
        var errors = new List<string>();
        if (code is null) errors.Add("กรุณากรอกรหัสแม่พิมพ์");
        else if (code.Length > CodeLength) errors.Add($"รหัสแม่พิมพ์ยาวได้ไม่เกิน {CodeLength} ตัว");
        else if (await db.Molds.AnyAsync(m => m.Code == code && m.Id != input.Id)) errors.Add($"มีรหัสแม่พิมพ์ '{code}' อยู่แล้ว");
        if (name is null) errors.Add("กรุณากรอกชื่อแม่พิมพ์");
        else if (name.Length > NameLength) errors.Add($"ชื่อแม่พิมพ์ยาวได้ไม่เกิน {NameLength} ตัว");
        if (errors.Count > 0) return new ServiceResult(false, errors);

        Mold? mold;
        if (input.Id is { } id)
        {
            mold = await db.Molds.FirstOrDefaultAsync(m => m.Id == id);
            if (mold is null) return ServiceResult.Fail("ไม่พบแม่พิมพ์นี้");
            mold.ExpectRowVersion(input.RowVersion
                ?? throw new ArgumentException("Editing needs the rowversion the screen loaded.", nameof(input)));
        }
        else
        {
            mold = db.Molds.Add(new Mold()).Entity;
        }
        mold.Code = code!;
        mold.Name = name!;
        await db.SaveChangesAsync();
        return ServiceResult.Ok(mold.Id);
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    /// <summary>Codes are compared and stored upper-case.</summary>
    internal static string? Code(string? text) => Clean(text)?.ToUpperInvariant();
}

/// <summary>The public, read-only face of ทะเบียนแม่พิมพ์ (ADR 0002). Writes go through <see cref="MoldRegistry"/>.</summary>
public sealed class MoldDirectory(ProdDbContext db)
{
    public async Task<IReadOnlyList<MoldDto>> GetAllAsync()
        => await db.Molds.AsNoTracking().OrderBy(m => m.Code)
            .Select(m => new MoldDto(m.Id, m.Code, m.Name, m.RowVersion)).ToListAsync();

    public async Task<MoldDto?> FindByCodeAsync(string code)
    {
        var normalized = MoldRegistry.Code(code);
        return await db.Molds.AsNoTracking().Where(m => m.Code == normalized)
            .Select(m => new MoldDto(m.Id, m.Code, m.Name, m.RowVersion)).FirstOrDefaultAsync();
    }
}
