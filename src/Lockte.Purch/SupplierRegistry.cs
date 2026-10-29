using Lockte.Purch.Data;
using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Purch;

/// <summary>ผู้ขาย as other modules and the screens read it (ADR 0002): code, name, address, credit, VAT and the flags.</summary>
public sealed record SupplierDto(
    int Id,
    string Code,
    string Name,
    string? Address1,
    string? Address2,
    string? Tel1,
    string? Tel2,
    string? Fax,
    string? Contact,
    int? CreditDays,
    bool Vat,
    string? ProductType,
    string? BusinessType,
    bool Certificate,
    bool UsedInProduction,
    byte[] RowVersion)
{
    /// <summary>"เงินสด" for 0 days, "30" for 30 days; blank when not stated.</summary>
    public string CreditText => CreditDays is { } d ? SupplierCredit.Text(d) : "";
}

public static class SupplierCredit
{
    public static string Text(int days) => days == 0 ? "เงินสด" : days.ToString();
}

/// <summary>The fields of SCR-02. <see cref="Id"/> null = new supplier; editing needs <see cref="RowVersion"/> as loaded.</summary>
public sealed record SupplierInput(
    int? Id,
    string Code,
    string Name,
    byte[]? RowVersion,
    string? Address1 = null,
    string? Address2 = null,
    string? Tel1 = null,
    string? Tel2 = null,
    string? Fax = null,
    string? Contact = null,
    int? CreditDays = null,
    bool Vat = false,
    string? ProductType = null,
    string? BusinessType = null,
    bool Certificate = false,
    bool UsedInProduction = false);

/// <summary>
/// เพิ่ม/แก้/ลบ ทะเบียนผู้ขาย (SCR-02, BR-002). Unlike the legacy the code cannot repeat or be blank (DQ-01, AC-32) and
/// the name cannot be blank, instead of deleting blank-named rows on exit. A newer save by someone else throws
/// <see cref="ConcurrencyConflictException"/>.
/// </summary>
public sealed class SupplierRegistry(PurchDbContext db)
{
    /// <summary>T-SP CODE is Text(255) in Access but codes are short; a limit that keeps the index small.</summary>
    public const int CodeLength = 50;
    public const int TextLength = 255;

    public async Task<ServiceResult> SaveAsync(SupplierInput input)
    {
        var code = Code(input.Code);
        var name = Clean(input.Name);
        var errors = new List<string>();
        if (code is null) errors.Add("กรุณากรอกรหัสผู้ขาย");
        else if (code.Length > CodeLength) errors.Add($"รหัสผู้ขายยาวได้ไม่เกิน {CodeLength} ตัว");
        else if (await db.Suppliers.AnyAsync(s => s.Code == code && s.Id != input.Id)) errors.Add($"มีรหัสผู้ขาย '{code}' อยู่แล้ว");
        if (name is null) errors.Add("กรุณากรอกชื่อผู้ขาย");
        var texts = new (string? Value, string Label)[]
        {
            (name, "ชื่อผู้ขาย"), (input.Address1, "ที่อยู่"), (input.Address2, "เขต(อำเภอ)และจังหวัด"), (input.Tel1, "โทรศัพท์ 1"),
            (input.Tel2, "โทรศัพท์ 2"), (input.Fax, "แฟกซ์"), (input.Contact, "ผู้ติดต่อ"), (input.ProductType, "ประเภทผลิตภัณฑ์"),
            (input.BusinessType, "ประเภทธุรกิจ"),
        };
        errors.AddRange(texts.Where(t => t.Value?.Trim().Length > TextLength).Select(t => $"{t.Label}ยาวได้ไม่เกิน {TextLength} ตัว"));
        if (input.CreditDays < 0) errors.Add("เครดิตต้องไม่ติดลบ (เงินสด = 0)");
        if (errors.Count > 0) return new ServiceResult(false, errors);

        var supplier = await LoadAsync(input.Id, input.RowVersion);
        if (input.Id is not null && supplier is null) return ServiceResult.Fail("ไม่พบผู้ขายนี้");
        supplier ??= db.Suppliers.Add(new Supplier()).Entity;
        supplier.Code = code!;
        supplier.Name = name!;
        supplier.Address1 = Clean(input.Address1);
        supplier.Address2 = Clean(input.Address2);
        supplier.Tel1 = Clean(input.Tel1);
        supplier.Tel2 = Clean(input.Tel2);
        supplier.Fax = Clean(input.Fax);
        supplier.Contact = Clean(input.Contact);
        supplier.CreditDays = input.CreditDays;
        supplier.Vat = input.Vat;
        supplier.ProductType = Clean(input.ProductType);
        supplier.BusinessType = Clean(input.BusinessType);
        supplier.Certificate = input.Certificate;
        supplier.UsedInProduction = input.UsedInProduction;
        await db.SaveChangesAsync();
        return ServiceResult.Ok(supplier.Id);
    }

    /// <summary>
    /// ลบผู้ขาย (ปุ่มลบพร้อมหน้ายืนยัน). A supplier that other tables refer to (ใบสั่งซื้อ, การรับของ…) is refused
    /// (ข้อสมมติ ง). Deleting a row that is already gone succeeds.
    /// </summary>
    public async Task<ServiceResult> DeleteAsync(int id, byte[] loadedRowVersion)
    {
        if (await LoadAsync(id, loadedRowVersion) is not { } supplier) return ServiceResult.Ok();
        db.Suppliers.Remove(supplier);
        return await db.TrySaveDeleteAsync()
            ? ServiceResult.Ok()
            : ServiceResult.Fail($"ลบผู้ขาย '{supplier.Code}' ไม่ได้ เพราะมีรายการอื่นอ้างถึงอยู่");
    }

