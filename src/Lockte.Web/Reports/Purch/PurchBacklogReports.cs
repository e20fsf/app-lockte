using Lockte.Purch;
using Lockte.Web.Accounts;

namespace Lockte.Web.Reports.Purch;

/// <summary>
/// RPT-03 ใบขออนุมัติที่ยังไม่พิมพ์ใบสั่งซื้อ (ยอดค้างสั่ง) and RPT-04 รายงานวัตถุดิบที่ยังไม่ได้รับ (ยอดค้างรับ), ระดับ B on A4 ตั้ง (#256): computed live from the requests, the orders
/// and the receipts as they are now — no work table; what is closed does not show. Both open straight from the menu in a new tab.
/// </summary>
public static class PurchBacklogReports
{
    public const string RequestsTitle = "ใบขออนุมัติสั่งซื้อที่ยังไม่ได้พิมพ์ใบสั่งซื้อ";
    public const string OrdersTitle = "รายงานวัตถุดิบที่ยังไม่ได้รับ";

    public static RouteGroupBuilder MapPurchBacklogReports(this RouteGroupBuilder reports)
    {
        reports.MapGet("/purch/backlog/requests", async (PurchaseOrderDirectory orders, HttpContext http, ReportPrinter printer, CancellationToken cancellationToken) =>
        {
            var lines = await orders.GetAllOutstandingLinesAsync();
            var pdf = await printer.PrintAsync<PurchRequestBacklogView>(RequestsTitle, Paper.A4Portrait, new() { ["Lines"] = lines }, AccountClaims.DisplayNameOf(http.User), cancellationToken: cancellationToken);
            return ReportsRegistration.Pdf(http, pdf, "ใบขอซื้อค้างสั่ง");
        });

        reports.MapGet("/purch/backlog/orders", async (GoodsReceiptDirectory receipts, HttpContext http, ReportPrinter printer, CancellationToken cancellationToken) =>
        {
            var lines = await receipts.GetAllOutstandingAsync();
            var pdf = await printer.PrintAsync<PurchOrderBacklogView>(OrdersTitle, Paper.A4Portrait, new() { ["Lines"] = lines }, AccountClaims.DisplayNameOf(http.User), cancellationToken: cancellationToken);
            return ReportsRegistration.Pdf(http, pdf, "วัตถุดิบค้างรับ");
        });
        return reports;
    }
}
