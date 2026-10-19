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
        services.AddMemoryCache();
        services.AddSingleton<OvertimeSheetPrints>();
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

        // RPT-16 ใบขออนุมัติทำงานล่วงเวลา (รายงาน OT): A5 ตั้ง, no document number. The sheet SCR-14 just saved (OvertimeSheetPrints).
        reports.MapGet("/hr/overtime-sheets/{key:guid}", async (Guid key, HttpContext http, OvertimeSheetPrints sheets, ReportPrinter printer,
            CancellationToken cancellationToken) =>
        {
            if (sheets.Find(key, http.User.Identity?.Name) is not { } sheet) return Results.NotFound("ไม่พบใบขออนุมัติทำ OT นี้ กรุณาพิมพ์ใหม่จากหน้าจอ");
            var pdf = await printer.PrintAsync<OvertimeSheetView>(OvertimeSheetView.Title, OvertimeSheetView.Paper,
                new() { [nameof(OvertimeSheetView.Sheet)] = sheet }, AccountClaims.DisplayNameOf(http.User), cancellationToken: cancellationToken);
            return Pdf(http, pdf, "ใบขออนุมัติทำ OT " + sheet.Date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
        });

        return app;
    }

    /// <summary>Shown in the browser's PDF viewer (inline) under a readable file name.</summary>
    private static IResult Pdf(HttpContext http, byte[] pdf, string name)
    {
        http.Response.Headers.ContentDisposition = $"inline; filename*=UTF-8''{Uri.EscapeDataString(name + ".pdf")}";
        http.Response.Headers.CacheControl = "no-store";
        return Results.File(pdf, "application/pdf");
    }
}
