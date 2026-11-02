using System.Diagnostics;
using Lockte.Cash;
using Lockte.Web.Reports;
using Lockte.Web.Reports.Cash;

namespace Lockte.Web.Tests;

/// <summary>รายการซื้อของเงินสดย่อย RPT-04 (#243, BR-008) without a database: the order, the total and what the page prints.</summary>
public sealed class CashPurchaseListReportTests
{
    private static ExpenseLineDto Line(int id, int day, string voucher, string name, decimal qty, decimal amount, string unit = "ตัว")
        => new(id, new DateOnly(2026, 9, day), voucher, name, qty, unit, 0, amount, 1, "ค่าใช้จ่ายแผนกซ่อมบำรุง", [1]);

    private static readonly ExpenseLineDto[] Day11 =
    [
        Line(1, 11, "09/36", "หัวสปริงเกอร์ดับเพลิง", 2, 80, "หัว"), Line(2, 11, "09/34", "แก๊ส NGV", 31.53m, 633, "กก."), Line(3, 11, "09/35", "ซีล TC 25-40-8", 1, 90),
    ];

    [Fact] // AC-20, BR-008 #2: the three lines of 11 September in the order of the number, 803.00
    public void AC20_the_lines_of_one_day_come_in_order_of_number_with_the_total()
    {
        var report = PurchaseListReports.Build(new DateOnly(2026, 9, 11), new DateOnly(2026, 9, 11), Day11);
        Assert.Equal(["09/34", "09/35", "09/36"], report.Lines.Select(l => l.VoucherNo));
        Assert.Equal([633m, 90m, 80m], report.Lines.Select(l => l.Amount));
        Assert.Equal(803m, report.Total);
    }

    [Fact] // the legacy sorted the number as text: 05/108 before 05/99; then the order keyed
    public void The_number_is_sorted_as_text_so_a_print_lines_up_with_the_old_one()
    {
        var report = PurchaseListReports.Build(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 31),
        [
            new(1, new DateOnly(2026, 5, 3), "05/99", "ข", 1, null, 0, 1, 1, "ก", [1]), new(2, new DateOnly(2026, 5, 3), "05/108", "ก", 1, null, 0, 1, 1, "ก", [1]),
            new(3, new DateOnly(2026, 5, 3), "05/99", "ค", 1, null, 0, 1, 1, "ก", [1]), new(4, new DateOnly(2026, 5, 2), "05/200", "ง", 1, null, 0, 1, 1, "ก", [1]),
        ]);
        Assert.Equal([4, 2, 1, 3], report.Lines.Select(l => l.Id));
    }

    [Fact] // BR-008 #3: to before from prints nothing and says nothing
    public async Task A_range_the_wrong_way_round_prints_empty_without_a_warning()
    {
        var report = PurchaseListReports.Build(new DateOnly(2026, 9, 12), new DateOnly(2026, 9, 11), Day11);
        Assert.Empty(report.Lines);
        var html = await ReportBody.RenderAsync<PurchaseListView>(new() { ["Report"] = report });
        Assert.Contains("ไม่มีข้อมูล", html);
        Assert.DoesNotContain("ต้องไม่", html);
    }

    [Fact]
    public async Task The_page_prints_the_head_the_columns_each_line_and_the_total()
    {
        var html = System.Net.WebUtility.HtmlDecode(await ReportBody.RenderAsync<PurchaseListView>(new() { ["Report"] = PurchaseListReports.Build(new DateOnly(2026, 9, 11), new DateOnly(2026, 9, 11), Day11) }));
        foreach (var text in new[] { "ตั้งแต่วันที่ 11/9/2569 ถึงวันที่ 11/9/2569", "วันที่", "ลำดับที่", "รายการ", "จำนวน", "จำนวนเงิน", "ประเภท", "09/34", "แก๊ส NGV", "31.53 กก.", "633.00", "ค่าใช้จ่ายแผนกซ่อมบำรุง", "รวมทั้งหมด", "803.00" })
            Assert.Contains(text, html, StringComparison.Ordinal);
    }
}

