using Lockte.Sales.Data;
using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Sales;

/// <summary>
/// Which of an ORDER line's two sets of values a screen works on (#28, BR-012): <see cref="Outstanding"/> = ค้างส่ง (the three
/// ways to find an order, ODD2/ODD4/ODD6), <see cref="History"/> = รับ (แก้ไข ORDER ที่เซลรับมาทั้งหมด, ODD8). Editing one side
/// never changes the other (Q-32 ⚠ kept).
/// </summary>
public enum OrderSide
{
    Outstanding,
    History,
}

/// <summary>The head of an order as the edit screens change it; the number stays (it is taken, #28).</summary>
public sealed record SalesOrderHeadEdit(
    DateOnly OrderDate,
    int CustomerId,
    string? CustomerName,
    int? SalesTerritoryId,
    DateOnly? DeliveryDate,
    string? PaymentTerms,
    string? Remark);

/// <summary>
/// A line of the side being edited. <see cref="Id"/> null = a new line (dropped when <see cref="Quantity"/> is blank).
/// <see cref="RowVersion"/> = as the screen loaded it.
/// </summary>
public sealed record SalesOrderLineEdit(int? Id, int ProductId, decimal? Quantity, decimal? UnitPrice, string? ProductName = null, string? Unit = null, byte[]? RowVersion = null);

/// <summary>One order of a search (SCR-06 "เลือกตาม…") with what the side being edited holds.</summary>
public sealed record SalesOrderListItem(int Id, string OrderNo, DateOnly OrderDate, string CustomerCode, string CustomerName, int Lines, decimal Amount);

/// <summary>How an order is found: by number, by date or by customer, among the orders of one side (BR-012).</summary>
public sealed record SalesOrderSearch(OrderSide Side, string? OrderNo = null, DateOnly? Date = null, int? CustomerId = null);

/// <summary>
/// แก้ไข/ลบ ORDER (SCR-06, BR-012). A save changes the head (shared by both sides) and the values of one side of the lines
/// it names; the value of a line follows the quantity and price whichever was edited last (ข้อสมมติ ก). Deleting a line
/// empties that side of it; a line empty on both sides goes, and an order with no line left goes with its number — which is
/// free again only then (#28). Nothing checks whether part of an order was delivered already (BR-012 ⚠ kept).
/// </summary>
public sealed class SalesOrderEditor(SalesDbContext db, TimeProvider clock, IProductLookup? products = null)
{
    public async Task<ServiceResult> SaveAsync(int orderId, byte[] orderRowVersion, OrderSide side, SalesOrderHeadEdit head, IReadOnlyList<SalesOrderLineEdit> lines)
    {
        var order = await LoadAsync(orderId);
        if (order is null) return ServiceResult.Fail("ไม่พบ ORDER นี้ อาจถูกลบไปแล้ว");
        order.ExpectRowVersion(orderRowVersion);

        var errors = new List<string>();
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == head.CustomerId);
        if (customer is null) errors.Add("กรุณาเลือกลูกค้าที่มีในทะเบียนลูกค้า");
        var customerName = Clean(head.CustomerName) ?? customer?.Name;
        if (customerName?.Length > CustomerRegistry.NameLength) errors.Add($"ชื่อลูกค้ายาวได้ไม่เกิน {CustomerRegistry.NameLength} ตัว");
        if (head.SalesTerritoryId is { } territoryId && !await db.SalesTerritories.AnyAsync(t => t.Id == territoryId)) errors.Add("ไม่พบเขตการขายที่เลือก");
        var paymentTerms = Clean(head.PaymentTerms);
        var remark = Clean(head.Remark);
        if (paymentTerms?.Length > SalesOrderRegistry.TextLength) errors.Add($"เงื่อนไขการชำระเงินยาวได้ไม่เกิน {SalesOrderRegistry.TextLength} ตัว");
        if (remark?.Length > SalesOrderRegistry.TextLength) errors.Add($"หมายเหตุยาวได้ไม่เกิน {SalesOrderRegistry.TextLength} ตัว");

        var changes = new List<(SalesOrderLine? Line, SalesOrderLineEdit Edit, ProductRef? Product)>();
        foreach (var edit in lines)
        {
            SalesOrderLine? line = null;
            if (edit.Id is { } lineId)
            {
                line = order.Lines.FirstOrDefault(l => l.Id == lineId);
                if (line is null) { errors.Add("ไม่พบบรรทัดที่แก้ อาจถูกลบไปแล้ว"); continue; }
                if (edit.RowVersion is not null) line.ExpectRowVersion(edit.RowVersion);
                if (edit.Quantity is null) { errors.Add($"บรรทัด {line.LineNo}: จำนวนว่าง — ใช้ปุ่มลบแถวเพื่อลบบรรทัด"); continue; }
            }
            else if (edit.Quantity is null || (side == OrderSide.Outstanding && edit.Quantity == 0)) continue; // a blank new line is dropped
            if (edit.Quantity < 0 || edit.UnitPrice < 0) { errors.Add("จำนวนและราคาต้องไม่ติดลบ"); continue; }
            ProductRef? product = null;
            if (line is null || line.ProductId != edit.ProductId)
            {
                product = products is null ? null : await products.FindAsync(edit.ProductId);
                if (product is null) { errors.Add("กรุณาเลือกสินค้าที่มีในทะเบียนสินค้าทุกบรรทัด"); continue; }
            }
            changes.Add((line, edit, product));
        }
        if (errors.Count > 0) return new ServiceResult(false, errors.Distinct().ToList());

