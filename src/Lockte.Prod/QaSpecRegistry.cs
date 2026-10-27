using Lockte.Prod.Data;
using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Prod;

public sealed record QaDocumentTypeDto(
    int Id, string Code, string? Revision, string Name, string? DepartmentStep, string? SampleUnit, QaResultColumns ResultColumns, string? AttributeRanges, QaPageLayout PageLayout,
    string? Paper, int Specs, byte[] RowVersion);

public sealed record QaDocumentTypeInput(
    int? Id, string Code, string? Revision, string Name, string? DepartmentStep, string? SampleUnit, QaResultColumns ResultColumns, string? AttributeRanges, QaPageLayout PageLayout,
    string? Paper, byte[]? RowVersion);

public sealed record QaMeasurePointInput(string? PointNo, string? Description, string? Standard);

/// <summary><paramref name="Image"/> replaces the picture when given; <paramref name="RemoveImage"/> removes it; neither keeps what is there.</summary>
public sealed record QaSpecInput(
    int? Id, int DocumentTypeId, string Name, string? Models, int? DocumentSequence, IReadOnlyList<QaMeasurePointInput> Points, IReadOnlyList<string> Defects,
    byte[]? Image, string? ImageContentType, bool RemoveImage, byte[]? RowVersion);

public sealed record QaMeasurePointDto(int Sequence, string? PointNo, string? Description, string? Standard);

public sealed record QaSpecDto(
    int Id, int DocumentTypeId, string DocumentTypeCode, string Name, string? Models, int? DocumentSequence, bool HasImage, IReadOnlyList<QaMeasurePointDto> Points,
    IReadOnlyList<string> Defects, byte[] RowVersion);

public sealed record QaSpecSummary(int Id, int DocumentTypeId, string DocumentTypeCode, string Name, string? Models, int? DocumentSequence, int Points, int Defects);

public sealed record QaSampleSizeDto(int? UpTo, int SampleSize);

/// <summary>
/// ข้อมูลสเปคตรวจคุณภาพ (#128, ข้อตัดสิน #27 #28): the document types (templates) and the specs of each model are master data the QA edits, with the history of every change, so the sheets
/// of ~290 models need no programmer. Printing the sheets is another ticket. The sample size of a lot (BR-017) is reference data read from <see cref="QaSampleSize"/>.
/// </summary>
public sealed class QaSpecRegistry(ProdDbContext db)
{
    public const int CodeLength = 30;
    public const int TextLength = 200;
    public const int LongTextLength = 500;
    public const int MaxImageBytes = 2 * 1024 * 1024;

    private static string? Clean(string? text) => ProductRegistry.Clean(text);

    private static T? Expect<T>(T? row, byte[]? rowVersion) where T : class, IAudited
    {
        row?.ExpectRowVersion(rowVersion ?? throw new ArgumentException("Editing needs the rowversion the screen loaded.", nameof(rowVersion)));
        return row;
    }

    private static void TooLong(List<string> errors, string label, string? text, int length)
    {
        if (text?.Length > length) errors.Add($"{label}ยาวได้ไม่เกิน {length} ตัว");
    }

    // ---------- ชนิดเอกสาร ----------

