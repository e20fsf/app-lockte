using Lockte.Cash.Data;
using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Cash;

/// <summary>A line of ค่าใช้จ่ายเงินสดย่อย as the screens and the reports read it; <see cref="ExpenseTypeName"/> is the name of the type now (a renamed type renames on every line).</summary>
public sealed record ExpenseLineDto(int Id, DateOnly Date, string VoucherNo, string Name, decimal Qty, string? Unit, decimal UnitPrice, decimal Amount,
    int ExpenseTypeId, string ExpenseTypeName, byte[] RowVersion);

/// <summary>The fields of a line on the screen; <see cref="Id"/> null = a new one. <see cref="Amount"/> left out is worked out as quantity × price when the price is above 0.</summary>
public sealed record ExpenseLineInput(int? Id, DateOnly? Date, string? VoucherNo, string? Name, decimal? Qty, string? Unit, decimal? UnitPrice, decimal? Amount,
    int? ExpenseTypeId, byte[]? RowVersion);

/// <summary>An item offered when a line is keyed: a name with the unit it was last used with.</summary>
public sealed record ExpenseItemSuggestion(string Name, string? Unit);

/// <summary>The money rule of a line (assumption ก, RND-B in the legacy): what the amount becomes when the quantity or the price is edited.</summary>
public static class ExpenseLineMath
{
    /// <summary>Quantity × price to 2 decimals, half up; null when the price is 0 (then the amount is typed, as for fuel).</summary>
    public static decimal? AmountOf(decimal qty, decimal unitPrice)
        => unitPrice > 0 ? Math.Round(qty * unitPrice, 2, MidpointRounding.AwayFromZero) : null;
}

/// <summary>The public, read-only face of ค่าใช้จ่ายเงินสดย่อย (ADR 0002): SCR-03/04, the voucher (RPT-01) and the summaries (RPT-02, RPT-04) read it.</summary>
public sealed class ExpenseLineDirectory(CashDbContext db)
{
    /// <summary>The lines of one day, in the order they were keyed.</summary>
    public async Task<IReadOnlyList<ExpenseLineDto>> ListByDateAsync(DateOnly date)
        => await ReadAsync(db.ExpenseLines.AsNoTracking().Where(l => l.Date == date).OrderBy(l => l.Id));

    /// <summary>The lines from <paramref name="from"/> to <paramref name="to"/> inclusive: by date, then voucher number, then the order keyed.</summary>
    public async Task<IReadOnlyList<ExpenseLineDto>> ListAsync(DateOnly from, DateOnly to)
        => await ReadAsync(db.ExpenseLines.AsNoTracking().Where(l => l.Date >= from && l.Date <= to).OrderBy(l => l.Date).ThenBy(l => l.VoucherNo).ThenBy(l => l.Id));

    public async Task<ExpenseLineDto?> GetAsync(int id) => (await ReadAsync(db.ExpenseLines.AsNoTracking().Where(l => l.Id == id))).FirstOrDefault();

    /// <summary>The chosen lines, in the order of a voucher: date, then voucher number, then the order keyed.</summary>
    public async Task<IReadOnlyList<ExpenseLineDto>> GetManyAsync(IReadOnlyCollection<int> ids)
        => await ReadAsync(db.ExpenseLines.AsNoTracking().Where(l => ids.Contains(l.Id)).OrderBy(l => l.Date).ThenBy(l => l.VoucherNo).ThenBy(l => l.Id));

    /// <summary>
    /// The items offered in the pick list of a line: the name and unit of every line saved so far, each pair once, by name (the legacy read a copy of 2016–17, so a new item never
    /// came back — BR-002 #4, Q-17).
    /// </summary>
    public async Task<IReadOnlyList<ExpenseItemSuggestion>> SuggestionsAsync()
    {
        var pairs = await db.ExpenseLines.AsNoTracking().GroupBy(l => new { l.Name, l.Unit }).Select(g => new { g.Key.Name, g.Key.Unit }).ToListAsync();
        var thai = StringComparer.Create(ThaiDate.Culture, ignoreCase: true);
        return pairs.OrderBy(p => p.Name, thai).ThenBy(p => p.Unit, thai).Select(p => new ExpenseItemSuggestion(p.Name, p.Unit)).ToList();
    }

    /// <summary>The lines, with the name of the type now: read in two plain queries (the lines, then the types they name) and put together here.</summary>
    private async Task<IReadOnlyList<ExpenseLineDto>> ReadAsync(IQueryable<ExpenseLine> lines)
    {
        var rows = await lines.ToListAsync();
        var typeIds = rows.Select(l => l.ExpenseTypeId).Distinct().ToList();
        var names = await db.ExpenseTypes.AsNoTracking().Where(t => typeIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Name);
        return rows.Select(l => new ExpenseLineDto(l.Id, l.Date, l.VoucherNo, l.Name, l.Qty, l.Unit, l.UnitPrice, l.Amount, l.ExpenseTypeId, names[l.ExpenseTypeId], l.RowVersion)).ToList();
    }
}

