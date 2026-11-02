using Lockte.Calib;
using Lockte.Web.Accounts;

namespace Lockte.Web.Reports.Calib;

/// <summary>What RPT-01 prints: the month of each round, the parties and the note — all from the settings of the module.</summary>
public sealed record PlanReport(int ExternalMonth, int InternalMonth, string Responsible, string Reviewer, string Note)
{
    /// <summary>มค.…ธค. as the legacy headed the twelve columns.</summary>
    public static IReadOnlyList<string> MonthNames { get; } = ["ม.ค.", "ก.พ.", "มี.ค.", "เม.ย.", "พ.ค.", "มิ.ย.", "ก.ค.", "ส.ค.", "ก.ย.", "ต.ค.", "พ.ย.", "ธ.ค."];

    public static PlanReport Of(CalibSettingsDto settings)
        => new(settings.PlanExternalMonth, settings.PlanInternalMonth, settings.PlanResponsible, settings.PlanReviewer, settings.PlanNote);
}

/// <summary>แผนการสอบเทียบ (RPT-01, #202), ระดับ B on A4 นอน, เลขเอกสารควบคุม <see cref="DocumentKeys.CalibPlan"/>.</summary>
public static class PlanReports
{
    public const string Title = "แผนการสอบเทียบ";

    public static RouteGroupBuilder MapCalibPlanReports(this RouteGroupBuilder reports)
    {
        reports.MapGet("/calib/plan", async (CalibSettingsDirectory settings, HttpContext http, ReportPrinter printer, CancellationToken cancellationToken) =>
        {
            var report = PlanReport.Of(await settings.GetAsync());
            var pdf = await printer.PrintAsync<PlanView>(Title, Paper.A4Landscape, new() { [nameof(PlanView.Report)] = report }, AccountClaims.DisplayNameOf(http.User),
                DocumentKeys.CalibPlan, cancellationToken);
            return ReportsRegistration.Pdf(http, pdf, Title);
        });
        return reports;
    }
}
