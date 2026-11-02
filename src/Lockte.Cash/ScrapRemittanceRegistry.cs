using System.Globalization;
using Lockte.Cash.Data;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Cash;

/// <summary>A closing as the print reads it (RPT-07): the range asked for and the receipts it took, each with its total.</summary>
public sealed record ScrapRemittanceDto(int Id, DateOnly MadeOn, string FromNo, string ToNo, string MadeBy, IReadOnlyList<ScrapReceiptDto> Receipts)
{
    public decimal Total => Receipts.Sum(r => r.Total);
}

/// <summary>The range of numbers to close. <see cref="MadeOn"/> left out is today.</summary>
public sealed record ScrapRemittanceInput(string? FromNo, string? ToNo, DateOnly? MadeOn = null);

/// <summary>What closing a range did; <see cref="Count"/> and <see cref="Total"/> are of the receipts taken.</summary>
public sealed record ScrapRemittanceResult(bool Succeeded, IReadOnlyList<string> Errors, int? Id = null, int Count = 0, decimal Total = 0);

/// <summary>The public, read-only face of การนำส่งสำนักงาน and ยอดรอนำส่ง (ADR 0002): SCR-12, RPT-07, RPT-08 read it.</summary>
public sealed class ScrapRemittanceDirectory(CashDbContext db)
{
    /// <summary>
    /// ยอดรอนำส่ง: the receipts not cancelled and not yet in a closing, by number. Replaces the legacy's copy of waiting lines and its "after the last closing" line (BR-015, BR-016).
    /// </summary>
    public async Task<IReadOnlyList<ScrapReceiptDto>> UnremittedAsync()
        => Order(await new ScrapReceiptDirectory(db).ReadAsync(db.ScrapReceipts.AsNoTracking().Where(r => !r.IsCancelled && r.RemittanceId == null)));

    /// <summary>The unremitted receipts whose total is above 0 (RPT-08 shows no other).</summary>
    public async Task<IReadOnlyList<ScrapReceiptDto>> UnremittedWithAmountAsync() => (await UnremittedAsync()).Where(r => r.Total > 0).ToList();

    public async Task<ScrapRemittanceDto?> GetAsync(int id)
    {
        var head = await db.ScrapRemittances.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);
        if (head is null) return null;
        var receipts = Order(await new ScrapReceiptDirectory(db).ReadAsync(db.ScrapReceipts.AsNoTracking().Where(r => r.RemittanceId == id)));
        return new ScrapRemittanceDto(head.Id, head.MadeOn, head.FromNo, head.ToNo, head.MadeBy, receipts);
    }

    /// <summary>The range of the last closing, offered again on SCR-12 (null when nothing was ever closed).</summary>
    public async Task<(string FromNo, string ToNo)?> LastRangeAsync()
    {
        var last = await db.ScrapRemittances.AsNoTracking().OrderByDescending(r => r.Id).Select(r => new { r.FromNo, r.ToNo }).FirstOrDefaultAsync();
        return last is null ? null : (last.FromNo, last.ToNo);
    }

    private static IReadOnlyList<ScrapReceiptDto> Order(IReadOnlyList<ScrapReceiptDto> receipts) => receipts.OrderBy(r => ScrapReceiptMath.SortKey(r.No)).ToList();
}

/// <summary>
/// ตัดยอดนำส่ง (SCR-12, BR-016, assumption ฉ): the unremitted receipts whose numbers lie in the range — compared as numbers, year code then running number — are tied to a new closing
/// in one save. The range is the one chosen, nothing is read from the "last closing" and no scratch set is emptied: a range with nothing waiting changes nothing and prints nothing,
/// and closing the same range again finds nothing to take. A cancelled receipt is not taken.
/// </summary>
public sealed class ScrapRemittanceRegistry(CashDbContext db, ICurrentUser user, TimeProvider clock)
{
    public async Task<ScrapRemittanceResult> CloseAsync(ScrapRemittanceInput input)
    {
        var from = input.FromNo?.Trim();
        var to = input.ToNo?.Trim();
        if (!ScrapReceiptMath.IsValidNumber(from) || !ScrapReceiptMath.IsValidNumber(to)) return Fail("กรุณาเลือกเลขที่ตั้งแต่และถึง (ตัวเลข 5 หลักขึ้นไป)");
        if (ScrapReceiptMath.Compare(from!, to!) > 0) return Fail("ตั้งแต่เลขที่ต้องไม่มากกว่าถึงเลขที่");

        var waiting = await db.ScrapReceipts.Include(r => r.Lines).Where(r => !r.IsCancelled && r.RemittanceId == null).ToListAsync();
        var taken = waiting.Where(r => ScrapReceiptMath.Compare(r.No, from!) >= 0 && ScrapReceiptMath.Compare(r.No, to!) <= 0).ToList();
        if (taken.Count == 0) return Fail($"ไม่มีใบเสร็จรอนำส่งในช่วงเลขที่ {from} ถึง {to} — ไม่มีการเปลี่ยนแปลง");

        var remittance = new ScrapRemittance { MadeOn = input.MadeOn ?? DateOnly.FromDateTime(clock.GetLocalNow().DateTime), FromNo = from!, ToNo = to!, MadeBy = user.UserName };
        db.ScrapRemittances.Add(remittance);
        foreach (var receipt in taken) receipt.Remittance = remittance;
        await db.SaveChangesAsync();
        return new ScrapRemittanceResult(true, [], remittance.Id, taken.Count, taken.Sum(r => ScrapReceiptMath.Total(r.Lines.Select(l => l.Amount))));
    }

    private static ScrapRemittanceResult Fail(string error) => new(false, [error]);
}
