using Lockte.Purch;
using Lockte.Web.Reports;
using Lockte.Web.Reports.Purch;

namespace Lockte.Web.Tests;

/// <summary>การซื้อวัตถุดิบรายปีตามชื่อ RPT-16 / RPT-17 (#260) without a database.</summary>
public sealed class MaterialYearReportTests
{
    private static ReceivedLine Line(int id, DateOnly date, int material, decimal qty, decimal price, bool failed = false)
        => new(id, date, "P" + id, "R" + id, 1, material, "M" + material, "วัตถุดิบ " + material, "กก.", true, qty, price, qty * price, 1, "S1", "ผู้ขาย", failed);

    private static readonly ReceivedLine[] Lines =
    [
        Line(1, new(2026, 1, 5), 10, 1m, 1234.56m),               // January: 1,234.56 → 1,235 (RND-D)
        Line(2, new(2026, 3, 9), 10, 2m, 500.25m),                // March: 1,000.50 → 1,000 (half to even)
        Line(3, new(2026, 3, 20), 10, 1m, 0m),
        Line(4, new(2026, 3, 21), 11, 4m, 100m, failed: true),    // failed deliveries count here
        Line(5, new(2026, 12, 31), 11, 3m, 10m),
        Line(6, new(2025, 12, 31), 11, 99m, 10m),                 // another year
    ];

    [Fact]
    public void Every_month_is_computed_at_once_rounded_to_whole_numbers_and_the_failed_count_RPT_16()
    {
        var report = MaterialYearReports.Build(Lines, 2026, false);
        Assert.Equal(["M10", "M11"], report.Rows.Select(r => r.Code));
        Assert.Equal(400m, report.Rows[1].Months[2]); // the failed delivery of 4 × 100 is in March
    }

    [Fact]
    public void The_order_is_by_the_total_of_the_year_and_the_total_is_the_sum_of_the_rounded_months()
    {
        var report = MaterialYearReports.Build(Lines, 2026, false);
        var m10 = report.Rows.Single(r => r.Code == "M10");
        Assert.Equal(1235m, m10.Months[0]);
        Assert.Equal(1000m, m10.Months[2]);
        Assert.Equal(2235m, m10.Total);
        var m11 = report.Rows.Single(r => r.Code == "M11");
        Assert.Equal((400m, 30m, 430m), (m11.Months[2], m11.Months[11], m11.Total));
        Assert.Equal("M10", report.Rows[0].Code);
        Assert.Equal(2665m, report.Total);
        Assert.Equal(1235m, report.MonthTotal(0));
        Assert.Equal(0m, report.MonthTotal(5)); // a month without receipts is zero, not missing
    }

    [Fact]
    public void The_units_report_adds_up_the_quantity_RPT_17()
    {
        var report = MaterialYearReports.Build(Lines, 2026, true);
        Assert.Equal((4m, 7m, 3m), (report.Rows.Single(r => r.Code == "M10").Total, report.Rows.Single(r => r.Code == "M11").Total, report.Rows.Single(r => r.Code == "M11").Months[11]));
        Assert.Equal(MaterialYearReports.Title(2026, true), "การซื้อวัตถุดิบปี 2569 (จำนวนหน่วย)");
    }

    [Fact]
    public async Task The_view_prints_the_months_the_totals_and_no_data_when_empty()
    {
        var html = System.Net.WebUtility.HtmlDecode(await ReportBody.RenderAsync<MaterialYearView>(new() { ["Report"] = MaterialYearReports.Build(Lines, 2026, false) }));
        Assert.Contains("มกราคม", html);
        Assert.Contains("ธันวาคม", html);
        Assert.Contains("2,235", html);
        Assert.DoesNotContain("<th>หน่วย</th>", html);
        Assert.Contains("<th>หน่วย</th>", await ReportBody.RenderAsync<MaterialYearView>(new() { ["Report"] = MaterialYearReports.Build(Lines, 2026, true) }));
        Assert.Contains("ไม่มีข้อมูล", await ReportBody.RenderAsync<MaterialYearView>(new() { ["Report"] = MaterialYearReports.Build([], 2026, false) }));
    }
}
