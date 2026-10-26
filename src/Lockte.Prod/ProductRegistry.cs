using Lockte.Prod.Data;
using Lockte.Sales;
using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Prod;

/// <summary>
/// The fields of SCR-03. <see cref="Id"/> null = new product; editing needs <see cref="RowVersion"/> as loaded.
/// The form's hints (contract injection work needs customer, aluminium weight and price; มอก. fields only for
/// มอก. products) are shown on screen, not enforced — as before.
/// </summary>
public sealed record ProductInput(
    int? Id,
    string Code,
    string Name,
    byte[]? RowVersion,
    string? Unit = null,
    string? Pack = null,
    decimal? AluminumWeight = null,
    decimal? BrakePadWeight = null,
    decimal? CorkWeight = null,
    decimal? SteelCost = null,
    decimal? PairWeight = null,
    decimal? SellingPrice = null,
    decimal? UnitCost = null,
    int? ProductTypeId = null,
    int? MoldId = null,
    int? CustomerId = null,
    DateOnly? ProducedOn = null,
    int? ModelId = null,
    string? Barcode = null,
    string? StandardNumber = null,
    int? BrakeTypeId = null,
    string? Size = null,
    string? Formula = null,
    string? StandardModel = null);

/// <summary>รหัส ชื่อ หน่วย — what other modules need to list and pick products.</summary>
public sealed record ProductListItem(int Id, string Code, string Name, string? Unit);

public sealed record ProductDto(
    int Id,
    string Code,
    string Name,
    string? Unit,
    string? Pack,
    decimal? AluminumWeight,
    decimal? BrakePadWeight,
    decimal? CorkWeight,
    decimal? SteelCost,
    decimal? PairWeight,
    decimal? SellingPrice,
    decimal? UnitCost,
    int? ProductTypeId,
    string? ProductTypeName,
    int? MoldId,
    string? MoldCode,
    int? CustomerId,
    string? CustomerCode,
    string? CustomerName,
    DateOnly? ProducedOn,
    int? ModelId,
    string? ModelName,
    string? Barcode,
    string? StandardNumber,
    int? BrakeTypeId,
    string? BrakeTypeName,
    string? Size,
    string? Formula,
    string? StandardModel,
    byte[] RowVersion);

/// <summary>A row of the product lists (RPT-21): the legacy columns of P_NAME that identify and price a product.</summary>
public sealed record ProductListLine(
    string Code,
    string Name,
    string? Unit,
    string? ProductTypeName,
    decimal? SellingPrice,
    decimal? UnitCost,
    string? MoldCode,
    string? CustomerCode,
    string? CustomerName);

public sealed record ProductTypeDto(int Id, string Name, byte[] RowVersion);

public sealed record BrakeTypeDto(int Id, string Code, string Name, byte[] RowVersion);

public sealed record ProductModelDto(int Id, string Name, byte[] RowVersion);

public sealed record ProductGroupDto(int Id, string Pattern, string Name, byte[] RowVersion);

/// <summary>
/// เพิ่ม/แก้/ลบ ทะเบียนสินค้า (SCR-03). ลูกค้าเจ้าของ is read from module ขายและส่งของ through
/// <see cref="CustomerDirectory"/> (ADR 0002). A newer save by someone else throws <see cref="ConcurrencyConflictException"/>.
/// </summary>
public sealed class ProductRegistry(ProdDbContext db, CustomerDirectory customers)
{
    /// <summary>T-P_NAME PD_C is Text(255) but real codes are short; BARCODE and CUS_ID are Text(50).</summary>
    public const int CodeLength = 50;
    public const int TextLength = 255;
    public const int BarcodeLength = 50;
    public const int BrakeTypeCodeLength = 10;
    public const int GroupPatternLength = 50;

