using Lockte.Calib;
using Lockte.Web.Components.Pages.Calib;

namespace Lockte.Web.Reports.Calib;

/// <summary>RPT-03 (#205 #206): the blank record sheets of one kind — a page for every instrument of the register (or the one asked for), in order of หมายเลขเครื่อง.</summary>
public static class RecordReports
{
    /// <summary>The kinds whose sheet is typed from the register; เพรสเซอร์เกจ and ตัวควบคุมอุณหภูมิ have one fixed sheet (#206).</summary>
    public static bool IsPerInstrument(InstrumentKind kind) => kind is InstrumentKind.External or InstrumentKind.Caliper or InstrumentKind.Scale;

    public static RouteGroupBuilder MapCalibRecordReports(this RouteGroupBuilder reports)
    {
        reports.MapGet("/calib/record/{kind}", async (string kind, int? instrument, InstrumentDirectory instruments, DocumentNumberService numbers, ReportPrinter printer, HttpContext http,
            CancellationToken cancellationToken) =>
        {
            if (InstrumentKindInfo.Find(kind) is not { } info || !IsPerInstrument(info.Kind)) return Results.NotFound("ไม่พบชนิดเครื่องมือวัด");
            var rows = await instruments.ListAsync(info.Kind);
            if (instrument is { } id) rows = rows.Where(i => i.Id == id).ToList();
            if (rows.Count == 0) return Results.NotFound("ไม่พบเครื่องมือวัด");
            var pages = RecordForm.Layout(rows.Select(RecordForm.HeadOf).ToList(), await numbers.PrintedTextAsync(DocumentKeys.CalibRecord));
            return ReportsRegistration.Pdf(http, await printer.PrintFormAsync(RecordForm.Title, pages, cancellationToken), "บันทึกผลการสอบเทียบ " + info.Key);
        });
        return reports;
    }
}
