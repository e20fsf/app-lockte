using Lockte.Cash.Data;
using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Cash;

public sealed record ScrapBuyerDto(int Id, string Name, string? Address, string? Tel, byte[] RowVersion);

/// <summary>The fields of a buyer on the screen; <see cref="Id"/> null = a new one.</summary>
public sealed record ScrapBuyerInput(int? Id, string? Name, string? Address, string? Tel, byte[]? RowVersion);

public sealed record ScrapItemDto(int Id, string Name, string? Unit, byte[] RowVersion);

/// <summary>The fields of a scrap kind on the screen; <see cref="Id"/> null = a new one.</summary>
public sealed record ScrapItemInput(int? Id, string? Name, string? Unit, byte[]? RowVersion);

/// <summary>The public, read-only face of ผู้ซื้อของเก่า (ADR 0002): the pick list of the receipt (SCR-09) and RPT-10 read it.</summary>
public sealed class ScrapBuyerDirectory(CashDbContext db)
{
    private static readonly StringComparer Thai = StringComparer.Create(ThaiDate.Culture, ignoreCase: true);

    /// <summary>By name in the Thai dictionary order.</summary>
    public async Task<IReadOnlyList<ScrapBuyerDto>> ListAsync()
        => (await db.ScrapBuyers.AsNoTracking().ToListAsync())
            .OrderBy(b => b.Name, Thai).ThenBy(b => b.Id)
            .Select(b => new ScrapBuyerDto(b.Id, b.Name, b.Address, b.Tel, b.RowVersion)).ToList();

    public async Task<ScrapBuyerDto?> GetAsync(int id)
        => await db.ScrapBuyers.AsNoTracking().Where(b => b.Id == id).Select(b => new ScrapBuyerDto(b.Id, b.Name, b.Address, b.Tel, b.RowVersion)).FirstOrDefaultAsync();
}

/// <summary>
/// เพิ่ม/แก้/ลบผู้ซื้อของเก่า (SCR-07, BR-010). Unlike the legacy the name is required and unique (after the blanks at the ends and repeated blanks are taken out — 10 of the 12 legacy
/// names had stray blanks), a name with an apostrophe is fine, and a buyer is deleted with a button instead of by clearing its name.
/// </summary>
public sealed class ScrapBuyerRegistry(CashDbContext db)
{
    /// <summary>The receipt keeps the name in 100 characters (data-model T-ขายของเก่า.NAMED).</summary>
    public const int NameLength = 100;
    public const int AddressLength = 255;
    public const int TelLength = 50;

    public async Task<ServiceResult> SaveAsync(ScrapBuyerInput input)
    {
        var name = CashText.Clean(input.Name);
        var address = CashText.Clean(input.Address);
        var tel = CashText.Clean(input.Tel);
        if (name is null) return ServiceResult.Fail("กรุณากรอกชื่อผู้ซื้อ");
        var errors = new List<string>();
        if (name.Length > NameLength) errors.Add($"ชื่อผู้ซื้อยาวได้ไม่เกิน {NameLength} ตัว");
        if (address?.Length > AddressLength) errors.Add($"ที่อยู่ยาวได้ไม่เกิน {AddressLength} ตัว");
        if (tel?.Length > TelLength) errors.Add($"เบอร์โทรศัพท์ยาวได้ไม่เกิน {TelLength} ตัว");
        if (errors.Count > 0) return new ServiceResult(false, errors);
        if (await db.ScrapBuyers.AnyAsync(b => b.Name == name && b.Id != (input.Id ?? 0))) return ServiceResult.Fail($"มีผู้ซื้อ '{name}' อยู่แล้ว");

        ScrapBuyer row;
        if (input.Id is { } id)
        {
            row = await LoadAsync(id, input.RowVersion) ?? throw new InvalidOperationException($"No scrap buyer {id}.");
            (row.Name, row.Address, row.Tel) = (name, address, tel);
        }
        else row = db.ScrapBuyers.Add(new ScrapBuyer { Name = name, Address = address, Tel = tel }).Entity;
        await db.SaveChangesAsync();
        return ServiceResult.Ok(row.Id);
    }

