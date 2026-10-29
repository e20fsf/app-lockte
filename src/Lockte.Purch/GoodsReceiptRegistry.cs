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

/// <summary>A line still waited for with the supplier's phone (RPT-04): ordered, received, ยอดค้างรับ.</summary>
public sealed record OutstandingReceiptLine(
    string OrderNo,
    DateOnly OrderDate,
    int LineNo,
    string MaterialName,
    string? Unit,
    decimal Outstanding,
    DateOnly? DesiredDate,
    string SupplierName,
    string? SupplierTel);

/// <summary>A delivery received. <see cref="OnTime"/> is ส่งทัน (BR-030); <see cref="OutstandingAfter"/> is ยอดค้างรับ after it (negative when over-received).</summary>
public sealed record GoodsReceiptDto(
    int Id,
    int OrderLineId,
    string OrderNo,
    int MaterialId,
    string MaterialCode,
    string MaterialName,
    string? Unit,
    bool Failed,
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
    string? Lot,
    bool? HasCertificate,
    string? QaNote,
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
    string? ReturnNote = null,
    bool Failed = false);

/// <summary>
/// A receipt in the history as SCR-12 edits it (BR-034). <see cref="MaterialName"/>/<see cref="Unit"/> are the document's text; changing <see cref="MaterialId"/> refills them.
/// </summary>
public sealed record GoodsReceiptUpdate(
    int Id,
    byte[] RowVersion,
    DateOnly ReceiveDate,
    decimal Quantity,
    string? InvoiceNo,
    string? InvoiceLineNo,
    decimal UnitPrice,
    int SupplierId,
    int MaterialId,
    string? MaterialName,
    string? Unit,
    bool Failed,
    string? ReturnNote);

