using Lockte.Calib.Data;
using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Calib;

/// <summary>
/// What the legacy printed as fixed text or fixed size, used until the settings are saved: the sticker size of the sample print (RPT-04, Q-14), the months of the plan (RPT-01,
/// Q-03), its responsible and checking parties and the two notes about the friction tester (RPT-01 ends at 350 °C, the external register at 250 — the legacy differs, so there
/// are two texts).
/// </summary>
public static class CalibSettingsDefaults
{
    public const decimal StickerWidthCm = 4.0m;
    public const decimal StickerHeightCm = 2.1m;
    /// <summary>ก.ค.: the month of the external calibration round.</summary>
    public const int PlanExternalMonth = 7;
    /// <summary>ส.ค.: the month of the internal round.</summary>
    public const int PlanInternalMonth = 8;
    public const string PlanResponsible = "QA";
    public const string PlanReviewer = "QMR";
    public const string PlanNote = "เครื่องทดสอบแรงเสียดทาน สอบเทียบที่โรงงาน มีรายละเอียดดังนี้ 1.ความเร็วรอบมอเตอร์ 500 rpm 2.ตัวควบคุมอุณหภูมิ (temp control) 100,150,200,250,300,350 3. แรงกด 125 Kgs.";
    public const string ExternalRegisterNote = "เครื่องทดสอบแรงเสียดทาน สอบเทียบที่โรงงาน มีรายละเอียดดังนี้ 1.ความเร็วรอบมอเตอร์ 500 rpm 2.ตัวควบคุมอุณหภูมิ (temp control) 100,150,200,250 3. แรงกด 125 Kgs.";
}

public sealed record CalibSettingsDto(
    decimal StickerWidthCm,
    decimal StickerHeightCm,
    int PlanExternalMonth,
    int PlanInternalMonth,
    string PlanResponsible,
    string PlanReviewer,
    string PlanNote,
    string ExternalRegisterNote,
    byte[]? RowVersion);

/// <summary>
/// The fields of the settings; <see cref="RowVersion"/> null while nothing has been saved yet. The fields after it are optional: left out (null) they keep the value they have
/// (or the default).
/// </summary>
public sealed record CalibSettingsInput(
    decimal StickerWidthCm,
    decimal StickerHeightCm,
    byte[]? RowVersion,
    int? PlanExternalMonth = null,
    int? PlanInternalMonth = null,
    string? PlanResponsible = null,
    string? PlanReviewer = null,
    string? PlanNote = null,
    string? ExternalRegisterNote = null);

/// <summary>The public, read-only face of ค่าตั้งค่า (ADR 0002). Writes go through <see cref="CalibSettingsRegistry"/>.</summary>
public sealed class CalibSettingsDirectory(CalibDbContext db)
{
    public async Task<CalibSettingsDto> GetAsync()
    {
        var row = await db.Settings.AsNoTracking().FirstOrDefaultAsync();
        return row is null
            ? new CalibSettingsDto(CalibSettingsDefaults.StickerWidthCm, CalibSettingsDefaults.StickerHeightCm, CalibSettingsDefaults.PlanExternalMonth,
                CalibSettingsDefaults.PlanInternalMonth, CalibSettingsDefaults.PlanResponsible, CalibSettingsDefaults.PlanReviewer, CalibSettingsDefaults.PlanNote,
                CalibSettingsDefaults.ExternalRegisterNote, null)
            : new CalibSettingsDto(row.StickerWidthCm, row.StickerHeightCm, row.PlanExternalMonth, row.PlanInternalMonth, row.PlanResponsible, row.PlanReviewer, row.PlanNote,
                row.ExternalRegisterNote, row.RowVersion);
    }
}

/// <summary>แก้ค่าตั้งค่า. A newer save by someone else throws <see cref="ConcurrencyConflictException"/>.</summary>
public sealed class CalibSettingsRegistry(CalibDbContext db)
{
    /// <summary>A4 is 21 × 29.7 cm: a sticker is printed on one page.</summary>
    public const decimal MaxWidthCm = 21m;
    public const decimal MaxHeightCm = 29.7m;
    public const int ShortTextLength = 100;
    public const int NoteLength = 1000;

    public async Task<ServiceResult> SaveAsync(CalibSettingsInput input)
    {
        var errors = new List<string>();
        if (input.StickerWidthCm <= 0 || input.StickerWidthCm > MaxWidthCm) errors.Add("ความกว้างสติกเกอร์ต้องมากกว่า 0 และไม่เกิน 21 ซม.");
        if (input.StickerHeightCm <= 0 || input.StickerHeightCm > MaxHeightCm) errors.Add("ความสูงสติกเกอร์ต้องมากกว่า 0 และไม่เกิน 29.7 ซม.");
        if (input.PlanExternalMonth is < 1 or > 12 || input.PlanInternalMonth is < 1 or > 12) errors.Add("เดือนของแผนการสอบเทียบต้องเป็น 1–12");
        var responsible = Clean(input.PlanResponsible);
        var reviewer = Clean(input.PlanReviewer);
        var note = Clean(input.PlanNote);
        var externalNote = Clean(input.ExternalRegisterNote);
        if (responsible?.Length > ShortTextLength || reviewer?.Length > ShortTextLength) errors.Add($"ผู้รับผิดชอบและผู้ตรวจสอบยาวได้ไม่เกิน {ShortTextLength} ตัว");
        if (note?.Length > NoteLength || externalNote?.Length > NoteLength) errors.Add($"หมายเหตุยาวได้ไม่เกิน {NoteLength} ตัว");
        if (errors.Count > 0) return new ServiceResult(false, errors);

        var row = await db.Settings.FirstOrDefaultAsync();
        if (row is null)
        {
            if (input.RowVersion is not null) return ServiceResult.Fail("ไม่พบค่าตั้งค่า");
            row = db.Settings.Add(new CalibSettings()).Entity;
        }
        else row.ExpectRowVersion(input.RowVersion ?? throw new ArgumentException("Editing needs the rowversion the screen loaded.", nameof(input)));
        row.StickerWidthCm = input.StickerWidthCm;
        row.StickerHeightCm = input.StickerHeightCm;
        if (input.PlanExternalMonth is { } e) row.PlanExternalMonth = e;
        if (input.PlanInternalMonth is { } i) row.PlanInternalMonth = i;
        if (input.PlanResponsible is not null) row.PlanResponsible = responsible ?? "";
        if (input.PlanReviewer is not null) row.PlanReviewer = reviewer ?? "";
        if (input.PlanNote is not null) row.PlanNote = note ?? "";
        if (input.ExternalRegisterNote is not null) row.ExternalRegisterNote = externalNote ?? "";
        await db.SaveChangesAsync();
        return ServiceResult.Ok(row.Id);
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