    public async Task<ServiceResult> SaveAsync(ProductInput input)
    {
        var code = Code(input.Code);
        var name = Clean(input.Name);
        var errors = new List<string>();
        if (code is null) errors.Add("กรุณากรอกรหัสสินค้า");
        else if (code.Length > CodeLength) errors.Add($"รหัสสินค้ายาวได้ไม่เกิน {CodeLength} ตัว");
        else if (await db.Products.AnyAsync(p => p.Code == code && p.Id != input.Id)) errors.Add($"มีรหัสสินค้า '{code}' อยู่แล้ว");
        if (name is null) errors.Add("กรุณากรอกชื่อสินค้า");
        TooLong(errors, "ชื่อสินค้า", name, TextLength);
        TooLong(errors, "หน่วย", Clean(input.Unit), TextLength);
        TooLong(errors, "หน่วยบรรจุ", Clean(input.Pack), TextLength);
        TooLong(errors, "บาร์โค้ด", Clean(input.Barcode), BarcodeLength);
        TooLong(errors, "เลข มอก.", Clean(input.StandardNumber), TextLength);
        TooLong(errors, "ขนาด", Clean(input.Size), TextLength);
        TooLong(errors, "สูตร", Clean(input.Formula), TextLength);
        TooLong(errors, "รุ่น มอก.", Clean(input.StandardModel), TextLength);
        if (new[] { input.AluminumWeight, input.BrakePadWeight, input.CorkWeight, input.SteelCost, input.PairWeight }.Any(v => v < 0))
            errors.Add("น้ำหนักและค่าเหล็กต้องไม่ติดลบ");
        if (input.SellingPrice < 0 || input.UnitCost < 0) errors.Add("ราคาขายและต้นทุนต่อหน่วยต้องไม่ติดลบ");
        if (input.ProductTypeId is { } typeId && !await db.ProductTypes.AnyAsync(t => t.Id == typeId)) errors.Add("ไม่พบประเภทสินค้าที่เลือก");
        if (input.ModelId is { } modelId && !await db.ProductModels.AnyAsync(m => m.Id == modelId)) errors.Add("ไม่พบรุ่นที่เลือก");
        if (input.BrakeTypeId is { } brakeId && !await db.BrakeTypes.AnyAsync(t => t.Id == brakeId)) errors.Add("ไม่พบชนิดเบรกที่เลือก");
        if (input.MoldId is { } moldId && !await db.Molds.AnyAsync(m => m.Id == moldId)) errors.Add("ไม่พบแม่พิมพ์ที่เลือก");
        if (input.CustomerId is { } customerId && await customers.GetAsync(customerId) is null) errors.Add("ไม่พบลูกค้าที่เลือก");
        if (errors.Count > 0) return new ServiceResult(false, errors);

        var product = await LoadAsync(input.Id, input.RowVersion);
        if (input.Id is not null && product is null) return ServiceResult.Fail("ไม่พบสินค้านี้");
        product ??= db.Products.Add(new Product()).Entity;
        product.Code = code!;
        product.Name = name!;
        product.Unit = Clean(input.Unit);
        product.Pack = Clean(input.Pack);
        product.AluminumWeight = input.AluminumWeight;
        product.BrakePadWeight = input.BrakePadWeight;
        product.CorkWeight = input.CorkWeight;
        product.SteelCost = input.SteelCost;
        product.PairWeight = input.PairWeight;
        product.SellingPrice = input.SellingPrice;
        product.UnitCost = input.UnitCost;
        product.ProductTypeId = input.ProductTypeId;
        product.MoldId = input.MoldId;
        product.CustomerId = input.CustomerId;
        product.ProducedOn = input.ProducedOn;
        product.ModelId = input.ModelId;
        product.Barcode = Clean(input.Barcode);
        product.StandardNumber = Clean(input.StandardNumber);
        product.BrakeTypeId = input.BrakeTypeId;
        product.Size = Clean(input.Size);
        product.Formula = Clean(input.Formula);
        product.StandardModel = Clean(input.StandardModel);
        await db.SaveChangesAsync();
        return ServiceResult.Ok(product.Id);
    }

