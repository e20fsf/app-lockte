using Lockte.Prod;
using Lockte.SharedKernel;

namespace Lockte.Web.Reports.Order;

/// <summary>RPT-13 รายงานการตัดสต็อกผลผลิตวันที่ …: the balances carried over, with cost and value, only those that are not 0.</summary>
public sealed record StockCutReport(DateOnly CutDate, IReadOnlyList<StockCutLine> Lines)
{
    /// <summary>มูลค่ารวม: a negative balance takes its value off; a product without a cost adds nothing (its value is blank).</summary>
    public decimal TotalValue => Lines.Sum(l => l.Value ?? 0);
}

/// <summary>RPT-13 (#123), ระดับ B on A4 ตั้ง.</summary>
public static class StockCutReports
{
    public const string TitlePrefix = "รายงานการตัดสต็อกผลผลิตวันที่";

    public static string TitleOf(DateOnly cutDate) => $"{TitlePrefix} {ThaiDate.Format(cutDate)}";

    public static async Task<StockCutReport> BuildAsync(StockClosingDirectory closing, DateOnly cutDate) => new(cutDate, await closing.GetCutReportAsync(cutDate));

    public static RouteGroupBuilder MapStockCutReports(this RouteGroupBuilder reports)
    {
        reports.MapGet("/order/stock/cut", async (DateOnly? cut, HttpContext http, StockClosingDirectory closing, ReportPrinter printer, CancellationToken ct) =>
        {
            if (cut is null) return Results.BadRequest("กรุณาเลือกวันตัดสต็อก");
            var report = await BuildAsync(closing, cut.Value);
            return await HrAttendanceReports.PrintAsync<StockCutReportView>(http, printer, TitleOf(cut.Value), Paper.A4Portrait, report, TitleOf(cut.Value), ct);
        });
        return reports;
    }
}
