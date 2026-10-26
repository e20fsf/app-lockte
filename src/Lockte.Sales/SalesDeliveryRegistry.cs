using Lockte.Sales.Data;
using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Sales;

/// <summary>What the store types on a line of the backorder before "บันทึก" (SCR-08): kept at once, processed with the others.</summary>
public sealed record PendingDeliveryInput(DateOnly? DeliveryDate, string? InvoiceNo, int? InvoiceLine, decimal? Quantity, string? Lot, decimal? Boxes);

/// <summary>What the "บันทึก" button did (BR-003): lines delivered in full, in part, and lines with a quantity of 0 (nothing delivered).</summary>
public sealed record DeliverySaveResult(int Complete, int Partial, int Zero)
{
    public int Total => Complete + Partial + Zero;
}

/// <summary>One delivery as the edit screen (SCR-09) and the reports read it.</summary>
public sealed record DeliveryDto(
    int Id,
    string OrderNo,
    DateOnly? OrderDate,
    int LineNo,
    int CustomerId,
    string CustomerCode,
    string CustomerName,
    int? SalesTerritoryId,
    int ProductId,
    string ProductName,
    string? Unit,
    decimal? UnitPrice,
    DateOnly? DueDate,
    DateOnly? DeliveryDate,
    string? InvoiceNo,
    int? InvoiceLine,
    string? Lot,
    decimal Quantity,
    decimal? Amount,
    decimal? Boxes,
    bool Complete,
    byte[] RowVersion);

/// <summary>A backorder line with what is typed on it for the delivery being recorded (SCR-08).</summary>
public sealed record DeliveryLineDto(
    int OrderId,
    string OrderNo,
    DateOnly OrderDate,
    string CustomerCode,
    string CustomerName,
    DateOnly? DueDate,
    int LineId,
    int LineNo,
    int ProductId,
    string? ProductCode,
    string ProductName,
    string? Unit,
    decimal? UnitPrice,
    decimal OutstandingQuantity,
    DateOnly? DeliveryDate,
    string? InvoiceNo,
    int? InvoiceLine,
    decimal? Quantity,
    decimal? Amount,
    string? Lot,
    decimal? Boxes,
    byte[] RowVersion);

/// <summary>An edit of a delivery (SCR-09 / BR-014): every field; <see cref="RowVersion"/> as the screen loaded it.</summary>
public sealed record DeliveryEdit(
    string OrderNo,
    DateOnly? OrderDate,
    int CustomerId,
    string? CustomerName,
    int? SalesTerritoryId,
    int ProductId,
    string? ProductName,
    string? Unit,
    decimal? UnitPrice,
    DateOnly? DueDate,
    DateOnly? DeliveryDate,
    string? InvoiceNo,
    int? InvoiceLine,
    string? Lot,
    decimal Quantity,
    decimal? Boxes,
    byte[] RowVersion);

/// <summary>How a delivery is found on SCR-09: by เลขที่บิล, เลขที่ ORDER or วันที่ส่ง.</summary>
public sealed record DeliverySearch(string? InvoiceNo = null, string? OrderNo = null, DateOnly? Date = null);

/// <summary>
/// บันทึกการส่งของ (SCR-08, BR-003, BR-004). The delivery typed on a line is kept as it is typed
/// (<see cref="SetPendingAsync"/>); "บันทึก" (<see cref="SaveAsync"/>) then processes <b>every</b> line that has a quantity, in
/// every order, in one transaction: delivered in full → the backorder of the line is closed; in part → what is left is
/// ค้าง − ส่ง; a quantity of 0 → nothing is recorded. As before nothing stops a delivery of more than the backorder (the
/// backorder goes negative, ⚠ BR-003 #3) or checks the date or the invoice number; the stock is not touched (#28).
/// </summary>
public sealed class SalesDeliveryRegistry(SalesDbContext db)
{
    public const int InvoiceNoLength = 11;
    public const int LotLength = 255;

