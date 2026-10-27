using System.Globalization;
using Lockte.Prod;

namespace Lockte.Web.Reports.Order.Qa;

/// <summary>
/// ใบตรวจคุณภาพ QA (RPT-20, #130): <c>GET /reports/order/qa/sheet?spec=…&amp;dept=D&amp;date=2026-05-04</c> prints the sheet of a spec, one page for each line of the department on the day that is
/// marked for inspection (MARK1 = Y); without <c>dept</c> and <c>date</c> (the new-mold sheets) it prints one blank sheet. The marked lines are not filtered by model (⚠ Q-58 kept).
/// </summary>
public static class QaSheetReports
{
    public static RouteGroupBuilder MapQaSheetReports(this RouteGroupBuilder reports)
    {
        reports.MapGet("/order/qa/sheet", async (int spec, string? dept, string? date, HttpContext http, QaSpecDirectory specs, QaSelectionDirectory selection, ReportPrinter printer,
            CancellationToken ct) =>
        {
            if (await specs.GetSheetAsync(spec) is not { } sheet) return Results.NotFound("ไม่พบสเปคนี้");
            if (QaFormTemplates.For(sheet.Name, sheet.Points.Count, sheet.Defects.Count) is not { } template) return Results.NotFound("ยังไม่มีโครงพิมพ์ใบตรวจของกลุ่มนี้");

            var lines = new List<QaSheetLine>();
            if (!string.IsNullOrWhiteSpace(dept) && template.Strip is not { Blank: true })
            {
                if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)) return Results.BadRequest("กรุณาระบุวันที่ (yyyy-MM-dd)");
                foreach (var row in await selection.GetLinesAsync(dept, day, onlySelected: true))
                    lines.Add(new QaSheetLine(row.ProductCode, row.ProductName, row.Lot, row.Date, row.Good, await specs.SampleSizeAsync(row.Good)));
            }

            var content = new QaSheetContent(sheet.DocumentName, QaSheetForm.DocumentNumber(sheet.DocumentCode, sheet.DocumentSequence, sheet.Revision), sheet.Models,
                sheet.Points.Select(QaSheetForm.PointText).ToList(), sheet.Defects, sheet.Image, sheet.ImageContentType);
            var pdf = await printer.PrintFormAsync(QaSheetForm.Title, QaSheetForm.Layout(template, content, lines), ct);
            return ReportsRegistration.Pdf(http, pdf, sheet.Name);
        });
        return reports;
    }
}
