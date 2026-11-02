using System.Globalization;
using Lockte.Cash;
using Lockte.SharedKernel.Persistence;
using Lockte.Web.Audit;

namespace Lockte.Web.Tests;

/// <summary>The numbering and the money of a scrap receipt (#237, BR-011, BR-012, RND-A, RND-C) without a database.</summary>
public sealed class CashScrapReceiptMathTests
{
    /// <summary>A date whose Buddhist year ends in <paramref name="yy"/> (2569 → 69 is 2026).</summary>
    internal static DateOnly DateInYy(int yy) => new(2000 + (yy - 43 + 100) % 100, 6, 15);

    [Theory] // AC-19, BR-011 #1, #2, #4
    [InlineData("69029", 69, "69030")]
    [InlineData("69009", 69, "69010")]
    [InlineData("69029", 70, "70001")] // January 2570: a new year starts at 001
    [InlineData("69999", 69, "691000")] // BR-011 #3: past 999 the number grows a digit; nothing is glued to it, so nothing clashes
    [InlineData("", 69, "69001")]
    public void The_next_number_follows_the_highest_of_the_year(string highest, int yy, string expected)
        => Assert.Equal(expected, ScrapReceiptMath.Next(highest.Length == 0 ? [] : [highest, "68500"], DateInYy(yy)));

    [Fact]
    public void The_highest_is_found_as_a_number_not_as_text()
        => Assert.Equal("691001", ScrapReceiptMath.Next(["69999", "691000", "69002"], DateInYy(69)));

    [Fact] // Q-13: the year is Buddhist whatever the regional setting says
    public void The_year_code_does_not_depend_on_the_culture_of_the_thread()
    {
        var before = CultureInfo.CurrentCulture;
        try
        {
            foreach (var culture in new[] { "en-US", "th-TH", "ja-JP" })
            {
                CultureInfo.CurrentCulture = new CultureInfo(culture);
                Assert.Equal("69", ScrapReceiptMath.YearCode(new DateOnly(2026, 10, 5)));
                Assert.Equal("70", ScrapReceiptMath.YearCode(new DateOnly(2027, 1, 3)));
            }
        }
        finally { CultureInfo.CurrentCulture = before; }
    }

