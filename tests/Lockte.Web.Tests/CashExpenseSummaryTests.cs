using Lockte.Cash;
using Lockte.Web.Reports;
using Lockte.Web.Reports.Cash;

namespace Lockte.Web.Tests;

/// <summary>สรุปค่าใช้จ่ายเงินสดย่อย RPT-02 (#242, BR-006) without a database: the totals, the words, and what the page prints.</summary>
public sealed class CashExpenseSummaryReportTests
{
    private static readonly (string Type, decimal Total)[] Sample =
    [
        ("ค่าขนส่ง", 70m), ("ค่าใช้จ่ายเบ็ดเตล็ด - โรงงาน", 6132m), ("ค่าใช้จ่ายแผนกซ่อมบำรุง", 750m), ("ค่าใช้จ่ายแผนกไดคาสท์", 3000m), ("ค่าใช้จ่ายแผนกอัดผ้าเบรก", 590m),
        ("ค่าใช้จ่ายยานพาหนะ (1 ฒง 494)", 348m), ("ค่าใช้จ่ายยานพาหนะ (3 ฒต 3671)", 2200m), ("ค่าใช้จ่ายยานพาหนะ (โฟล์คลิฟ)", 1000m), ("ค่าสวัสดิการพนักงานโรงงาน", 14620m),
    ];

    private static ExpenseSummaryReport Report(decimal lastPay, IEnumerable<(string Type, decimal Total)>? rows = null)
        => new(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 3), (rows ?? Sample).Select((r, i) => new ExpenseTypeTotal(i + 1, r.Type, r.Total)).ToList(), lastPay);

    [Fact] // AC-07, BR-006 #1
    public void AC07_the_period_the_last_period_and_the_grand_total_with_its_words()
    {
        var report = Report(21456m);
        Assert.Equal(28710m, report.Total);
        Assert.Equal(50166m, report.Grand);
        Assert.Equal("(ห้าหมื่นหนึ่งร้อยหกสิบหกบาทถ้วน)", report.GrandInWords);
    }

    [Fact]
    public async Task The_page_prints_the_range_in_long_dates_the_rows_the_three_totals_and_the_signature_boxes()
    {
        var html = System.Net.WebUtility.HtmlDecode(await ReportBody.RenderAsync<ExpenseSummaryView>(new() { ["Report"] = Report(21456m) }));
        foreach (var text in new[] { "ตั้งแต่วันที่ 1 กันยายน 2569 ถึงวันที่ 3 กันยายน 2569", "ลำดับที่", "รายการ", "จำนวนเงิน", "ค่าขนส่ง", "70.00", "ค่าสวัสดิการพนักงานโรงงาน", "14,620.00",
                     "รวมค่าใช้จ่ายงวดนี้", "28,710.00", "ค่าใช้จ่ายงวดที่แล้ว", "21,456.00", "รวมค่าใช้จ่ายทั้งสิ้น", "50,166.00", "(ห้าหมื่นหนึ่งร้อยหกสิบหกบาทถ้วน)",
                     "ผู้อนุมัติ", "ผู้เตรียม", "ผู้ตรวจ", "ผู้ลงบัญชี", "วันที่ ....../....../......" })
            Assert.Contains(text, html, StringComparison.Ordinal);
    }

    [Fact] // BR-006 #2 differs: nothing in the range still prints the totals, with the last period
    public async Task A_range_with_no_lines_prints_an_empty_table_but_the_totals()
    {
        var report = Report(21456m, []);
        Assert.Equal((0m, 21456m), (report.Total, report.Grand));
        var html = System.Net.WebUtility.HtmlDecode(await ReportBody.RenderAsync<ExpenseSummaryView>(new() { ["Report"] = report }));
        Assert.Contains("0.00", html, StringComparison.Ordinal);
        Assert.Contains("21,456.00", html, StringComparison.Ordinal);
        Assert.Contains("(สองหมื่นหนึ่งพันสี่ร้อยห้าสิบหกบาทถ้วน)", html, StringComparison.Ordinal);
    }

    [Fact]
    public void The_date_is_written_in_full_with_the_buddhist_year_whatever_the_thread_culture()
    {
        var before = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("en-US");
            Assert.Equal("1 กันยายน 2569", ExpenseSummaryReports.LongDate(new DateOnly(2026, 9, 1)));
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = before; }
    }
}

