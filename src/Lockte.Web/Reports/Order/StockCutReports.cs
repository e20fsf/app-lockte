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

    public const string OpeningHistoryTitle = "สรุปสต็อกยกมารายงวด";

    /// <summary>RPT-14: a row per product (the code group narrows it), a column per cut date of the range holding the opening balance; blank where the product had none at that cut.</summary>
    public static async Task<SummaryTable> OpeningHistoryAsync(StockClosingDirectory closing, DateOnly from, DateOnly to, string? pattern)
    {
        var (dates, products) = await closing.GetOpeningHistoryAsync(from, to, pattern);
        var columns = new List<SummaryColumn> { new("รหัส"), new("ชื่อสินค้า"), new("หน่วย") };
        columns.AddRange(dates.Select(d => new SummaryColumn(ThaiDate.Format(d), SummaryFormat.Quantity)));
        var rows = products.Select(p =>
        {
            var cells = new List<object?> { p.ProductCode, p.ProductName, p.Unit };
            cells.AddRange(dates.Select(d => p.Balances.TryGetValue(d, out var value) ? (object?)value : null));
            return cells.ToArray();
        }).ToList();
        var filter = $"ตั้งแต่วันที่ {ThaiDate.Format(from)} ถึงวันที่ {ThaiDate.Format(to)}" + (string.IsNullOrWhiteSpace(pattern) ? "" : $"  รหัสสินค้า {pattern.Trim()}");
        return new(OpeningHistoryTitle, filter, columns, rows);
    }

    public static RouteGroupBuilder MapStockCutReports(this RouteGroupBuilder reports)
    {
        reports.MapGet("/order/stock/cut", async (DateOnly? cut, HttpContext http, StockClosingDirectory closing, ReportPrinter printer, CancellationToken ct) =>
        {
            if (cut is null) return Results.BadRequest("กรุณาเลือกวันตัดสต็อก");
            var report = await BuildAsync(closing, cut.Value);
            return await HrAttendanceReports.PrintAsync<StockCutReportView>(http, printer, TitleOf(cut.Value), Paper.A4Portrait, report, TitleOf(cut.Value), ct);
        });
        reports.MapGet("/order/stock/opening-history", async (DateOnly? from, DateOnly? to, string? pattern, string? format, HttpContext http, StockClosingDirectory closing, ReportPrinter printer,
            CancellationToken ct) =>
        {
            if (HrAttendanceReports.Range(from, to) is { } bad) return bad;
            var table = await OpeningHistoryAsync(closing, from!.Value, to!.Value, pattern);
            if (string.Equals(format, "xlsx", StringComparison.OrdinalIgnoreCase)) return Results.File(XlsxWriter.Write(table), XlsxWriter.ContentType, table.Title + ".xlsx");
            var pdf = await printer.PrintAsync<SummaryTableView>(table.Title, Paper.A4Landscape, new() { ["Table"] = table }, Accounts.AccountClaims.DisplayNameOf(http.User), cancellationToken: ct);
            return ReportsRegistration.Pdf(http, pdf, table.Title);
        });
        return reports;
    }
}
