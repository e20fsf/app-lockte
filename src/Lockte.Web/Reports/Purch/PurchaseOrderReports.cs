using Lockte.Org;
using Lockte.Purch;
using Microsoft.Extensions.Options;

namespace Lockte.Web.Reports.Purch;

/// <summary>
/// RPT-02 ใบสั่งซื้อ (#224), ระดับ A: the PDF of an order that goes to the supplier — all its lines or only the ones picked (พิมพ์ซ้ำ, BR-024, AC-36), with the totals of
/// the lines printed (BR-022, as the legacy computed them from the selected lines) and the amount in words (BR-064). Opened in a new tab as Access opened its preview.
/// </summary>
public static class PurchaseOrderReports
{
    /// <summary>The data of the print; <paramref name="lineIds"/> null = every line.</summary>
    public static async Task<PurchaseOrderPrint?> BuildAsync(PurchaseOrderDirectory orders, OrgDirectory org, PurchSettingsDirectory settings, int id, IReadOnlyCollection<int>? lineIds = null)
    {
        if (await orders.GetAsync(id) is not { } order) return null;
        var picked = order.Lines.Where(l => lineIds is null || lineIds.Contains(l.Id)).ToList();
        var totals = PurchaseOrderMath.Calculate(picked.Select(l => l.NetAmount).ToList(), order.ChargesVat, (await settings.GetAsync()).VatRate);
        // the head takes the first line's department and delivery date (the legacy used the first row)
        var departmentName = picked.Count > 0 ? (await org.GetDepartmentAsync(picked[0].DepartmentId))?.Name : null;
        var lines = picked.Select(l => new PurchaseOrderPrintLine(l.LineNo, l.RequestNo, l.MaterialName, l.Quantity, l.Unit, l.UnitPrice, l.NetAmount)).ToList();
        return new PurchaseOrderPrint(order.OrderNo, order.OrderDate, order.SupplierName, order.SupplierAddress1, order.SupplierAddress2, order.SupplierTel, order.SupplierFax,
            departmentName, picked.FirstOrDefault()?.DesiredDate, order.CreditDays, lines, totals.Subtotal, totals.Tax, totals.Total, totals.TotalInWords, order.Reason, order.Note);
    }

    public static RouteGroupBuilder MapPurchaseOrderReports(this RouteGroupBuilder reports)
    {
        // ?lines=12,15 → only those lines (พิมพ์ซ้ำ); none given → every line
        reports.MapGet("/purch/order/{id:int}", async (int id, string? lines, PurchaseOrderDirectory orders, OrgDirectory org, PurchSettingsDirectory settings,
            DocumentNumberService numbers, IOptionsSnapshot<PurchLetterheadOptions> letterhead, ReportPrinter printer, HttpContext http, CancellationToken cancellationToken) =>
        {
            IReadOnlyCollection<int>? ids = null;
            if (lines is not null)
            {
                var parsed = lines.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(t => int.TryParse(t, out var n) ? n : (int?)null).ToList();
                if (parsed.Count == 0 || parsed.Any(n => n is null)) return Results.BadRequest("ไม่มีรายการ");
                ids = parsed.Select(n => n!.Value).ToList();
            }
            if (await BuildAsync(orders, org, settings, id, ids) is not { } report) return Results.NotFound("ไม่พบใบสั่งซื้อนี้");
            if (report.Lines.Count == 0) return Results.BadRequest("ไม่มีรายการ");
            var pages = PurchaseOrderForm.Layout(report, await numbers.PrintedTextAsync(DocumentKeys.PurchaseOrder), letterhead.Value);
            return ReportsRegistration.Pdf(http, await printer.PrintFormAsync(PurchaseOrderForm.Title, pages, cancellationToken), "ใบสั่งซื้อ " + report.OrderNo);
        });

        // พิมพ์ฟอร์มเปล่า
        reports.MapGet("/purch/order/blank", async (DocumentNumberService numbers, IOptionsSnapshot<PurchLetterheadOptions> letterhead, ReportPrinter printer, HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var page = PurchaseOrderForm.Blank(await numbers.PrintedTextAsync(DocumentKeys.PurchaseOrder), letterhead.Value);
            return ReportsRegistration.Pdf(http, await printer.PrintFormAsync(PurchaseOrderForm.Title, [page], cancellationToken), "ใบสั่งซื้อ ฟอร์มเปล่า");
        });
        return reports;
    }
}