/// <summary>The sums of RPT-02 from the lines (#242, BR-006). The lines sit in a window of days of their own: the database is shared by every test.</summary>
[Collection(LocalDbCollection.Name)]
public sealed class CashExpenseSummaryTests(LocalDbFixture db)
{
    private static string Unique(string name) => $"{name} {Guid.NewGuid():N}"[..(name.Length + 9)];

    /// <summary>Three days nobody else uses.</summary>
    private static DateOnly Window() => new DateOnly(2300, 1, 1).AddDays(Random.Shared.Next(0, 20000));

    private async Task<int> NewTypeAsync(string name)
    {
        await using var scope = db.Scope();
        return (await scope.Get<ExpenseTypeRegistry>().SaveAsync(new(null, name, null))).Id!.Value;
    }

    private async Task AddAsync(DateOnly day, int type, decimal amount)
    {
        await using var scope = db.Scope();
        var result = await scope.Get<ExpenseLineRegistry>().SaveAsync(new(null, day, "09/01", "ของ", 1, null, 0, amount, type, null));
        Assert.True(result.Succeeded, string.Join(", ", result.Errors));
    }

    [Fact] // AC-07 and BR-006 #1 through the service: the lines are added up by type, the ends of the range count
    public async Task The_lines_of_the_range_are_added_up_by_type_and_the_last_day_counts()
    {
        var start = Window();
        var transport = await NewTypeAsync(Unique("ค่าขนส่ง"));
        var welfare = await NewTypeAsync(Unique("ค่าสวัสดิการ"));
        await AddAsync(start, transport, 30m);
        await AddAsync(start.AddDays(2), transport, 40m); // the last day of the range
        await AddAsync(start.AddDays(1), welfare, 14620m);
        await AddAsync(start.AddDays(3), welfare, 999m); // the day after: not in the range
        await AddAsync(start.AddDays(-1), welfare, 888m); // the day before

        await using var scope = db.Scope();
        var rows = await scope.Get<ExpenseLineDirectory>().SummaryByTypeAsync(start, start.AddDays(2));

        Assert.Equal([(transport, 70m), (welfare, 14620m)], rows.OrderBy(r => r.ExpenseTypeId).Select(r => (r.ExpenseTypeId, r.Total)));
        var report = new ExpenseSummaryReport(start, start.AddDays(2), rows, 21456m);
        Assert.Equal((14690m, 36146m), (report.Total, report.Grand));
    }

    [Fact]
    public async Task A_retired_type_is_still_a_row_under_its_name_and_a_range_the_wrong_way_round_or_empty_has_no_rows()
    {
        var start = Window();
        var type = await NewTypeAsync(Unique("ค่าเลิกใช้"));
        await AddAsync(start, type, 100m);
        ExpenseTypeDto loaded;
        await using (var scope = db.Scope()) loaded = (await scope.Get<ExpenseTypeDirectory>().GetAsync(type))!;
        await using (var scope = db.Scope()) Assert.True((await scope.Get<ExpenseTypeRegistry>().SetActiveAsync(type, false, loaded.RowVersion)).Succeeded);

        await using var read = db.Scope();
        var directory = read.Get<ExpenseLineDirectory>();
        Assert.Equal([(loaded.Name, 100m)], (await directory.SummaryByTypeAsync(start, start)).Select(r => (r.TypeName, r.Total)));
        Assert.Empty(await directory.SummaryByTypeAsync(start.AddDays(5), start)); // to before from: nothing, no message
        Assert.Empty(await directory.SummaryByTypeAsync(start.AddDays(10), start.AddDays(12)));
    }

    [Fact] // the types come in the Thai dictionary order, a leading vowel not counting
    public async Task The_rows_are_in_the_thai_order_of_the_type_names()
    {
        var start = Window();
        var a = await NewTypeAsync(Unique("ข-ประเภท"));
        var b = await NewTypeAsync(Unique("เก-ประเภท")); // เก sorts with ก, before ข
        await AddAsync(start, a, 1m);
        await AddAsync(start, b, 2m);

        await using var scope = db.Scope();
        Assert.Equal([b, a], (await scope.Get<ExpenseLineDirectory>().SummaryByTypeAsync(start, start)).Select(r => r.ExpenseTypeId));
    }
}
