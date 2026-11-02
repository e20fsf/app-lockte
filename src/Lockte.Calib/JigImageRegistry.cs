using System.Security.Cryptography;
using Lockte.Calib.Data;
using Lockte.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Calib;

/// <summary>The picture of a sheet as the print needs it.</summary>
public sealed record JigImageData(byte[] Data, string ContentType);

/// <summary>A sheet that has no picture yet, for the warning on the edit screen and in the load report.</summary>
public sealed record JigSheetRef(int Id, JigSheetType Type, string ModelKey, string Title);

/// <summary>
/// What loading a folder of exported pictures did (no personal data): how many sheets got one, which rows of the mapping found no sheet or no file, and
/// the sheets still without a picture after it.
/// </summary>
public sealed record JigImageImportReport(
    int MappingRows,
    int Loaded,
    int DistinctImages,
    IReadOnlyList<string> SheetNotFound,
    IReadOnlyList<string> FileProblems,
    IReadOnlyList<JigSheetRef> StillWithoutImage)
{
    public bool Complete => SheetNotFound.Count == 0 && FileProblems.Count == 0 && StillWithoutImage.Count == 0;
}

/// <summary>The public, read-only face of รูป JIG (ADR 0002). Writes go through <see cref="JigImageRegistry"/>.</summary>
public sealed class JigImageDirectory(CalibDbContext db)
{
    /// <summary>The picture of a sheet, or null when it has none (the print then shows an empty frame).</summary>
    public async Task<JigImageData?> GetAsync(int sheetId)
    {
        var row = await db.JigInspectionSheets.AsNoTracking().Where(s => s.Id == sheetId && s.ImageId != null)
            .Select(s => new { s.Image!.Data, s.Image.ContentType }).FirstOrDefaultAsync();
        return row is null ? null : new JigImageData(row.Data, row.ContentType);
    }

    /// <summary>Sheets without a picture, in the order SCR-05 lists them.</summary>
    public async Task<IReadOnlyList<JigSheetRef>> GetSheetsWithoutImageAsync()
        => await db.JigInspectionSheets.AsNoTracking().Where(s => s.ImageId == null).OrderBy(s => s.Type).ThenBy(s => s.DocumentNo)
            .Select(s => new JigSheetRef(s.Id, s.Type, s.ModelKey, s.Title)).ToListAsync();
}

/// <summary>
/// ใส่/แทน/เอารูป JIG ของแบบตรวจ (#200). PNG or JPEG up to <see cref="MaxBytes"/>; the same picture is stored once however many sheets use it.
/// <see cref="ImportFolderAsync"/> loads what <c>tools/calib-jig-images</c> exported (images/*.png and mapping.csv).
/// </summary>
public sealed class JigImageRegistry(CalibDbContext db)
{
    public const int MaxBytes = 2 * 1024 * 1024;

    public async Task<ServiceResult> SetAsync(int sheetId, byte[] data, string? contentType = null)
    {
        var kind = Check(data);
        if (kind is null) return ServiceResult.Fail("รูปต้องเป็นไฟล์ PNG หรือ JPEG");
        if (data.Length > MaxBytes) return ServiceResult.Fail($"รูปใหญ่เกิน {MaxBytes / 1024 / 1024} MB");
        var sheet = await db.JigInspectionSheets.FirstOrDefaultAsync(s => s.Id == sheetId);
        if (sheet is null) return ServiceResult.Fail("ไม่พบแบบตรวจนี้");
        await AttachAsync(sheet, data, kind);
        await db.SaveChangesAsync();
        return ServiceResult.Ok(sheet.Id);
    }

    /// <summary>เอารูปออก: the sheet prints with an empty frame again. A picture no sheet uses any more is deleted.</summary>
    public async Task<ServiceResult> ClearAsync(int sheetId)
    {
        var sheet = await db.JigInspectionSheets.FirstOrDefaultAsync(s => s.Id == sheetId);
        if (sheet is null) return ServiceResult.Fail("ไม่พบแบบตรวจนี้");
        var old = sheet.ImageId;
        sheet.ImageId = null;
        await db.SaveChangesAsync();
        await DeleteIfUnusedAsync(old);
        return ServiceResult.Ok(sheet.Id);
    }

