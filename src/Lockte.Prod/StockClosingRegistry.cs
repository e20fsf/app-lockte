using Lockte.Prod.Data;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Prod;

/// <summary>What a stock cut did. <see cref="Error"/> is a Thai message when it could not be made (no opening balance of the previous period to start from), <see cref="Busy"/> when another person is cutting.</summary>
public sealed record StockCutResult(bool Succeeded, string? Error, bool Busy, int Products, int EntriesClosed)
{
    public static StockCutResult Failed(string error) => new(false, error, false, 0, 0);
}

/// <summary>A line of RPT-13 รายงานตัดสต็อก: the balance carried over for a product, its cost per unit (FML) and the value.</summary>
public sealed record StockCutLine(string ProductCode, string ProductName, string? Unit, decimal Quantity, decimal? UnitCost)
{
    /// <summary>มูลค่า = จำนวน × ต้นทุนต่อหน่วย; blank (not 0) when the product has no cost (⚠ BR-010).</summary>
    public decimal? Value => UnitCost is { } cost ? Math.Round(Quantity * cost, 2, MidpointRounding.AwayFromZero) : null;
}

/// <summary>The opening balances of one product at the cut dates of a range (RPT-14 สรุปยอดยกมารายงวด).</summary>
public sealed record StockOpeningRow(string ProductCode, string ProductName, string? Unit, IReadOnlyDictionary<DateOnly, decimal> Balances);

/// <summary>
/// ตัดสต็อกสิ้นงวด (SCR-15, BR-007): what was dated up to the cut date is <b>closed</b> (marked, not moved — one table for every year, ข้อตัดสิน #27) and the balance of
/// every product is carried over as an opening entry (<c>stock</c>) dated the cut date; the openings of every cut are kept as the history that replaces T-ต้นงวดP_ST.
/// A cut run again for the same date gives the same balances. "ตัดซ้ำ" takes the previous opening back and cuts again: the withdrawals and hand entries closed by
/// the first cut are not brought back (⚠ Q-29 kept — the balance of a second cut does not take the withdrawals off).
/// </summary>
public sealed class StockClosingRegistry(ProdDbContext db, AuditScope audit)
{
    private const string CloseLock = "lockte:prod:stock-close";
    public const string OpeningDocumentNo = "stock";

    /// <summary>ตัดครั้งแรก (ขั้น 1–5 of BR-007).</summary>
    public Task<StockCutResult> CutAsync(DateOnly cutDate) => RunAsync(cutDate, previousCut: null);

    /// <summary>ตัดซ้ำ: the opening of <paramref name="previousCut"/> (TO_DATE) is made open again, the opening of this cut is removed, and the cut is made.</summary>
    public Task<StockCutResult> RecutAsync(DateOnly cutDate, DateOnly previousCut) => RunAsync(cutDate, previousCut);

    private async Task<StockCutResult> RunAsync(DateOnly cut, DateOnly? previousCut)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        var granted = (await db.Database.SqlQuery<int>($"DECLARE @r int; EXEC @r = sp_getapplock @Resource = {CloseLock}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 0; SELECT @r AS Value")
            .ToListAsync()).Single();
        if (granted < 0) return new StockCutResult(false, null, true, 0, 0);

        using var _ = audit.SuppressRowChanges(); // derived data: no row-by-row history of a cut

        if (previousCut is { } previous)
        {
            if (!await db.StockEntries.AnyAsync(e => e.Kind == StockEntryKind.OpeningBalance && e.Date == previous))
                return StockCutResult.Failed("ไม่พบยอดยกมาของงวดก่อน");
            // 1-3: the opening of this period goes, the one of the previous period is open again
            await db.StockEntries.Where(e => e.Kind == StockEntryKind.OpeningBalance && e.Date == cut).ExecuteDeleteAsync();
            await db.StockEntries.Where(e => e.Kind == StockEntryKind.OpeningBalance && e.Date == previous).ExecuteUpdateAsync(s => s.SetProperty(e => e.ClosedOn, (DateOnly?)null));
        }

