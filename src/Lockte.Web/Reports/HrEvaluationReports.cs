using Lockte.Hr;
using Lockte.Web.Accounts;
using Lockte.Web.Reports.Hr;
using static Lockte.Web.Reports.HrAttendanceReports;

namespace Lockte.Web.Reports;

/// <summary>
/// The PDFs of ปรับค่าแรง and ประเมินผล (#67): RPT-37 (SCR-52, ระดับ B A4 ตั้ง, รายเดือน and รายวัน apart as the legacy's two
/// reports), RPT-43 รายงานการประเมินผลพนักงาน (SCR-53, ระดับ B A4 นอน) and ใบประเมินประจำปี (SCR-54, ระดับ A A5 ตั้ง, one
/// page per employee). Dates are ISO; a missing or reversed range is a 400.
/// </summary>
public static class HrEvaluationReports
{
    public static RouteGroupBuilder MapHrEvaluationReports(this RouteGroupBuilder reports)
    {
        // RPT-37: ?kind=daily (ปรับค่าแรง2) or monthly (ปรับค่าแรง1, the default), ?department= (none = ทุกแผนก), ?cap= TTL,
        // ?step= STEP1..STEP5 (five, else the legacy defaults).
        reports.MapGet("/hr/pay-adjustment", async (DateOnly? from, DateOnly? to, int? department, double? cap, double[]? step, string? kind,
            HttpContext http, PayAdjustmentDirectory adjustments, ReportPrinter printer, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            if (Range(from, to) is { } bad) return bad;
            var steps = step is { Length: 5 } ? step : PayAdjustmentInput.DefaultSteps;
            var report = await adjustments.GetReportAsync(new PayAdjustmentInput(from!.Value, to!.Value, department,
                cap ?? PayAdjustmentInput.DefaultLeaveCap, steps));
            var daily = kind == "daily";
            var title = PayAdjustmentReportView.TitleOf(daily, DateOnly.FromDateTime(clock.GetLocalNow().DateTime));
            var pdf = await printer.PrintAsync<PayAdjustmentReportView>(title, Paper.A4Portrait,
                new() { [nameof(PayAdjustmentReportView.Report)] = report, [nameof(PayAdjustmentReportView.Daily)] = daily },
                AccountClaims.DisplayNameOf(http.User), cancellationToken: cancellationToken);
            return ReportsRegistration.Pdf(http, pdf, title);
        });

        // RPT-43 รายงานการประเมินผลพนักงาน (SCR-53 "พิมพ์รายงาน").
        reports.MapGet("/hr/evaluations", async (HttpContext http, EvaluationDirectory evaluations, ReportPrinter printer,
            CancellationToken cancellationToken) =>
        {
            var pdf = await printer.PrintAsync<EvaluationListView>(EvaluationListView.Title, Paper.A4Landscape,
                new() { [nameof(EvaluationListView.Lines)] = await evaluations.GetEvaluatedAsync() },
                AccountClaims.DisplayNameOf(http.User), cancellationToken: cancellationToken);
            return ReportsRegistration.Pdf(http, pdf, EvaluationListView.Title);
        });

        // RPT-43 ใบประเมินประจำปี (SCR-54): ?kind=supervisor (ปุ่ม "หัวหน้าหรือรองฯ") or staff (ปุ่ม "พนักงาน", the default).
        reports.MapGet("/hr/yearly-evaluation", async (DateOnly? from, DateOnly? to, int? department, string? kind, EvaluationDirectory evaluations,
            DocumentNumberService numbers, ReportPrinter printer, HttpContext http, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            if (Range(from, to) is { } bad) return bad;
            var which = kind == "supervisor" ? YearlyEvaluationKind.Supervisor : YearlyEvaluationKind.Staff;
            var report = await evaluations.GetYearlyAsync(which, from!.Value, to!.Value, department);
            if (report.Sheets.Count == 0) return Results.NotFound("ไม่มีพนักงานตามเงื่อนไขที่เลือก");
            var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
            var number = await numbers.PrintedTextAsync(EvaluationForms.DocumentKeyOf(which));
            var pages = report.Sheets.Select(s => EvaluationForms.Sheet(report, s, today, number)).ToList();
            return ReportsRegistration.Pdf(http, await printer.PrintFormAsync(EvaluationForms.Title, pages, cancellationToken),
                $"{EvaluationForms.Title} {(which == YearlyEvaluationKind.Staff ? "พนักงาน" : "หัวหน้า")} {report.DepartmentName ?? "ทั้งหมด"}");
        });

        return reports;
    }
}
