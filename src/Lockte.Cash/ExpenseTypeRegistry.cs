using System.Text.RegularExpressions;
using Lockte.Cash.Data;
using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Cash;

public sealed record ExpenseTypeDto(int Id, string Name, bool IsActive, byte[] RowVersion);

/// <summary>The fields of a type on the screen; <see cref="Id"/> null = a new one.</summary>
public sealed record ExpenseTypeInput(int? Id, string? Name, byte[]? RowVersion);

/// <summary>What <see cref="ExpenseTypeRegistry.RemoveAsync"/> did: the legacy could not delete at all (BR-001 ⚠), here an unused type goes and a used one is retired.</summary>
public enum ExpenseTypeRemoval { Deleted, Retired, NotFound }

/// <summary>The public, read-only face of ประเภทค่าใช้จ่าย (ADR 0002): pick lists, the register and the print RPT-05 read it.</summary>
public sealed class ExpenseTypeDirectory(CashDbContext db)
{
    private static readonly StringComparer Thai = StringComparer.Create(SharedKernel.ThaiDate.Culture, ignoreCase: true);

    /// <summary>The types to pick from on a line (SCR-03/04) and to print (RPT-05): active only, by name in the Thai dictionary order.</summary>
    public async Task<IReadOnlyList<ExpenseTypeDto>> ListActiveAsync() => Order(await db.ExpenseTypes.AsNoTracking().Where(t => t.IsActive).ToListAsync());

    /// <summary>All of them, retired ones too (the register screen, and reading old lines).</summary>
    public async Task<IReadOnlyList<ExpenseTypeDto>> ListAllAsync() => Order(await db.ExpenseTypes.AsNoTracking().ToListAsync());

    public async Task<ExpenseTypeDto?> GetAsync(int id)
        => await db.ExpenseTypes.AsNoTracking().Where(t => t.Id == id).Select(t => new ExpenseTypeDto(t.Id, t.Name, t.IsActive, t.RowVersion)).FirstOrDefaultAsync();

    private static IReadOnlyList<ExpenseTypeDto> Order(List<ExpenseType> rows)
        => rows.OrderBy(t => t.Name, Thai).ThenBy(t => t.Id).Select(t => new ExpenseTypeDto(t.Id, t.Name, t.IsActive, t.RowVersion)).ToList();
}

/// <summary>
/// เพิ่ม/แก้/ลบ/เลิกใช้ประเภทค่าใช้จ่าย (SCR-02, BR-001). Names are unique after the blanks at either end, repeated blanks and line breaks are taken out (the legacy had
/// 70 lines whose type carried a line break, data-model); editing renames the type itself (Q-07) instead of overwriting the current row.
/// </summary>
public sealed class ExpenseTypeRegistry(CashDbContext db)
{
    public const int NameLength = 255;

    private static readonly Regex Blanks = new(@"\s+", RegexOptions.Compiled);

    /// <summary>The name as it is kept: leading/trailing blanks gone, any run of blanks or line breaks one space.</summary>
    public static string? CleanName(string? name) => string.IsNullOrWhiteSpace(name) ? null : Blanks.Replace(name.Trim(), " ");

    public async Task<ServiceResult> SaveAsync(ExpenseTypeInput input)
    {
        var name = CleanName(input.Name);
        if (name is null) return ServiceResult.Fail("กรุณากรอกชื่อค่าใช้จ่าย");
        if (name.Length > NameLength) return ServiceResult.Fail($"ชื่อค่าใช้จ่ายยาวได้ไม่เกิน {NameLength} ตัว");
        if (await db.ExpenseTypes.AnyAsync(t => t.Name == name && t.Id != (input.Id ?? 0)))
            return ServiceResult.Fail($"มีประเภทค่าใช้จ่าย '{name}' อยู่แล้ว");

        ExpenseType row;
        if (input.Id is { } id)
        {
            row = await LoadAsync(id, input.RowVersion) ?? throw new InvalidOperationException($"No expense type {id}.");
            row.Name = name;
        }
        else row = db.ExpenseTypes.Add(new ExpenseType { Name = name }).Entity;
        await db.SaveChangesAsync();
        return ServiceResult.Ok(row.Id);
    }

    /// <summary>Retire (เลิกใช้) or bring back (เปิดใช้อีกครั้ง) a type.</summary>
    public async Task<ServiceResult> SetActiveAsync(int id, bool active, byte[] loadedRowVersion)
    {
        if (await LoadAsync(id, loadedRowVersion) is not { } row) return ServiceResult.Fail("ไม่พบประเภทค่าใช้จ่าย");
        row.IsActive = active;
        await db.SaveChangesAsync();
        return ServiceResult.Ok(id);
    }

    /// <summary>
    /// ลบ: a type no line refers to is deleted; one that was used becomes เลิกใช้ instead (its lines keep showing its name).
    /// </summary>
    public async Task<ExpenseTypeRemoval> RemoveAsync(int id, byte[] loadedRowVersion)
    {
        if (await LoadAsync(id, loadedRowVersion) is not { } row) return ExpenseTypeRemoval.NotFound;
        if (await IsUsedAsync(row.Id))
        {
            row.IsActive = false;
            await db.SaveChangesAsync();
            return ExpenseTypeRemoval.Retired;
        }
        db.ExpenseTypes.Remove(row);
        await db.SaveChangesAsync();
        return ExpenseTypeRemoval.Deleted;
    }

    /// <summary>Whether a line of ค่าใช้จ่ายเงินสดย่อย refers to the type (none can yet: the lines come with SCR-03).</summary>
    private Task<bool> IsUsedAsync(int expenseTypeId) => Task.FromResult(false);

    private async Task<ExpenseType?> LoadAsync(int id, byte[]? loadedRowVersion)
    {
        var row = await db.ExpenseTypes.FirstOrDefaultAsync(t => t.Id == id);
        row?.ExpectRowVersion(loadedRowVersion ?? throw new ArgumentException("Editing needs the rowversion the screen loaded.", nameof(loadedRowVersion)));
        return row;
    }
}