        // 2: the new balance of each product = Σ of everything open dated up to the cut date (the old openings included) — also what came in after an earlier cut with an old date
        var totals = await db.StockEntries.AsNoTracking().Where(e => e.ClosedOn == null && e.Date != null && e.Date <= cut)
            .GroupBy(e => e.ProductId).Select(g => new { ProductId = g.Key, Quantity = g.Sum(e => e.Quantity) }).ToListAsync();

        // 1 and 3: close them (marked, not moved); the opening already dated the cut date is replaced (5)
        var closed = await db.StockEntries.Where(e => e.ClosedOn == null && e.Date != null && e.Date <= cut).ExecuteUpdateAsync(s => s.SetProperty(e => e.ClosedOn, (DateOnly?)cut));
        await db.StockEntries.Where(e => e.Kind == StockEntryKind.OpeningBalance && e.Date == cut).ExecuteDeleteAsync();

        // 4: the balance of every product, also those at 0, as the opening of the next period
        db.StockEntries.AddRange(totals.Select(t => new StockEntry
        {
            Date = cut, DocumentNo = OpeningDocumentNo, ProductId = t.ProductId, Quantity = t.Quantity, Kind = StockEntryKind.OpeningBalance,
        }));
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return new StockCutResult(true, null, false, totals.Count, closed);
    }
}

/// <summary>Reads of the stock cuts for RPT-13 and RPT-14.</summary>
public sealed class StockClosingDirectory(ProdDbContext db)
{
    /// <summary>The dates a cut was made (an opening balance exists), latest first.</summary>
    public async Task<IReadOnlyList<DateOnly>> GetCutDatesAsync()
        => await db.StockEntries.AsNoTracking().Where(e => e.Kind == StockEntryKind.OpeningBalance && e.Date != null).Select(e => e.Date!.Value).Distinct().OrderByDescending(d => d).ToListAsync();

    /// <summary>RPT-13: the balances of the cut date that are not 0, by product code, with the cost per unit.</summary>
    public async Task<IReadOnlyList<StockCutLine>> GetCutReportAsync(DateOnly cutDate)
        => (await db.StockEntries.AsNoTracking().Where(e => e.Kind == StockEntryKind.OpeningBalance && e.Date == cutDate && e.Quantity != 0)
                .Select(e => new { e.Product.Code, e.Product.Name, e.Product.Unit, e.Quantity, e.Product.UnitCost }).ToListAsync())
            .OrderBy(e => e.Code, StringComparer.Ordinal).Select(e => new StockCutLine(e.Code, e.Name, e.Unit, e.Quantity, e.UnitCost)).ToList();

    /// <summary>
    /// RPT-14: the opening balances of the cuts in the range, a row per product (those matching <paramref name="codePattern"/> such as <c>SB*</c>, all when null) and a
    /// value per cut date. The dates of the cuts are in <paramref name="cutDates"/>.
    /// </summary>
    public async Task<(IReadOnlyList<DateOnly> CutDates, IReadOnlyList<StockOpeningRow> Rows)> GetOpeningHistoryAsync(DateOnly from, DateOnly to, string? codePattern = null)
    {
        var entries = await db.StockEntries.AsNoTracking().Where(e => e.Kind == StockEntryKind.OpeningBalance && e.Date >= from && e.Date <= to)
            .Select(e => new { e.Date, e.Product.Code, e.Product.Name, e.Product.Unit, e.Quantity }).ToListAsync();
        var rows = entries.Where(e => string.IsNullOrWhiteSpace(codePattern) || ProductGroupPattern.Matches(codePattern.Trim(), e.Code))
            .GroupBy(e => e.Code).OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new StockOpeningRow(g.Key, g.First().Name, g.First().Unit, g.GroupBy(e => e.Date!.Value).ToDictionary(d => d.Key, d => d.Sum(e => e.Quantity)))).ToList();
        return (rows.SelectMany(r => r.Balances.Keys).Distinct().Order().ToList(), rows);
    }
}