    /// <summary>
    /// ลบสินค้า (ปุ่มลบพร้อมหน้ายืนยัน — instead of clearing PD_N, BR-021). A product that other tables refer to
    /// (ORDER, ใบรายงานผลผลิต, สต็อก) is refused (ข้อสมมติ ง). Deleting a row that is already gone succeeds.
    /// </summary>
    public async Task<ServiceResult> DeleteAsync(int id, byte[] loadedRowVersion)
    {
        if (await LoadAsync(id, loadedRowVersion) is not { } product) return ServiceResult.Ok();
        db.Products.Remove(product);
        return await db.TrySaveDeleteAsync()
            ? ServiceResult.Ok()
            : ServiceResult.Fail($"ลบสินค้า '{product.Code}' ไม่ได้ เพราะมีรายการอื่นอ้างถึงอยู่");
    }

    private async Task<Product?> LoadAsync(int? id, byte[]? loadedRowVersion)
    {
        if (id is not { } productId) return null;
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == productId);
        product?.ExpectRowVersion(loadedRowVersion
            ?? throw new ArgumentException("Editing needs the rowversion the screen loaded.", nameof(loadedRowVersion)));
        return product;
    }

    private static void TooLong(List<string> errors, string label, string? value, int max)
    {
        if (value?.Length > max) errors.Add($"{label}ยาวได้ไม่เกิน {max} ตัว");
    }

    internal static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    /// <summary>Codes are compared and stored upper-case.</summary>
    internal static string? Code(string? text) => Clean(text)?.ToUpperInvariant();
}

/// <summary>
/// The lists SCR-03 chooses from — ประเภทสินค้า (T-ประเภท), ชนิดเบรก (T-TYPE), รุ่น (T-MODEL) and กลุ่มสินค้า
/// (T-รหัสสินค้าย่อ) — as master data the user edits. One that products use cannot be deleted.
/// </summary>
public sealed class ProductListRegistry(ProdDbContext db)
{
    public async Task<ServiceResult> SaveProductTypeAsync(int? id, string name, byte[]? rowVersion)
    {
        var clean = ProductRegistry.Clean(name);
        if (clean is null) return ServiceResult.Fail("กรุณากรอกชื่อประเภทสินค้า");
        if (clean.Length > ProductRegistry.TextLength) return ServiceResult.Fail($"ชื่อประเภทสินค้ายาวได้ไม่เกิน {ProductRegistry.TextLength} ตัว");
        if (await db.ProductTypes.AnyAsync(t => t.Name == clean && t.Id != id)) return ServiceResult.Fail($"มีประเภทสินค้า '{clean}' อยู่แล้ว");
        var row = id is { } existing ? Expect(await db.ProductTypes.FirstOrDefaultAsync(t => t.Id == existing), rowVersion) : db.ProductTypes.Add(new ProductType()).Entity;
        if (row is null) return ServiceResult.Fail("ไม่พบประเภทสินค้านี้");
        row.Name = clean;
        await db.SaveChangesAsync();
        return ServiceResult.Ok(row.Id);
    }

    public Task<ServiceResult> DeleteProductTypeAsync(int id, byte[] rowVersion) => DeleteAsync(db.ProductTypes, id, rowVersion, "ประเภทสินค้า", t => t.Name);

