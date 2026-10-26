using Lockte.Prod;
using Lockte.Web.Reports;
using Lockte.Web.Reports.Order;

namespace Lockte.Web.Tests;

/// <summary>รายการสต็อกคงเหลือตามกลุ่มสินค้า (RPT-11) and STOCK CARD (RPT-12) (#124) from the ledger of module ผลิต. Fictional data only.</summary>
[Collection(LocalDbCollection.Name)]
public sealed class StockReportTests(LocalDbFixture db)
{
    private static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    private async Task<T> AsAsync<T>(Func<TestScope, Task<T>> action)
    {
        await using var scope = db.Scope("store1");
        return await action(scope);
    }

    private async Task<int> ProductAsync(string code, string unit = "คู่")
    {
        var saved = await AsAsync(s => s.Get<ProductRegistry>().SaveAsync(new ProductInput(null, code, "สินค้ารายงานสต็อก", null, Unit: unit)));
        Assert.True(saved.Succeeded, string.Join(" ", saved.Errors));
        return saved.Id!.Value;
    }

    private async Task AddAsync(int product, decimal quantity, DateOnly date, string document, int line = 1, string? remark = null)
    {
        var saved = await AsAsync(s => s.Get<StockRegistry>().SaveAsync(new StockEntryInput(null, null, date, document, line, product, quantity, null, remark)));
        Assert.True(saved.Succeeded);
    }

    private static async Task<string> HtmlAsync<TView>(object report) where TView : Microsoft.AspNetCore.Components.IComponent
        => System.Net.WebUtility.HtmlDecode(await ReportBody.RenderAsync<TView>(new() { ["Report"] = report }));

    [Fact] // RPT-11: the products of the group with a balance that is not 0 (a negative one too), by code; the head names the group and the date; totals per unit
    public async Task The_group_report_lists_the_balances_that_are_not_zero()
    {
        var tag = Unique("ZG");
        var (a, b, zero, other) = (await ProductAsync(tag + "1"), await ProductAsync(tag + "2", "ตลับ"), await ProductAsync(tag + "3"), await ProductAsync(Unique("ZH")));
        var day = new DateOnly(2026, 9, 1);
        await AddAsync(a, 500, day, "stock");
        await AddAsync(a, 200, day.AddDays(2), "D69205");
        await AddAsync(a, -300, day.AddDays(3), "4343");
        await AddAsync(b, -25, day, "A1"); // negative: printed, not hidden
        await AddAsync(zero, 10, day, "A2");
        await AddAsync(zero, -10, day.AddDays(1), "A3");
        await AddAsync(other, 999, day, "A4");

        var report = await AsAsync(s => StockReports.BalanceAsync(s.Get<StockDirectory>(), s.Get<ProductDirectory>(), tag + "*", day));

        Assert.Equal([(tag + "1", 400m), (tag + "2", -25m)], report.Lines.Select(l => (l.ProductCode, l.Quantity)));
        var html = await HtmlAsync<StockBalanceView>(report);
        Assert.Contains($"กลุ่มสินค้า {tag}*", html);
        Assert.Contains("ยอด ณ วันที่", html);
        Assert.Contains("-25", html);
        Assert.DoesNotContain(tag + "3", html);
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(html, "class=\"total\"").Count); // a total for each unit
    }

    [Fact] // a group by its name: the head carries it
    public async Task The_group_name_is_in_the_head()
    {
        var tag = Unique("Z");
        var saved = await AsAsync(s => s.Get<ProductListRegistry>().SaveGroupAsync(null, tag + "*", "กลุ่มทดสอบสต็อก", null));
        Assert.True(saved.Succeeded);

        var report = await AsAsync(s => StockReports.BalanceAsync(s.Get<StockDirectory>(), s.Get<ProductDirectory>(), tag.ToLowerInvariant() + "*", new DateOnly(2026, 9, 1)));

        Assert.Equal("กลุ่มทดสอบสต็อก", report.GroupName);
        Assert.Contains("ไม่มีข้อมูล", await HtmlAsync<StockBalanceView>(report));
    }

    [Fact] // RPT-12: in (≥ 0) and out (< 0) apart, by date → number → line, the running balance; the total row; BR-006 #1 ends at 400
    public async Task The_stock_card_has_in_and_out_apart_with_a_running_balance()
    {
        var product = await ProductAsync(Unique("ZK"));
        var day = new DateOnly(2026, 9, 1);
        await AddAsync(product, -300, day.AddDays(3), "4343");
        await AddAsync(product, 200, day.AddDays(2), "D69205");
        await AddAsync(product, 500, day, "stock");
        await AddAsync(product, -50, day.AddDays(3), "4343", line: 2, remark: "เสียหาย");

        var card = await AsAsync(s => StockReports.CardAsync(s.Get<StockDirectory>(), s.Get<ProductDirectory>(), s.Get<IProductionEmployeeLookup>(), product));

        Assert.NotNull(card);
        Assert.Equal(["stock", "D69205", "4343", "4343"], card!.Lines.Select(l => l.DocumentNo));
        Assert.Equal([500m, 700m, 400m, 350m], card.Lines.Select(l => l.Balance));
        Assert.Equal((500m, 0m), (card.Lines[0].In, card.Lines[0].Out));
        Assert.Equal((0m, -300m), (card.Lines[2].In, card.Lines[2].Out));
        Assert.Equal((700m, -350m, 350m), (card.TotalIn, card.TotalOut, card.Balance));
        var html = await HtmlAsync<StockCardView>(card);
        Assert.Contains($"รหัส {card.ProductCode}", html);
        Assert.Contains("เสียหาย", html);
        Assert.Contains("รวม", html);
        Assert.Equal(350m, await AsAsync(s => s.Get<StockDirectory>().GetBalanceAsync(product))); // the card ends at the balance of the ledger
    }

    [Fact] // a product with no entries, and one that does not exist
    public async Task A_card_without_entries_prints_no_data_and_an_unknown_product_has_no_card()
    {
        var product = await ProductAsync(Unique("ZN"));

        var card = await AsAsync(s => StockReports.CardAsync(s.Get<StockDirectory>(), s.Get<ProductDirectory>(), s.Get<IProductionEmployeeLookup>(), product));
        var unknown = await AsAsync(s => StockReports.CardAsync(s.Get<StockDirectory>(), s.Get<ProductDirectory>(), s.Get<IProductionEmployeeLookup>(), 999_999_999));

        Assert.Contains("ไม่มีข้อมูล", await HtmlAsync<StockCardView>(card!));
        Assert.Null(unknown);
    }
}
