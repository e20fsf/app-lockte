using Lockte.Prod;
using Lockte.Purch.Data;
using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Purch;

/// <summary>วัตถุดิบ as the screens and other modules read it (ADR 0002).</summary>
public sealed record MaterialDto(
    int Id,
    string Code,
    string Name,
    string? Unit,
    decimal? MinimumQuantity,
    decimal Cost,
    int? LatestSupplierId,
    string? LatestSupplierCode,
    string? LatestSupplierName,
    bool IsMain,
    decimal BuyBackPrice,
    int? ProductId,
    byte[] RowVersion);

/// <summary>
/// The fields of SCR-17. <see cref="Id"/> null = new material; editing needs <see cref="RowVersion"/> as loaded. The latest supplier
/// is not an input: the goods receipt sets it (BR-031).
/// </summary>
public sealed record MaterialInput(
    int? Id,
    string Code,
    string Name,
    byte[]? RowVersion,
    string? Unit = null,
    decimal? MinimumQuantity = null,
    decimal Cost = 0,
    bool IsMain = false,
    decimal BuyBackPrice = 0,
    int? ProductId = null,
    int? LatestSupplierId = null);

/// <summary>
/// เพิ่ม/แก้/ลบ ทะเบียนวัตถุดิบ (SCR-17, BR-001). The code cannot repeat or be blank and the name cannot be blank, instead of deleting
/// blank-named rows on exit (AC-31); a name over <see cref="NameAdvice"/> characters is allowed (only the form warns, as before).
/// A newer save by someone else throws <see cref="ConcurrencyConflictException"/>.
/// </summary>
public sealed class MaterialRegistry(PurchDbContext db, ProductDirectory products)
{
    /// <summary>T-MTR_NAME CODE is Text(6) in Access; a little room for longer codes.</summary>
    public const int CodeLength = 20;
    public const int NameLength = 255;
    public const int UnitLength = 30;
    /// <summary>The form's advice: a name that fits the purchase order's cell (BR-001).</summary>
    public const int NameAdvice = 56;

    public async Task<ServiceResult> SaveAsync(MaterialInput input)
    {
        var code = Code(input.Code);
        var name = Clean(input.Name);
        var unit = Clean(input.Unit);
        var errors = new List<string>();
        if (code is null) errors.Add("กรุณากรอกรหัสวัตถุดิบ");
        else if (code.Length > CodeLength) errors.Add($"รหัสวัตถุดิบยาวได้ไม่เกิน {CodeLength} ตัว");
        else if (await db.Materials.AnyAsync(m => m.Code == code && m.Id != input.Id)) errors.Add($"มีรหัสวัตถุดิบ '{code}' อยู่แล้ว");
        if (name is null) errors.Add("กรุณากรอกชื่อวัตถุดิบ");
        else if (name.Length > NameLength) errors.Add($"ชื่อวัตถุดิบยาวได้ไม่เกิน {NameLength} ตัว");
        if (unit?.Length > UnitLength) errors.Add($"หน่วยยาวได้ไม่เกิน {UnitLength} ตัว");
        if (input.Cost < 0 || input.BuyBackPrice < 0 || input.MinimumQuantity < 0) errors.Add("ราคาและจุดสั่งซื้อต่ำสุดต้องไม่ติดลบ");
        if (input.ProductId is { } productId && await products.GetAsync(productId) is null) errors.Add("ไม่พบสินค้าที่เลือก");
        if (errors.Count > 0) return new ServiceResult(false, errors);

        var material = await LoadAsync(input.Id, input.RowVersion);
        if (input.Id is not null && material is null) return ServiceResult.Fail("ไม่พบวัตถุดิบนี้");
        material ??= db.Materials.Add(new Material()).Entity;
        material.Code = code!;
        material.Name = name!;
        material.Unit = unit;
        material.MinimumQuantity = input.MinimumQuantity;
        material.Cost = input.Cost;
        material.IsMain = input.IsMain;
        material.BuyBackPrice = input.BuyBackPrice;
        material.ProductId = input.ProductId;
        if (input.LatestSupplierId is { } latestSupplierId) material.LatestSupplierId = latestSupplierId; // the screen leaves it to the goods receipt; only the migration gives it
        await db.SaveChangesAsync();
        return ServiceResult.Ok(material.Id);
    }

    /// <summary>
    /// ลบวัตถุดิบ (ปุ่มลบพร้อมหน้ายืนยัน). A material that other tables refer to (ใบสั่งซื้อ, การรับของ, สต็อก…) is refused
    /// (ข้อสมมติ ง). Deleting a row that is already gone succeeds.
    /// </summary>
    public async Task<ServiceResult> DeleteAsync(int id, byte[] loadedRowVersion)
    {
        if (await LoadAsync(id, loadedRowVersion) is not { } material) return ServiceResult.Ok();
        db.Materials.Remove(material);
        return await db.TrySaveDeleteAsync()
            ? ServiceResult.Ok()
            : ServiceResult.Fail($"ลบวัตถุดิบ '{material.Code}' ไม่ได้ เพราะมีรายการอื่นอ้างถึงอยู่");
    }