    public async Task<ServiceResult> SaveBrakeTypeAsync(int? id, string code, string name, byte[]? rowVersion)
    {
        var cleanCode = ProductRegistry.Code(code);
        var cleanName = ProductRegistry.Clean(name);
        var errors = new List<string>();
        if (cleanCode is null) errors.Add("กรุณากรอกรหัสชนิดเบรก");
        else if (cleanCode.Length > ProductRegistry.BrakeTypeCodeLength) errors.Add($"รหัสชนิดเบรกยาวได้ไม่เกิน {ProductRegistry.BrakeTypeCodeLength} ตัว");
        else if (await db.BrakeTypes.AnyAsync(t => t.Code == cleanCode && t.Id != id)) errors.Add($"มีรหัสชนิดเบรก '{cleanCode}' อยู่แล้ว");
        if (cleanName is null) errors.Add("กรุณากรอกชื่อชนิดเบรก");
        else if (cleanName.Length > ProductRegistry.TextLength) errors.Add($"ชื่อชนิดเบรกยาวได้ไม่เกิน {ProductRegistry.TextLength} ตัว");
        else if (await db.BrakeTypes.AnyAsync(t => t.Name == cleanName && t.Id != id)) errors.Add($"มีชนิดเบรก '{cleanName}' อยู่แล้ว");
        if (errors.Count > 0) return new ServiceResult(false, errors);
        var row = id is { } existing ? Expect(await db.BrakeTypes.FirstOrDefaultAsync(t => t.Id == existing), rowVersion) : db.BrakeTypes.Add(new BrakeType()).Entity;
        if (row is null) return ServiceResult.Fail("ไม่พบชนิดเบรกนี้");
        row.Code = cleanCode!;
        row.Name = cleanName!;
        await db.SaveChangesAsync();
        return ServiceResult.Ok(row.Id);
    }

    public Task<ServiceResult> DeleteBrakeTypeAsync(int id, byte[] rowVersion) => DeleteAsync(db.BrakeTypes, id, rowVersion, "ชนิดเบรก", t => t.Name);

    public async Task<ServiceResult> SaveModelAsync(int? id, string name, byte[]? rowVersion)
    {
        var clean = ProductRegistry.Clean(name);
        if (clean is null) return ServiceResult.Fail("กรุณากรอกชื่อรุ่น");
        if (clean.Length > ProductRegistry.TextLength) return ServiceResult.Fail($"ชื่อรุ่นยาวได้ไม่เกิน {ProductRegistry.TextLength} ตัว");
        if (await db.ProductModels.AnyAsync(m => m.Name == clean && m.Id != id)) return ServiceResult.Fail($"มีรุ่น '{clean}' อยู่แล้ว");
        var row = id is { } existing ? Expect(await db.ProductModels.FirstOrDefaultAsync(m => m.Id == existing), rowVersion) : db.ProductModels.Add(new ProductModel()).Entity;
        if (row is null) return ServiceResult.Fail("ไม่พบรุ่นนี้");
        row.Name = clean;
        await db.SaveChangesAsync();
        return ServiceResult.Ok(row.Id);
    }

    public Task<ServiceResult> DeleteModelAsync(int id, byte[] rowVersion) => DeleteAsync(db.ProductModels, id, rowVersion, "รุ่น", m => m.Name);

    public async Task<ServiceResult> SaveGroupAsync(int? id, string pattern, string name, byte[]? rowVersion)
    {
        var cleanPattern = ProductRegistry.Code(pattern);
        var cleanName = ProductRegistry.Clean(name);
        var errors = new List<string>();
        if (cleanPattern is null) errors.Add("กรุณากรอกรหัสกลุ่มสินค้า");
        else if (cleanPattern.Length > ProductRegistry.GroupPatternLength) errors.Add($"รหัสกลุ่มสินค้ายาวได้ไม่เกิน {ProductRegistry.GroupPatternLength} ตัว");
        else if (await db.ProductGroups.AnyAsync(g => g.Pattern == cleanPattern && g.Id != id)) errors.Add($"มีรหัสกลุ่มสินค้า '{cleanPattern}' อยู่แล้ว");
        if (cleanName is null) errors.Add("กรุณากรอกชื่อกลุ่มสินค้า");
        else if (cleanName.Length > ProductRegistry.TextLength) errors.Add($"ชื่อกลุ่มสินค้ายาวได้ไม่เกิน {ProductRegistry.TextLength} ตัว");
        if (errors.Count > 0) return new ServiceResult(false, errors);
        var row = id is { } existing ? Expect(await db.ProductGroups.FirstOrDefaultAsync(g => g.Id == existing), rowVersion) : db.ProductGroups.Add(new ProductGroup()).Entity;
        if (row is null) return ServiceResult.Fail("ไม่พบกลุ่มสินค้านี้");
        row.Pattern = cleanPattern!;
        row.Name = cleanName!;
        await db.SaveChangesAsync();
        return ServiceResult.Ok(row.Id);
    }

