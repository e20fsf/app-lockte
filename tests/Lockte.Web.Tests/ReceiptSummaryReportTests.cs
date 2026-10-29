using Lockte.Purch;
using Lockte.Web.Reports;
using Lockte.Web.Reports.Purch;

namespace Lockte.Web.Tests;

/// <summary>สรุปการรับวัตถุดิบ RPT-20 … 24 (#259) without a database: what is counted, the twelve months computed live, the views.</summary>
public sealed class ReceiptSummaryReportTests
{
    private static readonly DateOnly From = new(2026, 8, 1);
    private static readonly DateOnly To = new(2026, 8, 31);

    private static ReceivedLine Line(int id, DateOnly date, int department, int material, bool main, decimal qty, decimal price, string order, string request, bool failed = false)
        => new(id, date, order, request, department, material, "M" + material, "วัตถุดิบ " + material, "กก.", main, qty, price, qty * price, 1, "S1", "ผู้ขาย", failed);

    private static readonly ReceivedLine[] Lines =
    [
        Line(1, new(2026, 8, 3), 1, 10, true, 100m, 10m, "P1", "R1"),                // 1000
        Line(2, new(2026, 8, 9), 1, 10, true, 50m, 12m, "P2", "R1"),                 // 600
        Line(3, new(2026, 8, 12), 2, 11, false, 10m, 5m, "P2", "R2"),                // 50 (not a main material)
        Line(4, new(2026, 8, 20), 2, 10, true, 999m, 10m, "P3", "R3", failed: true), // failed: counted only by the main-material reports
        Line(5, new(2026, 9, 2), 1, 10, true, 20m, 10m, "P4", "R4"),                 // another month
        Line(6, new(2025, 8, 2), 1, 10, true, 7m, 10m, "P0", "R0"),                  // another year
    ];

    [Fact]
    public void The_monthly_summary_totals_per_material_without_the_failed_and_counts_the_documents_RPT_20()
    {
        var report = ReceiptSummaryReports.Monthly(Lines, false, From, To);
        Assert.Equal(["M10", "M11"], report.Rows.Select(r => r.Code));
        Assert.Equal((150m, 1600m), (report.Rows[0].Quantity, report.Rows[0].Amount));
        Assert.Equal(1650m, report.TotalAmount);
        Assert.Equal((2, 2), (report.RequestCount, report.OrderCount)); // R1, R2 and P1, P2 — the failed delivery's R3/P3 do not count
    }

    [Fact]
    public void The_main_material_summary_keeps_the_failed_and_leaves_out_the_other_materials_RPT_23()
    {
        var report = ReceiptSummaryReports.Monthly(Lines, true, From, To);
        Assert.Equal(["M10"], report.Rows.Select(r => r.Code));
        Assert.Equal(1000m + 600m + 9990m, report.TotalAmount);
    }

    [Fact]
    public void The_yearly_summary_has_a_row_per_month_of_that_year_only_and_prints_the_same_every_time_RPT_21_RPT_24()
    {
        var report = ReceiptSummaryReports.Yearly(Lines, false, 2026);
        Assert.Equal([8, 9], report.Months.Select(m => m.Month));
        Assert.Equal((2, 2, 1650m), (report.Months[0].RequestCount, report.Months[0].OrderCount, report.Months[0].Amount));
        Assert.Equal(1850m, report.TotalAmount); // the August of 2025 is not in the year 2026 (D-14)
        Assert.Equal(report.Months, ReceiptSummaryReports.Yearly(Lines, false, 2026).Months);
        var main = ReceiptSummaryReports.Yearly(Lines, true, 2026);
        Assert.Equal(1000m + 600m + 9990m + 200m, main.TotalAmount);
        Assert.Equal(1169m, main.TotalQuantity); // 100 + 50 + 999 + 20
    }

    [Fact]
    public void The_purchases_by_department_are_ranked_by_amount_without_the_failed_RPT_22()
    {
        var report = ReceiptSummaryReports.DepartmentPurchases(Lines, new Dictionary<int, string> { [1] = "ไดคาสท์", [2] = "บรรจุ" }, From, To);
        Assert.Equal([("ไดคาสท์", 1600m), ("บรรจุ", 50m)], report.Rows);
        Assert.Equal(1650m, report.Total);
    }

    private static async Task<string> RenderAsync<T>(object report) where T : Microsoft.AspNetCore.Components.IComponent
        => System.Net.WebUtility.HtmlDecode(await ReportBody.RenderAsync<T>(new() { ["Report"] = report }));

    [Fact]
    public async Task The_views_print_the_counts_the_months_the_totals_and_no_data_when_empty()
    {
        var monthly = await RenderAsync<MonthlyReceiptSummaryView>(ReceiptSummaryReports.Monthly(Lines, false, From, To));
        Assert.Contains("ใบขออนุมัติซื้อ 2 ใบ", monthly);
        Assert.Contains("1,650.00", monthly);
        Assert.DoesNotContain("ใบขออนุมัติซื้อ", await RenderAsync<MonthlyReceiptSummaryView>(ReceiptSummaryReports.Monthly(Lines, true, From, To)));
        var yearly = await RenderAsync<YearlyReceiptSummaryView>(ReceiptSummaryReports.Yearly(Lines, false, 2026));
        Assert.Contains("สิงหาคม 2569", yearly);
        Assert.Contains("1,850.00", yearly);
        Assert.Contains("ไม่มีข้อมูล", await RenderAsync<MonthlyReceiptSummaryView>(ReceiptSummaryReports.Monthly([], false, From, To)));
        Assert.Contains("ไม่มีข้อมูล", await RenderAsync<YearlyReceiptSummaryView>(ReceiptSummaryReports.Yearly([], true, 2026)));
    }
}