    /// <summary>ลบ: the buyer goes; receipts already issued keep their own copy of the name, address and phone.</summary>
    public async Task<ServiceResult> DeleteAsync(int id, byte[] loadedRowVersion)
    {
        if (await LoadAsync(id, loadedRowVersion) is not { } row) return ServiceResult.Ok();
        db.ScrapBuyers.Remove(row);
        await db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    private async Task<ScrapBuyer?> LoadAsync(int id, byte[]? loadedRowVersion)
    {
        var row = await db.ScrapBuyers.FirstOrDefaultAsync(b => b.Id == id);
        row?.ExpectRowVersion(loadedRowVersion ?? throw new ArgumentException("Editing needs the rowversion the screen loaded.", nameof(loadedRowVersion)));
        return row;
    }
}

/// <summary>The public, read-only face of ชนิดของเก่า (ADR 0002): the pick list of the receipt (SCR-09) and RPT-11 read it.</summary>
public sealed class ScrapItemDirectory(CashDbContext db)
{
    private static readonly StringComparer Thai = StringComparer.Create(ThaiDate.Culture, ignoreCase: true);

    /// <summary>By name in the Thai dictionary order.</summary>
    public async Task<IReadOnlyList<ScrapItemDto>> ListAsync()
        => (await db.ScrapItems.AsNoTracking().ToListAsync())
            .OrderBy(i => i.Name, Thai).ThenBy(i => i.Id)
            .Select(i => new ScrapItemDto(i.Id, i.Name, i.Unit, i.RowVersion)).ToList();

    public async Task<ScrapItemDto?> GetAsync(int id)
        => await db.ScrapItems.AsNoTracking().Where(i => i.Id == id).Select(i => new ScrapItemDto(i.Id, i.Name, i.Unit, i.RowVersion)).FirstOrDefaultAsync();
}

/// <summary>เพิ่ม/แก้/ลบชนิดของเก่า (SCR-08, BR-010), with the same differences from the legacy as <see cref="ScrapBuyerRegistry"/>.</summary>
public sealed class ScrapItemRegistry(CashDbContext db)
{
    public const int NameLength = 255;
    public const int UnitLength = 50;

    public async Task<ServiceResult> SaveAsync(ScrapItemInput input)
    {
        var name = CashText.Clean(input.Name);
        var unit = CashText.Clean(input.Unit);
        if (name is null) return ServiceResult.Fail("กรุณากรอกชื่อของเก่า");
        var errors = new List<string>();
        if (name.Length > NameLength) errors.Add($"ชื่อของเก่ายาวได้ไม่เกิน {NameLength} ตัว");
        if (unit?.Length > UnitLength) errors.Add($"หน่วยยาวได้ไม่เกิน {UnitLength} ตัว");
        if (errors.Count > 0) return new ServiceResult(false, errors);
        if (await db.ScrapItems.AnyAsync(i => i.Name == name && i.Id != (input.Id ?? 0))) return ServiceResult.Fail($"มีของเก่า '{name}' อยู่แล้ว");

        ScrapItem row;
        if (input.Id is { } id)
        {
            row = await LoadAsync(id, input.RowVersion) ?? throw new InvalidOperationException($"No scrap item {id}.");
            (row.Name, row.Unit) = (name, unit);
        }
        else row = db.ScrapItems.Add(new ScrapItem { Name = name, Unit = unit }).Entity;
        await db.SaveChangesAsync();
        return ServiceResult.Ok(row.Id);
    }

    /// <summary>ลบ: the kind goes; receipts already issued keep their own copy of the name and unit.</summary>
    public async Task<ServiceResult> DeleteAsync(int id, byte[] loadedRowVersion)
    {
        if (await LoadAsync(id, loadedRowVersion) is not { } row) return ServiceResult.Ok();
        db.ScrapItems.Remove(row);
        await db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    private async Task<ScrapItem?> LoadAsync(int id, byte[]? loadedRowVersion)
    {
        var row = await db.ScrapItems.FirstOrDefaultAsync(i => i.Id == id);
        row?.ExpectRowVersion(loadedRowVersion ?? throw new ArgumentException("Editing needs the rowversion the screen loaded.", nameof(loadedRowVersion)));
        return row;
    }
}
