using System.Text.RegularExpressions;
using Lockte.Sales.Data;
using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Sales;

public sealed record CustomerDto(
    int Id,
    string Code,
    string Name,
    byte[] RowVersion,
    int? CreditDays = null,
    string? Address = null,
    string? District = null,
    string? Province = null,
    string? PostalCode = null,
    string? TransportName = null,
    string? TransportAddress = null,
    string? TransportPhone = null,
    string? Phone1 = null,
    string? Phone2 = null,
    int? SalesTerritoryId = null,
    string? SalesTerritoryCode = null,
    string? Remark = null)
{
    /// <summary>เงื่อนไขการชำระเงิน as ORDER prints it: "เงินสด" for 0 days, "60 วัน", blank when not known.</summary>
    public string CreditText => CreditTerm.Format(CreditDays);
}

/// <summary>
/// The fields of SCR-02. <see cref="Id"/> null = new customer; editing needs <see cref="RowVersion"/> as loaded.
/// <see cref="CreditDays"/>: 0 = เงินสด. The เขตการขาย is what the user chose — the screen proposes it from the code
/// when adding (BR-023, <see cref="SalesTerritoryDirectory.DefaultForCustomerCodeAsync"/>).
/// </summary>
public sealed record CustomerInput(
    int? Id,
    string Code,
    string Name,
    byte[]? RowVersion,
    int? CreditDays = null,
    string? Address = null,
    string? District = null,
    string? Province = null,
    string? PostalCode = null,
    string? TransportName = null,
    string? TransportAddress = null,
    string? TransportPhone = null,
    string? Phone1 = null,
    string? Phone2 = null,
    int? SalesTerritoryId = null,
    string? Remark = null);

/// <summary>
/// เพิ่ม/แก้/ลบ ทะเบียนลูกค้า (SCR-02). The data-migration tool fills it too (a customer it knows only by รหัส + ชื่อ is
/// saved with the other fields blank). A newer save by someone else throws <see cref="ConcurrencyConflictException"/>.
/// </summary>
public sealed class CustomerRegistry(SalesDbContext db)
{
    /// <summary>รหัสลูกค้าของโปรแกรม ORDER มีรูปแบบ SS-GG-NNN (T-P_CUS.CODE) — not enforced.</summary>
    public const int CodeLength = 20;
    /// <summary>T-P_CUS / T-ชื่อลูกค้า NAME is Text(255).</summary>
    public const int NameLength = 255;
    public const int TextLength = 255;
    public const int PostalCodeLength = 50;
    /// <summary>The longest credit anyone could mean (days).</summary>
    public const int MaxCreditDays = 999;

    public async Task<ServiceResult> SaveAsync(CustomerInput input)
    {
        var code = Code(input.Code);
        var name = Clean(input.Name);
        var errors = new List<string>();
        if (code is null) errors.Add("กรุณากรอกรหัสลูกค้า");
        else if (code.Length > CodeLength) errors.Add($"รหัสลูกค้ายาวได้ไม่เกิน {CodeLength} ตัว");
        else if (await db.Customers.AnyAsync(c => c.Code == code && c.Id != input.Id)) errors.Add($"มีรหัสลูกค้า '{code}' อยู่แล้ว");
        if (name is null) errors.Add("กรุณากรอกชื่อลูกค้า");
        else if (name.Length > NameLength) errors.Add($"ชื่อลูกค้ายาวได้ไม่เกิน {NameLength} ตัว");
        if (input.CreditDays is < 0 or > MaxCreditDays) errors.Add($"เครดิตต้องเป็น 0 (เงินสด) ถึง {MaxCreditDays} วัน");
        TooLong(errors, "ที่อยู่", Clean(input.Address), TextLength);
        TooLong(errors, "อำเภอ", Clean(input.District), TextLength);
        TooLong(errors, "จังหวัด", Clean(input.Province), TextLength);
        TooLong(errors, "รหัสไปรษณีย์", Clean(input.PostalCode), PostalCodeLength);
        TooLong(errors, "ชื่อขนส่ง", Clean(input.TransportName), TextLength);
        TooLong(errors, "ที่อยู่ขนส่ง", Clean(input.TransportAddress), TextLength);
        TooLong(errors, "โทรฯ ขนส่ง", Clean(input.TransportPhone), TextLength);
        TooLong(errors, "เบอร์โทร 1", Clean(input.Phone1), TextLength);
        TooLong(errors, "เบอร์โทร 2", Clean(input.Phone2), TextLength);
        TooLong(errors, "หมายเหตุ", Clean(input.Remark), TextLength);
        if (input.SalesTerritoryId is { } territoryId && !await db.SalesTerritories.AnyAsync(t => t.Id == territoryId))
            errors.Add("ไม่พบเขตการขายที่เลือก");
        if (errors.Count > 0) return new ServiceResult(false, errors);

        var customer = await LoadAsync(input.Id, input.RowVersion);
        if (input.Id is not null && customer is null) return ServiceResult.Fail("ไม่พบลูกค้ารายนี้");
        customer ??= db.Customers.Add(new Customer()).Entity;
        customer.Code = code!;
        customer.Name = name!;
        customer.CreditDays = input.CreditDays;
        customer.Address = Clean(input.Address);
        customer.District = Clean(input.District);
        customer.Province = Clean(input.Province);
        customer.PostalCode = Clean(input.PostalCode);
        customer.TransportName = Clean(input.TransportName);
        customer.TransportAddress = Clean(input.TransportAddress);
        customer.TransportPhone = Clean(input.TransportPhone);
        customer.Phone1 = Clean(input.Phone1);
        customer.Phone2 = Clean(input.Phone2);
        customer.SalesTerritoryId = input.SalesTerritoryId;
        customer.Remark = Clean(input.Remark);
        await db.SaveChangesAsync();
        return ServiceResult.Ok(customer.Id);
    }