/// <summary>
/// ผลตรวจรับของ QA (SCR-13, BR-035): LOT, ผลการตรวจ (<see cref="Failed"/> = ไม่ผ่าน), ใบรับรอง, หมายเหตุ — and the name of the material and the supplier as the document shows
/// them (DQ-04). Null name/unit/supplier keep what the receipt has.
/// </summary>
public sealed record GoodsReceiptQaUpdate(
    int Id,
    byte[] RowVersion,
    string? Lot,
    bool Failed,
    bool? HasCertificate,
    string? Note,
    int? MaterialId = null,
    string? MaterialName = null,
    string? Unit = null,
    int? SupplierId = null);

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
                MaterialId = material.Id, MaterialName = line.MaterialName, Unit = line.Unit, Failed = input.Failed,
            });
            if (input.Failed) continue; // BR-032: evidence only — not received, not in the stock, the register is not touched
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

    /// <summary>
    /// แก้ประวัติรับของ (SCR-12, BR-034): date, quantity, price, material, supplier, result, return note — the amount recomputed as in BR-030. **The stock is not adjusted**
    /// and the order is not handed back to ยอดค้างรับ (AC-38, ข้อสมมติ ค): the screen tells the user to correct the stock on SCR-16. A stale row throws
    /// <see cref="ConcurrencyConflictException"/>.
    /// </summary>
    public async Task<ServiceResult> UpdateAsync(GoodsReceiptUpdate input)
    {
        var receipt = await db.GoodsReceipts.Include(r => r.PurchaseOrderLine).FirstOrDefaultAsync(r => r.Id == input.Id);
        if (receipt is null) return ServiceResult.Fail("ไม่พบรายการรับของนี้ (อาจถูกลบไปแล้ว)");
        receipt.ExpectRowVersion(input.RowVersion);
        var errors = new List<string>();
        if (input.ReceiveDate == default) errors.Add("กรุณากรอกวันที่รับ");
        if (input.Quantity <= 0) errors.Add("จำนวนรับต้องมากกว่า 0 (ถ้าไม่ต้องการรายการนี้ ใช้ปุ่มลบ)");
        if (input.UnitPrice < 0) errors.Add("ราคาต้องไม่ติดลบ");
        if (Clean(input.InvoiceNo)?.Length > StockLedger.TextLength || Clean(input.InvoiceLineNo)?.Length > StockLedger.TextLength || Clean(input.ReturnNote)?.Length > StockLedger.TextLength)
            errors.Add($"เลขที่ใบส่งสินค้า ลำดับ และอื่น ๆ ยาวได้ไม่เกิน {StockLedger.TextLength} ตัว");
        var material = await db.Materials.AsNoTracking().FirstOrDefaultAsync(m => m.Id == input.MaterialId);
        if (material is null) errors.Add("ไม่พบวัตถุดิบที่เลือก");
        if (!await db.Suppliers.AnyAsync(s => s.Id == input.SupplierId)) errors.Add("ไม่พบผู้ขายที่เลือก");
        if (errors.Count > 0) return new ServiceResult(false, errors);

        var line = receipt.PurchaseOrderLine!;
        var before = await OutstandingAsync(line);
        var materialChanged = receipt.MaterialId != material!.Id;
        receipt.ReceiveDate = input.ReceiveDate;
        receipt.Quantity = input.Quantity;
        receipt.InvoiceNo = Clean(input.InvoiceNo);
        receipt.InvoiceLineNo = Clean(input.InvoiceLineNo);
        receipt.UnitPrice = input.UnitPrice;
        receipt.NetAmount = PurchaseOrderMath.LineNet(input.UnitPrice, input.Quantity);
        receipt.SupplierId = input.SupplierId;
        receipt.MaterialId = material.Id;
        receipt.MaterialName = Clean(input.MaterialName) ?? (materialChanged ? material.Name : receipt.MaterialName);
        receipt.Unit = Clean(input.Unit) ?? (materialChanged ? material.Unit : receipt.Unit);
        receipt.Failed = input.Failed;
        receipt.ReturnNote = Clean(input.ReturnNote);
        await db.SaveChangesAsync();
        await KeepClosedAsync(line, before);
        return ServiceResult.Ok(receipt.Id);
    }

    /// <summary>
    /// ลบรายการในประวัติรับของ (SCR-12, BR-034 ข้อ 2, ปุ่มลบพร้อมหน้ายืนยัน): the row goes; the stock is not taken back and the order does not return to ยอดค้างรับ
    /// (AC-38); the document numbers are not touched (AC-35). Deleting a row that is already gone succeeds.
    /// </summary>
    public async Task<ServiceResult> DeleteAsync(int id, byte[] loadedRowVersion)
    {
        var receipt = await db.GoodsReceipts.Include(r => r.PurchaseOrderLine).FirstOrDefaultAsync(r => r.Id == id);
        if (receipt is null) return ServiceResult.Ok();
        receipt.ExpectRowVersion(loadedRowVersion);
        var line = receipt.PurchaseOrderLine!;
        var before = await OutstandingAsync(line);
        db.GoodsReceipts.Remove(receipt);
        await db.SaveChangesAsync();
        await KeepClosedAsync(line, before);
        return ServiceResult.Ok();
    }

    /// <summary>
    /// QA บันทึกผลตรวจรับ (SCR-13, BR-035). Marking a receipt that already went into the stock as ไม่ผ่าน **does not change the stock** (ข้อสมมติ ค), and the order does not go back
    /// to ยอดค้างรับ — the screen warns the user to correct the stock (AC-39). A stale row throws <see cref="ConcurrencyConflictException"/>.
    /// </summary>
    public async Task<ServiceResult> SaveQaAsync(GoodsReceiptQaUpdate input)
    {
        var receipt = await db.GoodsReceipts.Include(r => r.PurchaseOrderLine).FirstOrDefaultAsync(r => r.Id == input.Id);
        if (receipt is null) return ServiceResult.Fail("ไม่พบรายการรับของนี้ (อาจถูกลบไปแล้ว)");
        receipt.ExpectRowVersion(input.RowVersion);
        var errors = new List<string>();
        if (Clean(input.Lot)?.Length > StockLedger.TextLength || Clean(input.Note)?.Length > StockLedger.TextLength) errors.Add($"LOT และหมายเหตุยาวได้ไม่เกิน {StockLedger.TextLength} ตัว");
        Material? material = null;
        if (input.MaterialId is { } materialId && materialId != receipt.MaterialId)
        {
            material = await db.Materials.AsNoTracking().FirstOrDefaultAsync(m => m.Id == materialId);
            if (material is null) errors.Add("ไม่พบวัตถุดิบที่เลือก");
        }
        if (input.SupplierId is { } supplierId && !await db.Suppliers.AnyAsync(s => s.Id == supplierId)) errors.Add("ไม่พบผู้ขายที่เลือก");
        if (errors.Count > 0) return new ServiceResult(false, errors);

        var line = receipt.PurchaseOrderLine!;
        var before = await OutstandingAsync(line);
        receipt.Lot = Clean(input.Lot);
        receipt.Failed = input.Failed;
        receipt.HasCertificate = input.HasCertificate;
        receipt.QaNote = Clean(input.Note);
        if (material is not null)
        {
            receipt.MaterialId = material.Id;
            receipt.MaterialName = Clean(input.MaterialName) ?? material.Name;
            receipt.Unit = Clean(input.Unit) ?? material.Unit;
        }
        else
        {
            receipt.MaterialName = Clean(input.MaterialName) ?? receipt.MaterialName;
            receipt.Unit = Clean(input.Unit) ?? receipt.Unit;
        }
        if (input.SupplierId is { } supplier) receipt.SupplierId = supplier;
        await db.SaveChangesAsync();
        await KeepClosedAsync(line, before);
        return ServiceResult.Ok(receipt.Id);
    }

    /// <summary>ยอดค้างรับ of a line: ordered − Σ received that did not fail.</summary>
    private async Task<decimal> OutstandingAsync(PurchaseOrderLine line)
        => line.Quantity - (await db.GoodsReceipts.Where(r => r.PurchaseOrderLineId == line.Id && !r.Failed).SumAsync(r => (decimal?)r.Quantity) ?? 0);

    /// <summary>A line that was fully received does not go back to waiting when its history is corrected (AC-38): it is marked as no longer waited for.</summary>
    private async Task KeepClosedAsync(PurchaseOrderLine line, decimal outstandingBefore)
    {
        if (outstandingBefore > 0 || line.ReceivingCancelled) return;
        if (await OutstandingAsync(line) > 0)
        {
            line.ReceivingCancelled = true;
            await db.SaveChangesAsync();
        }
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

    /// <summary>
    /// ทุกรายการสั่งซื้อที่ยังค้างรับ (RPT-04) by เลขที่ใบสั่งซื้อ (the highest year last, as numbers sort) → ที่: ยอดค้างรับ = สั่ง − Σรับ (ไม่นับที่ไม่ผ่าน), not cancelled. Live, no work table.
    /// </summary>
    public async Task<IReadOnlyList<OutstandingReceiptLine>> GetAllOutstandingAsync()
    {
        var orders = await db.PurchaseOrders.AsNoTracking().Include(o => o.Lines).ToListAsync();
        var received = await ReceivedAsync(orders.SelectMany(o => o.Lines.Select(l => l.Id)).ToList());
        return orders.OrderBy(o => o.OrderNo.Length).ThenBy(o => o.OrderNo, StringComparer.Ordinal)
            .SelectMany(o => o.Lines.OrderBy(l => l.LineNo).ThenBy(l => l.Id).Where(l => IsOutstanding(l, received))
                .Select(l => new OutstandingReceiptLine(o.OrderNo, o.OrderDate, l.LineNo, l.MaterialName, l.Unit, l.Quantity - received.GetValueOrDefault(l.Id), l.DesiredDate, o.SupplierName, o.SupplierTel)))
            .ToList();
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
        var rows = await db.GoodsReceipts.AsNoTracking().Include(r => r.Material).Where(r => r.PurchaseOrderLineId == orderLineId).OrderBy(r => r.ReceiveDate).ThenBy(r => r.Id).ToListAsync();
        var running = 0m;
        var result = new List<GoodsReceiptDto>();
        foreach (var r in rows)
        {
            if (!r.Failed) running += r.Quantity; // a failed delivery is not received: the outstanding stays
            result.Add(ToDto(r, line.PurchaseOrder!.OrderNo, line.DesiredDate, line.Quantity - running));
        }
        return result;
    }

    /// <summary>
    /// ทุกรายการที่รับในวันที่ระบุ (SCR-13, BR-035 ข้อ 1) — every material, main or not — by order number and line. The date is a date: the legacy compared it with text.
    /// </summary>
    public async Task<IReadOnlyList<GoodsReceiptDto>> GetByDateAsync(DateOnly date)
    {
        var lineIds = await db.GoodsReceipts.AsNoTracking().Where(r => r.ReceiveDate == date).Select(r => r.PurchaseOrderLineId).Distinct().ToListAsync();
        var result = new List<GoodsReceiptDto>();
        foreach (var lineId in lineIds) result.AddRange((await GetReceiptsAsync(lineId)).Where(r => r.ReceiveDate == date));
        return result.OrderBy(r => r.OrderNo.Length).ThenBy(r => r.OrderNo).ThenBy(r => r.Id).ToList();
    }

    /// <summary>Orders that have receipts in the history (SCR-12's "เลือกเลขที่ใบสั่งซื้อ"), the highest number first.</summary>
    public async Task<IReadOnlyList<ReceivableOrder>> GetHistoryOrdersAsync()
        => (await db.GoodsReceipts.AsNoTracking().Select(r => new { r.PurchaseOrderLine!.PurchaseOrderId, r.PurchaseOrderLine.PurchaseOrder!.OrderNo, r.PurchaseOrderLine.PurchaseOrder.OrderDate, r.PurchaseOrderLine.PurchaseOrder.SupplierName })
                .Distinct().ToListAsync())
            .OrderByDescending(o => o.OrderNo.Length).ThenByDescending(o => o.OrderNo).Select(o => new ReceivableOrder(o.PurchaseOrderId, o.OrderNo, o.OrderDate, o.SupplierName, 0)).ToList();

    /// <summary>Every receipt of an order, by line then date.</summary>
    public async Task<IReadOnlyList<GoodsReceiptDto>> GetOrderReceiptsAsync(int orderId)
    {
        var lines = await db.PurchaseOrderLines.AsNoTracking().Include(l => l.PurchaseOrder).Where(l => l.PurchaseOrderId == orderId).OrderBy(l => l.LineNo).ThenBy(l => l.Id).ToListAsync();
        var result = new List<GoodsReceiptDto>();
        foreach (var line in lines) result.AddRange(await GetReceiptsAsync(line.Id));
        return result;
    }

    private static GoodsReceiptDto ToDto(GoodsReceipt r, string orderNo, DateOnly? desired, decimal outstandingAfter) => new(
        r.Id, r.PurchaseOrderLineId, orderNo, r.MaterialId, r.Material!.Code, r.MaterialName, r.Unit, r.Failed, r.ReceiveDate, r.Quantity, r.InvoiceNo, r.InvoiceLineNo, r.UnitPrice, r.NetAmount,
        r.SupplierId, r.ReturnNote, GoodsReceiptMath.OnTime(desired, r.ReceiveDate), outstandingAfter, r.Lot, r.HasCertificate, r.QaNote, r.RowVersion);

    private static bool IsOutstanding(PurchaseOrderLine line, Dictionary<int, decimal> received)
        => !line.ReceivingCancelled && line.Quantity - received.GetValueOrDefault(line.Id) > 0;

    private async Task<Dictionary<int, decimal>> ReceivedAsync(IReadOnlyList<int> lineIds)
        => await db.GoodsReceipts.AsNoTracking().Where(r => lineIds.Contains(r.PurchaseOrderLineId) && !r.Failed)
            .GroupBy(r => r.PurchaseOrderLineId).Select(g => new { g.Key, Total = g.Sum(r => r.Quantity) }).ToDictionaryAsync(g => g.Key, g => g.Total);
}