    public async Task<ServiceResult> SetPendingAsync(int lineId, byte[] lineRowVersion, PendingDeliveryInput input)
    {
        var line = await db.SalesOrderLines.FirstOrDefaultAsync(l => l.Id == lineId);
        if (line is null) return ServiceResult.Fail("ไม่พบบรรทัด ORDER นี้ อาจถูกส่งครบหรือลบไปแล้ว");
        line.ExpectRowVersion(lineRowVersion);
        var invoiceNo = Clean(input.InvoiceNo);
        var lot = Clean(input.Lot)?.ToUpperInvariant(); // LOT is converted to capitals as typed (BR-003)
        var errors = new List<string>();
        if (invoiceNo?.Length > InvoiceNoLength) errors.Add($"เลขที่บิลยาวได้ไม่เกิน {InvoiceNoLength} ตัว");
        if (lot?.Length > LotLength) errors.Add($"Lot ยาวได้ไม่เกิน {LotLength} ตัว");
        if (input.Quantity < 0 || input.Boxes < 0) errors.Add("จำนวนส่งและจำนวนกล่องต้องไม่ติดลบ");
        if (errors.Count > 0) return new ServiceResult(false, errors);

        line.PendingDeliveryDate = input.DeliveryDate;
        line.PendingInvoiceNo = invoiceNo;
        line.PendingInvoiceLine = input.InvoiceLine;
        line.PendingQuantity = input.Quantity;
        line.PendingAmount = SalesOrderRules.Amount(input.Quantity, line.UnitPrice); // DL_NET follows DL_QTY (BR-004)
        line.PendingLot = lot;
        line.PendingBoxes = input.Boxes;
        await db.SaveChangesAsync();
        return ServiceResult.Ok(line.Id);
    }

    /// <summary>The "บันทึก" button.</summary>
    public async Task<DeliverySaveResult> SaveAsync()
    {
        var orders = await db.SalesOrders.Include(o => o.Lines).Include(o => o.Customer)
            .Where(o => o.Lines.Any(l => l.PendingQuantity != null)).OrderBy(o => o.OrderNo).ToListAsync();
        int complete = 0, partial = 0, zero = 0;
        await using var transaction = await db.Database.BeginTransactionAsync();
        foreach (var order in orders)
        {
            foreach (var line in order.Lines.Where(l => l.PendingQuantity != null).OrderBy(l => l.LineNo).ToList())
            {
                var quantity = line.PendingQuantity!.Value;
                if (quantity == 0)
                {
                    zero++; // BR-003 #4: recorded and deleted at once — no delivery, the backorder as it was
                }
                else
                {
                    var done = line.OutstandingQuantity - quantity == 0; // MARK a: ส่งครบ
                    db.Deliveries.Add(new Delivery
                    {
                        OrderNo = order.OrderNo, OrderDate = order.OrderDate, LineNo = line.LineNo, Customer = order.Customer,
                        CustomerName = order.CustomerName, SalesTerritoryId = order.SalesTerritoryId, ProductId = line.ProductId,
                        ProductName = line.ProductName, Unit = line.Unit, UnitPrice = line.UnitPrice, DueDate = order.DeliveryDate,
                        DeliveryDate = line.PendingDeliveryDate, InvoiceNo = line.PendingInvoiceNo, InvoiceLine = line.PendingInvoiceLine,
                        Lot = line.PendingLot, Quantity = quantity, Amount = line.PendingAmount, Boxes = line.PendingBoxes, Complete = done,
                    });
                    line.OutstandingQuantity -= quantity;
                    line.OutstandingAmount = done ? null : SalesOrderRules.Amount(line.OutstandingQuantity, line.UnitPrice); // BR-003 step 4: ค้างเหลือ × ราคา; a closed line has no value
                    if (done) complete++; else partial++;
                }
                line.PendingDeliveryDate = null;
                line.PendingInvoiceNo = null;
                line.PendingInvoiceLine = null;
                line.PendingQuantity = null;
                line.PendingAmount = null;
                line.PendingLot = null;
                line.PendingBoxes = null;
            }
            // BR-003 step 6: a line delivered in full leaves the backorder; an order left with no line goes (its history of
            // deliveries stays). A line the history still holds stays as its รับ values.
            var gone = order.Lines.Where(l => l.OutstandingQuantity == 0 && l.ReceivedQuantity is null).ToList();
            foreach (var line in gone) db.SalesOrderLines.Remove(line);
            if (order.Lines.Except(gone).Any()) continue;
            db.SalesOrders.Remove(order);
        }
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return new DeliverySaveResult(complete, partial, zero);
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}

/// <summary>
/// แก้ไข/ลบรายการส่งของ (SCR-09, BR-014): the rows of the history of deliveries are edited directly; the amount follows the
/// quantity and price. Deleting or changing a delivery does <b>not</b> give the backorder back to the order (⚠ Q-57 kept).
/// </summary>
public sealed class SalesDeliveryEditor(SalesDbContext db, IProductLookup? products = null)
{
    public async Task<ServiceResult> SaveAsync(int id, DeliveryEdit edit)
    {
        var delivery = await db.Deliveries.Include(d => d.Customer).FirstOrDefaultAsync(d => d.Id == id);
        if (delivery is null) return ServiceResult.Fail("ไม่พบรายการส่งนี้ อาจถูกลบไปแล้ว");
        delivery.ExpectRowVersion(edit.RowVersion);

        var errors = new List<string>();
        var orderNo = Clean(edit.OrderNo);
        var invoiceNo = Clean(edit.InvoiceNo);
        var lot = Clean(edit.Lot)?.ToUpperInvariant();
        if (orderNo is null) errors.Add("กรุณากรอกเลขที่ ORDER");
        else if (orderNo.Length > SalesOrderRegistry.OrderNoLength) errors.Add($"เลขที่ ORDER ยาวได้ไม่เกิน {SalesOrderRegistry.OrderNoLength} ตัว");
        if (invoiceNo?.Length > SalesDeliveryRegistry.InvoiceNoLength) errors.Add($"เลขที่บิลยาวได้ไม่เกิน {SalesDeliveryRegistry.InvoiceNoLength} ตัว");
        if (lot?.Length > SalesDeliveryRegistry.LotLength) errors.Add($"Lot ยาวได้ไม่เกิน {SalesDeliveryRegistry.LotLength} ตัว");
        if (edit.Quantity < 0 || edit.UnitPrice < 0 || edit.Boxes < 0) errors.Add("จำนวน ราคา และจำนวนกล่องต้องไม่ติดลบ");
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == edit.CustomerId);
        if (customer is null) errors.Add("กรุณาเลือกลูกค้าที่มีในทะเบียนลูกค้า");
        if (edit.SalesTerritoryId is { } territoryId && !await db.SalesTerritories.AnyAsync(t => t.Id == territoryId)) errors.Add("ไม่พบเขตการขายที่เลือก");
        ProductRef? product = null;
        if (edit.ProductId != delivery.ProductId)
        {
            product = products is null ? null : await products.FindAsync(edit.ProductId);
            if (product is null) errors.Add("กรุณาเลือกสินค้าที่มีในทะเบียนสินค้า");
        }
        if (errors.Count > 0) return new ServiceResult(false, errors);