    /// <summary>
    /// ลบลูกค้า (ปุ่มลบพร้อมหน้ายืนยัน — instead of clearing CODE, BR-021). A customer that other tables refer to
    /// (ORDER, การส่งของ, สินค้า) is refused (ข้อสมมติ ง). Deleting a row that is already gone succeeds.
    /// </summary>
    public async Task<ServiceResult> DeleteAsync(int id, byte[] loadedRowVersion)
    {
        if (await LoadAsync(id, loadedRowVersion) is not { } customer) return ServiceResult.Ok();
        db.Customers.Remove(customer);
        return await db.TrySaveDeleteAsync()
            ? ServiceResult.Ok()
            : ServiceResult.Fail($"ลบลูกค้า '{customer.Code}' ไม่ได้ เพราะมีรายการอื่นอ้างถึงอยู่");
    }

    private async Task<Customer?> LoadAsync(int? id, byte[]? loadedRowVersion)
    {
        if (id is not { } customerId) return null;
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == customerId);
        customer?.ExpectRowVersion(loadedRowVersion
            ?? throw new ArgumentException("Editing needs the rowversion the screen loaded.", nameof(loadedRowVersion)));
        return customer;
    }

    private static void TooLong(List<string> errors, string label, string? value, int max)
    {
        if (value?.Length > max) errors.Add($"{label}ยาวได้ไม่เกิน {max} ตัว");
    }

    internal static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    /// <summary>Codes are compared and stored upper-case.</summary>
    internal static string? Code(string? text) => Clean(text)?.ToUpperInvariant();
}

/// <summary>เครดิต: the legacy text (<c>60 วัน</c>, <c>เงินสด</c>) as a number of days, and back.</summary>
public static class CreditTerm
{
    /// <summary>เงินสด / cash → 0; <c>60</c>, <c>60 วัน</c>, <c>เครดิต 60 วัน</c> → 60; anything else null (reported, not guessed).</summary>
    public static int? Parse(string? text)
    {
        var clean = text?.Trim();
        if (string.IsNullOrEmpty(clean)) return null;
        if (clean is "เงินสด" or "สด" || clean.Equals("cash", StringComparison.OrdinalIgnoreCase)) return 0;
        var match = Regex.Match(clean, @"^(?:เครดิต\s*)?(\d{1,3})\s*(?:วัน)?$");
        return match.Success ? int.Parse(match.Groups[1].Value) : null;
    }

    /// <summary>"เงินสด" for 0, "N วัน" otherwise, blank for unknown — the text of the ORDER form's เงื่อนไขการชำระเงิน.</summary>
    public static string Format(int? days) => days switch { null => "", 0 => "เงินสด", _ => $"{days} วัน" };
}

/// <summary>The public, read-only face of ทะเบียนลูกค้า (ADR 0002). Writes go through <see cref="CustomerRegistry"/>.</summary>
public sealed class CustomerDirectory(SalesDbContext db)
{
    public async Task<IReadOnlyList<CustomerDto>> GetAllAsync()
        => (await Query().OrderBy(c => c.Code).ToListAsync()).Select(ToDto).ToList();

    public async Task<CustomerDto?> GetAsync(int id)
        => await Query().FirstOrDefaultAsync(c => c.Id == id) is { } c ? ToDto(c) : null;

    public async Task<CustomerDto?> FindByCodeAsync(string code)
    {
        var normalized = CustomerRegistry.Code(code);
        return await Query().FirstOrDefaultAsync(c => c.Code == normalized) is { } c ? ToDto(c) : null;
    }

    /// <summary>ลูกค้า whose code or name contains <paramref name="text"/>, by รหัส.</summary>
    public async Task<IReadOnlyList<CustomerDto>> SearchAsync(string? text, int max = 200)
    {
        var term = text?.Trim();
        return (await Query().Where(c => string.IsNullOrEmpty(term) || c.Code.Contains(term) || c.Name.Contains(term))
            .OrderBy(c => c.Code).Take(max).ToListAsync()).Select(ToDto).ToList();
    }

    private IQueryable<Customer> Query() => db.Customers.AsNoTracking().Include(c => c.SalesTerritory);

    private static CustomerDto ToDto(Customer c) => new(
        c.Id, c.Code, c.Name, c.RowVersion, c.CreditDays, c.Address, c.District, c.Province, c.PostalCode, c.TransportName,
        c.TransportAddress, c.TransportPhone, c.Phone1, c.Phone2, c.SalesTerritoryId, c.SalesTerritory?.Code, c.Remark);
}
