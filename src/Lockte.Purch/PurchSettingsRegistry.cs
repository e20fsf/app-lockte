using Lockte.Purch.Data;
using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Purch;

/// <summary>What the legacy hard-coded, used until the settings are saved (data-model "ค่าที่ hard-code").</summary>
public static class PurchSettingsDefaults
{
    public const decimal VatRate = 0.07m;
    public const decimal SmallOrderLimit = 5000m;
    public const string NoCarryPrefixes = "sp";
    public const decimal GradeA = 80m;
    public const decimal GradeB = 70m;
    public const decimal GradeC = 60m;

    /// <summary>BR-062: กลุ่มสินค้าซื้อมาขาย in print order. Prefixes are lower case; comparison ignores case.</summary>
    public static IReadOnlyList<ResaleGroupDto> ResaleGroups { get; } =
    [
        new("ba", "แบตเตอร์รี่"), new("br", "ลูกปืน"), new("ch", "โซ่"), new("sl", "หัวเทียน"), new("sp", "สปริง"),
    ];
}

public sealed record ResaleGroupDto(string Prefix, string Name);

public sealed record PurchSettingsDto(
    decimal VatRate,
    decimal SmallOrderLimit,
    string? SmallOrderApprover,
    IReadOnlyList<string> NoCarryPrefixes,
    decimal GradeA,
    decimal GradeB,
    decimal GradeC,
    IReadOnlyList<ResaleGroupDto> ResaleGroups,
    byte[]? RowVersion)
{
    /// <summary>BR-050: grade of a percentage score — A ≥ A threshold, then B, C, otherwise D.</summary>
    public string GradeOf(decimal percent) => percent >= GradeA ? "A" : percent >= GradeB ? "B" : percent >= GradeC ? "C" : "D";

    /// <summary>The group a material/product code belongs to (BR-062), or null.</summary>
    public ResaleGroupDto? ResaleGroupOf(string code)
        => ResaleGroups.FirstOrDefault(g => code.StartsWith(g.Prefix, StringComparison.OrdinalIgnoreCase));

    /// <summary>Whether a code's leftover is not carried forward by ตัด STOCK (BR-042).</summary>
    public bool IsNoCarry(string code) => NoCarryPrefixes.Any(p => code.StartsWith(p, StringComparison.OrdinalIgnoreCase));
}

/// <summary>The fields of the settings screen; <see cref="RowVersion"/> null while nothing has been saved yet.</summary>
public sealed record PurchSettingsInput(
    decimal VatRate,
    decimal SmallOrderLimit,
    string? SmallOrderApprover,
    string NoCarryPrefixes,
    decimal GradeA,
    decimal GradeB,
    decimal GradeC,
    IReadOnlyList<ResaleGroupDto> ResaleGroups,
    byte[]? RowVersion);

/// <summary>The public, read-only face of ค่าตั้งค่า (ADR 0002). Writes go through <see cref="PurchSettingsRegistry"/>.</summary>
public sealed class PurchSettingsDirectory(PurchDbContext db)
{
    public async Task<PurchSettingsDto> GetAsync()
    {
        var row = await db.Settings.AsNoTracking().FirstOrDefaultAsync();
        var groups = await db.ResaleGroups.AsNoTracking().OrderBy(g => g.SortOrder).ThenBy(g => g.Prefix).ToListAsync();
        // Nothing saved yet: the legacy's five groups. Once the settings are saved the list is the user's, even if empty.
        IReadOnlyList<ResaleGroupDto> resale = row is null
            ? PurchSettingsDefaults.ResaleGroups
            : groups.Select(g => new ResaleGroupDto(g.Prefix, g.Name)).ToList();
        return row is null
            ? new PurchSettingsDto(PurchSettingsDefaults.VatRate, PurchSettingsDefaults.SmallOrderLimit, null,
                PurchSettingsRegistry.SplitPrefixes(PurchSettingsDefaults.NoCarryPrefixes), PurchSettingsDefaults.GradeA,
                PurchSettingsDefaults.GradeB, PurchSettingsDefaults.GradeC, resale, null)
            : new PurchSettingsDto(row.VatRate, row.SmallOrderLimit, row.SmallOrderApprover,
                PurchSettingsRegistry.SplitPrefixes(row.NoCarryPrefixes), row.GradeA, row.GradeB, row.GradeC, resale, row.RowVersion);
    }
}

