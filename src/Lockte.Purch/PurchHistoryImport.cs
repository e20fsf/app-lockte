using Lockte.Purch.Data;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Purch;

/// <summary>One row of ผลประเมินผู้ขาย as the old system kept it: the counts of a supplier over a period and the two service points.</summary>
public sealed record ImportedEvaluation(
    int SupplierId, DateOnly From, DateOnly To, int Quarter, int Passed, int Failed, int WithCertificate, int WithoutCertificate, int OnTime, int Late, int Point1, int Point2);

/// <summary>A row of the stock book as the old system kept it, the kind already worked out from MARK and INVOICE_NO.</summary>
public sealed record ImportedStockEntry(
    DateOnly Date, string? DocumentNo, string? LineNo, int MaterialId, decimal Quantity, StockEntryKind Kind, int? SupplierId, int? EmployeeId, decimal? UnitPrice, string? Note);

/// <summary>The balance carried forward at a cut, kept to print RPT-26 of that day again.</summary>
public sealed record ImportedPeriodBalance(DateOnly CutDate, int MaterialId, decimal Quantity);

/// <summary>
/// The delete-everything and bulk-load service of the history of module วัตถุดิบ (ผลประเมินผู้ขาย #268, and the documents and the stock book in the tickets after it), for the migration tool
/// only: registered in that tool, never in the web app. The counts of an old evaluation cannot be calculated again — the receipts they were counted from are not all moved — so they are
/// loaded as they were.
/// </summary>
public sealed class PurchHistoryImport(PurchDbContext db)
{
    public async Task DeleteEvaluationsAsync() => await db.SupplierEvaluations.ExecuteDeleteAsync();

    public async Task AddEvaluationsAsync(IReadOnlyList<ImportedEvaluation> rows)
    {
        foreach (var r in rows)
            db.SupplierEvaluations.Add(new SupplierEvaluation
            {
                SupplierId = r.SupplierId, FromDate = r.From, ToDate = r.To, Quarter = r.Quarter, Passed = r.Passed, Failed = r.Failed, WithCertificate = r.WithCertificate,
                WithoutCertificate = r.WithoutCertificate, OnTime = r.OnTime, Late = r.Late, Point1 = r.Point1, Point2 = r.Point2,
            });
        await db.SaveChangesAsync();
    }

    public async Task DeleteStockAsync()
    {
        await db.StockPeriodBalances.ExecuteDeleteAsync();
        await db.StockEntries.ExecuteDeleteAsync();
    }

    public async Task AddStockEntriesAsync(IReadOnlyList<ImportedStockEntry> rows)
    {
        foreach (var r in rows)
            db.StockEntries.Add(new StockEntry
            {
                EntryDate = r.Date, DocumentNo = r.DocumentNo, DocumentLineNo = r.LineNo, MaterialId = r.MaterialId, Quantity = r.Quantity, Kind = r.Kind, SupplierId = r.SupplierId,
                EmployeeId = r.EmployeeId, UnitPrice = r.UnitPrice, Note = r.Note,
            });
        await db.SaveChangesAsync();
    }

    public async Task AddPeriodBalancesAsync(IReadOnlyList<ImportedPeriodBalance> rows)
    {
        foreach (var r in rows) db.StockPeriodBalances.Add(new StockPeriodBalance { CutDate = r.CutDate, MaterialId = r.MaterialId, Quantity = r.Quantity });
        await db.SaveChangesAsync();
    }

    /// <summary>The balance of every material with rows in the stock book (Σ quantity of the open rows), by material id.</summary>
    public async Task<Dictionary<int, decimal>> GetStockBalancesAsync()
        => (await db.StockEntries.AsNoTracking().Where(e => !e.IsClosed).GroupBy(e => e.MaterialId).Select(g => new { g.Key, Sum = g.Sum(e => e.Quantity) }).ToListAsync()).ToDictionary(g => g.Key, g => g.Sum);

    /// <summary>The quantity of every open row of the stock book — what the check of the totals adds up again.</summary>
    public async Task<List<decimal>> GetStockRowQuantitiesAsync() => await db.StockEntries.AsNoTracking().Where(e => !e.IsClosed).Select(e => e.Quantity).ToListAsync();

    public async Task<(int Entries, int Balances)> CountStockAsync() => (await db.StockEntries.CountAsync(), await db.StockPeriodBalances.CountAsync());

    /// <summary>The rows now in the table and the sums of the six counts (passed, failed, with, without certificate, on time, late) — what the check of the totals reads back.</summary>
    public async Task<(int Rows, long[] Sums)> CountEvaluationsAsync()
    {
        var all = await db.SupplierEvaluations.AsNoTracking().ToListAsync();
        return (all.Count, [all.Sum(e => (long)e.Passed), all.Sum(e => (long)e.Failed), all.Sum(e => (long)e.WithCertificate), all.Sum(e => (long)e.WithoutCertificate), all.Sum(e => (long)e.OnTime), all.Sum(e => (long)e.Late)]);
    }
}
