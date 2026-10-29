using Lockte.Purch;

namespace Lockte.Web.Tests;

/// <summary>
/// ตัดสต็อกวัตถุดิบ (#252, SCR-18, BR-042, AC-20, AC-42). A cut closes every open row on or before its date, in the whole test database — so these cuts are dated in 2031,
/// and each test only looks at the materials it makes itself.
/// </summary>
[Collection(LocalDbCollection.Name)]
public sealed class MaterialStockClosingTests(LocalDbFixture db)
{
    private static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..6];

    private async Task<T> AsManagerAsync<T>(Func<TestScope, Task<T>> action)
    {
        await using var scope = db.Scope("manager1");
        return await action(scope);
    }

    private async Task<int> MaterialAsync(string prefix = "cl")
        => (await AsManagerAsync(s => s.Get<MaterialRegistry>().SaveAsync(new MaterialInput(null, Unique(prefix).ToUpperInvariant(), "วัตถุดิบตัดสต็อก", null, "กก.", IsMain: true)))).Id!.Value;

    private Task AddAsync(int material, decimal quantity, DateOnly date, string? document = null)
        => AsManagerAsync(s => s.Get<StockLedger>().SaveAsync(new StockEntryInput(null, date, material, quantity, null, document)));

    private Task<decimal> BalanceAsync(int material) => AsManagerAsync(s => s.Get<StockDirectory>().GetBalanceAsync(material));

    private Task<IReadOnlyList<StockEntryDto>> OpenRowsAsync(int material) => AsManagerAsync(s => s.Get<StockDirectory>().GetEntriesAsync(material));

    private Task<StockClosingResult> CloseAsync(DateOnly date) => AsManagerAsync(s => s.Get<StockClosingRegistry>().CloseAsync(date));

    private static DateOnly Day(int month, int day) => new(2031, month, day);

    [Fact]
    public async Task The_balance_is_carried_forward_as_one_opening_row_and_the_period_is_closed_BR_042_1_AC_20()
    {
        var material = await MaterialAsync();
        await AddAsync(material, 1000m, Day(1, 31), "stock"); // ยกมา
        await AddAsync(material, 3356m, Day(2, 9));
        await AddAsync(material, -1138.6m, Day(2, 20), "002/0098");

        Assert.True((await CloseAsync(Day(2, 28))).Succeeded);

        Assert.Equal(3217.4m, await BalanceAsync(material));
        var opening = Assert.Single(await OpenRowsAsync(material));
        Assert.Equal((StockEntryKind.OpeningBalance, Day(2, 28), "stock", 3217.4m), (opening.Kind, opening.EntryDate, opening.DocumentNo, opening.Quantity));
        var kept = Assert.Single(await AsManagerAsync(s => s.Get<StockClosingDirectory>().GetBalancesAsync(Day(2, 28))), b => b.MaterialId == material);
        Assert.Equal(3217.4m, kept.Quantity);
    }

    [Fact]
    public async Task A_code_of_a_no_carry_prefix_is_cleared_BR_042_2_AC_42()
    {
        var spring = await MaterialAsync("sp");
        await AddAsync(spring, 500m, Day(3, 1), "stock");
        await AddAsync(spring, 30000m, Day(3, 5));
        await AddAsync(spring, -1000m, Day(3, 9), "002/0100");

        await CloseAsync(Day(3, 31));

        Assert.Equal(0m, await BalanceAsync(spring));
        Assert.Empty(await OpenRowsAsync(spring));
        Assert.DoesNotContain(await AsManagerAsync(s => s.Get<StockClosingDirectory>().GetBalancesAsync(Day(3, 31))), b => b.MaterialId == spring);
    }

    [Fact]
    public async Task Cutting_the_same_date_again_gives_the_same_balance_BR_042_3()
    {
        var material = await MaterialAsync();
        await AddAsync(material, 800m, Day(4, 3));
        await AddAsync(material, -300m, Day(4, 12), "10/1");

        await CloseAsync(Day(4, 30));
        var first = await BalanceAsync(material);
        await CloseAsync(Day(4, 30));

        Assert.Equal((500m, 500m), (first, await BalanceAsync(material)));
        Assert.Single(await OpenRowsAsync(material));
        Assert.Single(await AsManagerAsync(s => s.Get<StockClosingDirectory>().GetBalancesAsync(Day(4, 30))), b => b.MaterialId == material);
    }

    [Fact]
    public async Task A_row_dated_after_the_cut_is_not_closed_BR_042_4_and_a_later_cut_carries_the_earlier_one_on()
    {
        var material = await MaterialAsync();
        await AddAsync(material, 100m, Day(5, 10));
        await AddAsync(material, -40m, Day(7, 22), "9/9"); // after the cut

        await CloseAsync(Day(5, 31));
        Assert.Equal([100m, -40m], (await OpenRowsAsync(material)).Select(r => r.Quantity));
        Assert.Equal(60m, await BalanceAsync(material));

        await AddAsync(material, 10m, Day(6, 15));
        var second = await CloseAsync(Day(6, 30));
        Assert.True(second.Succeeded);
        Assert.Equal([110m, -40m], (await OpenRowsAsync(material)).Select(r => r.Quantity)); // 100 + 10 carried to 30 June, the July row still open
        Assert.Equal(70m, await BalanceAsync(material));
    }

    [Fact]
    public async Task A_date_before_the_last_cut_is_warned_about_and_still_cut()
    {
        var material = await MaterialAsync();
        await AddAsync(material, 5m, Day(8, 1));
        Assert.True((await CloseAsync(Day(12, 31))).Succeeded);

        await AddAsync(material, 7m, Day(8, 15));
        var early = await CloseAsync(Day(8, 31));

        Assert.True(early.Succeeded);
        Assert.True(early.BeforePreviousCut);
        Assert.Equal(Day(12, 31), early.PreviousCut);
        Assert.Contains(Day(12, 31), await AsManagerAsync(s => s.Get<StockClosingDirectory>().GetCutDatesAsync()));
    }

    [Fact]
    public async Task A_cut_needs_a_date()
        => Assert.Equal(["กรุณากำหนดวันที่ตัดสต็อก"], (await CloseAsync(default)).Errors);
}
