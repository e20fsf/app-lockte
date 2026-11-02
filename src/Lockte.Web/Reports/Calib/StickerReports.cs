using Lockte.Calib;
using Lockte.Web.Components.Pages.Calib;

namespace Lockte.Web.Reports.Calib;

/// <summary>RPT-04 (#201): the PDF of the stickers of one kind whose หมายเลขเครื่อง matches the pattern (BR-010).</summary>
public static class StickerReports
{
    public static RouteGroupBuilder MapCalibStickerReports(this RouteGroupBuilder reports)
    {
        reports.MapGet("/calib/stickers/{kind}", async (string kind, string? id, DateOnly? cal, DateOnly? due, InstrumentDirectory instruments, CalibSettingsDirectory settings,
            ReportPrinter printer, HttpContext http, CancellationToken cancellationToken) =>
        {
            if (InstrumentKindInfo.Find(kind) is not { Internal: true } info) return Results.NotFound("ไม่พบชนิดเครื่องมือวัด");
            if (cal is null || due is null) return Results.BadRequest("กรุณากรอกวันที่สอบเทียบและวันที่ต้องสอบเทียบครั้งต่อไป");
            var found = await instruments.FindByPatternAsync(info.Kind, id);
            if (found.Count == 0) return Results.NotFound("ไม่พบเครื่องมือวัด");
            var size = await settings.GetAsync();
            var pages = StickerForm.Layout(found.Select(i => new StickerLabel(i.Code, cal.Value, due.Value)).ToList(), (double)size.StickerWidthCm * 10, (double)size.StickerHeightCm * 10);
            return ReportsRegistration.Pdf(http, await printer.PrintFormAsync(StickerForm.Title, pages, cancellationToken), "สติกเกอร์ " + info.Key);
        });
        return reports;
    }
}
