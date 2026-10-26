using System.Text.RegularExpressions;
using Lockte.Sales.Data;
using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Sales;

/// <summary>A สินค้า of module ผลิต as ORDER names it: code, the name and unit copied onto the line.</summary>
public sealed record ProductRef(int Id, string Code, string Name, string? Unit);

/// <summary>
/// How module ขายและส่งของ reads the product register of module ผลิต (ADR 0002). Defined here and implemented by
/// <c>prod</c>, which already depends on this module (customers) — the other way round would be a project cycle.
/// </summary>
public interface IProductLookup
{
    Task<ProductRef?> FindAsync(int productId);
}

/// <summary>
/// What a button does with the order (BR-002): <see cref="Print"/> = "พิมพ์" (macro ปรับปรุงORDER2: to the history, and the
/// order's หมายเหตุ written back to the customer's), <see cref="Save"/> = "กลับเมนูเดิม" of ODD1 (only a ค้างส่ง order — AC-22),
/// <see cref="SaveToHistory"/> = "กลับเมนูเดิม" of ODD11 (to the history, customer untouched).
/// </summary>
public enum OrderEntryMode
{
    Print,
    Save,
    SaveToHistory,
}

public sealed record SalesOrderLineInput(int ProductId, decimal? Quantity, decimal? UnitPrice, string? ProductName = null, string? Unit = null);

/// <summary>
/// A new ORDER as the form holds it. The head is stored as given: the screen fills it from the customer
/// (<see cref="SalesOrderDirectory.DefaultsForCustomerAsync"/>) and from the date (<see cref="SalesOrderRules.DefaultDeliveryDate"/>).
/// A line without a quantity is dropped on save (BR-002 step 1). <see cref="OriginalNo"/> is the เลขเดิม the migration keeps.
/// </summary>
public sealed record SalesOrderInput(
    string OrderNo,
    DateOnly OrderDate,
    int CustomerId,
    string? CustomerName,
    int? SalesTerritoryId,
    DateOnly? DeliveryDate,
    string? PaymentTerms,
    string? Remark,
    IReadOnlyList<SalesOrderLineInput> Lines,
    string? OriginalNo = null);

/// <summary>The outcome of saving an order; <see cref="NextNumber"/> is proposed again when the number was already used.</summary>
public sealed record OrderSaveResult(bool Succeeded, IReadOnlyList<string> Errors, int? Id = null, string? NextNumber = null);

/// <summary>Rules of the ORDER form that do not need the database.</summary>
public static class SalesOrderRules
{
    /// <summary>BR-002: กำหนดส่ง = วันที่ + 7 วัน.</summary>
    public static DateOnly DefaultDeliveryDate(DateOnly orderDate) => orderDate.AddDays(7);

    /// <summary>BR-004: มูลค่า = จำนวน × ราคา, no rounding; blank when either is blank.</summary>
    public static decimal? Amount(decimal? quantity, decimal? unitPrice) => quantity * unitPrice;

