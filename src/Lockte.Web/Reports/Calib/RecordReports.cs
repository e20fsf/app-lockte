using Lockte.Calib;
using Lockte.Web.Components.Pages.Calib;

namespace Lockte.Web.Reports.Calib;

/// <summary>RPT-03 (#205 #206): the blank record sheets of one kind.</summary>
public static class RecordReports
{
    /// <summary>The kinds whose sheet is typed from the register: a page for every instrument. เพรสเซอร์เกจ and ตัวควบคุมอุณหภูมิ have one fixed sheet for "ทุกเครื่อง" (#206).</summary>
    public static bool IsPerInstrument(InstrumentKind kind) => kind is InstrumentKind.External or InstrumentKind.Caliper or InstrumentKind.Scale;

    public static bool IsFixed(InstrumentKind kind) => kind is InstrumentKind.PressureGauge or InstrumentKind.TempControl;

    /// <summary>
    /// The one sheet of เพรสเซอร์เกจ / ตัวควบคุมอุณหภูมิ: the name, the range and the limit are what the settings hold (the legacy printed <c>50 - 150 Kg/cm²</c> and <c>190 - 210 °C</c> for every
    /// instrument whatever its own range — Q-13), หมายเลข "ทุกเครื่อง", and the calibrator and company boxes empty.
    /// </summary>
    public static RecordHead FixedHead(InstrumentKind kind, CalibSettingsDto settings) => kind == InstrumentKind.PressureGauge
        ? new RecordHead(settings.PressureGaugeName, AllMachines, settings.PressureGaugeRange, settings.PressureGaugeLimit, null, null, null, false)
        : new RecordHead(settings.TempControlName, AllMachines, settings.TempControlRange, settings.TempControlLimit, null, null, null, false);

    public const string AllMachines = "ทุกเครื่อง";

    public static RouteGroupBuilder MapCalibRecordReports(this RouteGroupBuilder reports)
    {
        reports.MapGet("/calib/record/{kind}", async (string kind, int? instrument, InstrumentDirectory instruments, CalibSettingsDirectory settings, DocumentNumberService numbers,
            ReportPrinter printer, HttpContext http, CancellationToken cancellationToken) =>
        {
            if (InstrumentKindInfo.Find(kind) is not { } info || !(IsPerInstrument(info.Kind) || IsFixed(info.Kind))) return Results.NotFound("ไม่พบชนิดเครื่องมือวัด");
            IReadOnlyList<RecordHead> heads;
            if (IsFixed(info.Kind)) heads = [FixedHead(info.Kind, await settings.GetAsync())];
            else
            {
                var rows = await instruments.ListAsync(info.Kind);
                if (instrument is { } id) rows = rows.Where(i => i.Id == id).ToList();
                if (rows.Count == 0) return Results.NotFound("ไม่พบเครื่องมือวัด");
                heads = rows.Select(RecordForm.HeadOf).ToList();
            }
            var pages = RecordForm.Layout(heads, await numbers.PrintedTextAsync(DocumentKeys.CalibRecord));
            return ReportsRegistration.Pdf(http, await printer.PrintFormAsync(RecordForm.Title, pages, cancellationToken), "บันทึกผลการสอบเทียบ " + info.Key);
        });
        return reports;
    }
}
