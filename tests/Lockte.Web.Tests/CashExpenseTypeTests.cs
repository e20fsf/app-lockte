using Lockte.Cash;
using Lockte.SharedKernel.Persistence;
using Lockte.Web.Audit;
using Lockte.Web.Reports;
using Lockte.Web.Reports.Cash;

namespace Lockte.Web.Tests;

/// <summary>ประเภทค่าใช้จ่าย (#229, SCR-02, BR-001, RPT-05). Names carry a random part: the database is shared by every test.</summary>
[Collection(LocalDbCollection.Name)]
public sealed class CashExpenseTypeTests(LocalDbFixture db)
{
    private static string Unique(string name) => $"{name} {Guid.NewGuid():N}"[..Math.Min(name.Length + 9, 60)];

    private async Task<int> AddAsync(string name, string user = "qa1")
    {
        await using var scope = db.Scope(user);
        var result = await scope.Get<ExpenseTypeRegistry>().SaveAsync(new(null, name, null));
        Assert.True(result.Succeeded, string.Join(", ", result.Errors));
        return result.Id!.Value;
    }

    private async Task<ExpenseTypeDto> GetAsync(int id)
    {
        await using var scope = db.Scope();
        return (await scope.Get<ExpenseTypeDirectory>().GetAsync(id))!;
    }

    [Fact]
    public async Task AC17_an_added_type_is_on_the_pick_list()
    {
        var name = Unique("ค่าทางด่วน");
        var id = await AddAsync(name);

        await using var scope = db.Scope();
        Assert.Contains(await scope.Get<ExpenseTypeDirectory>().ListActiveAsync(), t => t.Id == id && t.Name == name);
    }

    [Fact]
    public async Task A_name_repeated_after_trimming_blanks_and_line_breaks_is_refused()
    {
        var name = Unique("ค่ารับรอง");
        await AddAsync(name);

        await using var scope = db.Scope();
        var messy = $"  {name.Replace(" ", "\r\n  ")}  ";
        var result = await scope.Get<ExpenseTypeRegistry>().SaveAsync(new(null, messy, null));
        Assert.False(result.Succeeded);
        Assert.Contains(name, result.Errors.Single());
        Assert.False((await scope.Get<ExpenseTypeRegistry>().SaveAsync(new(null, "   ", null))).Succeeded);
    }

    [Fact]
    public async Task Editing_renames_the_picked_type_and_is_recorded_in_history()
    {
        var old = Unique("ค่าขนส่ง");
        var id = await AddAsync(old);
        var loaded = await GetAsync(id);
        var renamed = Unique("ค่าขนส่งสินค้า");

        await using (var scope = db.Scope("qa2"))
            Assert.True((await scope.Get<ExpenseTypeRegistry>().SaveAsync(new(id, renamed, loaded.RowVersion))).Succeeded);

        Assert.Equal(renamed, (await GetAsync(id)).Name);
        await using var read = db.Scope();
        var rows = await read.Get<HistoryService>().SearchAsync(new(Table: "ประเภทค่าใช้จ่าย", RowKey: renamed));
        var change = rows.Single(r => r.Action == HistoryActions.Modified && r.Column == "ชื่อค่าใช้จ่าย" && r.UserName == "qa2");
        Assert.Equal(old, change.OldValue);
        Assert.Equal(renamed, change.NewValue);
    }

    [Fact]
    public async Task A_stale_edit_is_a_conflict()
    {
        var id = await AddAsync(Unique("ค่าซ่อม"));
        var loaded = await GetAsync(id);
        await using (var scope = db.Scope())
            Assert.True((await scope.Get<ExpenseTypeRegistry>().SaveAsync(new(id, Unique("ค่าซ่อมแซม"), loaded.RowVersion))).Succeeded);
        await using (var scope = db.Scope())
            await Assert.ThrowsAsync<ConcurrencyConflictException>(() => scope.Get<ExpenseTypeRegistry>().SaveAsync(new(id, Unique("ค่าซ่อมบำรุง"), loaded.RowVersion)));
    }

