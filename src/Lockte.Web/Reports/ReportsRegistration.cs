using Lockte.Hr;
using Lockte.SharedKernel.Persistence;
using Lockte.Web.Accounts;
using Lockte.Web.Reports.Hr;

namespace Lockte.Web.Reports;

public static class ReportsRegistration
{
    /// <summary>เลขเอกสารควบคุม (schema <c>report</c>).</summary>
    public static IServiceCollection AddReportData(this IServiceCollection services, string connectionString)
    {
        services.AddModuleDbContext<ReportsDbContext>(connectionString, ReportsDbContext.Schema);
        services.AddScoped<DocumentNumberService>();
        return services;
    }

    /// <summary>The PDF pipeline: one Chromium with a warm page pool, opened in the background at startup.</summary>
    public static IServiceCollection AddReportPrinting(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ReportOptions>(configuration.GetSection(ReportOptions.Section));
        services.AddSingleton<ReportPdfRenderer>();
        services.AddHostedService<ReportWarmUp>();
        services.AddScoped<ReportPrinter>();
        return services;
    }

    /// <summary>
    /// Reports open as PDF in a new browser tab (the user prints from there, as Access opened a preview). One GET per
    /// report; every report is behind the default policy (staff).
    /// </summary>
    public static IEndpointRouteBuilder MapReports(this IEndpointRouteBuilder app)
    {
        var reports = app.MapGroup("/reports").RequireAuthorization();

        // RPT-01 เวลาทำงานรายคน (รายงาน คำนวณเวลาทำงาน13): A4 ตั้ง, no document number.
        reports.MapGet("/hr/pay-periods/{id:int}/time", async (int id, HttpContext http, PayPeriodDirectory periods, ReportPrinter printer,
            CancellationToken cancellationToken) =>
        {
            if (await periods.GetTimeReportAsync(id) is not { } report) return Results.NotFound("ยังไม่ได้คำนวณเวลาทำงานของงวดนี้");
            var pdf = await printer.PrintAsync<TimeReportView>(TimeReportView.Title, Paper.A4Portrait,
                new() { [nameof(TimeReportView.Report)] = report }, AccountClaims.DisplayNameOf(http.User), cancellationToken: cancellationToken);
            return Pdf(http, pdf, "เวลาทำงาน " + report.Period.From.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
        });

        // RPT-02 ค่าแรงรายคน (รายงาน ค่าแรง1, SCR-41): A4 ตั้ง, one department or all (no ?department).
        reports.MapGet("/hr/pay-periods/{id:int}/pay", async (int id, int? department, HttpContext http, PayPeriodDirectory periods,
            ReportPrinter printer, CancellationToken cancellationToken) =>
        {
            if (await periods.GetPayReportAsync(id, department) is not { } report) return Results.NotFound("ยังไม่ได้คำนวณค่าแรงของงวดนี้");
            var pdf = await printer.PrintAsync<PayReportView>(PayReportView.Title, Paper.A4Portrait,
                new() { [nameof(PayReportView.Report)] = report }, AccountClaims.DisplayNameOf(http.User), cancellationToken: cancellationToken);
            return Pdf(http, pdf, "ค่าแรง " + Iso(report.Period.From) + (report.DepartmentName is { } name ? " " + name : ""));
        });

        // RPT-03 สรุปค่าแรงรายแผนก (รายงาน สรุปค่าแรง): A4 ตั้ง. Printing it is step พิมพ์สรุปค่าแรง of เมนู 6.
        reports.MapGet("/hr/pay-periods/{id:int}/pay-summary", async (int id, HttpContext http, PayPeriodDirectory periods,
            PayPeriodRegistry registry, ReportPrinter printer, CancellationToken cancellationToken) =>
        {
            if (await periods.GetPaySummaryAsync(id) is not { } report) return Results.NotFound("ยังไม่ได้คำนวณค่าแรงของงวดนี้");
            var pdf = await printer.PrintAsync<PaySummaryView>(PaySummaryView.Title, Paper.A4Portrait,
                new() { [nameof(PaySummaryView.Report)] = report }, AccountClaims.DisplayNameOf(http.User), cancellationToken: cancellationToken);
            await registry.CompleteStepAsync(id, PayPeriodStep.SummaryPrinted);
            return Pdf(http, pdf, "สรุปค่าแรง " + Iso(report.Period.From));
        });

        return app;
    }

    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Shown in the browser's PDF viewer (inline) under a readable file name.</summary>
    private static IResult Pdf(HttpContext http, byte[] pdf, string name)
    {
        http.Response.Headers.ContentDisposition = $"inline; filename*=UTF-8''{Uri.EscapeDataString(name + ".pdf")}";
        http.Response.Headers.CacheControl = "no-store";
        return Results.File(pdf, "application/pdf");
    }
}
