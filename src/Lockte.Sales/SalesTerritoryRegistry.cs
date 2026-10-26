using Lockte.Sales.Data;
using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Sales;

/// <summary>An employee of module พนักงานและค่าแรง as เขตการขาย names one (พนักงานขาย).</summary>
public sealed record SalesRepresentativeRef(int Id, string Code, string Name);

/// <summary>
/// How module ขายและส่งของ reads the employee register of module พนักงานและค่าแรง (ADR 0002). Defined here and
/// implemented by <c>hr</c>, which already depends on this module — the other way round would be a project cycle.
/// </summary>
public interface IEmployeeLookup
{
    Task<SalesRepresentativeRef?> FindAsync(int employeeId);
}

public sealed record SalesTerritoryDto(
    int Id,
    string Code,
    string Name,
    int? EmployeeId,
    string? EmployeeCode,
    string? EmployeeName,
    byte[] RowVersion);

/// <summary><see cref="Id"/> null = new territory; editing needs <see cref="RowVersion"/> as loaded.</summary>
public sealed record SalesTerritoryInput(int? Id, string Code, string Name, int? EmployeeId, byte[]? RowVersion);

/// <summary>
/// เพิ่ม/แก้/ลบ เขตการขาย (SCR-07, T-เซล). The พนักงานขาย is picked from the employee register, blank allowed
/// (<c>00</c> ออฟฟิศ, <c>11</c> เซลออนไลน์). The name is required: clearing it is an error, never a delete (BR-021 → ปุ่มลบ).
/// </summary>
public sealed class SalesTerritoryRegistry(SalesDbContext db, IEmployeeLookup? employees = null)
{
    /// <summary>T-เซล.ID is Text(4); the codes are <c>00</c>–<c>11</c>.</summary>
    public const int CodeLength = 4;
    public const int NameLength = 255;

    public async Task<ServiceResult> SaveAsync(SalesTerritoryInput input)
    {
        var code = Clean(input.Code);
        var name = Clean(input.Name);
        var errors = new List<string>();
        if (code is null) errors.Add("กรุณากรอกรหัสเขตการขาย");
        else if (code.Length > CodeLength) errors.Add($"รหัสเขตการขายยาวได้ไม่เกิน {CodeLength} ตัว");
        else if (await db.SalesTerritories.AnyAsync(t => t.Code == code && t.Id != input.Id)) errors.Add($"มีรหัสเขตการขาย '{code}' อยู่แล้ว");
        if (name is null) errors.Add("กรุณากรอกชื่อเขตการขาย");
        else if (name.Length > NameLength) errors.Add($"ชื่อเขตการขายยาวได้ไม่เกิน {NameLength} ตัว");
        if (input.EmployeeId is { } employeeId && (employees is null || await employees.FindAsync(employeeId) is null))
            errors.Add("ไม่พบพนักงานขายที่เลือกในทะเบียนพนักงาน");
        if (errors.Count > 0) return new ServiceResult(false, errors);

        var territory = await LoadAsync(input.Id, input.RowVersion);
        if (input.Id is not null && territory is null) return ServiceResult.Fail("ไม่พบเขตการขายนี้");
        territory ??= db.SalesTerritories.Add(new SalesTerritory()).Entity;
        territory.Code = code!;
        territory.Name = name!;
        territory.EmployeeId = input.EmployeeId;
        await db.SaveChangesAsync();
        return ServiceResult.Ok(territory.Id);
    }

    /// <summary>
    /// ลบเขตการขาย (ปุ่มลบพร้อมหน้ายืนยัน). A territory that customers (or ORDER) refer to is refused (ข้อสมมติ ง).
    /// Deleting a row that is already gone succeeds.
    /// </summary>
    public async Task<ServiceResult> DeleteAsync(int id, byte[] loadedRowVersion)
    {
        if (await LoadAsync(id, loadedRowVersion) is not { } territory) return ServiceResult.Ok();
        db.SalesTerritories.Remove(territory);
        return await db.TrySaveDeleteAsync()
            ? ServiceResult.Ok()
            : ServiceResult.Fail($"ลบเขตการขาย '{territory.Code}' ไม่ได้ เพราะมีลูกค้าหรือรายการอื่นอ้างถึงอยู่");
    }

    private async Task<SalesTerritory?> LoadAsync(int? id, byte[]? loadedRowVersion)
    {
        if (id is not { } territoryId) return null;
        var territory = await db.SalesTerritories.FirstOrDefaultAsync(t => t.Id == territoryId);
        territory?.ExpectRowVersion(loadedRowVersion
            ?? throw new ArgumentException("Editing needs the rowversion the screen loaded.", nameof(loadedRowVersion)));
        return territory;
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}

/// <summary>The public, read-only face of ทะเบียนเขตการขาย (ADR 0002). Writes go through <see cref="SalesTerritoryRegistry"/>.</summary>
public sealed class SalesTerritoryDirectory(SalesDbContext db, IEmployeeLookup? employees = null)
{
    public async Task<IReadOnlyList<SalesTerritoryDto>> GetAllAsync()
    {
        var rows = await db.SalesTerritories.AsNoTracking().OrderBy(t => t.Code).ToListAsync();
        var result = new List<SalesTerritoryDto>();
        foreach (var t in rows) result.Add(await ToDtoAsync(t));
        return result;
    }

    public async Task<SalesTerritoryDto?> GetAsync(int id)
        => await db.SalesTerritories.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id) is { } t ? await ToDtoAsync(t) : null;

    public async Task<SalesTerritoryDto?> FindByCodeAsync(string code)
    {
        var clean = code.Trim();
        return await db.SalesTerritories.AsNoTracking().FirstOrDefaultAsync(t => t.Code == clean) is { } t ? await ToDtoAsync(t) : null;
    }

    /// <summary>
    /// BR-023: the เขตการขาย a new customer starts with — the first two characters of its code, or <c>00</c> (ออฟฟิศ) when
    /// they compare greater than <c>09</c> as text. ⚠ So เขต 10 and 11 are never proposed (kept as before, Q-20). Null when
    /// the code has no two characters or that territory is not in the register.
    /// </summary>
    public async Task<SalesTerritoryDto?> DefaultForCustomerCodeAsync(string customerCode)
    {
        var code = customerCode.Trim();
        if (code.Length < 2) return null;
        var first = code[..2];
        return await FindByCodeAsync(string.CompareOrdinal(first, "09") > 0 ? "00" : first);
    }

    private async Task<SalesTerritoryDto> ToDtoAsync(SalesTerritory t)
    {
        var employee = t.EmployeeId is { } id && employees is not null ? await employees.FindAsync(id) : null;
        return new SalesTerritoryDto(t.Id, t.Code, t.Name, t.EmployeeId, employee?.Code, employee?.Name, t.RowVersion);
    }
}
