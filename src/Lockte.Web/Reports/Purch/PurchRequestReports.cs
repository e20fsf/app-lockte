using Lockte.Org;
using Lockte.Purch;
using Lockte.Web.Accounts;

namespace Lockte.Web.Reports.Purch;

public sealed record PurchRequestPrintLine(int LineNo, string Code, string Name, decimal Quantity, string? Unit);

/// <summary>A request as RPT-01 prints it: the head with the แผนก and ผู้จัดซื้อ by name, and the items chosen to print.</summary>
public sealed record PurchRequestPrint(
    string RequestNo,
    DateOnly RequestDate,
    string DepartmentName,
    string PurchasingLocation,
    DateOnly? DesiredDate,
    string? Reason,
    string? Note,
    IReadOnlyList<PurchRequestPrintLine> Lines);

/// <summary>
/// RPT-01 ใบขออนุมัติสั่งซื้อ (#220), ระดับ B: the PDF of a request, all its items or only the ones picked (พิมพ์ซ้ำ, BR-012, AC-36).
/// Opened in a new tab as Access opened its preview.
/// </summary>
public static class PurchRequestReports
{
    public const string Title = "ใบขออนุมัติสั่งซื้อ";

    /// <summary>The data of the print; <paramref name="lineIds"/> null = every item. Items with no quantity are never kept, so none is shown.</summary>
    public static async Task<PurchRequestPrint?> BuildAsync(PurchaseRequestDirectory requests, OrgDirectory org, RequestSetupDirectory setup, int id, IReadOnlyCollection<int>? lineIds = null)
    {
        if (await requests.GetAsync(id) is not { } request) return null;
        var department = (await org.GetDepartmentAsync(request.DepartmentId))?.Name ?? "";
        var location = (await setup.GetLocationsAsync()).FirstOrDefault(l => l.Code == request.PurchasingLocation)?.Name ?? request.PurchasingLocation ?? "";
        var lines = request.Lines.Where(l => (lineIds is null || lineIds.Contains(l.Id)) && l.Quantity > 0)
            .Select(l => new PurchRequestPrintLine(l.LineNo, l.MaterialCode, l.MaterialName, l.Quantity, l.Unit)).ToList();
        return new PurchRequestPrint(request.RequestNo, request.RequestDate, department, location, request.DesiredDate, request.Reason, request.Note, lines);
    }

    public static RouteGroupBuilder MapPurchRequestReports(this RouteGroupBuilder reports)
    {
        // ?lines=12,15 → only those items (พิมพ์ซ้ำ); none given → every item
        reports.MapGet("/purch/request/{id:int}", async (int id, string? lines, HttpContext http, PurchaseRequestDirectory requests, OrgDirectory org,
            RequestSetupDirectory setup, ReportPrinter printer, CancellationToken cancellationToken) =>
        {
            IReadOnlyCollection<int>? ids = null;
            if (lines is not null)
            {
                var parsed = lines.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(t => int.TryParse(t, out var n) ? n : (int?)null).ToList();
                if (parsed.Count == 0 || parsed.Any(n => n is null)) return Results.BadRequest("ไม่มีรายการ");
                ids = parsed.Select(n => n!.Value).ToList();
            }
            if (await BuildAsync(requests, org, setup, id, ids) is not { } report) return Results.NotFound("ไม่พบใบขอซื้อนี้");
            if (report.Lines.Count == 0) return Results.BadRequest("ไม่มีรายการ");
            var pdf = await printer.PrintAsync<PurchRequestView>(Title, Paper.A4Portrait, new() { [nameof(PurchRequestView.Report)] = report },
                AccountClaims.DisplayNameOf(http.User), DocumentKeys.PurchRequest, cancellationToken);
            return ReportsRegistration.Pdf(http, pdf, "ใบขอซื้อ " + report.RequestNo);
        });
        return reports;
    }
}
