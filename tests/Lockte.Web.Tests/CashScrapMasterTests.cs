using Lockte.Cash;
using Lockte.SharedKernel.Persistence;
using Lockte.Web.Audit;
using Lockte.Web.Reports;
using Lockte.Web.Reports.Cash;

namespace Lockte.Web.Tests;

/// <summary>ผู้ซื้อของเก่าและชนิดของเก่า (#230, SCR-07/08, BR-010, RPT-10/11). Names carry a random part: the database is shared by every test.</summary>
[Collection(LocalDbCollection.Name)]
public sealed class CashScrapMasterTests(LocalDbFixture db)
{
    private static string Unique(string name) => $"{name} {Guid.NewGuid():N}"[..(name.Length + 9)];

    private async Task<int> AddBuyerAsync(string name, string? address = null, string? tel = null, string user = "qa1")
    {
        await using var scope = db.Scope(user);
        var result = await scope.Get<ScrapBuyerRegistry>().SaveAsync(new(null, name, address, tel, null));
        Assert.True(result.Succeeded, string.Join(", ", result.Errors));
        return result.Id!.Value;
    }

    private async Task<ScrapBuyerDto> BuyerAsync(int id)
    {
        await using var scope = db.Scope();
        return (await scope.Get<ScrapBuyerDirectory>().GetAsync(id))!;
    }

    private async Task<int> AddItemAsync(string name, string? unit = null)
    {
        await using var scope = db.Scope();
        var result = await scope.Get<ScrapItemRegistry>().SaveAsync(new(null, name, unit, null));
        Assert.True(result.Succeeded, string.Join(", ", result.Errors));
        return result.Id!.Value;
    }

    [Fact]
    public async Task AC18_deleting_one_buyer_leaves_the_others()
    {
        var ids = new List<int>();
        for (var i = 0; i < 3; i++) ids.Add(await AddBuyerAsync(Unique("คุณตัวอย่าง"), "ที่อยู่ตัวอย่าง", "000-000-0000"));
        int before;
        await using (var scope = db.Scope()) before = (await scope.Get<ScrapBuyerDirectory>().ListAsync()).Count;
        var loaded = await BuyerAsync(ids[1]);

        await using (var scope = db.Scope())
            Assert.True((await scope.Get<ScrapBuyerRegistry>().DeleteAsync(ids[1], loaded.RowVersion)).Succeeded);

        await using var read = db.Scope();
        var after = await read.Get<ScrapBuyerDirectory>().ListAsync();
        Assert.Equal(before - 1, after.Count);
        Assert.DoesNotContain(after, b => b.Id == ids[1]);
        Assert.Contains(after, b => b.Id == ids[0]);
        Assert.Contains(after, b => b.Id == ids[2]);
    }

    [Fact]
    public async Task A_buyer_name_repeated_after_trimming_blanks_is_refused_and_a_name_is_required()
    {
        var name = Unique("คุณตัวอย่าง");
        await AddBuyerAsync(name);

        await using var scope = db.Scope();
        var result = await scope.Get<ScrapBuyerRegistry>().SaveAsync(new(null, $"  {name.Replace(" ", "   ")} ", null, null, null));
        Assert.False(result.Succeeded);
        Assert.Contains(name, result.Errors.Single());
        Assert.False((await scope.Get<ScrapBuyerRegistry>().SaveAsync(new(null, "  ", "ที่อยู่", null, null))).Succeeded);
    }

    [Fact]
    public async Task A_buyer_with_an_apostrophe_and_several_phone_numbers_is_saved_as_typed()
    {
        var id = await AddBuyerAsync(Unique("Mr. O'Neil"), null, "000-111-1111, 000-222-2222");

        var saved = await BuyerAsync(id);
        Assert.Contains("'", saved.Name);
        Assert.Null(saved.Address);
        Assert.Equal("000-111-1111, 000-222-2222", saved.Tel);
    }

    [Fact]
    public async Task Editing_a_buyer_changes_that_row_and_is_recorded_in_history()
    {
        var name = Unique("คุณตัวอย่าง");
        var id = await AddBuyerAsync(name, "ที่อยู่เดิม", "000-000-0001");
        var loaded = await BuyerAsync(id);

        await using (var scope = db.Scope("qa2"))
            Assert.True((await scope.Get<ScrapBuyerRegistry>().SaveAsync(new(id, name, "ที่อยู่ใหม่", "000-000-0001", loaded.RowVersion))).Succeeded);

        Assert.Equal("ที่อยู่ใหม่", (await BuyerAsync(id)).Address);
        await using var read = db.Scope();
        var rows = await read.Get<HistoryService>().SearchAsync(new(Table: "ผู้ซื้อของเก่า", RowKey: name));
        var change = rows.Single(r => r.Action == HistoryActions.Modified && r.Column == "ที่อยู่" && r.UserName == "qa2");
        Assert.Equal("ที่อยู่เดิม", change.OldValue);
        Assert.Equal("ที่อยู่ใหม่", change.NewValue);
    }

