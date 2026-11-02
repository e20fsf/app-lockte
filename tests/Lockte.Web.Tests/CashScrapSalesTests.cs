using Lockte.Cash;
using Lockte.Web.Reports;
using Lockte.Web.Reports.Cash;

namespace Lockte.Web.Tests;

/// <summary>รายงานยอดขายของเก่า RPT-09 (#244, BR-018) without a database: the rows, the order, the sum and the count.</summary>
public sealed class CashScrapSalesReportTests
{
    private static ScrapReceiptDto Receipt(string no, DateOnly date, bool cancelled = false, params (int Seq, string Item, decimal Amount)[] lines)
        => new(1, no, date, "คุณตัวอย่าง ข", null, null, null, cancelled, null, null, null, lines.Select((l, i) => new ScrapReceiptLineDto(i, l.Seq, l.Item, null, "กก.", 1, l.Amount, l.Amount)).ToList(), [1]);

    private static readonly DateOnly Sep8 = new(2026, 9, 8);

    [Fact] // AC-15: 69028 (1 line) and 69029 (5 lines) in September are 6 lines, 3,270.00
    public void AC15_two_receipts_make_six_lines_and_3270()
    {
        var report = ScrapSalesReports.Build(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30),
        [
            Receipt("69029", Sep8, false, (1, "ก", 220m), (2, "ข", 120m), (3, "ค", 1200m), (4, "ง", 330m), (5, "จ", 400m)), Receipt("69028", Sep8, false, (1, "ฝุ่น", 1000m)),
            Receipt("69027", new DateOnly(2026, 8, 5), false, (1, "นอกช่วง", 1360m)),
        ]);
        Assert.Equal((6, 3270m), (report.Count, report.Total));
        Assert.Equal(["69028", "69029", "69029", "69029", "69029", "69029"], report.Rows.Select(r => r.No)); // by number as a number within the day
        Assert.Equal([1, 1, 2, 3, 4, 5], report.Rows.Select(r => r.Seq));
    }

    [Fact] // BR-018 #2: one day, receipt 69027 with 4 lines, 1,360.00
    public void BR018_2_one_day_gives_its_receipt()
    {
        var day = new DateOnly(2026, 8, 5);
        var report = ScrapSalesReports.Build(day, day, [Receipt("69027", day, false, (1, "ก", 100m), (2, "ข", 200m), (3, "ค", 300m), (4, "ง", 760m)), Receipt("69028", Sep8, false, (1, "ฝุ่น", 1000m))]);
        Assert.Equal((4, 1360m), (report.Count, report.Total));
    }

    [Fact] // assumption ง: a cancelled receipt is not counted; the order of days and numbers across the range
    public void A_cancelled_receipt_is_left_out_and_receipts_come_by_date_then_number()
    {
        var report = ScrapSalesReports.Build(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30),
        [
            Receipt("69030", new DateOnly(2026, 9, 9), false, (1, "ก", 5m)), Receipt("69029", Sep8, true, (1, "ยกเลิก", 999m)), Receipt("69999", Sep8, false, (1, "ข", 7m)),
            Receipt("691000", Sep8, false, (1, "ค", 9m)),
        ]);
        Assert.Equal(["69999", "691000", "69030"], report.Rows.Select(r => r.No)); // 691000 follows 69999 as numbers
        Assert.Equal(21m, report.Total);
    }

    [Fact]
    public void A_range_the_wrong_way_round_or_without_receipts_is_empty()
    {
        Assert.Equal(0, ScrapSalesReports.Build(new DateOnly(2026, 9, 30), new DateOnly(2026, 9, 1), [Receipt("69028", Sep8, false, (1, "ก", 1m))]).Count);
        Assert.Equal(0, ScrapSalesReports.Build(new DateOnly(2027, 1, 1), new DateOnly(2027, 1, 31), [Receipt("69028", Sep8, false, (1, "ก", 1m))]).Count);
    }

    [Fact]
    public async Task The_page_prints_the_head_with_the_month_each_row_with_its_date_and_number_and_the_count()
    {
        var report = ScrapSalesReports.Build(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), [Receipt("69028", Sep8, false, (1, "ฝุ่น", 1000m))]);
        var html = System.Net.WebUtility.HtmlDecode(await ReportBody.RenderAsync<ScrapSalesView>(new() { ["Report"] = report }));
        foreach (var text in new[] { "ตั้งแต่วันที่ 1/9/2569 ถึงวันที่ 30/9/2569 เดือน กันยายน 2569", "วันที่", "เลขที่", "ลำดับ", "รายการ", "จำนวน", "หน่วย", "ราคา", "จำนวนเงิน", "ผู้ซื้อ",
                     "8/9/2569", "69028", "ฝุ่น", "คุณตัวอย่าง ข", "รวมทั้งหมด", "(1 รายการ)", "1,000.00" })
            Assert.Contains(text, html, StringComparison.Ordinal);
        Assert.Contains("ไม่มีข้อมูล", await ReportBody.RenderAsync<ScrapSalesView>(new() { ["Report"] = ScrapSalesReports.Build(new DateOnly(2027, 1, 1), new DateOnly(2027, 1, 2), []) }));
    }
}