    public async Task<ServiceResult> SaveTypeAsync(QaDocumentTypeInput input)
    {
        var code = Clean(input.Code);
        var name = Clean(input.Name);
        var errors = new List<string>();
        if (code is null) errors.Add("กรุณากรอกรหัสเอกสาร");
        else if (code.Length > CodeLength) errors.Add($"รหัสเอกสารยาวได้ไม่เกิน {CodeLength} ตัว");
        else if (await db.QaDocumentTypes.AnyAsync(t => t.Code == code && t.Revision == Clean(input.Revision) && t.Id != input.Id)) errors.Add($"มีเอกสาร '{code}' ฉบับที่นี้อยู่แล้ว");
        if (name is null) errors.Add("กรุณากรอกชื่อใบตรวจ");
        else if (name.Length > TextLength) errors.Add($"ชื่อใบตรวจยาวได้ไม่เกิน {TextLength} ตัว");
        TooLong(errors, "ฉบับที่", Clean(input.Revision), CodeLength);
        TooLong(errors, "แผนก/ขั้นตอน", Clean(input.DepartmentStep), TextLength);
        TooLong(errors, "หน่วยสุ่ม", Clean(input.SampleUnit), CodeLength);
        TooLong(errors, "ช่วงคอลัมน์ตำหนิ", Clean(input.AttributeRanges), LongTextLength);
        TooLong(errors, "ขนาดกระดาษ", Clean(input.Paper), CodeLength);
        if (errors.Count > 0) return new ServiceResult(false, errors);

        var row = input.Id is { } id ? Expect(await db.QaDocumentTypes.FirstOrDefaultAsync(t => t.Id == id), input.RowVersion) : db.QaDocumentTypes.Add(new QaDocumentType()).Entity;
        if (row is null) return ServiceResult.Fail("ไม่พบชนิดเอกสารนี้");
        row.Code = code!;
        row.Revision = Clean(input.Revision);
        row.Name = name!;
        row.DepartmentStep = Clean(input.DepartmentStep);
        row.SampleUnit = Clean(input.SampleUnit);
        row.ResultColumns = input.ResultColumns;
        row.AttributeRanges = Clean(input.AttributeRanges);
        row.PageLayout = input.PageLayout;
        row.Paper = Clean(input.Paper);
        await db.SaveChangesAsync();
        return ServiceResult.Ok(row.Id);
    }

    public async Task<ServiceResult> DeleteTypeAsync(int id, byte[] rowVersion)
    {
        if (Expect(await db.QaDocumentTypes.FirstOrDefaultAsync(t => t.Id == id), rowVersion) is not { } row) return ServiceResult.Ok();
        db.QaDocumentTypes.Remove(row);
        return await db.TrySaveDeleteAsync() ? ServiceResult.Ok() : ServiceResult.Fail($"ลบชนิดเอกสาร '{row.Code}' ไม่ได้ เพราะมีสเปครายรุ่นใช้อยู่");
    }

    // ---------- สเปครายรุ่น ----------

    public async Task<ServiceResult> SaveSpecAsync(QaSpecInput input)
    {
        var name = Clean(input.Name);
        var errors = new List<string>();
        if (!await db.QaDocumentTypes.AnyAsync(t => t.Id == input.DocumentTypeId)) errors.Add("กรุณาเลือกชนิดเอกสาร");
        if (name is null) errors.Add("กรุณากรอกชื่อสเปค");
        else if (name.Length > TextLength) errors.Add($"ชื่อสเปคยาวได้ไม่เกิน {TextLength} ตัว");
        else if (await db.QaModelSpecs.AnyAsync(s => s.DocumentTypeId == input.DocumentTypeId && s.Name == name && s.Id != input.Id)) errors.Add($"มีสเปค '{name}' ในชนิดเอกสารนี้อยู่แล้ว");
        TooLong(errors, "รุ่นที่ใช้ร่วม", Clean(input.Models), LongTextLength);
        if (input.DocumentSequence is < 0) errors.Add("ลำดับเลขเอกสารต้องไม่ติดลบ");
        if (input.Image is { Length: > MaxImageBytes }) errors.Add($"รูปใหญ่เกิน {MaxImageBytes / 1024 / 1024} MB");
        foreach (var (point, index) in input.Points.Select((p, i) => (p, i + 1)))
        {
            TooLong(errors, $"เลขจุดวัดที่ {index} ", Clean(point.PointNo), CodeLength);
            TooLong(errors, $"ข้อความค่าที่กำหนดของจุดที่ {index} ", Clean(point.Description), LongTextLength);
            TooLong(errors, $"ค่ามาตรฐานของจุดที่ {index} ", Clean(point.Standard), LongTextLength);
        }
        foreach (var (defect, index) in input.Defects.Select((d, i) => (d, i + 1))) TooLong(errors, $"รายการตำหนิที่ {index} ", Clean(defect), TextLength);
        if (errors.Count > 0) return new ServiceResult(false, errors);

        var row = input.Id is { } id ? Expect(await db.QaModelSpecs.Include(s => s.Points).Include(s => s.Defects).FirstOrDefaultAsync(s => s.Id == id), input.RowVersion) : db.QaModelSpecs.Add(new QaModelSpec()).Entity;
        if (row is null) return ServiceResult.Fail("ไม่พบสเปคนี้");
        row.DocumentTypeId = input.DocumentTypeId;
        row.Name = name!;
        row.Models = Clean(input.Models);
        row.DocumentSequence = input.DocumentSequence;
        if (input.RemoveImage) { row.ImageData = null; row.ImageContentType = null; }
        if (input.Image is { Length: > 0 } image) { row.ImageData = image; row.ImageContentType = Clean(input.ImageContentType) ?? "image/png"; }

        if (input.Id is not null) db.Entry(row).Property(x => x.Name).IsModified = true; // a change of the points alone is a change of the spec: its rowversion moves
        // the points and defects are replaced by the screen's list, in its order
        db.QaMeasurePoints.RemoveRange(row.Points);
        db.QaDefects.RemoveRange(row.Defects);
        row.Points = input.Points.Where(p => Clean(p.PointNo) is not null || Clean(p.Description) is not null || Clean(p.Standard) is not null)
            .Select((p, i) => new QaMeasurePoint { Sequence = i + 1, PointNo = Clean(p.PointNo), Description = Clean(p.Description), Standard = Clean(p.Standard) }).ToList();
        row.Defects = input.Defects.Select(Clean).OfType<string>().Select((d, i) => new QaDefect { Sequence = i + 1, Name = d }).ToList();
        await db.SaveChangesAsync();
        return ServiceResult.Ok(row.Id);
    }

