using Lockte.Sales.Data;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Sales;

/// <summary>
/// A line of a migrated ORDER (#112) with both sets of values as the legacy tables held them: <b>รับ</b> from T-ORDER (null when the
/// order is not in the history) and <b>ค้างส่ง</b> from T-ODD (0 when nothing is left), and the delivery typed but not yet saved.
/// Amounts are the stored ones, not worked out again (Q-39).
/// </summary>
public sealed record OrderImportLine(
    int LineNo, int ProductId, string ProductName, string? Unit, decimal? UnitPrice,
    decimal? ReceivedQuantity, decimal? ReceivedAmount, decimal OutstandingQuantity, decimal? OutstandingAmount,
    DateOnly? PendingDeliveryDate = null, string? PendingInvoiceNo = null, int? PendingInvoiceLine = null, decimal? PendingQuantity = null,
    decimal? PendingAmount = null, string? PendingLot = null, decimal? PendingBoxes = null);

/// <summary>
/// An ORDER of the migration. <see cref="OriginalNo"/> is the number it had in Access when the migration had to change it (a
/// number several customers used, #28). <see cref="InHistory"/> = the order is in T-ORDER (it has รับ values).
/// </summary>
public sealed record OrderImportRow(
    string OrderNo, string? OriginalNo, DateOnly OrderDate, int CustomerId, string CustomerName, int? SalesTerritoryId, DateOnly? DeliveryDate,
    string? PaymentTerms, string? Remark, bool InHistory, IReadOnlyList<OrderImportLine> Lines);

/// <summary>A migrated delivery (T-ODDส่งออก).</summary>
public sealed record DeliveryImportRow(
    string OrderNo, DateOnly? OrderDate, int LineNo, int CustomerId, string CustomerName, int? SalesTerritoryId, int ProductId, string ProductName,
    string? Unit, decimal? UnitPrice, DateOnly? DueDate, DateOnly? DeliveryDate, string? InvoiceNo, int? InvoiceLine, string? Lot, decimal Quantity,
    decimal? Amount, decimal? Boxes, bool Complete);

/// <summary>ORDER of one month and one territory, counted back for รายงานตรวจยอด (AC-17, AC-19).</summary>
public sealed record OrderMonthTotal(string Month, string TerritoryCode, int Orders, decimal ReceivedAmount, int OutstandingLines, decimal OutstandingQuantity, decimal OutstandingAmount);

/// <summary>การส่งของ of one month, counted back for รายงานตรวจยอด (AC-16).</summary>
public sealed record DeliveryMonthTotal(string Month, int Rows, decimal Quantity, decimal Amount, decimal Boxes);

/// <summary>
/// ย้ายข้อมูล ORDER, ประวัติ ORDER และประวัติการส่งของ (#112) into module ขายและส่งของ. For the data-migration tool only, never registered in
/// the web app: the rows are taken as they were (no check of the amounts, an old number allowed), in batches, with no row-by-row
/// history (#39). Also counts the rows back for the tool's รายงานตรวจยอด.
/// </summary>
public sealed class SalesOrderImport(SalesDbContext db, AuditScope audit)
{
    private const int BatchSize = 500;

