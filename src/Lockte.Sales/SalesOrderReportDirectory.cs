using Lockte.Sales.Data;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Sales;

/// <summary>What narrows the lines of a report on ORDER (#113): the order date, the customer, the territory. All optional.</summary>
public sealed record OrderReportFilter(DateOnly? From = null, DateOnly? To = null, int? CustomerId = null, int? SalesTerritoryId = null);

/// <summary>
/// One line of an ORDER as a report lists it: the head (date, number, customer as it was written on the order, territory) and the
/// line. <see cref="Quantity"/> and <see cref="Amount"/> are the values of the set the report is about — ค้างส่ง or รับ.
/// </summary>
public sealed record OrderLineReportRow(
    DateOnly OrderDate,
    string OrderNo,
    int CustomerId,
    string CustomerCode,
    string CustomerName,
    int? SalesTerritoryId,
    string? SalesTerritoryCode,
    int LineNo,
    int ProductId,
    string? ProductCode,
    string ProductName,
    string? Unit,
    decimal Quantity,
    decimal? UnitPrice,
    decimal? Amount,
    DateOnly? DeliveryDue);

/// <summary>A product a customer has received, from the delivery history (สินค้าที่ลูกค้าสั่งซื้อ).</summary>
public sealed record DeliveredProductRow(int ProductId, string? ProductCode, string ProductName, string? Unit, int CustomerId, string CustomerCode, string CustomerName);

/// <summary>
/// The rows behind the ORDER reports (#113): RPT-02/03/04 read the <b>ค้างส่ง</b> set, RPT-06 the <b>รับ</b> set of the orders that
/// are in the history — an order never printed is not counted, one whose backorder is closed still is (BR-012, #28). The module
/// returns rows; grouping, totals and layout belong to the report.
/// </summary>
public sealed class SalesOrderReportDirectory(SalesDbContext db, IProductLookup? products = null)
{
    /// <summary>The ค้างส่ง lines (≠ 0), by order date, customer code, order number, line.</summary>
    public async Task<IReadOnlyList<OrderLineReportRow>> GetBackorderAsync(OrderReportFilter? filter = null)
    {
        var rows = await Lines(filter).Where(l => l.OutstandingQuantity != 0)
            .Select(l => new Raw(l.SalesOrder.OrderDate, l.SalesOrder.OrderNo, l.SalesOrder.CustomerId, l.SalesOrder.Customer.Code, l.SalesOrder.CustomerName,
                l.SalesOrder.SalesTerritoryId, l.SalesOrder.SalesTerritory!.Code, l.LineNo, l.ProductId, l.ProductName, l.Unit, l.OutstandingQuantity,
                l.UnitPrice, l.OutstandingAmount, l.SalesOrder.DeliveryDate))
            .ToListAsync();
        return await WithProductCodesAsync(rows);
    }

    /// <summary>The รับ lines of the orders in the history, by order date, customer code, order number, line.</summary>
    public async Task<IReadOnlyList<OrderLineReportRow>> GetReceivedAsync(OrderReportFilter? filter = null)
    {
        var rows = await Lines(filter).Where(l => l.SalesOrder.RecordedAt != null && l.ReceivedQuantity != null)
            .Select(l => new Raw(l.SalesOrder.OrderDate, l.SalesOrder.OrderNo, l.SalesOrder.CustomerId, l.SalesOrder.Customer.Code, l.SalesOrder.CustomerName,
                l.SalesOrder.SalesTerritoryId, l.SalesOrder.SalesTerritory!.Code, l.LineNo, l.ProductId, l.ProductName, l.Unit, l.ReceivedQuantity!.Value,
                l.UnitPrice, l.ReceivedAmount, l.SalesOrder.DeliveryDate))
            .ToListAsync();
        return await WithProductCodesAsync(rows);
    }

    /// <summary>Each product with each customer that has received it, from การส่งของ; narrowed to one product or one customer.</summary>
    public async Task<IReadOnlyList<DeliveredProductRow>> GetDeliveredProductsAsync(int? productId = null, int? customerId = null)
    {
        var rows = await db.Deliveries.AsNoTracking()
            .Where(d => (productId == null || d.ProductId == productId) && (customerId == null || d.CustomerId == customerId))
            .Select(d => new { d.ProductId, d.ProductName, d.Unit, d.CustomerId, CustomerCode = d.Customer.Code, d.CustomerName, d.DeliveryDate })
            .ToListAsync();
        var codes = await ProductCodesAsync(rows.Select(r => r.ProductId));
        return rows.GroupBy(r => (r.ProductId, r.CustomerId)).Select(g =>
        {
            var last = g.OrderByDescending(r => r.DeliveryDate).First();
            return new DeliveredProductRow(last.ProductId, codes.GetValueOrDefault(last.ProductId), last.ProductName, last.Unit, last.CustomerId, last.CustomerCode, last.CustomerName);
        }).OrderBy(r => r.ProductCode, StringComparer.Ordinal).ThenBy(r => r.CustomerCode, StringComparer.Ordinal).ToList();
    }

    private IQueryable<SalesOrderLine> Lines(OrderReportFilter? filter)
    {
        var lines = db.SalesOrderLines.AsNoTracking();
        if (filter?.From is { } from) lines = lines.Where(l => l.SalesOrder.OrderDate >= from);
        if (filter?.To is { } to) lines = lines.Where(l => l.SalesOrder.OrderDate <= to);
        if (filter?.CustomerId is { } customer) lines = lines.Where(l => l.SalesOrder.CustomerId == customer);
        if (filter?.SalesTerritoryId is { } territory) lines = lines.Where(l => l.SalesOrder.SalesTerritoryId == territory);
        return lines;
    }

    private sealed record Raw(DateOnly OrderDate, string OrderNo, int CustomerId, string CustomerCode, string CustomerName, int? TerritoryId, string? TerritoryCode,
        int LineNo, int ProductId, string ProductName, string? Unit, decimal Quantity, decimal? UnitPrice, decimal? Amount, DateOnly? DeliveryDue);

    private async Task<IReadOnlyList<OrderLineReportRow>> WithProductCodesAsync(List<Raw> rows)
    {
        var codes = await ProductCodesAsync(rows.Select(r => r.ProductId));
        return rows.OrderBy(r => r.OrderDate).ThenBy(r => r.CustomerCode, StringComparer.Ordinal).ThenBy(r => r.OrderNo, StringComparer.Ordinal).ThenBy(r => r.LineNo)
            .Select(r => new OrderLineReportRow(r.OrderDate, r.OrderNo, r.CustomerId, r.CustomerCode, r.CustomerName, r.TerritoryId, r.TerritoryCode, r.LineNo, r.ProductId,
                codes.GetValueOrDefault(r.ProductId), r.ProductName, r.Unit, r.Quantity, r.UnitPrice, r.Amount, r.DeliveryDue))
            .ToList();
    }

    private async Task<Dictionary<int, string>> ProductCodesAsync(IEnumerable<int> ids)
    {
        var codes = new Dictionary<int, string>();
        if (products is null) return codes;
        foreach (var id in ids.Distinct())
            if (await products.FindAsync(id) is { } product) codes[id] = product.Code;
        return codes;
    }
}
