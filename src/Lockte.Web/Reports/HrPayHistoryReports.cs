using Lockte.Hr;
using Lockte.Web.Accounts;
using Lockte.Web.Reports.Hr;
using Microsoft.Extensions.Options;
using static Lockte.Web.Reports.HrAttendanceReports;

namespace Lockte.Web.Reports;

/// <summary>
/// The PDFs over many งวดค่าแรง (#56): RPT-08 ค่าแรงสิ้นเดือน and its two summaries (SCR-43), RPT-09 and RPT-10 (เมนู 9), all
/// ระดับ B. Dates are ISO as <see cref="HrAttendanceReports"/>; a missing or reversed range is a 400. Paper as the legacy:
/// A4 นอน for RPT-08 รายคน and RPT-09, A4 ตั้ง for the summaries and RPT-10.
/// </summary>
public static class HrPayHistoryReports
{
    public static RouteGroupBuilder MapHrPayHistoryReports(this RouteGroupBuilder reports)
    {
        // RPT-08 บัญชีคำนวณค่าจ้าง รายคน (SCR-43 "พิมพ์"): ?department= one แผนก (none = ทั้งหมด)
        reports.MapGet("/hr/pay/month-end", async (DateOnly? from, DateOnly? to, int? department, HttpContext http,
            PayHistoryReportDirectory pay, ReportPrinter printer, CancellationToken cancellationToken) =>
        {
            if (Range(from, to) is { } bad) return bad;
            var report = await pay.GetMonthEndReportAsync(from!.Value, to!.Value, department);
            return await PrintAsync<MonthEndPayReportView>(http, printer, MonthEndPayReportView.Title, Paper.A4Landscape, report,
                "ค่าแรงสิ้นเดือน " + (report.DepartmentName ?? "ทั้งหมด") + " " + Iso(report.To), cancellationToken);
        });

        // สรุปบัญชีคำนวณค่าจ้าง (SCR-43 "พิมพ์สรุป")
        reports.MapGet("/hr/pay/month-end/summary", async (DateOnly? from, DateOnly? to, int? department, HttpContext http,
            PayHistoryReportDirectory pay, ReportPrinter printer, CancellationToken cancellationToken) =>
        {
            if (Range(from, to) is { } bad) return bad;
            var report = await pay.GetMonthEndSummaryAsync(from!.Value, to!.Value, department);
            var title = MonthEndSummaryView.TitleOf(report);
            var pdf = await printer.PrintAsync<MonthEndSummaryView>(title, Paper.A4Portrait,
                new() { [nameof(MonthEndSummaryView.Report)] = report },
                AccountClaims.DisplayNameOf(http.User), cancellationToken: cancellationToken);
            return ReportsRegistration.Pdf(http, pdf, title);
        });

        // ยอดเงินส่งกองทุนสงเคราะห์ลูกจ้าง (SCR-43, UAT-23): Excel ตามแบบกองทุน, ช่วงวันที่/แผนกเดียวกับ "พิมพ์"
        reports.MapGet("/hr/pay/month-end/welfare-fund", async (DateOnly? from, DateOnly? to, int? department, PayHistoryReportDirectory pay,
            IOptionsSnapshot<EmployerOptions> employer, TimeProvider clock) =>
        {
            if (Range(from, to) is { } bad) return bad;
            var report = await pay.GetWelfareFundReportAsync(from!.Value, to!.Value, department);
            // UAT-43: no rate set for a งวด (or two rates) is said, not guessed; the page checks first and shows the same text.
            if (report.RateProblem is { } problem) return Results.Text(problem, statusCode: StatusCodes.Status409Conflict);
            var table = WelfareFundTable.Build(report, employer.Value.Name, DateOnly.FromDateTime(clock.GetLocalNow().DateTime));
            return Results.File(Order.XlsxWriter.Write(table), Order.XlsxWriter.ContentType,
                "ยอดเงินส่งกองทุนสงเคราะห์ลูกจ้าง " + Iso(report.To) + ".xlsx");
        });

        // RPT-09 สรุปค่าแรงแต่ละเดือน (เมนู 9): วันที่จ่าย within the dates
        reports.MapGet("/hr/pay/monthly", async (DateOnly? from, DateOnly? to, HttpContext http, PayHistoryReportDirectory pay,
            ReportPrinter printer, CancellationToken cancellationToken) =>
        {
            if (Range(from, to) is { } bad) return bad;
            var report = await pay.GetMonthlyReportAsync(from!.Value, to!.Value);
            return await PrintAsync<MonthlyPayReportView>(http, printer, MonthlyPayReportView.TitleOf(report), Paper.A4Landscape, report,
                MonthlyPayReportView.TitleOf(report), cancellationToken);
        });

        // RPT-10 รวมค่าแรง (เมนู 9): the งวด within the dates
        reports.MapGet("/hr/pay/totals", async (DateOnly? from, DateOnly? to, HttpContext http, PayHistoryReportDirectory pay,
            ReportPrinter printer, CancellationToken cancellationToken) =>
        {
            if (Range(from, to) is { } bad) return bad;
            var report = await pay.GetPayTotalsReportAsync(from!.Value, to!.Value);
            return await PrintAsync<PayTotalsReportView>(http, printer, PayTotalsReportView.Title, Paper.A4Portrait, report,
                "รวมค่าแรง " + Iso(report.From) + " " + Iso(report.To), cancellationToken);
        });

        return reports;
    }
}