    /// <summary>Deletes every ORDER, its lines and every delivery (the load starts again, #47). ลูกค้า and เขตการขาย stay.</summary>
    public async Task DeleteAllAsync()
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.Deliveries.ExecuteDeleteAsync();
        await db.SalesOrderLines.ExecuteDeleteAsync();
        await db.SalesOrders.ExecuteDeleteAsync();
        await transaction.CommitAsync();
    }

    public async Task AddOrdersAsync(IEnumerable<OrderImportRow> rows)
    {
        using (audit.SuppressRowChanges())
            foreach (var batch in rows.Chunk(BatchSize))
            {
                db.SalesOrders.AddRange(batch.Select(r =>
                {
                    var order = new SalesOrder
                    {
                        OrderNo = r.OrderNo, OriginalNo = r.OriginalNo, OrderDate = r.OrderDate, CustomerId = r.CustomerId, CustomerName = r.CustomerName,
                        SalesTerritoryId = r.SalesTerritoryId, DeliveryDate = r.DeliveryDate, PaymentTerms = r.PaymentTerms, Remark = r.Remark,
                        RecordedAt = r.InHistory ? new DateTimeOffset(r.OrderDate.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(7)) : null,
                    };
                    order.Lines.AddRange(r.Lines.Select(l => new SalesOrderLine
                    {
                        SalesOrder = order, LineNo = l.LineNo, ProductId = l.ProductId, ProductName = l.ProductName, Unit = l.Unit, UnitPrice = l.UnitPrice,
                        ReceivedQuantity = l.ReceivedQuantity, ReceivedAmount = l.ReceivedAmount,
                        OutstandingQuantity = l.OutstandingQuantity, OutstandingAmount = l.OutstandingAmount,
                        PendingDeliveryDate = l.PendingDeliveryDate, PendingInvoiceNo = l.PendingInvoiceNo, PendingInvoiceLine = l.PendingInvoiceLine,
                        PendingQuantity = l.PendingQuantity, PendingAmount = l.PendingAmount, PendingLot = l.PendingLot, PendingBoxes = l.PendingBoxes,
                    }));
                    return order;
                }));
                await db.SaveChangesAsync();
                db.ChangeTracker.Clear();
            }
    }

    public async Task AddDeliveriesAsync(IEnumerable<DeliveryImportRow> rows)
    {
        using (audit.SuppressRowChanges())
            foreach (var batch in rows.Chunk(BatchSize * 4))
            {
                db.Deliveries.AddRange(batch.Select(r => new Delivery
                {
                    OrderNo = r.OrderNo, OrderDate = r.OrderDate, LineNo = r.LineNo, CustomerId = r.CustomerId, CustomerName = r.CustomerName,
                    SalesTerritoryId = r.SalesTerritoryId, ProductId = r.ProductId, ProductName = r.ProductName, Unit = r.Unit, UnitPrice = r.UnitPrice,
                    DueDate = r.DueDate, DeliveryDate = r.DeliveryDate, InvoiceNo = r.InvoiceNo, InvoiceLine = r.InvoiceLine, Lot = r.Lot,
                    Quantity = r.Quantity, Amount = r.Amount, Boxes = r.Boxes, Complete = r.Complete,
                }));
                await db.SaveChangesAsync();
                db.ChangeTracker.Clear();
            }
    }

    /// <summary>Lines that have a ค้างส่ง value (≠ 0).</summary>
    public Task<int> CountOutstandingLinesAsync() => db.SalesOrderLines.CountAsync(l => l.OutstandingQuantity != 0);

    /// <summary>Lines that have a รับ value.</summary>
    public Task<int> CountReceivedLinesAsync() => db.SalesOrderLines.CountAsync(l => l.ReceivedQuantity != null);

    public Task<int> CountDeliveriesAsync() => db.Deliveries.CountAsync();

    /// <summary>
    /// ORDER by month of the order date and territory (AC-17): orders in the history, Σ มูลค่าที่รับ, and the ค้างส่ง lines of the
    /// order with their Σ จำนวน and Σ มูลค่า (AC-19).
    /// </summary>
    public async Task<IReadOnlyList<OrderMonthTotal>> GetOrderMonthTotalsAsync()
    {
        var territories = await db.SalesTerritories.AsNoTracking().ToDictionaryAsync(t => t.Id, t => t.Code);
        var orders = await db.SalesOrders.AsNoTracking().Select(o => new
        {
            o.OrderDate, o.SalesTerritoryId, o.RecordedAt,
            Received = o.Lines.Sum(l => l.ReceivedAmount ?? 0),
            OutLines = o.Lines.Count(l => l.OutstandingQuantity != 0),
            OutQuantity = o.Lines.Sum(l => l.OutstandingQuantity),
            OutAmount = o.Lines.Sum(l => l.OutstandingAmount ?? 0),
        }).ToListAsync();
        return orders
            .GroupBy(o => (Month: Month(o.OrderDate), Territory: o.SalesTerritoryId is { } t ? territories.GetValueOrDefault(t, "") : ""))
            .OrderBy(g => g.Key.Month).ThenBy(g => g.Key.Territory, StringComparer.Ordinal)
            .Select(g => new OrderMonthTotal(g.Key.Month, g.Key.Territory, g.Count(o => o.RecordedAt != null), g.Sum(o => o.Received),
                g.Sum(o => o.OutLines), g.Sum(o => o.OutQuantity), g.Sum(o => o.OutAmount)))
            .ToList();
    }

    /// <summary>การส่งของ by month of the delivery date (AC-16): rows, Σ จำนวน, Σ มูลค่า, Σ กล่อง.</summary>
    public async Task<IReadOnlyList<DeliveryMonthTotal>> GetDeliveryMonthTotalsAsync()
    {
        var rows = await db.Deliveries.AsNoTracking().Select(d => new { d.DeliveryDate, d.Quantity, d.Amount, d.Boxes }).ToListAsync();
        return rows.GroupBy(d => d.DeliveryDate is { } date ? Month(date) : "")
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new DeliveryMonthTotal(g.Key, g.Count(), g.Sum(d => d.Quantity), g.Sum(d => d.Amount ?? 0), g.Sum(d => d.Boxes ?? 0)))
            .ToList();
    }

    private static string Month(DateOnly date) => $"{date.Year:0000}-{date.Month:00}";
}
