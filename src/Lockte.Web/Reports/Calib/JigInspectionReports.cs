using Lockte.Calib;

namespace Lockte.Web.Reports.Calib;

/// <summary>RPT-05 / RPT-06 (#207 #208): the sheet of one model of JIG, from the sheets QA maintains (#199) and the picture of the sheet (#200).</summary>
public static class JigInspectionReports
{
    public static RouteGroupBuilder MapCalibJigInspectionReports(this RouteGroupBuilder reports)
    {
        reports.MapGet("/calib/jig/{id:int}", async (int id, JigSheetDirectory sheets, JigImageDirectory images, DocumentNumberService numbers, ReportPrinter printer, HttpContext http,
            CancellationToken cancellationToken) =>
        {
            if (await sheets.GetAsync(id) is not { } sheet) return Results.NotFound("ไม่พบแบบตรวจ JIG");
            var page = JigInspectionForm.Page(sheet, await images.GetAsync(sheet.Id), await numbers.PrintedTextAsync(sheet.DocumentKey));
            return ReportsRegistration.Pdf(http, await printer.PrintFormAsync(JigInspectionForm.TitleOf(sheet), [page], cancellationToken), JigInspectionForm.FileName(sheet));
        });
        return reports;
    }
}
