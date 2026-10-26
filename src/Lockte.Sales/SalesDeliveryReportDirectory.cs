using Microsoft.EntityFrameworkCore;

namespace Lockte.Sales;

/// <summary>One delivery as the delivery reports list it (RPT-07, RPT-08).</summary>
public sealed record DeliveryReportRow(
    DateOnly DeliveryDate,
    string? InvoiceNo,
    int? InvoiceLine,
    int CustomerId,
    string CustomerCode,
    string CustomerName,
    int ProductId,
    string? ProductCode,
    string ProductName,
    string? Unit,
    decimal Quantity,
    decimal? UnitPrice,
    decimal? Amount,
    string OrderNo,
    DateOnly? DueDate,
    string? Lot,
    decimal? Boxes,
    int? SalesTerritoryId = null,
    DateOnly? OrderDate = null);

/// <summary>
/// The rows behind the delivery reports (#136, #114, #115): the deliveries whose date is in a range. Grouping, totals and layout belong
/// to the report; the module returns the rows.
/// </summary>
public sealed class SalesDeliveryReportDirectory(Data.SalesDbContext db, IProductLookup? products = null)
{
    /// <summary>The deliveries whose date is in the range, by customer code, date, invoice number and line (a delivery with no date is on no dated report).</summary>
    public async Task<IReadOnlyList<DeliveryReportRow>> GetDeliveriesAsync(DateOnly from, DateOnly to)
    {
        var rows = await db.Deliveries.AsNoTracking().Where(d => d.DeliveryDate >= from && d.DeliveryDate <= to)
            .Select(d => new
            {
                Date = d.DeliveryDate!.Value, d.InvoiceNo, d.InvoiceLine, d.CustomerId, CustomerCode = d.Customer.Code, d.CustomerName, d.ProductId, d.ProductName,
                d.Unit, d.Quantity, d.UnitPrice, d.Amount, d.OrderNo, d.DueDate, d.Lot, d.Boxes, d.SalesTerritoryId, d.OrderDate,
            })
            .ToListAsync();
        var codes = new Dictionary<int, string>();
        if (products is not null)
            foreach (var id in rows.Select(r => r.ProductId).Distinct())
                if (await products.FindAsync(id) is { } product) codes[id] = product.Code;
        return rows.OrderBy(r => r.CustomerCode, StringComparer.Ordinal).ThenBy(r => r.Date).ThenBy(r => r.InvoiceNo, StringComparer.Ordinal).ThenBy(r => r.InvoiceLine)
            .Select(r => new DeliveryReportRow(r.Date, r.InvoiceNo, r.InvoiceLine, r.CustomerId, r.CustomerCode, r.CustomerName, r.ProductId, codes.GetValueOrDefault(r.ProductId),
                r.ProductName, r.Unit, r.Quantity, r.UnitPrice, r.Amount, r.OrderNo, r.DueDate, r.Lot, r.Boxes, r.SalesTerritoryId, r.OrderDate))
            .ToList();
    }
}
