using Lockte.Purch.Data;
using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Purch;

/// <summary>A purchase order line that is still waited for (ยอดค้างรับ = สั่ง − Σรับ &gt; 0, not cancelled).</summary>
public sealed record ReceivableLine(
    int OrderLineId,
    string OrderNo,
    DateOnly OrderDate,
    string RequestNo,
    int LineNo,
    int MaterialId,
    string MaterialCode,
    string MaterialName,
    string? Unit,
    bool MaterialIsMain,
    decimal Ordered,
    decimal Received,
    decimal Outstanding,
    decimal UnitPrice,
    DateOnly? DesiredDate,
    int DepartmentId,
    int SupplierId,
    string SupplierName,
    byte[] RowVersion);

/// <summary>A delivery received. <see cref="OnTime"/> is ส่งทัน (BR-030); <see cref="OutstandingAfter"/> is ยอดค้างรับ after it (negative when over-received).</summary>
public sealed record GoodsReceiptDto(
    int Id,
    int OrderLineId,
    string OrderNo,
    DateOnly ReceiveDate,
    decimal Quantity,
    string? InvoiceNo,
    string? InvoiceLineNo,
    decimal UnitPrice,
    decimal NetAmount,
    int SupplierId,
    string? ReturnNote,
    bool OnTime,
    decimal OutstandingAfter,
    byte[] RowVersion);

/// <summary>One delivery to record: <see cref="UnitPrice"/> null = the order's price, <see cref="SupplierId"/> null = the order's supplier.</summary>
public sealed record GoodsReceiptInput(
    int OrderLineId,
    DateOnly ReceiveDate,
    decimal? Quantity,
    string? InvoiceNo = null,
    string? InvoiceLineNo = null,
    decimal? UnitPrice = null,
    int? SupplierId = null,
    string? ReturnNote = null);

/// <summary>An order with something left to receive (SCR-11's "เลือกเลขที่ใบสั่งซื้อ").</summary>
public sealed record ReceivableOrder(int OrderId, string OrderNo, DateOnly OrderDate, string SupplierName, int OutstandingLines);

/// <summary>BR-030 in two small rules, apart from the screen so they can be tested.</summary>
public static class GoodsReceiptMath
{
    /// <summary>ส่งทัน: the delivery came on or before the date wanted; no date wanted = not on time (a null fails the legacy test).</summary>
    public static bool OnTime(DateOnly? desired, DateOnly received) => desired is { } d && d >= received;
}

