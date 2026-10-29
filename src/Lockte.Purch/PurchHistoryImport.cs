using Lockte.Purch.Data;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Purch;

/// <summary>One row of ผลประเมินผู้ขาย as the old system kept it: the counts of a supplier over a period and the two service points.</summary>
public sealed record ImportedEvaluation(
    int SupplierId, DateOnly From, DateOnly To, int Quarter, int Passed, int Failed, int WithCertificate, int WithoutCertificate, int OnTime, int Late, int Point1, int Point2);

/// <summary>A line of a request as the old rows say it. <see cref="Key"/> ties the order lines to it (the legacy has no ids).</summary>
public sealed record ImportedRequestLine(string Key, int LineNo, int MaterialId, string MaterialName, string? Unit, decimal Quantity, decimal UnitPrice, decimal NetAmount);

public sealed record ImportedRequest(
    string RequestNo, DateOnly RequestDate, int DepartmentId, DateOnly? DesiredDate, string? PurchasingLocation, string? Reason, string? Note, IReadOnlyList<ImportedRequestLine> Lines);

public sealed record ImportedReceipt(
    DateOnly Date, decimal Quantity, string? InvoiceNo, string? InvoiceLineNo, decimal UnitPrice, decimal NetAmount, int SupplierId, string? ReturnNote, int MaterialId, string MaterialName,
    string? Unit, bool Failed, string? Lot, bool? HasCertificate, string? QaNote);

public sealed record ImportedOrderLine(
    string RequestNo, string RequestLineKey, int LineNo, int MaterialId, string MaterialName, string? Unit, decimal RequestedQuantity, decimal Quantity, decimal UnitPrice, decimal NetAmount,
    decimal TaxIncludedAmount, DateOnly? DesiredDate, int DepartmentId, IReadOnlyList<ImportedReceipt> Receipts);

public sealed record ImportedOrder(
    string OrderNo, DateOnly OrderDate, int SupplierId, string SupplierName, string? SupplierAddress1, string? SupplierAddress2, string? SupplierTel, string? SupplierFax, int? CreditDays,
    bool ChargesVat, string? PurchasingLocation, string? Reason, string? Note, decimal Subtotal, decimal Tax, decimal Total, IReadOnlyList<ImportedOrderLine> Lines);

/// <summary>What the check of the totals reads back from the documents loaded.</summary>
public sealed record DocumentTotals(int Requests, int RequestLines, int Orders, int OrderLines, int Receipts, decimal OrderedQuantity, decimal ReceivedQuantity, decimal ReceivedAmount);

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

    public async Task DeleteDocumentsAsync()
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.GoodsReceipts.ExecuteDeleteAsync();
        await db.PurchaseOrderLines.ExecuteDeleteAsync();
        await db.PurchaseOrders.ExecuteDeleteAsync();
        await db.PurchaseRequestLines.ExecuteDeleteAsync();
        await db.PurchaseRequests.ExecuteDeleteAsync();
        await transaction.CommitAsync();
    }

    /// <summary>Requests, orders and receipts in one save: each order line points at its request line by <see cref="ImportedRequestLine.Key"/>.</summary>
    public async Task AddDocumentsAsync(IReadOnlyList<ImportedRequest> requests, IReadOnlyList<ImportedOrder> orders)
    {
        var lines = new Dictionary<(string, string), PurchaseRequestLine>();
        foreach (var r in requests)
        {
            var request = new PurchaseRequest
            {
                RequestNo = r.RequestNo, RequestDate = r.RequestDate, DepartmentId = r.DepartmentId, DesiredDate = r.DesiredDate, PurchasingLocation = r.PurchasingLocation, Reason = r.Reason, Note = r.Note,
            };
            foreach (var l in r.Lines)
            {
                var line = new PurchaseRequestLine
                {
                    LineNo = l.LineNo, MaterialId = l.MaterialId, MaterialName = l.MaterialName, Unit = l.Unit, Quantity = l.Quantity, UnitPrice = l.UnitPrice, NetAmount = l.NetAmount,
                };
                request.Lines.Add(line);
                lines[(r.RequestNo, l.Key)] = line;
            }
            db.PurchaseRequests.Add(request);
        }
        foreach (var o in orders)
        {
            var order = new PurchaseOrder
            {
                OrderNo = o.OrderNo, OrderDate = o.OrderDate, SupplierId = o.SupplierId, SupplierName = o.SupplierName, SupplierAddress1 = o.SupplierAddress1, SupplierAddress2 = o.SupplierAddress2,
                SupplierTel = o.SupplierTel, SupplierFax = o.SupplierFax, CreditDays = o.CreditDays, ChargesVat = o.ChargesVat, PurchasingLocation = o.PurchasingLocation, Reason = o.Reason, Note = o.Note,
                Subtotal = o.Subtotal, Tax = o.Tax, Total = o.Total,
            };
            foreach (var l in o.Lines)
            {
                var line = new PurchaseOrderLine
                {
                    LineNo = l.LineNo, PurchaseRequestLine = lines[(l.RequestNo, l.RequestLineKey)], MaterialId = l.MaterialId, MaterialName = l.MaterialName, Unit = l.Unit,
                    RequestedQuantity = l.RequestedQuantity, Quantity = l.Quantity, UnitPrice = l.UnitPrice, NetAmount = l.NetAmount, TaxIncludedAmount = l.TaxIncludedAmount, DesiredDate = l.DesiredDate,
                    DepartmentId = l.DepartmentId,
                };
                order.Lines.Add(line);
                foreach (var g in l.Receipts)
                    db.GoodsReceipts.Add(new GoodsReceipt
                    {
                        PurchaseOrderLine = line, ReceiveDate = g.Date, Quantity = g.Quantity, InvoiceNo = g.InvoiceNo, InvoiceLineNo = g.InvoiceLineNo, UnitPrice = g.UnitPrice, NetAmount = g.NetAmount,
                        SupplierId = g.SupplierId, ReturnNote = g.ReturnNote, MaterialId = g.MaterialId, MaterialName = g.MaterialName, Unit = g.Unit, Failed = g.Failed, Lot = g.Lot,
                        HasCertificate = g.HasCertificate, QaNote = g.QaNote,
                    });
            }
            db.PurchaseOrders.Add(order);
        }
        await db.SaveChangesAsync();
    }

    public async Task<DocumentTotals> GetDocumentTotalsAsync() => new(
        await db.PurchaseRequests.CountAsync(), await db.PurchaseRequestLines.CountAsync(), await db.PurchaseOrders.CountAsync(), await db.PurchaseOrderLines.CountAsync(), await db.GoodsReceipts.CountAsync(),
        await db.PurchaseOrderLines.SumAsync(l => (decimal?)l.Quantity) ?? 0, await db.GoodsReceipts.SumAsync(g => (decimal?)g.Quantity) ?? 0, await db.GoodsReceipts.SumAsync(g => (decimal?)g.NetAmount) ?? 0);

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