/// <summary>RPT-09 from the saved receipts (#244): AC-15 through the service. The receipts sit in year code 52 and in dates of their own, the database being shared.</summary>
[Collection(LocalDbCollection.Name)]
public sealed class CashScrapSalesTests(LocalDbFixture db)
{
    private async Task<ScrapReceiptDto> SaveAsync(string no, DateOnly date, params (string Item, decimal Price)[] lines)
    {
        await using var scope = db.Scope();
        var result = await scope.Get<ScrapReceiptRegistry>().SaveAsync(new(null, no, date, "คุณตัวอย่าง ข", null, null, null, lines.Select((l, i) => new ScrapReceiptLineInput(i + 1, l.Item, null, "กก.", 1, l.Price, null)).ToList(), null, date));
        Assert.True(result.Succeeded, string.Join(", ", result.Errors));
        return (await scope.Get<ScrapReceiptDirectory>().GetAsync(result.Id!.Value))!;
    }

    [Fact] // AC-15 and BR-018 #1–#2
    public async Task AC15_the_range_gives_the_lines_of_its_receipts_and_not_those_cancelled()
    {
        var sep = new DateOnly(2009, 9, 8);
        await SaveAsync("52027", new DateOnly(2009, 8, 5), ("เศษเหล็ก", 100m), ("ถัง", 200m), ("ฝุ่น", 300m), ("น้ำมัน", 760m));
        await SaveAsync("52028", sep, ("ฝุ่น", 1000m));
        await SaveAsync("52029", sep, ("เบ้าเหล็ก", 220m), ("ถังพลาสติก", 120m), ("น้ำมันเก่า", 1200m), ("พลาสติก", 330m), ("ถังเหล็ก", 400m));
        var wrong = await SaveAsync("52030", sep, ("ออกผิด", 5000m));
        await using (var scope = db.Scope()) Assert.True((await scope.Get<ScrapReceiptRegistry>().CancelAsync(wrong.Id, "ออกผิด", wrong.RowVersion)).Succeeded);

        await using var read = db.Scope();
        var directory = read.Get<ScrapReceiptDirectory>();
        var september = ScrapSalesReports.Build(new DateOnly(2009, 9, 1), new DateOnly(2009, 9, 30), await directory.ListAsync(new DateOnly(2009, 9, 1), new DateOnly(2009, 9, 30)));
        Assert.Equal((6, 3270m), (september.Count, september.Total));
        var day = new DateOnly(2009, 8, 5);
        var august = ScrapSalesReports.Build(day, day, await directory.ListAsync(day, day));
        Assert.Equal((4, 1360m), (august.Count, august.Total));
        Assert.Equal(0, ScrapSalesReports.Build(new DateOnly(2010, 1, 1), new DateOnly(2010, 1, 31), await directory.ListAsync(new DateOnly(2010, 1, 1), new DateOnly(2010, 1, 31))).Count);
    }
}
