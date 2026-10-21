using Lockte.Hr;
using Lockte.Web.Reports.Hr;
using static Lockte.Web.Reports.HrAttendanceReports;

namespace Lockte.Web.Reports;

/// <summary>
/// RPT-36 ค่าพักร้อน (SCR-51 "พิมพ์", #63): ระดับ B on A4 นอน as the legacy. Dates are ISO as <see cref="HrAttendanceReports"/>;
/// a missing or reversed range is a 400.
/// </summary>
public static class HrVacationPayReports
{
    public static RouteGroupBuilder MapHrVacationPayReports(this RouteGroupBuilder reports)
    {
        reports.MapGet("/hr/vacation-pay", async (DateOnly? from, DateOnly? to, HttpContext http, VacationPayDirectory vacation,
            ReportPrinter printer, CancellationToken cancellationToken) =>
        {
            if (Range(from, to) is { } bad) return bad;
            var report = await vacation.GetReportAsync(from!.Value, to!.Value);
            var title = VacationPayReportView.TitleOf(report);
            return await PrintAsync<VacationPayReportView>(http, printer, title, Paper.A4Landscape, report, title, cancellationToken);
        });

        return reports;
    }
}