    /// <summary>
    /// Loads the pictures of a folder written by <c>tools/calib-jig-images</c>: <c>mapping.csv</c> (type,model,report,image) and <c>images/</c>. A sheet that already
    /// has a picture gets the new one; a row without a picture or a file that is not there is reported, not stopped on.
    /// </summary>
    public async Task<JigImageImportReport> ImportFolderAsync(string folder)
    {
        var mapping = Path.Combine(folder, "mapping.csv");
        if (!File.Exists(mapping)) throw new FileNotFoundException("ไม่พบ mapping.csv ในโฟลเดอร์รูป JIG", mapping);
        var rows = File.ReadAllLines(mapping).Skip(1).Where(l => !string.IsNullOrWhiteSpace(l)).Select(l => Csv(l.TrimStart('﻿'))).ToList();

        var sheetNotFound = new List<string>();
        var fileProblems = new List<string>();
        var distinct = new HashSet<string>();
        var loaded = 0;
        foreach (var row in rows)
        {
            if (row.Count < 4) { fileProblems.Add("แถวในแมปปิ้งไม่ครบคอลัมน์"); continue; }
            var (typeText, model, report, image) = (row[0], row[1], row[2], row[3]);
            var type = typeText == nameof(JigSheetType.Grinding) ? JigSheetType.Grinding : JigSheetType.ZeroCheck;
            var key = model.ToUpper();
            var sheet = await db.JigInspectionSheets.FirstOrDefaultAsync(s => s.Type == type && s.ModelKey.ToUpper() == key);
            if (sheet is null) { sheetNotFound.Add($"{typeText} {model} ({report})"); continue; }
            if (string.IsNullOrWhiteSpace(image)) { fileProblems.Add($"{typeText} {model}: ไม่มีรูปในแมปปิ้ง"); continue; }
            var path = Path.Combine(folder, "images", image);
            if (!File.Exists(path)) { fileProblems.Add($"{typeText} {model}: ไม่พบไฟล์ {image}"); continue; }
            var data = await File.ReadAllBytesAsync(path);
            if (Check(data) is not { } kind || data.Length > MaxBytes) { fileProblems.Add($"{typeText} {model}: ไฟล์ {image} ไม่ใช่ PNG/JPEG ที่ขนาดไม่เกิน {MaxBytes / 1024 / 1024} MB"); continue; }
            await AttachAsync(sheet, data, kind);
            distinct.Add(image);
            loaded++;
        }
        var without = await new JigImageDirectory(db).GetSheetsWithoutImageAsync();
        return new JigImageImportReport(rows.Count, loaded, distinct.Count, sheetNotFound, fileProblems, without);
    }

    private async Task AttachAsync(JigInspectionSheet sheet, byte[] data, string contentType)
    {
        var hash = Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
        var image = await db.JigImages.FirstOrDefaultAsync(i => i.Sha256 == hash);
        if (image is null)
        {
            image = new JigImage { Sha256 = hash, ContentType = contentType, Data = data };
            db.JigImages.Add(image);
        }
        var old = sheet.ImageId;
        sheet.Image = image;
        await db.SaveChangesAsync();
        if (old is { } oldId && oldId != image.Id) await DeleteIfUnusedAsync(oldId);
    }

    private async Task DeleteIfUnusedAsync(int? imageId)
    {
        if (imageId is not { } id || await db.JigInspectionSheets.AnyAsync(s => s.ImageId == id)) return;
        if (await db.JigImages.FirstOrDefaultAsync(i => i.Id == id) is { } image)
        {
            db.JigImages.Remove(image);
            await db.SaveChangesAsync();
        }
    }

    /// <summary>The content type of PNG/JPEG bytes (by their first bytes), or null.</summary>
    internal static string? Check(byte[] data)
        => data.Length > 8 && data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47 ? "image/png"
            : data.Length > 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF ? "image/jpeg"
            : null;

    private static List<string> Csv(string line)
    {
        var cells = new List<string>();
        var cell = new System.Text.StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (quoted && c == '"' && i + 1 < line.Length && line[i + 1] == '"') { cell.Append('"'); i++; }
            else if (c == '"') quoted = !quoted;
            else if (c == ',' && !quoted) { cells.Add(cell.ToString()); cell.Clear(); }
            else cell.Append(c);
        }
        cells.Add(cell.ToString());
        return cells;
    }
}
