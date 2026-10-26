using Lockte.Sales;

namespace Lockte.Web.Reports.Order;

/// <summary>
/// RPT-01 ใบ SALE ORDER (#110): the PDF of an order's ค้างส่ง lines, and the blank form. Both ระดับ A, opened in a new tab as
/// Access opened its preview.
/// </summary>
public static class SaleOrderReports
{
    public static RouteGroupBuilder MapSaleOrderReports(this RouteGroupBuilder reports)
    {
        var group = reports.MapGroup("/order/sale-order");

        // พิมพ์ใบ sale order (เลขที่ ORDER): the order by its id
        group.MapGet("/{id:int}", async (int id, HttpContext http, SalesOrderDirectory orders, DocumentNumberService numbers, ReportPrinter printer, CancellationToken cancellationToken) =>
        {
            if (await orders.GetPrintAsync(id) is not { } order) return Results.NotFound("ไม่พบ ORDER ค้างส่งของเลขที่นี้ (ใบที่ส่งครบแล้วพิมพ์ซ้ำไม่ได้)");
            var pages = SaleOrderForm.Layout(order, await numbers.PrintedTextAsync(DocumentKeys.SaleOrder));
            return ReportsRegistration.Pdf(http, await printer.PrintFormAsync(SaleOrderForm.Title, pages, cancellationToken), "SALE ORDER " + order.OrderNo.Replace('/', '-'));
        });

        // พิมพ์ฟอร์มเปล่า (รายงาน ใบ sale order1)
        group.MapGet("/blank", async (HttpContext http, DocumentNumberService numbers, ReportPrinter printer, CancellationToken cancellationToken) =>
        {
            var page = SaleOrderForm.Blank(await numbers.PrintedTextAsync(DocumentKeys.SaleOrder));
            return ReportsRegistration.Pdf(http, await printer.PrintFormAsync(SaleOrderForm.Title, [page], cancellationToken), "SALE ORDER ฟอร์มเปล่า");
        });

        return reports;
    }
}
