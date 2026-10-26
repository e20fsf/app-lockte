using Lockte.Sales.Data;
using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Sales;

public sealed record CustomerDto(int Id, string Code, string Name, byte[] RowVersion);

/// <summary><see cref="Id"/> null = new customer; editing needs <see cref="RowVersion"/> as loaded.</summary>
public sealed record CustomerInput(int? Id, string Code, string Name, byte[]? RowVersion);

/// <summary>
/// เพิ่ม/แก้ ทะเบียนลูกค้าขั้นต่ำ (รหัส + ชื่อ). The data-migration tool fills it now; the ORDER program gets its screen.
/// A newer save by someone else throws <see cref="ConcurrencyConflictException"/>.
/// </summary>
public sealed class CustomerRegistry(SalesDbContext db)
{
    /// <summary>รหัสลูกค้าของโปรแกรม ORDER มีรูปแบบ SS-GG-NNN (T-P_CUS.CODE).</summary>
    public const int CodeLength = 20;
    /// <summary>T-P_CUS / T-ชื่อลูกค้า NAME is Text(255).</summary>
    public const int NameLength = 255;

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
        if (errors.Count > 0) return new ServiceResult(false, errors);

        Customer? customer;
        if (input.Id is { } id)
        {
            customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == id);
            if (customer is null) return ServiceResult.Fail("ไม่พบลูกค้ารายนี้");
            customer.ExpectRowVersion(input.RowVersion
                ?? throw new ArgumentException("Editing needs the rowversion the screen loaded.", nameof(input)));
        }
        else
        {
            customer = db.Customers.Add(new Customer()).Entity;
        }
        customer.Code = code!;
        customer.Name = name!;
        await db.SaveChangesAsync();
        return ServiceResult.Ok(customer.Id);
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    /// <summary>Codes are compared and stored upper-case.</summary>
    internal static string? Code(string? text) => Clean(text)?.ToUpperInvariant();
}

/// <summary>The public, read-only face of ทะเบียนลูกค้า (ADR 0002). Writes go through <see cref="CustomerRegistry"/>.</summary>
public sealed class CustomerDirectory(SalesDbContext db)
{
    public async Task<IReadOnlyList<CustomerDto>> GetAllAsync()
        => await db.Customers.AsNoTracking().OrderBy(c => c.Code)
            .Select(c => new CustomerDto(c.Id, c.Code, c.Name, c.RowVersion)).ToListAsync();

    public async Task<CustomerDto?> GetAsync(int id)
        => await db.Customers.AsNoTracking().Where(c => c.Id == id)
            .Select(c => new CustomerDto(c.Id, c.Code, c.Name, c.RowVersion)).FirstOrDefaultAsync();

    public async Task<CustomerDto?> FindByCodeAsync(string code)
    {
        var normalized = CustomerRegistry.Code(code);
        return await db.Customers.AsNoTracking().Where(c => c.Code == normalized)
            .Select(c => new CustomerDto(c.Id, c.Code, c.Name, c.RowVersion)).FirstOrDefaultAsync();
    }
}