        order.OrderDate = head.OrderDate;
        order.Customer = customer!;
        order.CustomerName = customerName ?? "";
        order.SalesTerritoryId = head.SalesTerritoryId;
        order.DeliveryDate = head.DeliveryDate;
        order.PaymentTerms = paymentTerms;
        order.Remark = remark;

        var nextNo = order.Lines.Select(l => l.LineNo).DefaultIfEmpty(0).Max();
        foreach (var (existing, edit, product) in changes)
        {
            var line = existing;
            if (line is null)
            {
                line = new SalesOrderLine { SalesOrder = order, LineNo = ++nextNo, ProductId = edit.ProductId };
                order.Lines.Add(line);
            }
            if (product is not null)
            {
                line.ProductId = product.Id;
                line.ProductName = Clean(edit.ProductName) ?? product.Name;
                line.Unit = Clean(edit.Unit) ?? product.Unit;
            }
            else
            {
                line.ProductName = Clean(edit.ProductName) ?? line.ProductName;
                line.Unit = Clean(edit.Unit) ?? line.Unit;
            }
            line.UnitPrice = edit.UnitPrice;
            var amount = SalesOrderRules.Amount(edit.Quantity, edit.UnitPrice);
            if (side == OrderSide.Outstanding)
            {
                line.OutstandingQuantity = edit.Quantity!.Value;
                line.OutstandingAmount = amount;
            }
            else
            {
                line.ReceivedQuantity = edit.Quantity;
                line.ReceivedAmount = amount;
            }
        }
        Settle(order);
        await db.SaveChangesAsync();
        return ServiceResult.Ok(order.Id);
    }

    /// <summary>
    /// ลบบรรทัด (the delete button, instead of clearing the quantity, BR-021): empties that side of the line only; when both
    /// sides are empty the line goes, and with the last line the order. Deleting a line already gone succeeds.
    /// </summary>
    public async Task<ServiceResult> DeleteLineAsync(int orderId, int lineId, OrderSide side, byte[] lineRowVersion)
    {
        var order = await LoadAsync(orderId);
        if (order is null || order.Lines.FirstOrDefault(l => l.Id == lineId) is not { } line) return ServiceResult.Ok();
        line.ExpectRowVersion(lineRowVersion);
        Empty(line, side);
        Settle(order);
        await db.SaveChangesAsync();
        return ServiceResult.Ok(order.Id);
    }

    /// <summary>
    /// ลบทั้งใบ (macro ลบORDER, with a confirmation on the screen): from the ค้างส่ง side it closes the backorder of every line —
    /// the order stays in the history and its number stays used; from the history side it empties the รับ values. An order
    /// left with no line on either side is deleted, and only then is its number free (#28, AC-06). Deleting an order that is
    /// already gone succeeds.
    /// </summary>
    public async Task<ServiceResult> DeleteOrderAsync(int orderId, OrderSide side, byte[] orderRowVersion)
    {
        var order = await LoadAsync(orderId);
        if (order is null) return ServiceResult.Ok();
        order.ExpectRowVersion(orderRowVersion);
        foreach (var line in order.Lines) Empty(line, side);
        Settle(order);
        await db.SaveChangesAsync();
        return ServiceResult.Ok(order.Id);
    }

    private static void Empty(SalesOrderLine line, OrderSide side)
    {
        if (side == OrderSide.Outstanding)
        {
            line.OutstandingQuantity = 0;
            line.OutstandingAmount = null;
        }
        else
        {
            line.ReceivedQuantity = null;
            line.ReceivedAmount = null;
        }
    }

    /// <summary>A line empty on both sides goes; an order without lines goes; the order is in the history while any line has a รับ value.</summary>
    private void Settle(SalesOrder order)
    {
        // The removed lines stay in order.Lines until the save, so the history can still name them by the order's number.
        var gone = order.Lines.Where(l => l.ReceivedQuantity is null && l.OutstandingQuantity == 0 && l.Id != 0).ToList();
        foreach (var line in gone) db.SalesOrderLines.Remove(line);
        var remaining = order.Lines.Except(gone).ToList();
        if (remaining.Count == 0)
        {
            db.SalesOrders.Remove(order);
            return;
        }
        var inHistory = remaining.Any(l => l.ReceivedQuantity is not null);
        order.RecordedAt = inHistory ? order.RecordedAt ?? clock.GetUtcNow() : null;
    }

    private Task<SalesOrder?> LoadAsync(int orderId)
        => db.SalesOrders.Include(o => o.Lines).Include(o => o.Customer).FirstOrDefaultAsync(o => o.Id == orderId);

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
