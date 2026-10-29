using Lockte.Purch;
using Lockte.Web.Reports;
using Lockte.Web.Reports.Purch;

namespace Lockte.Web.Tests;

/// <summary>STOCK CARD RPT-14 (BR-041) and รายงานการตัด STOCK RPT-26 (BR-042) (#262) without a database.</summary>
public sealed class MaterialStockReportTests
{
    private static readonly MaterialDto Material = new(1, "R0019", "วัตถุดิบทดสอบ", "กก.", null, 0m, null, null, null, true, 0m, null, []);

    private static StockEntryDto Entry(int id, DateOnly date, string? doc, decimal qty, StockEntryKind kind, int? employee = null)
        => new(id, date, doc, null, 1, "R0019", "วัตถุดิบทดสอบ", qty, kind, null, employee, null, null, false, []);

    [Fact]
    public void The_card_lists_in_and_out_with_the_running_balance_AC_19()
    {
        var card = MaterialStockReports.Card(Material,
        [
            Entry(3, new(2026, 9, 20), "IS1", -1138.6m, StockEntryKind.Issue, employee: 5),
            Entry(2, new(2026, 9, 9), null, 3356m, StockEntryKind.Receipt),
            Entry(1, new(2025, 10, 31), "stock", 1000m, StockEntryKind.OpeningBalance),
        ], new Dictionary<int, string> { [5] = "E005" });
        Assert.Equal([1000m, 4356m, 3217.4m], card.Rows.Select(r => r.Balance));
        Assert.Equal("stock", card.Rows[0].DocumentNo);
        Assert.Equal((null, -1138.6m, "E005"), (card.Rows[2].In, card.Rows[2].Out, card.Rows[2].EmployeeCode));
        Assert.Equal((4356m, -1138.6m, 3217.4m), (card.TotalIn, card.TotalOut, card.Balance));
    }

    [Fact]
    public void An_overdrawn_opening_balance_stands_in_the_issue_column()
    {
        var card = MaterialStockReports.Card(Material, [Entry(1, new(2026, 1, 31), "stock", -13250m, StockEntryKind.OpeningBalance)], new Dictionary<int, string>());
        Assert.Equal((null, -13250m), (card.Rows[0].In, card.Rows[0].Out));
    }

    [Fact]
    public void The_opening_balance_comes_before_the_other_rows_of_its_day()
    {
        var day = new DateOnly(2026, 9, 30);
        var card = MaterialStockReports.Card(Material, [Entry(1, day, "A1", 5m, StockEntryKind.Receipt), Entry(2, day, "stock", 10m, StockEntryKind.OpeningBalance)], new Dictionary<int, string>());
        Assert.Equal(["stock", "A1"], card.Rows.Select(r => r.DocumentNo));
    }

    [Fact]
    public void The_cut_report_values_each_balance_at_the_price_and_leaves_out_zero_balances_AC_20()
    {
        var cut = new DateOnly(2026, 9, 30);
        var report = MaterialStockReports.Cut(cut,
        [
            new StockPeriodBalanceDto(cut, 2, "R0020", "ข", "กก.", 0m, 9m),
            new StockPeriodBalanceDto(cut, 1, "R0019", "ก", "กก.", 3217.4m, 10m),
            new StockPeriodBalanceDto(cut, 3, "R0021", "ค", "ใบ", -10m, 4.165m),
        ]);
        Assert.Equal(["R0019", "R0021"], report.Rows.Select(r => r.Code));
        Assert.Equal(32174m, report.Rows[0].Amount);
        Assert.Equal(32174m - 41.65m, report.Total); // an overdrawn balance is a negative value
    }

    [Fact]
    public async Task The_views_print_the_rows_the_totals_and_no_data_when_empty()
    {
        var card = MaterialStockReports.Card(Material, [Entry(1, new(2025, 10, 31), "stock", 1000m, StockEntryKind.OpeningBalance)], new Dictionary<int, string>());
        var html = System.Net.WebUtility.HtmlDecode(await ReportBody.RenderAsync<StockCardView>(new() { ["Report"] = card }));
        Assert.Contains("R0019", html);
        Assert.Contains("1,000.00", html);
        var cut = new DateOnly(2026, 9, 30);
        var cutHtml = System.Net.WebUtility.HtmlDecode(await ReportBody.RenderAsync<StockCutView>(new() { ["Report"] = MaterialStockReports.Cut(cut, [new StockPeriodBalanceDto(cut, 1, "R0019", "ก", "กก.", 2m, 4.165m)]) }));
        Assert.Contains("4.165", cutHtml);
        Assert.Contains("รวม 1 รายการ", cutHtml);
        Assert.Contains("ไม่มีข้อมูล", await ReportBody.RenderAsync<StockCardView>(new() { ["Report"] = MaterialStockReports.Card(Material, [], new Dictionary<int, string>()) }));
        Assert.Contains("ไม่มีข้อมูล", await ReportBody.RenderAsync<StockCutView>(new() { ["Report"] = MaterialStockReports.Cut(cut, []) }));
    }
}
