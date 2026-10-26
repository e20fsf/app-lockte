using Lockte.Sales.Data;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Sales;

public sealed record SalesOrderLineDto(
    int Id,
    int LineNo,
    int ProductId,
    string? ProductCode,
    string ProductName,
    string? Unit,
    decimal? UnitPrice,
    decimal? ReceivedQuantity,
    decimal? ReceivedAmount,
    decimal OutstandingQuantity,
    decimal? OutstandingAmount,
    byte[] RowVersion);

public sealed record SalesOrderDto(
    int Id,
    string OrderNo,
    string? OriginalNo,
    DateOnly OrderDate,
    int CustomerId,
    string CustomerCode,
    string CustomerName,
    int? SalesTerritoryId,
    string? SalesTerritoryCode,
    DateOnly? DeliveryDate,
    string? PaymentTerms,
    string? Remark,
    DateTimeOffset? RecordedAt,
    IReadOnlyList<SalesOrderLineDto> Lines,
    byte[] RowVersion)
{
    /// <summary>The order is in ประวัติการรับ ORDER (the "พิมพ์" button was used, AC-22).</summary>
    public bool InHistory => RecordedAt is not null;

    /// <summary>ยอดรวม = Σ มูลค่าบรรทัดที่รับ (AC-03).</summary>
    public decimal TotalReceivedAmount => Lines.Sum(l => l.ReceivedAmount ?? 0);

    public decimal TotalOutstandingAmount => Lines.Sum(l => l.OutstandingAmount ?? 0);
}

/// <summary>What choosing a customer fills into the head of an ORDER (BR-002): the copied name, its เขตการขาย, เงื่อนไขการชำระเงิน and หมายเหตุ.</summary>
public sealed record OrderDefaults(string CustomerName, int? SalesTerritoryId, string? PaymentTerms, string? Remark);

/// <summary>
/// The public, read-only face of ORDER (ADR 0002) for the screens and reports of the sales program: the head with its lines,
/// the next number, and what the head starts with. Writes go through <see cref="SalesOrderRegistry"/>.
/// </summary>
public sealed class SalesOrderDirectory(SalesDbContext db, IProductLookup? products = null)
{
    /// <summary>
    /// BR-001: the next เลขที่ ORDER of a เขตการขาย — the largest <c>SS-YY-NNNN</c> of the year of <paramref name="today"/> plus
    /// one; the first of a new year is <c>0001</c>; null when the territory has no number yet (the user types the first).
    /// The number is not reserved: a number used meanwhile is refused on save (#28).
    /// </summary>
    public Task<string?> ProposeNumberAsync(string territoryCode, DateOnly today) => SalesOrderNumbering.ProposeAsync(db, territoryCode, today);

    public async Task<SalesOrderDto?> GetAsync(int id)
        => await Query().FirstOrDefaultAsync(o => o.Id == id) is { } order ? await ToDtoAsync(order) : null;

    public async Task<SalesOrderDto?> FindByNumberAsync(string orderNo)
    {
        var number = orderNo.Trim();
        return await Query().FirstOrDefaultAsync(o => o.OrderNo == number) is { } order ? await ToDtoAsync(order) : null;
    }

    /// <summary>
    /// The orders SCR-06 offers to edit: the ones with a ค้างส่ง line (ODD2/ODD4/ODD6) or in the history (ODD8), found by
    /// part of the number, the date or the customer; by number. <see cref="SalesOrderListItem.Amount"/> is the total of the side.
    /// </summary>
    public async Task<IReadOnlyList<SalesOrderListItem>> SearchAsync(SalesOrderSearch search, int max = 200)
    {
        var number = search.OrderNo?.Trim();
        var query = db.SalesOrders.AsNoTracking().Where(o => search.Side == OrderSide.Outstanding
            ? o.Lines.Any(l => l.OutstandingQuantity > 0) : o.Lines.Any(l => l.ReceivedQuantity != null));
        if (!string.IsNullOrEmpty(number)) query = query.Where(o => o.OrderNo.Contains(number));
        if (search.Date is { } date) query = query.Where(o => o.OrderDate == date);
        if (search.CustomerId is { } customer) query = query.Where(o => o.CustomerId == customer);
        return await query.OrderBy(o => o.OrderNo).Take(max).Select(o => new SalesOrderListItem(o.Id, o.OrderNo, o.OrderDate, o.Customer.Code, o.CustomerName,
            search.Side == OrderSide.Outstanding ? o.Lines.Count(l => l.OutstandingQuantity > 0) : o.Lines.Count(l => l.ReceivedQuantity != null),
            search.Side == OrderSide.Outstanding ? o.Lines.Sum(l => l.OutstandingAmount ?? 0) : o.Lines.Sum(l => l.ReceivedAmount ?? 0))).ToListAsync();
    }

    public async Task<bool> NumberExistsAsync(string orderNo) => await db.SalesOrders.AnyAsync(o => o.OrderNo == orderNo.Trim());

    /// <summary>The head's starting values for a customer (BR-002); null when there is no such customer.</summary>
    public async Task<OrderDefaults?> DefaultsForCustomerAsync(int customerId)
        => await db.Customers.AsNoTracking().Where(c => c.Id == customerId)
            .Select(c => new { c.Name, c.SalesTerritoryId, c.CreditDays, c.Remark }).FirstOrDefaultAsync() is { } c
            ? new OrderDefaults(c.Name, c.SalesTerritoryId, c.CreditDays is null ? null : CreditTerm.Format(c.CreditDays), c.Remark)
            : null;

    private IQueryable<SalesOrder> Query()
        => db.SalesOrders.AsNoTracking().AsSplitQuery().Include(o => o.Customer).Include(o => o.SalesTerritory).Include(o => o.Lines);

    private async Task<SalesOrderDto> ToDtoAsync(SalesOrder o)
    {
        var codes = new Dictionary<int, string?>();
        foreach (var id in o.Lines.Select(l => l.ProductId).Distinct())
            codes[id] = products is null ? null : (await products.FindAsync(id))?.Code;
        return new SalesOrderDto(o.Id, o.OrderNo, o.OriginalNo, o.OrderDate, o.CustomerId, o.Customer.Code, o.CustomerName, o.SalesTerritoryId,
            o.SalesTerritory?.Code, o.DeliveryDate, o.PaymentTerms, o.Remark, o.RecordedAt,
            o.Lines.OrderBy(l => l.LineNo).Select(l => new SalesOrderLineDto(l.Id, l.LineNo, l.ProductId, codes[l.ProductId], l.ProductName, l.Unit,
                l.UnitPrice, l.ReceivedQuantity, l.ReceivedAmount, l.OutstandingQuantity, l.OutstandingAmount, l.RowVersion)).ToList(),
            o.RowVersion);
    }
}
