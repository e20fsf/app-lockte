using Lockte.Prod.Data;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Prod;

/// <summary>An entry of the stock ledger as the migration tool brings it (#126). <see cref="ClosedOn"/> = the cut that closed it (null = in the period now open).</summary>
public sealed record StockEntryImportRow(
    DateOnly? Date, string? DocumentNo, int? LineNo, int ProductId, decimal Quantity, int? EmployeeId, StockEntryKind Kind, string? Remark, DateOnly? ClosedOn);

public sealed record MoldUsageImportRow(int MoldId, DateOnly Day, int Shots, bool IsOpening);

public sealed record MoldCutoffImportRow(int MoldId, DateOnly CutDate, int Cumulative);

/// <summary>Σ of the entries of one month, split by kind and by closed/open, as counted back for รายงานตรวจยอด (AC-20).</summary>
public sealed record StockMonthTotal(string Month, StockEntryKind Kind, bool Closed, int Entries, decimal Quantity);

/// <summary>The opening balance of one product at a cut date, as loaded.</summary>
public sealed record StockOpeningTotal(DateOnly CutDate, string ProductCode, decimal Quantity);

/// <summary>
/// ย้ายข้อมูลสมุดสต็อกและการใช้งานแม่พิมพ์ (#126) into the ledger. For the data-migration tool only, never registered in the web app: the rows are taken as they were (the checks are the
/// tool's), in batches, with no row-by-row history (#39). Deleting loads again the stock entries and the mold usage — the production reports are another ticket (#122) and are not touched.
/// </summary>
public sealed class StockImport(ProdDbContext db, AuditScope audit)
{
    private const int BatchSize = 2000;

    public async Task DeleteAllAsync()
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.StockEntries.ExecuteDeleteAsync();
        await db.MoldUsages.ExecuteDeleteAsync();
        await db.MoldCutoffs.ExecuteDeleteAsync();
        await transaction.CommitAsync();
    }

    public async Task AddEntriesAsync(IEnumerable<StockEntryImportRow> rows)
    {
        using (audit.SuppressRowChanges())
            foreach (var batch in rows.Chunk(BatchSize))
            {
                db.StockEntries.AddRange(batch.Select(r => new StockEntry
                {
                    Date = r.Date, DocumentNo = r.DocumentNo, LineNo = r.LineNo, ProductId = r.ProductId, Quantity = r.Quantity, EmployeeId = r.EmployeeId,
                    Kind = r.Kind, Remark = r.Remark, ClosedOn = r.ClosedOn,
                }));
                await db.SaveChangesAsync();
                db.ChangeTracker.Clear();
            }
    }

    public async Task AddMoldUsagesAsync(IEnumerable<MoldUsageImportRow> rows)
    {
        using (audit.SuppressRowChanges())
            foreach (var batch in rows.Chunk(BatchSize))
            {
                db.MoldUsages.AddRange(batch.Select(r => new MoldUsage { MoldId = r.MoldId, Day = r.Day, Shots = r.Shots, IsOpening = r.IsOpening }));
                await db.SaveChangesAsync();
                db.ChangeTracker.Clear();
            }
    }

    public async Task AddMoldCutoffsAsync(IEnumerable<MoldCutoffImportRow> rows)
    {
        using (audit.SuppressRowChanges())
            foreach (var batch in rows.Chunk(BatchSize))
            {
                db.MoldCutoffs.AddRange(batch.Select(r => new MoldCutoff { MoldId = r.MoldId, CutDate = r.CutDate, Cumulative = r.Cumulative }));
                await db.SaveChangesAsync();
                db.ChangeTracker.Clear();
            }
    }

    /// <summary>The balance of each product now (BR-006): Σ of the entries not closed, by product code — AC-09.</summary>
    public async Task<IReadOnlyDictionary<string, decimal>> GetBalancesAsync()
        => (await db.StockEntries.AsNoTracking().Where(e => e.ClosedOn == null).GroupBy(e => e.Product.Code).Select(g => new { Code = g.Key, Quantity = g.Sum(e => e.Quantity) }).ToListAsync())
            .ToDictionary(g => g.Code, g => g.Quantity, StringComparer.OrdinalIgnoreCase);

    /// <summary>The opening balances of every cut as loaded, by product code — AC-11.</summary>
    public async Task<IReadOnlyList<StockOpeningTotal>> GetOpeningsAsync()
        => (await db.StockEntries.AsNoTracking().Where(e => e.Kind == StockEntryKind.OpeningBalance && e.Date != null)
                .GroupBy(e => new { e.Date, e.Product.Code }).Select(g => new { g.Key.Date, g.Key.Code, Quantity = g.Sum(e => e.Quantity) }).ToListAsync())
            .Select(g => new StockOpeningTotal(g.Date!.Value, g.Code, g.Quantity)).ToList();

    /// <summary>Entries and Σ จำนวน by month of the date (entries with no date under ""), kind and closed or open.</summary>
    public async Task<IReadOnlyList<StockMonthTotal>> GetMonthTotalsAsync()
    {
        var rows = await db.StockEntries.AsNoTracking().Select(e => new { e.Date, e.Kind, Closed = e.ClosedOn != null, e.Quantity }).ToListAsync();
        return rows.GroupBy(r => (Month: r.Date is { } d ? $"{d.Year:0000}-{d.Month:00}" : "", r.Kind, r.Closed))
            .Select(g => new StockMonthTotal(g.Key.Month, g.Key.Kind, g.Key.Closed, g.Count(), g.Sum(r => r.Quantity))).ToList();
    }

    /// <summary>The mold usage of the current period (rows and Σ shots) and the totals of every cut, by mold code — AC-13.</summary>
    public async Task<(int UsageRows, long UsageShots, IReadOnlyList<(DateOnly CutDate, string MoldCode, int Cumulative)> Cutoffs)> GetMoldTotalsAsync()
    {
        var usage = await db.MoldUsages.AsNoTracking().GroupBy(_ => 1).Select(g => new { Rows = g.Count(), Shots = g.Sum(u => (long)u.Shots) }).ToListAsync();
        var cutoffs = await db.MoldCutoffs.AsNoTracking().Select(c => new { c.CutDate, c.Mold.Code, c.Cumulative }).ToListAsync();
        return (usage.FirstOrDefault()?.Rows ?? 0, usage.FirstOrDefault()?.Shots ?? 0, cutoffs.Select(c => (c.CutDate, c.Code, c.Cumulative)).ToList());
    }
}