    [Fact]
    public async Task A_stale_buyer_edit_is_a_conflict()
    {
        var name = Unique("คุณตัวอย่าง");
        var id = await AddBuyerAsync(name);
        var loaded = await BuyerAsync(id);
        await using (var scope = db.Scope())
            Assert.True((await scope.Get<ScrapBuyerRegistry>().SaveAsync(new(id, name, "ที่อยู่ A", null, loaded.RowVersion))).Succeeded);
        await using (var scope = db.Scope())
            await Assert.ThrowsAsync<ConcurrencyConflictException>(() => scope.Get<ScrapBuyerRegistry>().SaveAsync(new(id, name, "ที่อยู่ B", null, loaded.RowVersion)));
    }

    [Fact]
    public async Task A_scrap_kind_is_added_edited_refused_when_repeated_and_deleted()
    {
        var name = Unique("เศษเหล็ก");
        var id = await AddItemAsync(name, "กก.");

        await using (var scope = db.Scope())
        {
            var repeated = await scope.Get<ScrapItemRegistry>().SaveAsync(new(null, $" {name} ", "ถัง", null));
            Assert.False(repeated.Succeeded);
            Assert.False((await scope.Get<ScrapItemRegistry>().SaveAsync(new(null, "", "ถัง", null))).Succeeded);
        }

        ScrapItemDto loaded;
        await using (var scope = db.Scope()) loaded = (await scope.Get<ScrapItemDirectory>().GetAsync(id))!;
        await using (var scope = db.Scope("qa2"))
            Assert.True((await scope.Get<ScrapItemRegistry>().SaveAsync(new(id, name, "ถัง", loaded.RowVersion))).Succeeded);
        await using (var scope = db.Scope()) loaded = (await scope.Get<ScrapItemDirectory>().GetAsync(id))!;
        Assert.Equal("ถัง", loaded.Unit);

        await using (var read = db.Scope())
        {
            var rows = await read.Get<HistoryService>().SearchAsync(new(Table: "ชนิดของเก่า", RowKey: name));
            var change = rows.Single(r => r.Action == HistoryActions.Modified && r.Column == "หน่วย" && r.UserName == "qa2");
            Assert.Equal("กก.", change.OldValue);
            Assert.Equal("ถัง", change.NewValue);
        }

        await using (var scope = db.Scope())
            Assert.True((await scope.Get<ScrapItemRegistry>().DeleteAsync(id, loaded.RowVersion)).Succeeded);
        await using var gone = db.Scope();
        Assert.Null(await gone.Get<ScrapItemDirectory>().GetAsync(id));
    }
}

/// <summary>Parts of ผู้ซื้อของเก่า/ชนิดของเก่า that need no database: the two prints.</summary>
public sealed class CashScrapListReportTests
{
    [Fact]
    public async Task The_buyer_print_numbers_the_rows_and_leaves_a_missing_address_empty()
    {
        var rows = new ScrapBuyerDto[] { new(1, "คุณตัวอย่าง ก", null, "000-000-0000", [1]), new(2, "คุณตัวอย่าง ข", "ที่อยู่ตัวอย่าง", null, [1]) };
        var body = System.Net.WebUtility.HtmlDecode(await ReportBody.RenderAsync<ScrapBuyerListView>(new() { ["Report"] = new ScrapBuyerListReport(rows) }));
        Assert.Contains("คุณตัวอย่าง ก", body);
        Assert.Contains("000-000-0000", body);
        Assert.Contains("ที่อยู่ตัวอย่าง", body);
        Assert.Contains("เบอร์โทรศัพท์", body);
        Assert.Contains("ไม่มีข้อมูล", await ReportBody.RenderAsync<ScrapBuyerListView>(new() { ["Report"] = new ScrapBuyerListReport([]) }));
    }

    [Fact]
    public async Task The_item_print_shows_name_and_unit()
    {
        var rows = new ScrapItemDto[] { new(1, "น้ำมันเก่า", "ถัง", [1]), new(2, "เศษเหล็ก", "กก.", [1]) };
        var body = System.Net.WebUtility.HtmlDecode(await ReportBody.RenderAsync<ScrapItemListView>(new() { ["Report"] = new ScrapItemListReport(rows) }));
        Assert.Contains("น้ำมันเก่า", body);
        Assert.Contains("ถัง", body);
        Assert.Contains("หน่วย", body);
        Assert.Contains("ไม่มีข้อมูล", await ReportBody.RenderAsync<ScrapItemListView>(new() { ["Report"] = new ScrapItemListReport([]) }));
    }
}