    public async Task<ServiceResult> DeleteSpecAsync(int id, byte[] rowVersion)
    {
        if (Expect(await db.QaModelSpecs.Include(s => s.Points).Include(s => s.Defects).FirstOrDefaultAsync(s => s.Id == id), rowVersion) is not { } row) return ServiceResult.Ok();
        db.QaMeasurePoints.RemoveRange(row.Points);
        db.QaDefects.RemoveRange(row.Defects);
        db.QaModelSpecs.Remove(row);
        await db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    // ---------- จำนวนสุ่ม ----------

    /// <summary>Replaces the table of sample sizes (the reference data); the ranges must rise and the last one has no limit.</summary>
    public async Task<ServiceResult> SaveSampleSizesAsync(IReadOnlyList<QaSampleSizeDto> sizes)
    {
        if (sizes.Count == 0) return ServiceResult.Fail("ต้องมีอย่างน้อยหนึ่งช่วง");
        if (sizes.Take(sizes.Count - 1).Any(s => s.UpTo is null) || sizes[^1].UpTo is not null) return ServiceResult.Fail("ช่วงสุดท้ายต้องไม่มีขีดจำกัด และช่วงอื่นต้องมีขีดจำกัด");
        if (sizes.Zip(sizes.Skip(1), (a, b) => a.UpTo >= b.UpTo).Any(x => x)) return ServiceResult.Fail("ขีดจำกัดของช่วงต้องเพิ่มขึ้นตามลำดับ");
        if (sizes.Any(s => s.SampleSize <= 0 || s.UpTo is <= 0)) return ServiceResult.Fail("จำนวนสุ่มและขีดจำกัดต้องมากกว่า 0");
        db.QaSampleSizes.RemoveRange(await db.QaSampleSizes.ToListAsync());
        db.QaSampleSizes.AddRange(sizes.Select(s => new QaSampleSize { UpTo = s.UpTo, SampleSize = s.SampleSize }));
        await db.SaveChangesAsync();
        return ServiceResult.Ok();
    }
}

/// <summary>จำนวนสุ่มตามขนาด Lot (BR-017): the first range whose limit is not below the number of good pieces.</summary>
public static class QaSampling
{
    /// <summary>The table of the legacy: ≤50→5, 51–150→20, 151–280→32, 281–500→50, 501–1,200→80, 1,201–3,200→125, 3,201–10,000→200, 10,001–35,000→315, over 35,000→500.</summary>
    public static IReadOnlyList<QaSampleSizeDto> Standard { get; } =
    [
        new(50, 5), new(150, 20), new(280, 32), new(500, 50), new(1200, 80), new(3200, 125), new(10000, 200), new(35000, 315), new(null, 500),
    ];

