using Lockte.Cash;
using Lockte.SharedKernel.Persistence;
using Lockte.Web.Audit;
using Lockte.Web.Reports;
using Lockte.Web.Reports.Cash;

namespace Lockte.Web.Tests;

/// <summary>ยอดรอนำส่ง and ตัดยอดนำส่ง without a database: the order of numbers, and what RPT-07 and RPT-08 print.</summary>
public sealed class CashRemittanceReportTests
{
    [Theory] // BR-016, assumption ฉ: numbers are compared as numbers, year code first
    [InlineData("69028", "69029", -1)]
    [InlineData("69999", "691000", -1)] // 69999 comes before 691000, which text order would reverse
    [InlineData("691000", "69999", 1)]
    [InlineData("69029", "69029", 0)]
    [InlineData("68500", "69001", -1)]
    [InlineData("70001", "69999", 1)]
    public void Numbers_are_compared_by_year_code_then_running_number(string a, string b, int sign) => Assert.Equal(sign, Math.Sign(ScrapReceiptMath.Compare(a, b)));

    private static ScrapReceiptDto Receipt(string no, params decimal[] amounts)
        => new(1, no, new DateOnly(2026, 9, 8), "คุณตัวอย่าง", null, null, null, false, null, null, null, amounts.Select((a, i) => new ScrapReceiptLineDto(i, i + 1, "ของ", null, null, 1, a, a)).ToList(), [1]);

    [Fact] // RPT-08 shows only the numbers whose total is above 0
    public void The_unremitted_report_lists_only_receipts_with_an_amount()
    {
        var report = RemittanceReports.Unremitted([Receipt("69027", 1360m), Receipt("69030", 0m), Receipt("69028", 1000m)]);
        Assert.Equal(["69027", "69028"], report.Rows.Select(r => r.No));
        Assert.Equal(2360m, report.Total);
    }

