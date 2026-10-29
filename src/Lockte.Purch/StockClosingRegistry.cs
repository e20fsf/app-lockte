using Lockte.Purch.Data;
using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Purch;

/// <summary>What a ตัดสต็อก did: the rows closed, the opening balances written and whether the date was before the last cut (a warning, not a stop — ข้อสมมติ ซ).</summary>
public sealed record StockClosingResult(bool Succeeded, IReadOnlyList<string> Errors, DateOnly Date, int ClosedRows, int OpeningBalances, DateOnly? PreviousCut, bool BeforePreviousCut);

/// <summary>ยอดยกมาของวัตถุดิบหนึ่งรหัสที่ตัดสต็อกยกไป ณ วันตัด (for RPT-26: quantity × the register's latest price).</summary>
public sealed record StockPeriodBalanceDto(DateOnly CutDate, int MaterialId, string MaterialCode, string MaterialName, string? Unit, decimal Quantity, decimal Cost);

/// <summary>
/// ตัดสต็อกสิ้นเดือน (SCR-18, BR-042): one transaction. Every open row dated on or before the cut date <c>D</c> is marked closed (not moved: closed rows are the history); the
/// quantities of each material are summed and the sum carried forward as a row of kind ยกมา dated <c>D</c> — except for the codes of the no-carry prefixes of the settings
/// (สปริง <c>sp</c>: cleared, as before, Q-21). A material whose sum is zero gets no row. Cutting the same date again gives the same balances (the ones of that date are replaced);
/// rows dated after <c>D</c> are left alone. The balances of the cut are kept for the report of it (T-ต้นงวดP_ST).
/// </summary>
public sealed class StockClosingRegistry(PurchDbContext db, PurchSettingsDirectory settings)
{
    private const string Lock = "purch.StockClosing";

    public async Task<StockClosingResult> CloseAsync(DateOnly date)
    {
        if (date == default) return new StockClosingResult(false, ["กรุณากำหนดวันที่ตัดสต็อก"], date, 0, 0, null, false);
        var noCarry = (await settings.GetAsync()).NoCarryPrefixes;

        await using var transaction = await db.Database.BeginTransactionAsync();
        var granted = (await db.Database.SqlQuery<int>($"DECLARE @r int; EXEC @r = sp_getapplock @Resource = {Lock}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 0; SELECT @r AS Value")
            .ToListAsync()).Single();
        if (granted < 0) return new StockClosingResult(false, ["มีผู้อื่นกำลังตัดสต็อกอยู่ กรุณาลองใหม่ภายหลัง"], date, 0, 0, null, false);

        var previous = await db.StockPeriodBalances.MaxAsync(b => (DateOnly?)b.CutDate);
        var rows = await db.StockEntries.Include(e => e.Material).Where(e => !e.IsClosed && e.EntryDate <= date).ToListAsync();

        // the opening rows of this same date are being made again; the earlier ones are history
        var replaced = rows.Where(e => e.Kind == StockEntryKind.OpeningBalance && e.EntryDate == date).ToList();
        var sums = rows.GroupBy(e => e.Material!).Select(g => (Material: g.Key, Total: g.Sum(e => e.Quantity))).ToList();
        foreach (var e in rows.Except(replaced)) e.IsClosed = true;
        db.StockEntries.RemoveRange(replaced);

        var opening = 0;
        foreach (var (material, total) in sums.Where(x => x.Total != 0 && !noCarry.Any(p => x.Material.Code.StartsWith(p, StringComparison.OrdinalIgnoreCase))))
        {
            db.StockEntries.Add(new StockEntry { Kind = StockEntryKind.OpeningBalance, EntryDate = date, MaterialId = material.Id, Quantity = total, DocumentNo = "stock" });
            opening++;
        }

        // the balances of the cut for its report, replacing those of a cut of the same date
        db.StockPeriodBalances.RemoveRange(await db.StockPeriodBalances.Where(b => b.CutDate == date).ToListAsync());
        foreach (var (material, total) in sums.Where(x => x.Total != 0 && !noCarry.Any(p => x.Material.Code.StartsWith(p, StringComparison.OrdinalIgnoreCase))))
            db.StockPeriodBalances.Add(new StockPeriodBalance { CutDate = date, MaterialId = material.Id, Quantity = total });

        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return new StockClosingResult(true, [], date, rows.Count - replaced.Count, opening, previous, previous is { } p && date < p);
    }
}

/// <summary>The public, read-only face of ตัดสต็อก (ADR 0002).</summary>
public sealed class StockClosingDirectory(PurchDbContext db)
{
    /// <summary>วันตัดล่าสุด, null before the first cut (the default of the date on SCR-18).</summary>
    public async Task<DateOnly?> GetLastCutDateAsync() => await db.StockPeriodBalances.AsNoTracking().MaxAsync(b => (DateOnly?)b.CutDate);

    /// <summary>The dates of the cuts, newest first.</summary>
    public async Task<IReadOnlyList<DateOnly>> GetCutDatesAsync()
        => await db.StockPeriodBalances.AsNoTracking().Select(b => b.CutDate).Distinct().OrderByDescending(d => d).ToListAsync();

    /// <summary>ยอดยกมาของแต่ละรหัส ณ วันตัด, by รหัส (RPT-26 prices them at the register's latest price).</summary>
    public async Task<IReadOnlyList<StockPeriodBalanceDto>> GetBalancesAsync(DateOnly cutDate)
        => (await db.StockPeriodBalances.AsNoTracking().Include(b => b.Material).Where(b => b.CutDate == cutDate).ToListAsync())
            .OrderBy(b => b.Material!.Code).Select(b => new StockPeriodBalanceDto(b.CutDate, b.MaterialId, b.Material!.Code, b.Material.Name, b.Material.Unit, b.Quantity, b.Material.Cost)).ToList();
}