    public static int SampleSizeFor(decimal good, IReadOnlyList<QaSampleSizeDto> table)
    {
        var ranges = (table.Count == 0 ? Standard : table).OrderBy(t => t.UpTo ?? int.MaxValue).ToList();
        return (ranges.FirstOrDefault(t => t.UpTo is null || good <= t.UpTo) ?? ranges[^1]).SampleSize;
    }
}

/// <summary>Reads of the QA specs for the screens and for the sheets (ADR 0002).</summary>
public sealed class QaSpecDirectory(ProdDbContext db)
{
    public async Task<IReadOnlyList<QaDocumentTypeDto>> GetTypesAsync()
        => (await db.QaDocumentTypes.AsNoTracking().OrderBy(t => t.Code).ThenBy(t => t.Revision)
                .Select(t => new QaDocumentTypeDto(t.Id, t.Code, t.Revision, t.Name, t.DepartmentStep, t.SampleUnit, t.ResultColumns, t.AttributeRanges, t.PageLayout, t.Paper,
                    db.QaModelSpecs.Count(s => s.DocumentTypeId == t.Id), t.RowVersion)).ToListAsync());

    public async Task<QaDocumentTypeDto?> GetTypeAsync(int id) => (await GetTypesAsync()).FirstOrDefault(t => t.Id == id);

    /// <summary>The specs of one document type (all when null), by name.</summary>
    public async Task<IReadOnlyList<QaSpecSummary>> GetSpecsAsync(int? documentTypeId = null)
        => await db.QaModelSpecs.AsNoTracking().Where(s => documentTypeId == null || s.DocumentTypeId == documentTypeId).OrderBy(s => s.DocumentType.Code).ThenBy(s => s.Name)
            .Select(s => new QaSpecSummary(s.Id, s.DocumentTypeId, s.DocumentType.Code, s.Name, s.Models, s.DocumentSequence, s.Points.Count, s.Defects.Count)).ToListAsync();

    public async Task<QaSpecDto?> GetSpecAsync(int id)
    {
        var s = await db.QaModelSpecs.AsNoTracking().Include(x => x.DocumentType).Include(x => x.Points).Include(x => x.Defects).FirstOrDefaultAsync(x => x.Id == id);
        return s is null ? null : new QaSpecDto(s.Id, s.DocumentTypeId, s.DocumentType.Code, s.Name, s.Models, s.DocumentSequence, s.ImageData != null,
            s.Points.OrderBy(p => p.Sequence).Select(p => new QaMeasurePointDto(p.Sequence, p.PointNo, p.Description, p.Standard)).ToList(),
            s.Defects.OrderBy(d => d.Sequence).Select(d => d.Name).ToList(), s.RowVersion);
    }

    public async Task<(byte[] Data, string ContentType)?> GetImageAsync(int specId)
    {
        var row = await db.QaModelSpecs.AsNoTracking().Where(s => s.Id == specId && s.ImageData != null).Select(s => new { s.ImageData, s.ImageContentType }).FirstOrDefaultAsync();
        return row is null ? null : (row.ImageData!, row.ImageContentType ?? "image/png");
    }

    /// <summary>The specs that serve a model: its name is one of the comma-separated models of the spec (case-insensitive), or the name of the spec.</summary>
    public async Task<IReadOnlyList<QaSpecSummary>> FindByModelAsync(string model)
    {
        var wanted = model.Trim();
        var all = await db.QaModelSpecs.AsNoTracking().Select(s => new { s.Id, s.DocumentTypeId, TypeCode = s.DocumentType.Code, s.Name, s.Models, s.DocumentSequence, Points = s.Points.Count, Defects = s.Defects.Count }).ToListAsync();
        return all.Where(s => string.Equals(s.Name, wanted, StringComparison.OrdinalIgnoreCase)
                || (s.Models ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Contains(wanted, StringComparer.OrdinalIgnoreCase))
            .Select(s => new QaSpecSummary(s.Id, s.DocumentTypeId, s.TypeCode, s.Name, s.Models, s.DocumentSequence, s.Points, s.Defects)).ToList();
    }

    public async Task<IReadOnlyList<QaSampleSizeDto>> GetSampleSizesAsync()
        => (await db.QaSampleSizes.AsNoTracking().Select(s => new QaSampleSizeDto(s.UpTo, s.SampleSize)).ToListAsync()).OrderBy(s => s.UpTo ?? int.MaxValue).ToList();

    /// <summary>จำนวนสุ่ม of a lot of <paramref name="good"/> good pieces (BR-017).</summary>
    public async Task<int> SampleSizeAsync(decimal good) => QaSampling.SampleSizeFor(good, await GetSampleSizesAsync());
}
