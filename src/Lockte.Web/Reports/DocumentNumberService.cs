using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Web.Reports;

public sealed record DocumentNumberDto(int Id, string Key, string Name, string Code, string Edition, byte[] RowVersion)
{
    public string PrintedText => Code + Edition;
}

/// <summary>An edit of SCR "เลขเอกสารควบคุม": the row and its rowversion as loaded.</summary>
public sealed record DocumentNumberInput(int Id, string? Code, string? Edition, byte[] RowVersion);

/// <summary>Reads and edits เลขเอกสารควบคุม; the rows themselves come with the migration (the program knows its documents).</summary>
public sealed class DocumentNumberService(ReportsDbContext db)
{
    public const string Table = "เลขเอกสารควบคุม";
    public const int TextLength = 50;

    public async Task<IReadOnlyList<DocumentNumberDto>> ListAsync()
        => await db.DocumentNumbers.AsNoTracking().OrderBy(d => d.Key)
            .Select(d => new DocumentNumberDto(d.Id, d.Key, d.Name, d.Code, d.Edition, d.RowVersion)).ToListAsync();

    /// <summary>What prints top right, or null for a document without a number.</summary>
    public async Task<string?> PrintedTextAsync(string key)
        => await db.DocumentNumbers.AsNoTracking().Where(d => d.Key == key).Select(d => d.Code + d.Edition).FirstOrDefaultAsync();

    /// <summary>
    /// The code is trimmed (blank is refused); the edition is kept as typed, its leading spaces being part of the print.
    /// A newer save by someone else throws <see cref="ConcurrencyConflictException"/>.
    /// </summary>
    public async Task<ServiceResult> SaveAsync(DocumentNumberInput input)
    {
        var code = input.Code?.Trim();
        var edition = (input.Edition ?? "").TrimEnd();
        if (string.IsNullOrEmpty(code)) return ServiceResult.Fail("กรุณากรอกรหัสเอกสาร");
        if (code.Length > TextLength || edition.Length > TextLength) return ServiceResult.Fail($"ยาวได้ไม่เกิน {TextLength} ตัวอักษร");
        var row = await db.DocumentNumbers.FirstOrDefaultAsync(d => d.Id == input.Id);
        if (row is null) return ServiceResult.Fail("ไม่พบเอกสารนี้");
        row.ExpectRowVersion(input.RowVersion);
        row.Code = code;
        row.Edition = edition;
        await db.SaveChangesAsync();
        return ServiceResult.Ok(row.Id);
    }
}
