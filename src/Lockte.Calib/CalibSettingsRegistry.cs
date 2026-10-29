using Lockte.Calib.Data;
using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Calib;

/// <summary>The sticker size of the sample print (print-samples, RPT-04) until the real one is settled (Q-14).</summary>
public static class CalibSettingsDefaults
{
    public const decimal StickerWidthCm = 4.0m;
    public const decimal StickerHeightCm = 2.1m;
}

public sealed record CalibSettingsDto(decimal StickerWidthCm, decimal StickerHeightCm, byte[]? RowVersion);

/// <summary>The fields of the settings; <see cref="RowVersion"/> null while nothing has been saved yet.</summary>
public sealed record CalibSettingsInput(decimal StickerWidthCm, decimal StickerHeightCm, byte[]? RowVersion);

/// <summary>The public, read-only face of ค่าตั้งค่า (ADR 0002). Writes go through <see cref="CalibSettingsRegistry"/>.</summary>
public sealed class CalibSettingsDirectory(CalibDbContext db)
{
    public async Task<CalibSettingsDto> GetAsync()
    {
        var row = await db.Settings.AsNoTracking().FirstOrDefaultAsync();
        return row is null
            ? new CalibSettingsDto(CalibSettingsDefaults.StickerWidthCm, CalibSettingsDefaults.StickerHeightCm, null)
            : new CalibSettingsDto(row.StickerWidthCm, row.StickerHeightCm, row.RowVersion);
    }
}

/// <summary>แก้ค่าตั้งค่า. A newer save by someone else throws <see cref="ConcurrencyConflictException"/>.</summary>
public sealed class CalibSettingsRegistry(CalibDbContext db)
{
    /// <summary>A4 is 21 × 29.7 cm: a sticker is printed on one page.</summary>
    public const decimal MaxWidthCm = 21m;
    public const decimal MaxHeightCm = 29.7m;

    public async Task<ServiceResult> SaveAsync(CalibSettingsInput input)
    {
        var errors = new List<string>();
        if (input.StickerWidthCm <= 0 || input.StickerWidthCm > MaxWidthCm) errors.Add("ความกว้างสติกเกอร์ต้องมากกว่า 0 และไม่เกิน 21 ซม.");
        if (input.StickerHeightCm <= 0 || input.StickerHeightCm > MaxHeightCm) errors.Add("ความสูงสติกเกอร์ต้องมากกว่า 0 และไม่เกิน 29.7 ซม.");
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
        await db.SaveChangesAsync();
        return ServiceResult.Ok(row.Id);
    }
}
