using Lockte.Calib;
using Lockte.Web.Accounts;
using Lockte.Web.Components.Pages.Calib;

namespace Lockte.Web.Reports.Calib;

/// <summary>What RPT-02 prints: the instruments of one kind in order of หมายเลขเครื่อง, whether the calibrator column is a company (the external register), and its note.</summary>
public sealed record RegisterReport(InstrumentKind Kind, IReadOnlyList<InstrumentDto> Rows, string? Note)
{
    public bool External => Kind == InstrumentKind.External;
}

/// <summary>
/// บัญชีรายชื่อเครื่องมือวัด (RPT-02, #203 สอบเทียบภายใน 5 ชนิด และ #204 ภายนอก), ระดับ B on A4 นอน; control numbers PC-QA009-1/2552 (ภายใน) and PC-QA009-1/2547 (ภายนอก).
/// Rows are in order of หมายเลขเครื่อง; the legacy printed a row for an instrument whose หมายเลข had been cleared (BR-004 ตัวอย่าง 2) — not any more.
/// </summary>
public static class RegisterReports
{
    public const string InternalTitle = "บัญชีรายชื่อเครื่องมือวัด (สอบเทียบภายใน)";
    public const string ExternalTitle = "บัญชีรายชื่อเครื่องมือวัด(สอบเทียบภายนอก)";

    public static string TitleOf(InstrumentKind kind) => kind == InstrumentKind.External ? ExternalTitle : InternalTitle;

    public static string DocumentKeyOf(InstrumentKind kind) => kind == InstrumentKind.External ? DocumentKeys.CalibRegisterExternal : DocumentKeys.CalibRegisterInternal;

    public static RouteGroupBuilder MapCalibRegisterReports(this RouteGroupBuilder reports)
    {
        reports.MapGet("/calib/register/{kind}", async (string kind, InstrumentDirectory instruments, CalibSettingsDirectory settings, HttpContext http, ReportPrinter printer,
            CancellationToken cancellationToken) =>
        {
            if (InstrumentKindInfo.Find(kind) is not { } info) return Results.NotFound("ไม่พบชนิดเครื่องมือวัด");
            var rows = await instruments.ListAsync(info.Kind);
            var note = info.Kind == InstrumentKind.External ? (await settings.GetAsync()).ExternalRegisterNote : null;
            var report = new RegisterReport(info.Kind, rows, note);
            var pdf = await printer.PrintAsync<RegisterView>(TitleOf(info.Kind), Paper.A4Landscape, new() { [nameof(RegisterView.Report)] = report }, AccountClaims.DisplayNameOf(http.User),
                DocumentKeyOf(info.Kind), cancellationToken);
            return ReportsRegistration.Pdf(http, pdf, "บัญชีรายชื่อเครื่องมือวัด " + info.Key);
        });
        return reports;
    }

}