    /// <summary>แก้กลุ่มรหัสวัตถุดิบ: the list is replaced by the one given (prefixes upper case, no repeats).</summary>
    public async Task<ServiceResult> SaveGroupsAsync(IReadOnlyList<string> prefixes)
    {
        var wanted = prefixes.Select(Code).OfType<string>().Select(p => p.TrimEnd('*')).Where(p => p.Length > 0).Distinct().ToList();
        if (wanted.Any(p => p.Length > CodeLength)) return ServiceResult.Fail($"รหัสขึ้นต้นยาวได้ไม่เกิน {CodeLength} ตัว");
        var old = await db.MaterialGroups.ToListAsync();
        db.MaterialGroups.RemoveRange(old.Where(g => !wanted.Contains(g.Prefix)));
        foreach (var p in wanted.Where(p => old.All(g => g.Prefix != p))) db.MaterialGroups.Add(new MaterialGroup { Prefix = p });
        await db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    private async Task<Material?> LoadAsync(int? id, byte[]? loadedRowVersion)
    {
        if (id is not { } materialId) return null;
        var material = await db.Materials.FirstOrDefaultAsync(m => m.Id == materialId);
        material?.ExpectRowVersion(loadedRowVersion
            ?? throw new ArgumentException("Editing needs the rowversion the screen loaded.", nameof(loadedRowVersion)));
        return material;
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    /// <summary>Codes are compared and stored upper-case.</summary>
    internal static string? Code(string? text) => Clean(text)?.ToUpperInvariant();
}

/// <summary>The public, read-only face of ทะเบียนวัตถุดิบ (ADR 0002). Writes go through <see cref="MaterialRegistry"/>.</summary>
public sealed class MaterialDirectory(PurchDbContext db)
{
    /// <summary>T-รหัสตัวแรกของวัตถุดิบ as the legacy had it (written there as patterns such as <c>E0*</c>).</summary>
    public static IReadOnlyList<string> DefaultGroups { get; } =
        ["B", "C", "E0", "EQ", "F0", "M0", "MI", "MM", "R", "S", "T0", "TE", "W", "FD", "DC", "DB"];

    /// <summary>By รหัส; <paramref name="codePrefix"/> = only the codes that start with it (กลุ่มรหัส), <paramref name="mainOnly"/> = วัตถุดิบหลัก.</summary>
    public async Task<IReadOnlyList<MaterialDto>> GetAllAsync(string? codePrefix = null, bool mainOnly = false)
    {
        var prefix = MaterialRegistry.Code(codePrefix)?.TrimEnd('*');
        return (await db.Materials.AsNoTracking().Include(m => m.LatestSupplier)
                .Where(m => !mainOnly || m.IsMain).Where(m => prefix == null || prefix == "" || m.Code.StartsWith(prefix))
                .OrderBy(m => m.Code).ToListAsync())
            .Select(ToDto).ToList();
    }

    /// <summary>วัตถุดิบ whose code or name contains <paramref name="text"/> (ignoring case), by ชื่อ, at most <paramref name="max"/> — what the item pickers search.</summary>
    public async Task<IReadOnlyList<MaterialDto>> SearchAsync(string? text, int max = 50)
    {
        var term = text?.Trim();
        return (await db.Materials.AsNoTracking().Include(m => m.LatestSupplier)
                .Where(m => string.IsNullOrEmpty(term) || m.Code.Contains(term) || m.Name.Contains(term))
                .OrderBy(m => m.Name).ThenBy(m => m.Code).Take(max).ToListAsync())
            .Select(ToDto).ToList();
    }

    public async Task<MaterialDto?> GetAsync(int id)
        => await db.Materials.AsNoTracking().Include(m => m.LatestSupplier).FirstOrDefaultAsync(m => m.Id == id) is { } m ? ToDto(m) : null;

    public async Task<MaterialDto?> FindByCodeAsync(string code)
    {
        var normalized = MaterialRegistry.Code(code);
        return await db.Materials.AsNoTracking().Include(m => m.LatestSupplier).FirstOrDefaultAsync(m => m.Code == normalized) is { } m ? ToDto(m) : null;
    }

    /// <summary>The code groups of the list print, in order; the legacy ones until the list is edited.</summary>
    public async Task<IReadOnlyList<string>> GetGroupsAsync()
    {
        var saved = await db.MaterialGroups.AsNoTracking().OrderBy(g => g.Prefix).Select(g => g.Prefix).ToListAsync();
        return saved.Count == 0 ? DefaultGroups : saved;
    }

    private static MaterialDto ToDto(Material m) => new(
        m.Id, m.Code, m.Name, m.Unit, m.MinimumQuantity, m.Cost, m.LatestSupplierId, m.LatestSupplier?.Code, m.LatestSupplier?.Name,
        m.IsMain, m.BuyBackPrice, m.ProductId, m.RowVersion);
}