    public Task<ServiceResult> DeleteGroupAsync(int id, byte[] rowVersion) => DeleteAsync(db.ProductGroups, id, rowVersion, "กลุ่มสินค้า", g => g.Pattern);

    private static T? Expect<T>(T? row, byte[]? rowVersion) where T : class, IAudited
    {
        row?.ExpectRowVersion(rowVersion ?? throw new ArgumentException("Editing needs the rowversion the screen loaded.", nameof(rowVersion)));
        return row;
    }

    private async Task<ServiceResult> DeleteAsync<T>(DbSet<T> rows, int id, byte[] rowVersion, string label, Func<T, string> describe) where T : class, IAudited
    {
        if (Expect(await rows.FirstOrDefaultAsync(r => EF.Property<int>(r, "Id") == id), rowVersion) is not { } row) return ServiceResult.Ok();
        rows.Remove(row);
        var text = describe(row);
        return await db.TrySaveDeleteAsync()
            ? ServiceResult.Ok()
            : ServiceResult.Fail($"ลบ{label} '{text}' ไม่ได้ เพราะมีสินค้าใช้อยู่");
    }
}

/// <summary>The public, read-only face of ทะเบียนสินค้า and its lists (ADR 0002). Writes go through the registries.</summary>
public sealed class ProductDirectory(ProdDbContext db, CustomerDirectory customers)
{
    /// <summary>
    /// สินค้า by รหัส; <paramref name="text"/> (if any) must be in the code or name, ignoring case.
    /// <paramref name="customerId"/> = only that customer's products (ลูกค้าเจ้าของ).
    /// </summary>
    public async Task<IReadOnlyList<ProductListItem>> SearchAsync(string? text = null, int? customerId = null, int max = 500)
    {
        var term = text?.Trim();
        return await db.Products.AsNoTracking()
            .Where(p => customerId == null || p.CustomerId == customerId)
            .Where(p => string.IsNullOrEmpty(term) || p.Code.Contains(term) || p.Name.Contains(term))
            .OrderBy(p => p.Code).Take(max)
            .Select(p => new ProductListItem(p.Id, p.Code, p.Name, p.Unit)).ToListAsync();
    }

    /// <summary>
    /// The lines of รายชื่อสินค้า (RPT-21) by รหัส: all, <paramref name="codePattern"/> only (พิมพ์ตามรหัส — Access <c>LIKE</c>:
    /// <c>G1*</c>), and/or one customer's (พิมพ์ตามลูกค้า).
    /// </summary>
    public async Task<IReadOnlyList<ProductListLine>> GetListAsync(string? codePattern = null, int? customerId = null)
    {
        var rows = await db.Products.AsNoTracking().Include(p => p.ProductType).Include(p => p.Mold)
            .Where(p => customerId == null || p.CustomerId == customerId).OrderBy(p => p.Code).ToListAsync();
        var pattern = string.IsNullOrWhiteSpace(codePattern) ? null : codePattern.Trim();
        var customerNames = new Dictionary<int, CustomerDto?>();
        foreach (var id in rows.Where(r => r.CustomerId is not null).Select(r => r.CustomerId!.Value).Distinct())
            customerNames[id] = await customers.GetAsync(id);
        return rows.Where(p => pattern is null || ProductGroupPattern.Matches(pattern, p.Code))
            .Select(p => new ProductListLine(p.Code, p.Name, p.Unit, p.ProductType?.Name, p.SellingPrice, p.UnitCost, p.Mold?.Code,
                p.CustomerId is { } c ? customerNames[c]?.Code : null, p.CustomerId is { } c2 ? customerNames[c2]?.Name : null)).ToList();
    }