    /// <summary>YY of <c>SS-YY-NNNN</c>: the last two digits of the Buddhist year, whatever the PC's regional setting (BR-001).</summary>
    public static string YearPart(DateOnly date) => ((date.Year + 543) % 100).ToString("00", System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>
/// บันทึก ORDER ใหม่ (SCR-05, BR-002): the head and lines in one transaction — nothing is half saved. The number must be
/// new (#28): a number someone else took meanwhile is refused and the next one proposed. Saved through the module's
/// conventions (rowversion, ประวัติการแก้ไข).
/// </summary>
public sealed class SalesOrderRegistry(SalesDbContext db, TimeProvider clock, IProductLookup? products = null)
{
    public const int OrderNoLength = 50;
    public const int TextLength = 255;

    public async Task<OrderSaveResult> CreateAsync(SalesOrderInput input, OrderEntryMode mode)
    {
        var orderNo = Clean(input.OrderNo);
        var errors = new List<string>();
        if (orderNo is null) errors.Add("กรุณากรอกเลขที่ ORDER");
        else if (orderNo.Length > OrderNoLength) errors.Add($"เลขที่ ORDER ยาวได้ไม่เกิน {OrderNoLength} ตัว");
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == input.CustomerId);
        if (customer is null) errors.Add("กรุณาเลือกลูกค้าที่มีในทะเบียนลูกค้า");
        var customerName = Clean(input.CustomerName) ?? customer?.Name;
        if (customerName?.Length > CustomerRegistry.NameLength) errors.Add($"ชื่อลูกค้ายาวได้ไม่เกิน {CustomerRegistry.NameLength} ตัว");
        if (input.SalesTerritoryId is { } territoryId && !await db.SalesTerritories.AnyAsync(t => t.Id == territoryId))
            errors.Add("ไม่พบเขตการขายที่เลือก");
        var paymentTerms = Clean(input.PaymentTerms);
        var remark = Clean(input.Remark);
        if (paymentTerms?.Length > TextLength) errors.Add($"เงื่อนไขการชำระเงินยาวได้ไม่เกิน {TextLength} ตัว");
        if (remark?.Length > TextLength) errors.Add($"หมายเหตุยาวได้ไม่เกิน {TextLength} ตัว");
        if (Clean(input.OriginalNo) is { Length: > OrderNoLength }) errors.Add($"เลขเดิมยาวได้ไม่เกิน {OrderNoLength} ตัว");

        // BR-002 step 1: a line without a quantity is dropped, quietly.
        var kept = input.Lines.Where(l => l.Quantity is not null).ToList();
        if (kept.Count == 0) errors.Add("ORDER ต้องมีอย่างน้อยหนึ่งบรรทัดที่มีจำนวน");
        if (kept.Any(l => l.Quantity < 0 || l.UnitPrice < 0)) errors.Add("จำนวนและราคาต้องไม่ติดลบ");
        var lines = new List<(SalesOrderLineInput Input, ProductRef Product)>();
        foreach (var line in kept)
        {
            var product = products is null ? null : await products.FindAsync(line.ProductId);
            if (product is null) { errors.Add("กรุณาเลือกสินค้าที่มีในทะเบียนสินค้าทุกบรรทัด"); break; }
            lines.Add((line, product));
        }
        if (errors.Count > 0) return new OrderSaveResult(false, errors);

        if (await db.SalesOrders.AnyAsync(o => o.OrderNo == orderNo)) return await DuplicateAsync(orderNo!, input);

        var now = clock.GetUtcNow();
        var order = new SalesOrder
        {
            OrderNo = orderNo!,
            OriginalNo = Clean(input.OriginalNo),
            OrderDate = input.OrderDate,
            Customer = customer!,
            CustomerName = customerName ?? "",
            SalesTerritoryId = input.SalesTerritoryId,
            DeliveryDate = input.DeliveryDate,
            PaymentTerms = paymentTerms,
            Remark = remark,
            RecordedAt = mode == OrderEntryMode.Save ? null : now,
        };
        var no = 0;
        foreach (var (line, product) in lines)
        {
            var amount = SalesOrderRules.Amount(line.Quantity, line.UnitPrice);
            order.Lines.Add(new SalesOrderLine
            {
                SalesOrder = order,
                LineNo = ++no,
                ProductId = product.Id,
                ProductName = Clean(line.ProductName) ?? product.Name,
                Unit = Clean(line.Unit) ?? product.Unit,
                UnitPrice = line.UnitPrice,
                // BR-002 step 2: ค้างส่ง = จำนวน on every line; step 4: the history keeps the same as รับ.
                ReceivedQuantity = mode == OrderEntryMode.Save ? null : line.Quantity,
                ReceivedAmount = mode == OrderEntryMode.Save ? null : amount,
                OutstandingQuantity = line.Quantity!.Value,
                OutstandingAmount = amount,
            });
        }
        db.SalesOrders.Add(order);

        // The order and the customer's หมายเหตุ are one unit: nothing is half saved (BR-002 is not one transaction in Access).
        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // Someone saved the same number between the check and the insert.
            db.ChangeTracker.Clear();
            return await DuplicateAsync(orderNo!, input);
        }
        // BR-002 step 6: the order's หมายเหตุ goes back to the customer's — only with "พิมพ์".
        if (mode == OrderEntryMode.Print) await WriteCustomerRemarkAsync(customer!, remark);
        await transaction.CommitAsync();
        return new OrderSaveResult(true, [], order.Id);
    }

    /// <summary>
    /// The last order printed for a customer wins: if another print changed the customer meanwhile, the row is read again and the
    /// หมายเหตุ written over it, so two people printing for one customer never refuse each other.
    /// </summary>
    private async Task WriteCustomerRemarkAsync(Customer customer, string? remark)
    {
        for (var attempt = 1; ; attempt++)
        {
            customer.Remark = remark;
            try
            {
                await db.SaveChangesAsync();
                return;
            }
            catch (ConcurrencyConflictException) when (attempt < 5)
            {
                await db.Entry(customer).ReloadAsync();
            }
        }
    }

    private async Task<OrderSaveResult> DuplicateAsync(string orderNo, SalesOrderInput input)
    {
        string? next = null;
        if (input.SalesTerritoryId is { } id && await db.SalesTerritories.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id) is { } territory)
            next = await SalesOrderNumbering.ProposeAsync(db, territory.Code, input.OrderDate);
        return new OrderSaveResult(false,
            [$"เลขที่ ORDER '{orderNo}' มีอยู่แล้ว" + (next is null ? "" : $" — เลขถัดไปที่ว่างคือ {next}")], null, next);
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}

/// <summary>เลขที่ ORDER <c>SS-YY-NNNN</c> (BR-001).</summary>
internal static class SalesOrderNumbering
{
    public static async Task<string?> ProposeAsync(SalesDbContext db, string territoryCode, DateOnly today)
    {
        var prefix = territoryCode.Trim();
        var used = await db.SalesOrders.AsNoTracking().Where(o => o.OrderNo.StartsWith(prefix + "-")).Select(o => o.OrderNo).ToListAsync();
        if (used.Count == 0) return null; // a territory with no number yet: the user types the first (BR-001 #3)
        var year = SalesOrderRules.YearPart(today);
        var pattern = new Regex($"^{Regex.Escape(prefix)}-{year}-(\\d{{4}})$", RegexOptions.IgnoreCase);
        var last = used.Select(n => pattern.Match(n)).Where(m => m.Success).Select(m => int.Parse(m.Groups[1].Value)).DefaultIfEmpty(0).Max();
        var next = last + 1;
        return next > 9999 ? null : $"{prefix}-{year}-{next:0000}";
    }
}