    [Fact]
    public async Task A_type_no_line_uses_is_deleted()
    {
        var id = await AddAsync(Unique("ค่าลบ"));
        var loaded = await GetAsync(id);

        await using (var scope = db.Scope())
            Assert.Equal(ExpenseTypeRemoval.Deleted, await scope.Get<ExpenseTypeRegistry>().RemoveAsync(id, loaded.RowVersion));
        await using var read = db.Scope();
        Assert.Null(await read.Get<ExpenseTypeDirectory>().GetAsync(id));
    }

    [Fact]
    public async Task A_retired_type_leaves_the_pick_list_but_stays_readable_and_can_be_brought_back()
    {
        var name = Unique("ค่าเลิกใช้");
        var id = await AddAsync(name);
        var loaded = await GetAsync(id);

        await using (var scope = db.Scope())
            Assert.True((await scope.Get<ExpenseTypeRegistry>().SetActiveAsync(id, false, loaded.RowVersion)).Succeeded);

        await using (var scope = db.Scope())
        {
            Assert.DoesNotContain(await scope.Get<ExpenseTypeDirectory>().ListActiveAsync(), t => t.Id == id);
            Assert.Contains(await scope.Get<ExpenseTypeDirectory>().ListAllAsync(), t => t.Id == id && !t.IsActive);
            Assert.Equal(name, (await scope.Get<ExpenseTypeDirectory>().GetAsync(id))!.Name);
        }

        var retired = await GetAsync(id);
        await using (var scope = db.Scope())
            Assert.True((await scope.Get<ExpenseTypeRegistry>().SetActiveAsync(id, true, retired.RowVersion)).Succeeded);
        await using var read = db.Scope();
        Assert.Contains(await read.Get<ExpenseTypeDirectory>().ListActiveAsync(), t => t.Id == id);
    }

    [Fact]
    public async Task Removing_a_type_that_is_gone_says_not_found()
    {
        await using var scope = db.Scope();
        Assert.Equal(ExpenseTypeRemoval.NotFound, await scope.Get<ExpenseTypeRegistry>().RemoveAsync(int.MaxValue, [1]));
    }
}

/// <summary>Parts of ประเภทค่าใช้จ่าย that need no database.</summary>
public sealed class CashExpenseTypeUnitTests
{
    [Theory]
    [InlineData("  ค่าขนส่ง ", "ค่าขนส่ง")]
    [InlineData("ค่าใช้จ่ายอบรม\r\nค่าใช้จ่ายการเดินทางในประเทศ", "ค่าใช้จ่ายอบรม ค่าใช้จ่ายการเดินทางในประเทศ")]
    [InlineData("(1 ฒง 494)", "(1 ฒง 494)")]
    [InlineData("a   b\tc", "a b c")]
    public void Names_are_trimmed_and_blank_runs_become_one_space(string input, string expected)
        => Assert.Equal(expected, ExpenseTypeRegistry.CleanName(input));

    [Fact]
    public void A_blank_name_is_nothing()
    {
        Assert.Null(ExpenseTypeRegistry.CleanName(null));
        Assert.Null(ExpenseTypeRegistry.CleanName(" \r\n "));
    }

    [Fact]
    public async Task The_print_numbers_the_names_and_says_no_data_when_empty()
    {
        var body = System.Net.WebUtility.HtmlDecode(await ReportBody.RenderAsync<ExpenseTypeListView>(new() { ["Report"] = new ExpenseTypeListReport(["ค่าขนส่ง", "ค่าทางด่วน"]) }));
        Assert.Contains("ค่าขนส่ง", body);
        Assert.Contains("ค่าทางด่วน", body);
        Assert.Contains("ลำดับ", body);
        Assert.Contains("ไม่มีข้อมูล", await ReportBody.RenderAsync<ExpenseTypeListView>(new() { ["Report"] = new ExpenseTypeListReport([]) }));
    }
}