    public async Task<ProductDto?> GetAsync(int id)
        => await Query().FirstOrDefaultAsync(p => p.Id == id) is { } p ? await ToDtoAsync(p) : null;

    public async Task<ProductDto?> FindByCodeAsync(string code)
    {
        var normalized = ProductRegistry.Code(code);
        return await Query().FirstOrDefaultAsync(p => p.Code == normalized) is { } p ? await ToDtoAsync(p) : null;
    }

    public async Task<IReadOnlyList<ProductTypeDto>> GetProductTypesAsync()
        => await db.ProductTypes.AsNoTracking().OrderBy(t => t.Name).Select(t => new ProductTypeDto(t.Id, t.Name, t.RowVersion)).ToListAsync();

    public async Task<IReadOnlyList<BrakeTypeDto>> GetBrakeTypesAsync()
        => await db.BrakeTypes.AsNoTracking().OrderBy(t => t.Code).Select(t => new BrakeTypeDto(t.Id, t.Code, t.Name, t.RowVersion)).ToListAsync();

    public async Task<IReadOnlyList<ProductModelDto>> GetModelsAsync()
        => await db.ProductModels.AsNoTracking().OrderBy(m => m.Name).Select(m => new ProductModelDto(m.Id, m.Name, m.RowVersion)).ToListAsync();

    public async Task<IReadOnlyList<ProductGroupDto>> GetGroupsAsync()
        => await db.ProductGroups.AsNoTracking().OrderBy(g => g.Pattern).Select(g => new ProductGroupDto(g.Id, g.Pattern, g.Name, g.RowVersion)).ToListAsync();

    /// <summary>
    /// The กลุ่มสินค้า a product code belongs to: every group whose pattern matches it. Patterns overlap on purpose
    /// (G1* and G3* are both ก้ามเบรก; <c>*</c> matches everything — Q-26), so there can be several.
    /// </summary>
    public async Task<IReadOnlyList<ProductGroupDto>> GroupsOfAsync(string productCode)
    {
        var code = ProductRegistry.Code(productCode);
        if (code is null) return [];
        return (await GetGroupsAsync()).Where(g => ProductGroupPattern.Matches(g.Pattern, code)).ToList();
    }

    private IQueryable<Product> Query()
        => db.Products.AsNoTracking().Include(p => p.ProductType).Include(p => p.Mold).Include(p => p.Model).Include(p => p.BrakeType);

    private async Task<ProductDto> ToDtoAsync(Product p)
    {
        var customer = p.CustomerId is { } id ? await customers.GetAsync(id) : null;
        return new ProductDto(
            p.Id, p.Code, p.Name, p.Unit, p.Pack, p.AluminumWeight, p.BrakePadWeight, p.CorkWeight, p.SteelCost, p.PairWeight,
            p.SellingPrice, p.UnitCost, p.ProductTypeId, p.ProductType?.Name, p.MoldId, p.Mold?.Code, p.CustomerId, customer?.Code,
            customer?.Name, p.ProducedOn, p.ModelId, p.Model?.Name, p.Barcode, p.StandardNumber, p.BrakeTypeId, p.BrakeType?.Name,
            p.Size, p.Formula, p.StandardModel, p.RowVersion);
    }
}

/// <summary>Access <c>LIKE</c> on a product code (T-รหัสสินค้าย่อ.C): <c>*</c> any text, <c>?</c> one character, <c>#</c> one digit; case-insensitive.</summary>
public static class ProductGroupPattern
{
    public static bool Matches(string pattern, string code)
    {
        var regex = new System.Text.StringBuilder("^");
        foreach (var c in pattern)
            regex.Append(c switch { '*' => ".*", '?' => ".", '#' => "[0-9]", _ => System.Text.RegularExpressions.Regex.Escape(c.ToString()) });
        return System.Text.RegularExpressions.Regex.IsMatch(code, regex.Append('$').ToString(),
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);
    }
}