    [Theory]
    [InlineData("69030", true)]
    [InlineData("691000", true)]
    [InlineData("6903", false)] // shorter than 5
    [InlineData("69A30", false)]
    [InlineData(" 69030", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void A_number_typed_by_hand_is_digits_five_or_more(string? no, bool valid) => Assert.Equal(valid, ScrapReceiptMath.IsValidNumber(no));

    [Fact]
    public void Numbers_sort_by_year_and_then_by_the_running_number()
        => Assert.Equal(["68500", "69002", "69999", "691000"], new[] { "691000", "69002", "68500", "69999" }.OrderBy(ScrapReceiptMath.SortKey).ToArray());

    [Theory] // AC-11, RND-A of this program: banker's to two decimals
    [InlineData("100.125", "100.12")]
    [InlineData("100.135", "100.14")]
    [InlineData("2270", "2270.00")]
    [InlineData("99.999", "100.00")]
    public void The_total_is_the_sum_to_two_decimals_by_bankers_rounding(string sum, string expected)
        => Assert.Equal(decimal.Parse(expected, CultureInfo.InvariantCulture), ScrapReceiptMath.Total([decimal.Parse(sum, CultureInfo.InvariantCulture)]));

    [Fact] // AC-10, BR-012 #1: the five lines of receipt 69029
    public void AC10_the_sample_receipt_totals_2270_with_its_words()
    {
        var total = ScrapReceiptMath.Total([220m * 1, 1m * 120, 1.5m * 800, 110m * 3, 5m * 80]);
        Assert.Equal(2270m, total);
        Assert.Equal("(สองพันสองร้อยเจ็ดสิบบาทถ้วน)", ScrapReceiptMath.InWords(total));
    }

    [Fact] // BR-012 #2, BR-013 #2: a line without an item is skipped; a repeated ลำดับที่ is told, not dropped
    public void Lines_without_an_item_are_skipped_and_a_repeated_sequence_is_refused()
    {
        var errors = new List<string>();
        var lines = ScrapReceiptRegistry.BuildLines([new(1, "เศษเหล็ก", null, "กก.", 2, 10, null), new(2, null, null, null, 5, 5, null), new(null, "น้ำมันเก่า", null, "ถัง", 1, null, 300)], errors);
        Assert.Empty(errors);
        Assert.Equal([(1, "เศษเหล็ก", 20m), (2, "น้ำมันเก่า", 300m)], lines.Select(l => (l.Seq, l.ItemName, l.Amount))); // a skipped line uses no number

        var repeated = new List<string>();
        ScrapReceiptRegistry.BuildLines([new(1, "ก", null, null, 1, 1, null), new(1, "ข", null, null, 1, 1, null)], repeated);
        Assert.Contains("ซ้ำ", Assert.Single(repeated));

        var noQty = new List<string>();
        ScrapReceiptRegistry.BuildLines([new(1, "ก", null, null, 0, 1, null)], noQty);
        Assert.Contains("จำนวน", Assert.Single(noQty));

        var none = new List<string>();
        ScrapReceiptRegistry.BuildLines([new(1, null, null, null, 1, 1, null)], none);
        Assert.Contains("อย่างน้อยหนึ่ง", Assert.Single(none));
    }
}

/// <summary>ใบเสร็จขายของเก่า (#237, SCR-09). Each test works in a year code of its own: the numbers of a year run on in the database every test shares.</summary>
[Collection(LocalDbCollection.Name)]
public sealed class CashScrapReceiptTests(LocalDbFixture db)
{
    private static DateOnly DateInYy(int yy) => CashScrapReceiptMathTests.DateInYy(yy);

    private static IReadOnlyList<ScrapReceiptLineInput> Sample() =>
    [
        new(1, "เบ้าเหล็ก", null, "กก.", 220, 1, null), new(2, "ถังพลาสติก", null, "ใบ", 1, 120, null), new(3, "น้ำมันเก่า", null, "ถัง", 1.5m, 800, null),
        new(4, "พลาสติกสกินแพ็ค", null, "กก.", 110, 3, null), new(5, "ถังเหล็ก", null, "ใบ", 5, 80, null),
    ];

    private async Task<ScrapReceiptSaveResult> SaveAsync(DateOnly today, string? no = null, IReadOnlyList<ScrapReceiptLineInput>? lines = null, string user = "qa1", string buyer = "คุณตัวอย่าง ข")
    {
        await using var scope = db.Scope(user);
        return await scope.Get<ScrapReceiptRegistry>().SaveAsync(new(null, no, today, buyer, "ที่อยู่ตัวอย่าง", "000-000-0000", null, lines ?? Sample(), null, today));
    }

    [Fact]
    public async Task AC10_a_receipt_is_saved_with_its_lines_and_the_total_and_gets_the_next_number()
    {
        var today = DateInYy(21);
        var result = await SaveAsync(today);
        Assert.True(result.Succeeded, string.Join(", ", result.Errors));
        Assert.Equal("21001", result.No);

        await using var scope = db.Scope();
        var receipt = (await scope.Get<ScrapReceiptDirectory>().GetByNoAsync("21001"))!;
        Assert.Equal((today, "คุณตัวอย่าง ข", false), (receipt.Date, receipt.BuyerName, receipt.IsCancelled));
        Assert.Equal([1, 2, 3, 4, 5], receipt.Lines.Select(l => l.Seq));
        Assert.Equal(2270m, receipt.Total);
        Assert.Equal("(สองพันสองร้อยเจ็ดสิบบาทถ้วน)", receipt.TotalInWords);
        Assert.Equal("21002", await scope.Get<ScrapReceiptDirectory>().NextNumberAsync(today));
    }

    [Fact] // BR-011: AC-19 on saved receipts
    public async Task AC19_the_next_number_follows_the_highest_saved_in_the_year()
    {
        Assert.True((await SaveAsync(DateInYy(22), "22029")).Succeeded);
        await using var scope = db.Scope();
        var directory = scope.Get<ScrapReceiptDirectory>();
        Assert.Equal("22030", await directory.NextNumberAsync(DateInYy(22)));
        Assert.Equal("23001", await directory.NextNumberAsync(DateInYy(23))); // the next year starts again
        Assert.True((await SaveAsync(DateInYy(24), "24999")).Succeeded);
        Assert.Equal("241000", await scope.Get<ScrapReceiptDirectory>().NextNumberAsync(DateInYy(24)));
    }

    [Fact] // two people open the screen together and are offered the same number
    public async Task Two_people_offered_the_same_number_do_not_both_get_it()
    {
        var today = DateInYy(25);
        string offeredToA, offeredToB;
        await using (var a = db.Scope("qa1")) offeredToA = await a.Get<ScrapReceiptDirectory>().NextNumberAsync(today);
        await using (var b = db.Scope("qa2")) offeredToB = await b.Get<ScrapReceiptDirectory>().NextNumberAsync(today);
        Assert.Equal(offeredToA, offeredToB);

        var first = await SaveAsync(today, offeredToA, user: "qa1");
        var second = await SaveAsync(today, offeredToB, user: "qa2");

        Assert.True(first.Succeeded);
        Assert.False(second.Succeeded);
        Assert.Contains(offeredToA, second.Errors.Single());
        Assert.Equal("25002", second.NextNo);
        Assert.True((await SaveAsync(today, second.NextNo, user: "qa2")).Succeeded);

        await using var read = db.Scope();
        Assert.Equal(2, (await read.Get<ScrapReceiptDirectory>().ListNumbersAsync()).Count(r => r.No.StartsWith("25", StringComparison.Ordinal)));
    }

    [Fact] // without a typed number, a number taken meanwhile moves on by itself
    public async Task A_number_that_is_not_typed_is_taken_when_the_receipt_is_saved()
    {
        var today = DateInYy(26);
        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(i => SaveAsync(today, null, user: $"qa{i}")));
        Assert.All(results, r => Assert.True(r.Succeeded, string.Join(", ", r.Errors)));
        Assert.Equal(4, results.Select(r => r.No).Distinct().Count());
        Assert.Equal(["26001", "26002", "26003", "26004"], results.Select(r => r.No!).Order().ToArray());
    }

    [Fact]
    public async Task A_number_typed_by_hand_must_be_digits_and_unused()
    {
        var today = DateInYy(27);
        Assert.True((await SaveAsync(today, "27500")).Succeeded);

        var repeated = await SaveAsync(today, "27500");
        Assert.False(repeated.Succeeded);
        Assert.Contains("27500", repeated.Errors.Single());
        var wrong = await SaveAsync(today, "27A00");
        Assert.False(wrong.Succeeded);
        Assert.Contains("ตัวเลข", wrong.Errors.Single());
        Assert.False((await SaveAsync(today, "2750")).Succeeded);
    }

    [Fact] // BR-014: nothing is saved without a good "พิมพ์", so no number is used up and no half receipt is left
    public async Task A_receipt_that_is_refused_leaves_no_row_and_uses_no_number()
    {
        var today = DateInYy(28);
        var noLines = await SaveAsync(today, lines: [new(1, null, null, null, 1, 1, null)]);
        var repeatedSeq = await SaveAsync(today, lines: [new(1, "ก", null, null, 1, 1, null), new(1, "ข", null, null, 1, 1, null)]);
        var noQty = await SaveAsync(today, lines: [new(1, "ก", null, null, null, 1, null)]);
        Assert.All([noLines, repeatedSeq, noQty], r => Assert.False(r.Succeeded));

        await using var scope = db.Scope();
        Assert.DoesNotContain(await scope.Get<ScrapReceiptDirectory>().ListNumbersAsync(), r => r.No.StartsWith("28", StringComparison.Ordinal));
        Assert.Equal("28001", await scope.Get<ScrapReceiptDirectory>().NextNumberAsync(today));
    }

    [Fact] // Q-30: the receipt keeps its own copy, so deleting the buyer from the register changes nothing on it
    public async Task A_receipt_keeps_the_buyer_and_the_items_as_written_when_the_register_loses_them()
    {
        int buyerId;
        await using (var scope = db.Scope())
            buyerId = (await scope.Get<ScrapBuyerRegistry>().SaveAsync(new(null, "คุณตัวอย่าง " + Guid.NewGuid().ToString("N")[..8], "ที่อยู่ทะเบียน", "000-111-1111", null))).Id!.Value;
        var buyer = await Buyer(buyerId);
        var today = DateInYy(29);
        ScrapReceiptSaveResult saved;
        await using (var scope = db.Scope())
            saved = await scope.Get<ScrapReceiptRegistry>().SaveAsync(new(null, null, today, buyer.Name, buyer.Address, buyer.Tel, buyerId, Sample(), null, today));
        Assert.True(saved.Succeeded);

        await using (var scope = db.Scope()) Assert.True((await scope.Get<ScrapBuyerRegistry>().DeleteAsync(buyerId, buyer.RowVersion)).Succeeded);

        await using var read = db.Scope();
        var receipt = (await read.Get<ScrapReceiptDirectory>().GetAsync(saved.Id!.Value))!;
        Assert.Equal((buyer.Name, "ที่อยู่ทะเบียน", "000-111-1111", null), (receipt.BuyerName, receipt.BuyerAddress, receipt.BuyerTel, receipt.ScrapBuyerId));
    }

    private async Task<ScrapBuyerDto> Buyer(int id)
    {
        await using var scope = db.Scope();
        return (await scope.Get<ScrapBuyerDirectory>().GetAsync(id))!;
    }

    [Fact]
    public async Task Saving_a_receipt_is_recorded_in_history()
    {
        var result = await SaveAsync(DateInYy(30), user: "qa7");
        await using var read = db.Scope();
        var rows = await read.Get<HistoryService>().SearchAsync(new(Table: "ใบเสร็จขายของเก่า", RowKey: result.No));
        Assert.Contains(rows, r => r.Action == HistoryActions.Added && r.UserName == "qa7");
        Assert.NotEmpty(await read.Get<HistoryService>().SearchAsync(new(Table: "บรรทัดใบเสร็จขายของเก่า", RowKey: $"{result.No} ลำดับ 1")));
    }
}
