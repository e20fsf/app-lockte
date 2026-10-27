using Lockte.Prod.Data;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Prod;

/// <summary>A line of a migrated ใบรายงานผลผลิต (#122): the legacy values after the checks of the migration tool; <see cref="Total"/> is ดี + เสีย.</summary>
public sealed record ProductionReportLineImport(
    int LineNo, int? ShiftId, decimal? Hours, string? MaterialLot1, string? MaterialLot2, int? ProductId, string ProductName, string? Unit,
    decimal Good, decimal? Reject, decimal Total, int? EmployeeId, int? MoldId, string? Lot, bool QaSelected = false);

/// <summary>A migrated report. <see cref="Stocked"/> = its lines are in the transferred copy (เข้าสต็อกแล้ว).</summary>
public sealed record ProductionReportImportRow(
    string DepartmentCode, DateOnly? ReportDate, string ReportNo, int? MachineId, bool Stocked, IReadOnlyList<ProductionReportLineImport> Lines);

/// <summary>One month of one department as counted back for รายงานตรวจยอด (AC-20, AC-18).</summary>
public sealed record ProductionMonthTotal(string DepartmentCode, string Month, int Lines, decimal Good, decimal Reject);

/// <summary>
/// ย้ายข้อมูลใบรายงานผลผลิต 7 แผนก (#122) into the one table. For the data-migration tool only, never registered in the web app: the rows are taken as they were
/// (no check of the numbers, a product or mold not in the register left blank), in batches, with no row-by-row history (#39). Deleting loads again only the reports —
/// stock entries are another ticket (#126) and are not touched.
/// </summary>
public sealed class ProductionReportImport(ProdDbContext db, AuditScope audit)
{
    private const int BatchSize = 500;

    public async Task DeleteAllAsync()
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.ProductionReportLines.ExecuteDeleteAsync();
        await db.ProductionReports.ExecuteDeleteAsync();
        await transaction.CommitAsync();
    }

    public async Task AddAsync(IEnumerable<ProductionReportImportRow> rows)
    {
        var stamp = DateTimeOffset.UtcNow;
        using (audit.SuppressRowChanges())
            foreach (var batch in rows.Chunk(BatchSize))
            {
                db.ProductionReports.AddRange(batch.Select(r =>
                {
                    var report = new ProductionReport { DepartmentCode = r.DepartmentCode, ReportDate = r.ReportDate, ReportNo = r.ReportNo, MachineId = r.MachineId, StockedAt = r.Stocked ? stamp : null };
                    report.Lines.AddRange(r.Lines.Select(l => new ProductionReportLine
                    {
                        ProductionReport = report, LineNo = l.LineNo, ShiftId = l.ShiftId, Hours = l.Hours, MaterialLot1 = l.MaterialLot1, MaterialLot2 = l.MaterialLot2,
                        ProductId = l.ProductId, ProductName = l.ProductName, Unit = l.Unit, GoodQuantity = l.Good, RejectQuantity = l.Reject, TotalQuantity = l.Total,
                        EmployeeId = l.EmployeeId, MoldId = l.MoldId, Lot = l.Lot, QaSelected = l.QaSelected,
                    }));
                    return report;
                }));
                await db.SaveChangesAsync();
                db.ChangeTracker.Clear();
            }
    }

    public Task<int> CountLinesAsync() => db.ProductionReportLines.CountAsync();

    /// <summary>Lines, Σ จำนวนดี and Σ เสีย by department and month of the report date (reports with no date under the month ""), counted back.</summary>
    public async Task<IReadOnlyList<ProductionMonthTotal>> GetMonthTotalsAsync()
    {
        var rows = await db.ProductionReportLines.AsNoTracking()
            .Select(l => new { l.ProductionReport.DepartmentCode, l.ProductionReport.ReportDate, l.GoodQuantity, Reject = l.RejectQuantity ?? 0 }).ToListAsync();
        return rows.GroupBy(r => (r.DepartmentCode, Month: r.ReportDate is { } d ? $"{d.Year:0000}-{d.Month:00}" : ""))
            .OrderBy(g => g.Key.DepartmentCode, StringComparer.Ordinal).ThenBy(g => g.Key.Month, StringComparer.Ordinal)
            .Select(g => new ProductionMonthTotal(g.Key.DepartmentCode, g.Key.Month, g.Count(), g.Sum(r => r.GoodQuantity), g.Sum(r => r.Reject))).ToList();
    }
}
