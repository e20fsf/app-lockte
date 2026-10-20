using Lockte.Hr.Data;
using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Hr;

/// <summary>
/// ผู้ลงนามบนสลิป (RPT-04, #55): the name and signature picture of แผนกบุคคล and ผู้จัดการโรงงาน. The legacy typed the
/// names into the report and kept one picture in ตาราง3.SIGN; here HR changes them without a new program. The rowversion the
/// screen loaded guards against a newer save by someone else (<see cref="ConcurrencyConflictException"/>).
/// </summary>
public sealed class SlipSignerRegistry(HrDbContext db)
{
    public const int NameLength = 100;
    /// <summary>A signature is a small picture; this keeps a phone photo of a page out of every slip.</summary>
    public const int MaxSignatureBytes = 512 * 1024;
    public static readonly IReadOnlyList<string> SignatureTypes = ["image/png", "image/jpeg"];

    public async Task<ServiceResult> SaveAsync(SlipSignerInput input)
    {
        var name = string.IsNullOrWhiteSpace(input.Name) ? null : input.Name.Trim();
        if (name?.Length > NameLength) return ServiceResult.Fail($"ชื่อยาวได้ไม่เกิน {NameLength} ตัว");
        if (input.Signature is { } picture)
        {
            if (picture.Length > MaxSignatureBytes) return ServiceResult.Fail($"รูปลายเซ็นใหญ่ได้ไม่เกิน {MaxSignatureBytes / 1024} KB");
            if (input.SignatureContentType is not { } type || !SignatureTypes.Contains(type) || !Looks(picture, type))
                return ServiceResult.Fail("รูปลายเซ็นต้องเป็นไฟล์ PNG หรือ JPEG");
        }

        var row = await db.SlipSigners.FirstOrDefaultAsync(s => s.Role == input.Role);
        if (row is null)
        {
            if (input.RowVersion is not null) throw new ConcurrencyConflictException();
            row = db.SlipSigners.Add(new SlipSigner { Role = input.Role }).Entity;
        }
        else
        {
            row.ExpectRowVersion(input.RowVersion ?? throw new ConcurrencyConflictException());
        }
        row.Name = name;
        if (input.Signature is not null)
        {
            row.Signature = input.Signature;
            row.SignatureContentType = input.SignatureContentType;
        }
        else if (input.RemoveSignature)
        {
            row.Signature = null;
            row.SignatureContentType = null;
        }
        await db.SaveChangesAsync();
        return ServiceResult.Ok(row.Id);
    }

    /// <summary>The file's first bytes say what the browser claimed.</summary>
    private static bool Looks(byte[] picture, string type) => type == "image/png"
        ? picture.AsSpan().StartsWith((ReadOnlySpan<byte>)[0x89, (byte)'P', (byte)'N', (byte)'G'])
        : picture.AsSpan().StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF]);
}