/// <summary>
/// บันทึก/แก้/ลบบรรทัดค่าใช้จ่ายเงินสดย่อย (SCR-03, SCR-04, BR-002–004). Unlike the legacy a line that lacks a required field is refused with a message naming the field — the legacy
/// deleted it silently when the screen was left (BR-003) — and a line is deleted only by <see cref="DeleteAsync"/>, never by setting the quantity to 0 (BR-004, Q-10). The type must
/// be one in use; a line that already has a retired type keeps it until the type is changed.
/// </summary>
public sealed class ExpenseLineRegistry(CashDbContext db)
{
    public const int VoucherNoLength = 50;
    public const int NameLength = 255;
    public const int UnitLength = 50;

    public async Task<ServiceResult> SaveAsync(ExpenseLineInput input)
    {
        var voucher = input.VoucherNo?.Trim();
        var name = input.Name?.Trim();
        var unit = string.IsNullOrWhiteSpace(input.Unit) ? null : input.Unit.Trim();
        var errors = new List<string>();
        if (input.Date is null) errors.Add("กรุณาเลือกวันที่");
        if (string.IsNullOrEmpty(voucher)) errors.Add("กรุณากรอกเลขที่");
        if (string.IsNullOrEmpty(name)) errors.Add("กรุณากรอกรายการ");
        if (input.ExpenseTypeId is null) errors.Add("กรุณาเลือกประเภท");
        if (input.Qty is not > 0) errors.Add(input.Qty == 0 ? "จำนวนต้องมากกว่า 0 — ถ้าต้องการลบบรรทัดให้ใช้ปุ่มลบ" : "กรุณากรอกจำนวนที่มากกว่า 0");
        if (voucher?.Length > VoucherNoLength) errors.Add($"เลขที่ยาวได้ไม่เกิน {VoucherNoLength} ตัว");
        if (name?.Length > NameLength) errors.Add($"รายการยาวได้ไม่เกิน {NameLength} ตัว");
        if (unit?.Length > UnitLength) errors.Add($"หน่วยยาวได้ไม่เกิน {UnitLength} ตัว");
        if (input.UnitPrice < 0 || input.Amount < 0) errors.Add("ราคาและจำนวนเงินต้องไม่ติดลบ");
        if (errors.Count > 0) return new ServiceResult(false, errors);

        var row = input.Id is { } id ? await db.ExpenseLines.FirstOrDefaultAsync(l => l.Id == id) : null;
        if (input.Id is not null)
        {
            if (row is null) return ServiceResult.Fail("ไม่พบบรรทัดนี้แล้ว (อาจถูกลบไปแล้ว)");
            row.ExpectRowVersion(input.RowVersion ?? throw new ArgumentException("Editing needs the rowversion the screen loaded.", nameof(input)));
        }

        var typeId = input.ExpenseTypeId!.Value;
        if (row is null || row.ExpenseTypeId != typeId)
        {
            var type = await db.ExpenseTypes.AsNoTracking().FirstOrDefaultAsync(t => t.Id == typeId);
            if (type is null) return ServiceResult.Fail("ไม่พบประเภทค่าใช้จ่ายที่เลือก");
            if (!type.IsActive) return ServiceResult.Fail($"ประเภท '{type.Name}' เลิกใช้แล้ว เลือกประเภทอื่น");
        }

        var price = input.UnitPrice ?? 0m;
        var amount = input.Amount ?? ExpenseLineMath.AmountOf(input.Qty!.Value, price) ?? 0m;
        row ??= db.ExpenseLines.Add(new ExpenseLine()).Entity;
        row.Date = input.Date!.Value;
        row.VoucherNo = voucher!;
        row.Name = name!;
        row.Qty = input.Qty!.Value;
        row.Unit = unit;
        row.UnitPrice = price;
        row.Amount = amount;
        row.ExpenseTypeId = typeId;
        await db.SaveChangesAsync();
        return ServiceResult.Ok(row.Id);
    }

    /// <summary>ลบบรรทัด: the one way to remove a line. A line already gone is not an error.</summary>
    public async Task<ServiceResult> DeleteAsync(int id, byte[] loadedRowVersion)
    {
        var row = await db.ExpenseLines.FirstOrDefaultAsync(l => l.Id == id);
        if (row is null) return ServiceResult.Ok();
        row.ExpectRowVersion(loadedRowVersion);
        db.ExpenseLines.Remove(row);
        await db.SaveChangesAsync();
        return ServiceResult.Ok();
    }
}