/// <summary>
/// บันทึกการรับของ (SCR-11, BR-030, BR-031): one transaction per save. Each delivery is a row of its own; a วัตถุดิบหลัก goes into the stock ledger as a receipt
/// (AC-12), another does not (AC-14); the register's latest price and supplier of the material received are set by its id (AC-15), not by its name. What was
/// ordered and not received stays as ยอดค้างรับ; received in full or over, the line is closed. Saving twice does not post twice — each save is its own delivery.
/// </summary>
public sealed class GoodsReceiptRegistry(PurchDbContext db, StockLedger ledger)
{
    public async Task<ServiceResult> ReceiveAsync(IReadOnlyList<GoodsReceiptInput> inputs)
    {
        var errors = new List<string>();
        var kept = inputs.Where(i => i.Quantity is not null and not 0).ToList();
        if (kept.Any(i => i.Quantity < 0)) errors.Add("จำนวนรับต้องไม่ติดลบ");
        if (kept.Any(i => i.UnitPrice < 0)) errors.Add("ราคาต้องไม่ติดลบ");
        if (kept.Any(i => i.ReceiveDate == default)) errors.Add("กรุณากรอกวันที่รับ");
        if (kept.Any(i => Clean(i.InvoiceNo)?.Length > StockLedger.TextLength || Clean(i.InvoiceLineNo)?.Length > StockLedger.TextLength || Clean(i.ReturnNote)?.Length > StockLedger.TextLength))
            errors.Add($"เลขที่ใบส่งสินค้า ลำดับ และอื่น ๆ ยาวได้ไม่เกิน {StockLedger.TextLength} ตัว");
        var lineIds = kept.Select(i => i.OrderLineId).Distinct().ToList();
        var lines = await db.PurchaseOrderLines.Include(l => l.PurchaseOrder).Include(l => l.Material)
            .Where(l => lineIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id);
        if (lineIds.Any(id => !lines.ContainsKey(id))) errors.Add("ไม่พบรายการใบสั่งซื้อที่รับ (อาจถูกลบไปแล้ว)");
        else if (lines.Values.Any(l => l.ReceivingCancelled)) errors.Add("รายการใบสั่งซื้อที่ยกเลิกการรับแล้ว รับของไม่ได้");
        var supplierIds = kept.Where(i => i.SupplierId is not null).Select(i => i.SupplierId!.Value).Distinct().ToList();
        if (supplierIds.Count > 0 && await db.Suppliers.CountAsync(s => supplierIds.Contains(s.Id)) != supplierIds.Count) errors.Add("ไม่พบผู้ขายที่เลือก");
        if (errors.Count > 0) return new ServiceResult(false, errors);
        if (kept.Count == 0) return ServiceResult.Ok();

        foreach (var input in kept)
        {
            var line = lines[input.OrderLineId];
            var order = line.PurchaseOrder!;
            var material = line.Material!;
            var price = input.UnitPrice ?? line.UnitPrice;
            var supplierId = input.SupplierId ?? order.SupplierId;
            var quantity = input.Quantity!.Value;
            db.GoodsReceipts.Add(new GoodsReceipt
            {
                PurchaseOrderLineId = line.Id, ReceiveDate = input.ReceiveDate, Quantity = quantity, InvoiceNo = Clean(input.InvoiceNo), InvoiceLineNo = Clean(input.InvoiceLineNo),
                UnitPrice = price, NetAmount = PurchaseOrderMath.LineNet(price, quantity), SupplierId = supplierId, ReturnNote = Clean(input.ReturnNote),
            });
            // only a วัตถุดิบหลัก goes into the stock (BR-003, ข้อสมมติ ข)
            if (material.IsMain)
                ledger.Add(StockEntryKind.Receipt, input.ReceiveDate, material.Id, quantity, Clean(input.InvoiceNo), Clean(input.InvoiceLineNo), supplierId: supplierId, unitPrice: price);
            // the register's latest price and supplier, of the material received (by id, not by its name — DQ-02, DQ-04)
            if (await db.Materials.FirstOrDefaultAsync(m => m.Id == material.Id) is { } tracked)
            {
                tracked.Cost = price;
                tracked.LatestSupplierId = supplierId;
            }
        }
        await db.SaveChangesAsync();
        return ServiceResult.Ok(kept.Count);
    }

    /// <summary>
    /// ลบรายการค้างรับ (BR-033, ผู้ขายยกเลิกการส่ง): the line leaves ยอดค้างรับ. It stays on the order, whose number is untouched (AC-37); what was already
    /// received stays too.
    /// </summary>
    public async Task<ServiceResult> CancelOutstandingAsync(int orderLineId, byte[] loadedRowVersion)
    {
        if (await db.PurchaseOrderLines.FirstOrDefaultAsync(l => l.Id == orderLineId) is not { } line) return ServiceResult.Ok();
        line.ExpectRowVersion(loadedRowVersion);
        line.ReceivingCancelled = true;
        await db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}

/// <summary>The public, read-only face of การรับของ (ADR 0002). Writes go through <see cref="GoodsReceiptRegistry"/>.</summary>
public sealed class GoodsReceiptDirectory(PurchDbContext db)
{
    /// <summary>Orders with at least one line still to receive, the highest number first.</summary>
    public async Task<IReadOnlyList<ReceivableOrder>> GetOrdersAsync()
    {
        var orders = await db.PurchaseOrders.AsNoTracking().Include(o => o.Lines).ToListAsync();
        var received = await ReceivedAsync(orders.SelectMany(o => o.Lines.Select(l => l.Id)).ToList());
        return orders.Select(o => new ReceivableOrder(o.Id, o.OrderNo, o.OrderDate, o.SupplierName, o.Lines.Count(l => IsOutstanding(l, received))))
            .Where(o => o.OutstandingLines > 0).OrderByDescending(o => o.OrderNo.Length).ThenByDescending(o => o.OrderNo).ToList();
    }

