using Lockte.Hr;
using Lockte.Web.Reports;
using Lockte.Web.Reports.Hr;
using Microsoft.AspNetCore.Components;

namespace Lockte.Web.Tests;

/// <summary>
/// The HTML of the #56 reports before it becomes a PDF (แบบกลาง B, #40): no rows → "ไม่มีข้อมูล" (D-6), the legacy titles
/// and formats, and the RPT-08 form as the legacy printed it (blank overtime columns, รวมเงินทั้งสิ้น = ค่าจ้าง).
/// </summary>
public sealed class PayHistoryReportLayoutTests
{
    private static readonly DateOnly From = new(2026, 8, 27);
    private static readonly DateOnly To = new(2026, 9, 26);

    /// <summary>The body with its character references decoded (Razor writes data text such as Thai as &amp;#xE27;…).</summary>
    private static async Task<string> HtmlAsync<TView>(Dictionary<string, object?> parameters) where TView : IComponent
        => System.Net.WebUtility.HtmlDecode(await ReportBody.RenderAsync<TView>(parameters));

    private static Task<string> HtmlAsync<TView>(object report) where TView : IComponent => HtmlAsync<TView>(new() { ["Report"] = report });

    [Fact] // D-6: ไม่มีข้อมูล → "ไม่มีข้อมูล" ไม่พิมพ์แถวว่าง
    public async Task Every_report_without_rows_says_there_is_no_data()
    {
        string[] pages =
        [
            await HtmlAsync<MonthEndPayReportView>(new MonthEndPayReport(From, To, null, [])),
            await HtmlAsync<MonthEndSummaryView>(new MonthEndPaySummary(From, To, [])),
            await HtmlAsync<MonthlyPayReportView>(new MonthlyPayReport(From, To, [])),
            await HtmlAsync<PayTotalsReportView>(new PayTotalsReport(From, To, [])),
        ];

        Assert.All(pages, html =>
        {
            Assert.Contains("ไม่มีข้อมูล", html);
            Assert.DoesNotContain("<tbody>", html);
        });
    }

    [Fact] // RPT-08: แผนก ทั้งหมด, ประจำเดือน = เดือนของถึงวันที่, จำนวนวัน 27 / 26.5 / ว่าง, รวมเงินทั้งสิ้น = ค่าจ้าง ไม่บวก OT
    public async Task The_month_end_report_prints_the_wage_as_the_total_and_leaves_the_overtime_columns_blank()
    {
        var report = new MonthEndPayReport(From, To, null,
        [
            new MonthEndPayLine("U001", "นาย ก", "1234567890123", "ประกอบ", 372, 27, 10044m, 10044m, 150m),
            new MonthEndPayLine("U002", "นาย ข", "1234567890124", "ประกอบ", 372, 26.5, 9858m, 9858m, 0m),
            new MonthEndPayLine("U003", "นาย ค", null, "ประกอบ", 12340, null, 12340m, 6170m, 80m),
        ]);

        var html = await HtmlAsync<MonthEndPayReportView>(report);

        Assert.Contains("แผนก ทั้งหมด", html);
        Assert.Contains("ประจำเดือน กันยายน 2569", html);
        Assert.Contains("ตั้งแต่วันที่ 27/8/2569 ถึงวันที่ 26/9/2569", html);
        Assert.Contains(">27<", html);
        Assert.Contains(">26.5<", html);
        Assert.Contains(">12,340.00<", html);
        Assert.DoesNotContain("10,194.00", html); // 10,044 + 150 is not printed
        Assert.Equal(2, html.Split(">32,242.00<").Length - 1); // รวมทั้งหมด under ค่าจ้าง and รวมเงินทั้งสิ้น
        Assert.Contains("ลายมือชื่อ", html);
    }

    [Fact] // สรุปบัญชีคำนวณค่าจ้าง: Σค่าจ้าง; สรุปค่าแรงประจำเดือน (ต้นทุน): Σ(PAY_W + PAY_OT); หัวตามถึงวันที่
    public async Task The_summaries_print_the_wages_or_the_cost()
    {
        var summary = new MonthEndPaySummary(From, To,
            [new MonthEndDepartmentTotal("ไดคาสท์", 12, 150442m, 150850m), new MonthEndDepartmentTotal("ตกแต่ง", 22, 230950m, 231000m)]);

        var wages = await HtmlAsync<MonthEndSummaryView>(new() { ["Report"] = summary });
        var cost = await HtmlAsync<MonthEndSummaryView>(new() { ["Report"] = summary, ["Cost"] = true });

        Assert.Equal("สรุปบัญชีคำนวณค่าจ้างเดือน กันยายน 2569", MonthEndSummaryView.TitleOf(summary, cost: false));
        Assert.Equal("สรุปค่าแรงประจำเดือน กันยายน 2569", MonthEndSummaryView.TitleOf(summary, cost: true));
        Assert.Contains(">150,442.00<", wages);
        Assert.Contains(">381,392.00<", wages);
        Assert.Contains(">381,850.00<", cost);
        Assert.Contains(">34<", wages);
        Assert.Contains("รวมทั้งหมด", wages);
    }

    [Fact] // RPT-09: ปีของตั้งแต่วันที่, 12 เดือน, เดือนที่ไม่มีว่าง แต่รวมยังบวก, จำนวนเต็ม
    public async Task The_monthly_report_leaves_months_without_pay_blank_and_still_adds_up_the_rest()
    {
        decimal?[] months = [1000.4m, 2000.6m, null, null, null, null, null, null, null, null, null, null];
        var report = new MonthlyPayReport(new(2026, 1, 2), new(2027, 1, 1), [new MonthlyPayLine("แผนกประกอบ", months)]);

        var html = await HtmlAsync<MonthlyPayReportView>(report);

        Assert.Equal("สรุปค่าแรงแต่ละเดือนปี 2569", MonthlyPayReportView.TitleOf(report));
        Assert.Contains("มกราคม", html);
        Assert.Contains("ธันวาคม", html);
        Assert.Contains(">1,000<", html);
        Assert.Contains(">2,001<", html);
        Assert.Equal(2, html.Split(">3,001<").Length - 1); // รวม of the line and of the report
    }

    [Fact] // RPT-10: ค่าแรง ค่าล่วงเวลา รวม จำนวนเต็ม + รวมทุกคอลัมน์
    public async Task The_pay_totals_print_whole_baht_and_the_sums()
    {
        var report = new PayTotalsReport(From, To,
            [new DepartmentPaySum("ไดคาสท์", 149835m, 408m), new DepartmentPaySum("ซ่อมบำรุง", 26456m, 0m)]);

        var html = await HtmlAsync<PayTotalsReportView>(report);

        Assert.Contains(">150,243<", html);
        Assert.Contains(">176,291<", html);
        Assert.Contains(">176,699<", html);
    }
}