    /// <summary>แก้ตัวเลือกเครดิต (จำนวนวัน, 0 = เงินสด) และประเภทธุรกิจ: each list is replaced by the one given.</summary>
    public async Task<ServiceResult> SaveOptionsAsync(IReadOnlyList<int> creditDays, IReadOnlyList<string> businessTypes)
    {
        var errors = new List<string>();
        if (creditDays.Any(d => d < 0)) errors.Add("เครดิตต้องไม่ติดลบ");
        var types = businessTypes.Select(Clean).OfType<string>().Distinct().ToList();
        if (types.Any(t => t.Length > TextLength)) errors.Add($"ประเภทธุรกิจยาวได้ไม่เกิน {TextLength} ตัว");
        if (errors.Count > 0) return new ServiceResult(false, errors);
        var days = creditDays.Distinct().ToList();

        var oldDays = await db.SupplierCreditOptions.ToListAsync();
        db.SupplierCreditOptions.RemoveRange(oldDays.Where(o => !days.Contains(o.Days)));
        foreach (var d in days.Where(d => oldDays.All(o => o.Days != d))) db.SupplierCreditOptions.Add(new SupplierCreditOption { Days = d });
        var oldTypes = await db.SupplierBusinessTypes.ToListAsync();
        db.SupplierBusinessTypes.RemoveRange(oldTypes.Where(o => !types.Contains(o.Name)));
        foreach (var t in types.Where(t => oldTypes.All(o => o.Name != t))) db.SupplierBusinessTypes.Add(new SupplierBusinessType { Name = t });
        await db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    private async Task<Supplier?> LoadAsync(int? id, byte[]? loadedRowVersion)
    {
        if (id is not { } supplierId) return null;
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == supplierId);
        supplier?.ExpectRowVersion(loadedRowVersion
            ?? throw new ArgumentException("Editing needs the rowversion the screen loaded.", nameof(loadedRowVersion)));
        return supplier;
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    /// <summary>Codes are compared and stored upper-case.</summary>
    internal static string? Code(string? text) => Clean(text)?.ToUpperInvariant();
}

/// <summary>The public, read-only face of ทะเบียนผู้ขาย (ADR 0002). Writes go through <see cref="SupplierRegistry"/>.</summary>
public sealed class SupplierDirectory(PurchDbContext db)
{
    /// <summary>T-CREDIT as the legacy had it.</summary>
    public static IReadOnlyList<int> DefaultCreditDays { get; } = [30, 45, 60, 90, 120, 0];

    /// <summary>T-ประเภทธุรกิจ as the legacy had it.</summary>
    public static IReadOnlyList<string> DefaultBusinessTypes { get; } =
        ["ผลิต (manufacturing)", "บริการ (service)", "ขายสินค้า (trading)", "รับเหมาช่วง (sub contractor)"];

    /// <summary>By รหัส.</summary>
    public async Task<IReadOnlyList<SupplierDto>> GetAllAsync(bool usedInProductionOnly = false)
        => (await db.Suppliers.AsNoTracking().Where(s => !usedInProductionOnly || s.UsedInProduction).OrderBy(s => s.Code).ToListAsync())
            .Select(ToDto).ToList();

    /// <summary>ผู้ขายที่รหัสหรือชื่อมี <paramref name="text"/> (ไม่สนตัวพิมพ์), by ชื่อ, at most <paramref name="max"/> — what the supplier pickers search.</summary>
    public async Task<IReadOnlyList<SupplierDto>> SearchAsync(string? text, int max = 50)
    {
        var term = text?.Trim();
        return (await db.Suppliers.AsNoTracking().Where(s => string.IsNullOrEmpty(term) || s.Code.Contains(term) || s.Name.Contains(term))
            .OrderBy(s => s.Name).ThenBy(s => s.Code).Take(max).ToListAsync()).Select(ToDto).ToList();
    }

    public async Task<SupplierDto?> GetAsync(int id)
        => await db.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id) is { } s ? ToDto(s) : null;

    public async Task<SupplierDto?> FindByCodeAsync(string code)
    {
        var normalized = SupplierRegistry.Code(code);
        return await db.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.Code == normalized) is { } s ? ToDto(s) : null;
    }

    /// <summary>The credit choices in the order the form lists them: the legacy order until the list is edited, then by days with เงินสด last.</summary>
    public async Task<IReadOnlyList<int>> GetCreditOptionsAsync()
    {
        var saved = await db.SupplierCreditOptions.AsNoTracking().Select(o => o.Days).ToListAsync();
        return saved.Count == 0 ? DefaultCreditDays : saved.OrderBy(d => d == 0 ? int.MaxValue : d).ToList();
    }

    public async Task<IReadOnlyList<string>> GetBusinessTypesAsync()
    {
        var saved = await db.SupplierBusinessTypes.AsNoTracking().OrderBy(o => o.Id).Select(o => o.Name).ToListAsync();
        return saved.Count == 0 ? DefaultBusinessTypes : saved;
    }

    private static SupplierDto ToDto(Supplier s) => new(
        s.Id, s.Code, s.Name, s.Address1, s.Address2, s.Tel1, s.Tel2, s.Fax, s.Contact, s.CreditDays, s.Vat, s.ProductType,
        s.BusinessType, s.Certificate, s.UsedInProduction, s.RowVersion);
}