        var recompute = edit.Quantity != delivery.Quantity || edit.UnitPrice != delivery.UnitPrice;
        delivery.OrderNo = orderNo!;
        delivery.OrderDate = edit.OrderDate;
        delivery.Customer = customer!;
        delivery.CustomerName = Clean(edit.CustomerName) ?? customer!.Name;
        delivery.SalesTerritoryId = edit.SalesTerritoryId;
        if (product is not null)
        {
            delivery.ProductId = product.Id;
            delivery.ProductName = Clean(edit.ProductName) ?? product.Name;
            delivery.Unit = Clean(edit.Unit) ?? product.Unit;
        }
        else
        {
            delivery.ProductName = Clean(edit.ProductName) ?? delivery.ProductName;
            delivery.Unit = Clean(edit.Unit) ?? delivery.Unit;
        }
        delivery.UnitPrice = edit.UnitPrice;
        delivery.DueDate = edit.DueDate;
        delivery.DeliveryDate = edit.DeliveryDate;
        delivery.InvoiceNo = invoiceNo;
        delivery.InvoiceLine = edit.InvoiceLine;
        delivery.Lot = lot;
        delivery.Quantity = edit.Quantity;
        delivery.Boxes = edit.Boxes;
        if (recompute) delivery.Amount = SalesOrderRules.Amount(edit.Quantity, edit.UnitPrice); // DL_NET = DL_QTY × U_PRICE (BR-004)
        await db.SaveChangesAsync();
        return ServiceResult.Ok(delivery.Id);
    }

