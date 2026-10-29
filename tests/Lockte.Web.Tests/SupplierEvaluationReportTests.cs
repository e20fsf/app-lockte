using Lockte.Purch;
using Lockte.Web.Reports;
using Lockte.Web.Reports.Purch;

namespace Lockte.Web.Tests;

/// <summary>RPT-07 (#254) without a database: the form shows the counts, points, percentage and grade it is given (AC-22, AC-23).</summary>
public sealed class SupplierEvaluationViewTests
{
    private static readonly PurchSettingsDto Settings = new(0.07m, 5000m, null, ["sp"], 80m, 70m, 60m, [], null);

    private static SupplierDto Supplier(bool certificate) => new(1, "DC-31", "บริษัท ผู้ขายทดสอบ จำกัด", "99 ถนนทดสอบ", "เขตทดสอบ", "02-111-1111", null, null, null, 30, true, "อลูมิเนียม",
        "ผลิต (manufacturing)", certificate, true, []);

    private static SupplierEvaluationPrint Print(SupplierCounts counts, bool certificate, int p1, int p2)
        => new(new DateOnly(2026, 10, 29), 3, new DateOnly(2026, 7, 1), new DateOnly(2026, 9, 30), Supplier(certificate), counts, p1, p2,
            SupplierScore.Calculate(counts, certificate, p1, p2, Settings));

    private static async Task<string> RenderAsync(SupplierEvaluationPrint? print)
        => System.Net.WebUtility.HtmlDecode(await ReportBody.RenderAsync<SupplierEvaluationView>(new() { [nameof(SupplierEvaluationView.Report)] = print }));

    [Fact] // AC-22
    public async Task Two_failures_and_no_certificate_print_15_points_60_percent_grade_C()
    {
        var html = await RenderAsync(Print(new SupplierCounts(3, 2, 0, 4, 5, 0), false, 3, 3));
        Assert.Contains("มีใบ (NCR) 2 ใบ คะแนนเต็ม 5 ได้ 3 คะแนน", html);
        Assert.Contains("ได้ 1 คะแนน", html); // no certificate
        Assert.Contains("15 คะแนน", html);
        Assert.Contains("60.00 %", html);
        Assert.Contains("เกรด C — ระมัดระวังในการสั่งซื้อ,สั่งซื้อเท่าที่จำเป็น", html);
        Assert.Contains("3 คะแนน — บางครั้ง", html);
        Assert.Contains("DC-31", html);
    }

    [Fact] // AC-23 and BR-050 #1
    public async Task Twenty_points_is_80_percent_and_the_late_count_is_printed()
    {
        var html = await RenderAsync(Print(new SupplierCounts(5, 0, 5, 0, 4, 1), true, 5, 5));
        Assert.Contains("ล้าช้า 1 ครั้ง คะแนนเต็ม 5 ได้ 4 คะแนน", html);
        Assert.Contains("24 คะแนน", html);
        Assert.Contains("96.00 %", html);
        Assert.Contains("เกรด A — ผู้ขายชั้นดี", html);
        Assert.Contains("5 คะแนน — ดีมาก", html);
    }

    [Fact]
    public async Task A_supplier_with_nothing_counted_prints_no_data()
        => Assert.Contains("ไม่มีข้อมูล", await RenderAsync(null));
}
