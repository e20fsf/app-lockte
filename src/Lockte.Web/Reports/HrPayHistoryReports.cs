using Lockte.Hr;
using Lockte.Web.Accounts;
using Lockte.Web.Reports.Hr;
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

        // สรุปบัญชีคำนวณค่าจ้าง (SCR-43 "พิมพ์สรุป") and สรุปค่าแรงประจำเดือน (SCR-43 "พิมพ์สรุป (ต้นทุน)", ?cost=true)
        reports.MapGet("/hr/pay/month-end/summary", async (DateOnly? from, DateOnly? to, int? department, bool? cost, HttpContext http,
            PayHistoryReportDirectory pay, ReportPrinter printer, CancellationToken cancellationToken) =>
        {
            if (Range(from, to) is { } bad) return bad;
            var report = await pay.GetMonthEndSummaryAsync(from!.Value, to!.Value, department);
            var isCost = cost == true;
            var title = MonthEndSummaryView.TitleOf(report, isCost);
            var pdf = await printer.PrintAsync<MonthEndSummaryView>(title, Paper.A4Portrait,
                new() { [nameof(MonthEndSummaryView.Report)] = report, [nameof(MonthEndSummaryView.Cost)] = isCost },
                AccountClaims.DisplayNameOf(http.User), cancellationToken: cancellationToken);
            return ReportsRegistration.Pdf(http, pdf, title);
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
