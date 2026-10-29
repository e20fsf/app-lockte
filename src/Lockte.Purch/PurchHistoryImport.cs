using Lockte.Purch.Data;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Purch;

/// <summary>One row of ผลประเมินผู้ขาย as the old system kept it: the counts of a supplier over a period and the two service points.</summary>
public sealed record ImportedEvaluation(
    int SupplierId, DateOnly From, DateOnly To, int Quarter, int Passed, int Failed, int WithCertificate, int WithoutCertificate, int OnTime, int Late, int Point1, int Point2);

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

    /// <summary>The rows now in the table and the sums of the six counts (passed, failed, with, without certificate, on time, late) — what the check of the totals reads back.</summary>
    public async Task<(int Rows, long[] Sums)> CountEvaluationsAsync()
    {
        var all = await db.SupplierEvaluations.AsNoTracking().ToListAsync();
        return (all.Count, [all.Sum(e => (long)e.Passed), all.Sum(e => (long)e.Failed), all.Sum(e => (long)e.WithCertificate), all.Sum(e => (long)e.WithoutCertificate), all.Sum(e => (long)e.OnTime), all.Sum(e => (long)e.Late)]);
    }
}