/// <summary>RPT-04 from the lines (#243): the range through the service, the year against RPT-02, and the time a year takes. Lines sit in days of their own: the database is shared.</summary>
[Collection(LocalDbCollection.Name)]
public sealed class CashPurchaseListTests(LocalDbFixture db)
{
    private static string Unique(string name) => $"{name} {Guid.NewGuid():N}"[..(name.Length + 9)];

    private async Task<int> NewTypeAsync()
    {
        await using var scope = db.Scope();
        return (await scope.Get<ExpenseTypeRegistry>().SaveAsync(new(null, Unique("ประเภททดสอบ"), null))).Id!.Value;
    }

    private async Task AddAsync(DateOnly day, int type, string voucher, string name, decimal qty, decimal amount)
    {
        await using var scope = db.Scope();
        Assert.True((await scope.Get<ExpenseLineRegistry>().SaveAsync(new(null, day, voucher, name, qty, "ตัว", 0, amount, type, null))).Succeeded);
    }

    [Fact] // AC-20 through the service, and the total of the range is the "this period" of RPT-02 for the same dates
    public async Task AC20_the_range_through_the_service_matches_the_sum_of_rpt02()
    {
        var start = new DateOnly(2400, 1, 1).AddDays(Random.Shared.Next(0, 20000));
        var type = await NewTypeAsync();
        await AddAsync(start, type, "09/35", "ซีล TC 25-40-8", 1, 90m);
        await AddAsync(start, type, "09/34", "แก๊ส NGV", 31.53m, 633m);
        await AddAsync(start, type, "09/36", "หัวสปริงเกอร์ดับเพลิง", 2, 80m);
        await AddAsync(start.AddDays(1), type, "09/37", "ของวันถัดไป", 1, 5m);

        await using var scope = db.Scope();
        var lines = scope.Get<ExpenseLineDirectory>();
        var report = PurchaseListReports.Build(start, start, await lines.ListAsync(start, start));

        Assert.Equal(["09/34", "09/35", "09/36"], report.Lines.Select(l => l.VoucherNo));
        Assert.Equal(803m, report.Total);
        var wide = PurchaseListReports.Build(start, start.AddDays(1), await lines.ListAsync(start, start.AddDays(1)));
        Assert.Equal(wide.Total, (await lines.SummaryByTypeAsync(start, start.AddDays(1))).Sum(r => r.Total));
        Assert.Empty(PurchaseListReports.Build(start.AddDays(1), start, await lines.ListAsync(start.AddDays(1), start)).Lines);
    }
}

/// <summary>A whole year of RPT-04 through Chromium (#243): about 2,000 lines, 48 pages in the legacy sample; the time is measured and kept in the output.</summary>
[Collection(ChromiumCollection.Name)]
public sealed class CashPurchaseListPdfTests(ChromiumFixture chromium, Xunit.Abstractions.ITestOutputHelper output)
{
    [Fact]
    public async Task A_year_of_lines_prints_to_many_pages_in_a_time_a_person_can_wait_for()
    {
        var lines = Enumerable.Range(1, 2000).Select(i => new ExpenseLineDto(i, new DateOnly(2026, 1, 1).AddDays(i % 365), $"{1 + i % 12:00}/{1 + i % 99:00}", $"รายการซื้อของตัวอย่างหมายเลข {i}", 1 + i % 5, "ตัว", 0, 10m + i % 90, 1, "ค่าใช้จ่ายแผนกซ่อมบำรุง", [1])).ToList();
        var report = PurchaseListReports.Build(new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1), lines);

        var watch = Stopwatch.StartNew();
        var html = ReportHtml.Compose(new ReportDocument(PurchaseListReports.Title, Paper.A4Portrait), new PrintStamp(DateTimeOffset.Now, "ผู้ทดสอบ"), await ReportBody.RenderAsync<PurchaseListView>(new() { ["Report"] = report }));
        var pdf = await chromium.Renderer.RenderAsync(html, Paper.A4Portrait);
        watch.Stop();

        var pages = System.Text.RegularExpressions.Regex.Matches(System.Text.Encoding.Latin1.GetString(pdf), @"/Type /Page\b").Count;
        output.WriteLine($"RPT-04: {lines.Count} lines, {pages} pages, {watch.Elapsed.TotalSeconds:0.0} s");
        Assert.InRange(pages, 20, 120);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(60), $"took {watch.Elapsed.TotalSeconds:0} s");
    }
}