/// <summary>แก้ค่าตั้งค่า (#212). A newer save by someone else throws <see cref="ConcurrencyConflictException"/>.</summary>
public sealed class PurchSettingsRegistry(PurchDbContext db)
{
    public const int NameLength = 100;
    public const int PrefixLength = 10;
    public const int PrefixListLength = 200;

    public async Task<ServiceResult> SaveAsync(PurchSettingsInput input)
    {
        var errors = new List<string>();
        if (input.VatRate is < 0 or > 1) errors.Add("อัตรา VAT ต้องอยู่ระหว่าง 0 ถึง 1 (เช่น 0.07 = 7%)");
        if (input.SmallOrderLimit < 0) errors.Add("เกณฑ์ใบสั่งซื้อเล็กต้องไม่ติดลบ");
        var approver = string.IsNullOrWhiteSpace(input.SmallOrderApprover) ? null : input.SmallOrderApprover.Trim();
        if (approver?.Length > NameLength) errors.Add($"ผู้อนุมัติยาวได้ไม่เกิน {NameLength} ตัว");
        var noCarry = SplitPrefixes(input.NoCarryPrefixes);
        var noCarryText = string.Join(",", noCarry);
        if (noCarryText.Length > PrefixListLength || noCarry.Any(p => p.Length > PrefixLength)) errors.Add("prefix ที่ไม่ยกยอดยาวเกินกำหนด");
        if (input.GradeA is < 0 or > 100 || input.GradeB is < 0 or > 100 || input.GradeC is < 0 or > 100) errors.Add("เกณฑ์เกรดต้องอยู่ระหว่าง 0 ถึง 100");
        else if (!(input.GradeA >= input.GradeB && input.GradeB >= input.GradeC)) errors.Add("เกณฑ์เกรดต้องเรียง A ≥ B ≥ C");

        var groups = new List<ResaleGroupDto>();
        foreach (var g in input.ResaleGroups)
        {
            var prefix = g.Prefix?.Trim().ToLowerInvariant();
            var name = g.Name?.Trim();
            if (string.IsNullOrEmpty(prefix) && string.IsNullOrEmpty(name)) continue;
            if (string.IsNullOrEmpty(prefix) || string.IsNullOrEmpty(name)) errors.Add("กลุ่มสินค้าซื้อมาขายต้องกรอกทั้งรหัสขึ้นต้นและชื่อกลุ่ม");
            else if (prefix.Length > PrefixLength || name.Length > NameLength) errors.Add($"กลุ่ม '{prefix}' ยาวเกินกำหนด");
            else if (groups.Any(x => x.Prefix == prefix)) errors.Add($"รหัสขึ้นต้น '{prefix}' ซ้ำ");
            else groups.Add(new ResaleGroupDto(prefix, name));
        }
        if (errors.Count > 0) return new ServiceResult(false, errors);

        var row = await db.Settings.FirstOrDefaultAsync();
        if (row is null)
        {
            if (input.RowVersion is not null) return ServiceResult.Fail("ไม่พบค่าตั้งค่า");
            row = db.Settings.Add(new PurchSettings()).Entity;
        }
        else row.ExpectRowVersion(input.RowVersion ?? throw new ArgumentException("Editing needs the rowversion the screen loaded.", nameof(input)));
        row.VatRate = input.VatRate;
        row.SmallOrderLimit = input.SmallOrderLimit;
        row.SmallOrderApprover = approver;
        row.NoCarryPrefixes = noCarryText;
        row.GradeA = input.GradeA;
        row.GradeB = input.GradeB;
        row.GradeC = input.GradeC;

        var existing = await db.ResaleGroups.ToListAsync();
        foreach (var old in existing.Where(e => groups.All(g => g.Prefix != e.Prefix))) db.ResaleGroups.Remove(old);
        for (var i = 0; i < groups.Count; i++)
        {
            var entity = existing.FirstOrDefault(e => e.Prefix == groups[i].Prefix) ?? db.ResaleGroups.Add(new ResaleGroup { Prefix = groups[i].Prefix }).Entity;
            entity.Name = groups[i].Name;
            entity.SortOrder = i + 1;
        }
        await db.SaveChangesAsync();
        return ServiceResult.Ok(row.Id);
    }

    /// <summary>"sp, SL" → ["sp","sl"]: lower case, no blanks, no repeats.</summary>
    internal static IReadOnlyList<string> SplitPrefixes(string? text)
        => (text ?? "").Split([',', ' ', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => p.ToLowerInvariant()).Distinct().ToList();
}