    /// <summary>ลบรายการส่ง (the delete button with a confirmation, instead of clearing a field, BR-021). Already gone succeeds.</summary>
    public async Task<ServiceResult> DeleteAsync(int id, byte[] rowVersion)
    {
        var delivery = await db.Deliveries.Include(d => d.Customer).FirstOrDefaultAsync(d => d.Id == id);
        if (delivery is null) return ServiceResult.Ok();
        delivery.ExpectRowVersion(rowVersion);
        db.Deliveries.Remove(delivery);
        await db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}

/// <summary>The public, read-only face of the deliveries (ADR 0002): what SCR-08 / SCR-09 and the delivery reports read.</summary>
public sealed class SalesDeliveryDirectory(SalesDbContext db, IProductLookup? products = null)
{
    /// <summary>The order numbers that still have a backorder, for the choice on SCR-08.</summary>
    public async Task<IReadOnlyList<string>> BackorderNumbersAsync(string? text = null, int max = 100)
    {
        var term = text?.Trim();
        return await db.SalesOrders.AsNoTracking().Where(o => o.Lines.Any(l => l.OutstandingQuantity != 0))
            .Where(o => string.IsNullOrEmpty(term) || o.OrderNo.Contains(term)).OrderBy(o => o.OrderNo).Take(max).Select(o => o.OrderNo).ToListAsync();
    }

    /// <summary>The backorder lines of an order with what is typed on them, by เลขที่บิล then ลำดับ (SCR-08).</summary>
    public async Task<IReadOnlyList<DeliveryLineDto>> GetLinesAsync(string orderNo)
    {
        var number = orderNo.Trim();
        var order = await db.SalesOrders.AsNoTracking().Include(o => o.Customer).Include(o => o.Lines).FirstOrDefaultAsync(o => o.OrderNo == number);
        if (order is null) return [];
        var codes = new Dictionary<int, string?>();
        foreach (var id in order.Lines.Select(l => l.ProductId).Distinct()) codes[id] = products is null ? null : (await products.FindAsync(id))?.Code;
        return order.Lines.Where(l => l.OutstandingQuantity != 0).OrderBy(l => l.PendingInvoiceNo ?? "").ThenBy(l => l.LineNo)
            .Select(l => new DeliveryLineDto(order.Id, order.OrderNo, order.OrderDate, order.Customer.Code, order.CustomerName, order.DeliveryDate, l.Id, l.LineNo,
                l.ProductId, codes[l.ProductId], l.ProductName, l.Unit, l.UnitPrice, l.OutstandingQuantity, l.PendingDeliveryDate, l.PendingInvoiceNo,
                l.PendingInvoiceLine, l.PendingQuantity, l.PendingAmount, l.PendingLot, l.PendingBoxes, l.RowVersion)).ToList();
    }

    /// <summary>How many lines have a quantity typed — what "บันทึก" will process, in every order.</summary>
    public Task<int> CountPendingAsync() => db.SalesOrderLines.CountAsync(l => l.PendingQuantity != null);

    /// <summary>SCR-09: the deliveries of an invoice, an order or a day, by วันที่ส่ง, เลขที่บิล, ลำดับ (all given must match).</summary>
    public async Task<IReadOnlyList<DeliveryDto>> SearchAsync(DeliverySearch search, int max = 500)
    {
        var query = db.Deliveries.AsNoTracking().Include(d => d.Customer).AsQueryable();
        if (Clean(search.InvoiceNo) is { } invoice) query = query.Where(d => d.InvoiceNo == invoice);
        if (Clean(search.OrderNo) is { } order) query = query.Where(d => d.OrderNo == order);
        if (search.Date is { } date) query = query.Where(d => d.DeliveryDate == date);
        return (await query.OrderBy(d => d.DeliveryDate).ThenBy(d => d.InvoiceNo).ThenBy(d => d.OrderNo).ThenBy(d => d.LineNo).Take(max).ToListAsync())
            .Select(ToDto).ToList();
    }

    public async Task<DeliveryDto?> GetAsync(int id)
        => await db.Deliveries.AsNoTracking().Include(d => d.Customer).FirstOrDefaultAsync(d => d.Id == id) is { } d ? ToDto(d) : null;

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static DeliveryDto ToDto(Delivery d) => new(d.Id, d.OrderNo, d.OrderDate, d.LineNo, d.CustomerId, d.Customer.Code, d.CustomerName, d.SalesTerritoryId,
        d.ProductId, d.ProductName, d.Unit, d.UnitPrice, d.DueDate, d.DeliveryDate, d.InvoiceNo, d.InvoiceLine, d.Lot, d.Quantity, d.Amount, d.Boxes, d.Complete, d.RowVersion);
}