    /// <summary>The lines of an order that are still waited for.</summary>
    public async Task<IReadOnlyList<ReceivableLine>> GetReceivableLinesAsync(int orderId)
    {
        var order = await db.PurchaseOrders.AsNoTracking().Include(o => o.Lines).ThenInclude(l => l.Material)
            .Include(o => o.Lines).ThenInclude(l => l.PurchaseRequestLine).ThenInclude(r => r!.PurchaseRequest).FirstOrDefaultAsync(o => o.Id == orderId);
        if (order is null) return [];
        var received = await ReceivedAsync(order.Lines.Select(l => l.Id).ToList());
        return order.Lines.Where(l => IsOutstanding(l, received)).OrderBy(l => l.LineNo).ThenBy(l => l.Id)
            .Select(l => new ReceivableLine(l.Id, order.OrderNo, order.OrderDate, l.PurchaseRequestLine!.PurchaseRequest!.RequestNo, l.LineNo, l.MaterialId, l.Material!.Code, l.MaterialName, l.Unit,
                l.Material.IsMain, l.Quantity, received.GetValueOrDefault(l.Id), l.Quantity - received.GetValueOrDefault(l.Id), l.UnitPrice, l.DesiredDate, l.DepartmentId, order.SupplierId,
                order.SupplierName, l.RowVersion)).ToList();
    }

    /// <summary>The deliveries of an order line, in the order they were received.</summary>
    public async Task<IReadOnlyList<GoodsReceiptDto>> GetReceiptsAsync(int orderLineId)
    {
        var line = await db.PurchaseOrderLines.AsNoTracking().Include(l => l.PurchaseOrder).FirstOrDefaultAsync(l => l.Id == orderLineId);
        if (line is null) return [];
        var rows = await db.GoodsReceipts.AsNoTracking().Where(r => r.PurchaseOrderLineId == orderLineId).OrderBy(r => r.ReceiveDate).ThenBy(r => r.Id).ToListAsync();
        var running = 0m;
        var result = new List<GoodsReceiptDto>();
        foreach (var r in rows)
        {
            running += r.Quantity;
            result.Add(new GoodsReceiptDto(r.Id, r.PurchaseOrderLineId, line.PurchaseOrder!.OrderNo, r.ReceiveDate, r.Quantity, r.InvoiceNo, r.InvoiceLineNo, r.UnitPrice, r.NetAmount, r.SupplierId,
                r.ReturnNote, GoodsReceiptMath.OnTime(line.DesiredDate, r.ReceiveDate), line.Quantity - running, r.RowVersion));
        }
        return result;
    }

    private static bool IsOutstanding(PurchaseOrderLine line, Dictionary<int, decimal> received)
        => !line.ReceivingCancelled && line.Quantity - received.GetValueOrDefault(line.Id) > 0;

    private async Task<Dictionary<int, decimal>> ReceivedAsync(IReadOnlyList<int> lineIds)
        => await db.GoodsReceipts.AsNoTracking().Where(r => lineIds.Contains(r.PurchaseOrderLineId))
            .GroupBy(r => r.PurchaseOrderLineId).Select(g => new { g.Key, Total = g.Sum(r => r.Quantity) }).ToDictionaryAsync(g => g.Key, g => g.Total);
}
