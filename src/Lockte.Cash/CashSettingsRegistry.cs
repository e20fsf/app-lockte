using Lockte.Cash.Data;
using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Cash;

public sealed record CashSettingsDto(string? LastRemittedReceiptNo, byte[]? RowVersion);

/// <summary>The fields of the settings; <see cref="RowVersion"/> null while nothing has been saved yet.</summary>
public sealed record CashSettingsInput(string? LastRemittedReceiptNo, byte[]? RowVersion);

/// <summary>The public, read-only face of ค่าตั้งค่า (ADR 0002). Writes go through <see cref="CashSettingsRegistry"/>.</summary>
public sealed class CashSettingsDirectory(CashDbContext db)
{
    public async Task<CashSettingsDto> GetAsync()
    {
        var row = await db.Settings.AsNoTracking().FirstOrDefaultAsync();
        return row is null ? new CashSettingsDto(null, null) : new CashSettingsDto(row.LastRemittedReceiptNo, row.RowVersion);
    }
}

/// <summary>แก้ค่าตั้งค่า. A newer save by someone else throws <see cref="ConcurrencyConflictException"/>.</summary>
public sealed class CashSettingsRegistry(CashDbContext db)
{
    public async Task<ServiceResult> SaveAsync(CashSettingsInput input)
    {
        var lastNo = string.IsNullOrWhiteSpace(input.LastRemittedReceiptNo) ? null : input.LastRemittedReceiptNo.Trim();
        if (lastNo?.Length > CashSettings.ReceiptNoLength) return ServiceResult.Fail($"เลขที่ใบเสร็จยาวได้ไม่เกิน {CashSettings.ReceiptNoLength} ตัว");

        var row = await db.Settings.FirstOrDefaultAsync();
        if (row is null)
        {
            if (input.RowVersion is not null) return ServiceResult.Fail("ไม่พบค่าตั้งค่า");
            row = db.Settings.Add(new CashSettings()).Entity;
        }
        else row.ExpectRowVersion(input.RowVersion ?? throw new ArgumentException("Editing needs the rowversion the screen loaded.", nameof(input)));
        row.LastRemittedReceiptNo = lastNo;
        await db.SaveChangesAsync();
        return ServiceResult.Ok(row.Id);
    }
}