    [Fact] // AC-13 / BR-016 #1: the summary of 69028–69029
    public async Task The_summary_prints_the_range_asked_for_each_receipt_and_the_total()
    {
        var remittance = new ScrapRemittanceDto(1, new DateOnly(2026, 10, 5), "69028", "69029", "qa1", [Receipt("69028", 1000m), Receipt("69029", 220m, 2050m)]);
        var report = RemittanceReports.Summary(remittance, new DateOnly(2026, 10, 5));
        Assert.Equal(3270m, report.Total);

        var html = System.Net.WebUtility.HtmlDecode(await ReportBody.RenderAsync<RemittanceSummaryView>(new() { ["Report"] = report }));
        foreach (var text in new[] { "ตั้งแต่เลขที่ 69028 ถึงเลขที่ 69029", "69028", "1,000.00", "69029", "2,270.00", "รวมทั้งหมด", "3,270.00", "ลำดับ", "จำนวนเงิน" })
            Assert.Contains(text, html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_unremitted_view_prints_rows_and_says_no_data_when_empty()
    {
        var html = System.Net.WebUtility.HtmlDecode(await ReportBody.RenderAsync<UnremittedView>(new() { ["Report"] = RemittanceReports.Unremitted([Receipt("69027", 1360m)]) }));
        Assert.Contains("69027", html, StringComparison.Ordinal);
        Assert.Contains("1,360.00", html, StringComparison.Ordinal);
        Assert.Contains("ไม่มีข้อมูล", await ReportBody.RenderAsync<UnremittedView>(new() { ["Report"] = RemittanceReports.Unremitted([]) }));
    }
}

/// <summary>ยอดรอนำส่งและตัดยอดนำส่ง (#240, SCR-12, BR-016). Each test has a year code of its own: the receipts of every test stay in the database they share.</summary>
[Collection(LocalDbCollection.Name)]
public sealed class CashRemittanceTests(LocalDbFixture db)
{
    private static DateOnly DateInYy(int yy) => CashScrapReceiptMathTests.DateInYy(yy);

    private async Task<ScrapReceiptDto> NewAsync(int yy, string no, decimal amount = 100m)
    {
        await using var scope = db.Scope("qa1");
        var today = DateInYy(yy);
        var result = await scope.Get<ScrapReceiptRegistry>().SaveAsync(new(null, no, today, "คุณตัวอย่าง ข", null, null, null, [new(1, "เศษเหล็ก", null, "กก.", 1, amount, null)], null, today));
        Assert.True(result.Succeeded, string.Join(", ", result.Errors));
        return (await scope.Get<ScrapReceiptDirectory>().GetAsync(result.Id!.Value))!;
    }

    private async Task<ScrapRemittanceResult> CloseAsync(string from, string to, string user = "qa1")
    {
        await using var scope = db.Scope(user);
        return await scope.Get<ScrapRemittanceRegistry>().CloseAsync(new(from, to, new DateOnly(2026, 10, 5)));
    }

    private async Task<IReadOnlyList<string>> WaitingAsync(string yearCode)
    {
        await using var scope = db.Scope();
        return (await scope.Get<ScrapRemittanceDirectory>().UnremittedAsync()).Where(r => r.No.StartsWith(yearCode, StringComparison.Ordinal)).Select(r => r.No).ToList();
    }

    [Fact] // AC-13
    public async Task AC13_closing_two_receipts_takes_them_out_of_the_unremitted_amount_and_only_them()
    {
        await NewAsync(41, "41027", 1360m);
        await NewAsync(41, "41028", 1000m);
        await NewAsync(41, "41029", 2270m);
        Assert.Equal(["41027", "41028", "41029"], await WaitingAsync("41"));
        decimal before;
        await using (var scope = db.Scope()) before = ScrapReceiptMath.Total((await scope.Get<ScrapRemittanceDirectory>().UnremittedWithAmountAsync()).Select(r => r.Total));

        var closed = await CloseAsync("41028", "41029", "qa6");

        Assert.True(closed.Succeeded, string.Join(", ", closed.Errors));
        Assert.Equal((2, 3270m), (closed.Count, closed.Total));
        Assert.Equal(["41027"], await WaitingAsync("41")); // the lower number is still waiting
        await using var read = db.Scope();
        var remittance = (await read.Get<ScrapRemittanceDirectory>().GetAsync(closed.Id!.Value))!;
        Assert.Equal(("41028", "41029", "qa6", 3270m), (remittance.FromNo, remittance.ToNo, remittance.MadeBy, remittance.Total));
        Assert.Equal(["41028", "41029"], remittance.Receipts.Select(r => r.No));
        Assert.Equal([1000m, 2270m], remittance.Receipts.Select(r => r.Total));
        Assert.Equal(before - 3270m, ScrapReceiptMath.Total((await read.Get<ScrapRemittanceDirectory>().UnremittedWithAmountAsync()).Select(r => r.Total))); // the report falls by what was closed
        Assert.Equal(("41028", "41029"), await read.Get<ScrapRemittanceDirectory>().LastRangeAsync());
    }

    [Fact] // BR-016 #2 differs: nothing waiting in the range changes nothing and makes no empty summary
    public async Task A_range_with_nothing_waiting_changes_nothing()
    {
        await NewAsync(42, "42001");
        var lastBefore = await LastRangeAsync();

        var result = await CloseAsync("42100", "42200");

        Assert.False(result.Succeeded);
        Assert.Contains("ไม่มีใบเสร็จรอนำส่ง", result.Errors.Single());
        Assert.Equal(["42001"], await WaitingAsync("42"));
        Assert.Equal(lastBefore, await LastRangeAsync());
    }

    private async Task<(string, string)?> LastRangeAsync()
    {
        await using var scope = db.Scope();
        return await scope.Get<ScrapRemittanceDirectory>().LastRangeAsync();
    }

    [Fact]
    public async Task A_range_over_receipts_some_already_closed_takes_only_the_waiting_and_closing_again_takes_nothing()
    {
        await NewAsync(43, "43001");
        await NewAsync(43, "43002");
        await NewAsync(43, "43003");
        Assert.True((await CloseAsync("43001", "43001")).Succeeded);

        var second = await CloseAsync("43001", "43003");
        Assert.True(second.Succeeded);
        Assert.Equal(2, second.Count); // 43002 and 43003, not 43001 again

        var third = await CloseAsync("43001", "43003");
        Assert.False(third.Succeeded);
        Assert.Empty(await WaitingAsync("43"));
    }

    [Fact]
    public async Task A_cancelled_receipt_is_not_taken_and_is_not_counted_as_waiting()
    {
        var cancelled = await NewAsync(44, "44001");
        await NewAsync(44, "44002");
        await using (var scope = db.Scope()) Assert.True((await scope.Get<ScrapReceiptRegistry>().CancelAsync(cancelled.Id, "ออกผิด", cancelled.RowVersion)).Succeeded);
        Assert.Equal(["44002"], await WaitingAsync("44"));

        var result = await CloseAsync("44001", "44002");

        Assert.Equal(1, result.Count);
        await using var read = db.Scope();
        Assert.Null((await read.Get<ScrapReceiptDirectory>().GetByNoAsync("44001"))!.RemittanceId);
    }

    [Fact] // numbers are compared as numbers: 691000 follows 69999
    public async Task The_range_follows_the_numeric_order_of_numbers()
    {
        await NewAsync(45, "45500");
        await NewAsync(45, "45999");
        await NewAsync(45, "451000");

        var result = await CloseAsync("45500", "45999");

        Assert.Equal(2, result.Count);
        Assert.Equal(["451000"], await WaitingAsync("45"));
        Assert.Equal(1, (await CloseAsync("45999", "451000")).Count); // a range across the thousand takes the one left
    }

    [Fact]
    public async Task A_range_the_wrong_way_round_or_not_numbers_is_refused()
    {
        Assert.False((await CloseAsync("46005", "46001")).Succeeded);
        Assert.False((await CloseAsync("46A01", "46002")).Succeeded);
        Assert.False((await CloseAsync("", "46002")).Succeeded);
    }

    [Fact] // a receipt closed into a การนำส่ง can still be edited — the closing does not change — but cannot be cancelled
    public async Task A_receipt_that_was_remitted_can_be_edited_but_not_cancelled()
    {
        var receipt = await NewAsync(47, "47001", 500m);
        var closed = await CloseAsync("47001", "47001");
        await using var scope = db.Scope("qa8");
        var remitted = (await scope.Get<ScrapReceiptDirectory>().GetAsync(receipt.Id))!;
        Assert.True(remitted.IsRemitted);
        Assert.Equal(new DateOnly(2026, 10, 5), remitted.RemittedOn);

        var cancel = await scope.Get<ScrapReceiptRegistry>().CancelAsync(receipt.Id, "ออกผิด", remitted.RowVersion);
        Assert.False(cancel.Succeeded);
        Assert.Contains("นำส่งสำนักงานแล้ว", cancel.Errors.Single());

        var edit = await scope.Get<ScrapReceiptRegistry>().SaveAsync(new(receipt.Id, receipt.No, receipt.Date, receipt.BuyerName, "ที่อยู่ใหม่", null, null,
            [new(1, "เศษเหล็ก", null, "กก.", 1, 600, null, remitted.Lines[0].Id)], remitted.RowVersion));
        Assert.True(edit.Succeeded, string.Join(", ", edit.Errors));
        var after = (await scope.Get<ScrapReceiptDirectory>().GetAsync(receipt.Id))!;
        Assert.Equal((closed.Id, 600m), (after.RemittanceId, after.Total)); // still in the same closing
        Assert.Empty(await WaitingAsync("47"));
    }

    [Fact]
    public async Task A_closing_is_recorded_in_history_with_the_receipts_it_took()
    {
        await NewAsync(48, "48001");
        var closed = await CloseAsync("48001", "48001", "qa9");

        await using var read = db.Scope();
        var history = read.Get<HistoryService>();
        var made = await history.SearchAsync(new(Table: "การนำส่งสำนักงาน", RowKey: "2026-10-05 48001-48001"));
        Assert.Contains(made, r => r.Action == HistoryActions.Added && r.Column == "ผู้ทำ" && r.NewValue == "qa9");
        var receipt = await history.SearchAsync(new(Table: "ใบเสร็จขายของเก่า", RowKey: "48001"));
        Assert.Contains(receipt, r => r.Action == HistoryActions.Modified && r.Column == "การนำส่ง (รหัสภายใน)" && r.UserName == "qa9" && r.NewValue == closed.Id!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task A_receipt_with_a_total_of_zero_is_closed_but_is_not_on_the_unremitted_report()
    {
        await using (var scope = db.Scope())
        {
            var today = DateInYy(49);
            var zero = await scope.Get<ScrapReceiptRegistry>().SaveAsync(new(null, "49001", today, "ก", null, null, null, [new(1, "ของแถม", null, null, 1, 0, null)], null, today));
            Assert.True(zero.Succeeded);
        }
        await using (var scope = db.Scope()) Assert.DoesNotContain(await scope.Get<ScrapRemittanceDirectory>().UnremittedWithAmountAsync(), r => r.No == "49001");
        Assert.Equal(["49001"], await WaitingAsync("49"));
        Assert.Equal(1, (await CloseAsync("49001", "49001")).Count);
    }
}
