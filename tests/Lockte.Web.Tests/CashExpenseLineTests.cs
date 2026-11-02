using Lockte.Cash;
using Lockte.SharedKernel.Persistence;
using Lockte.Web.Audit;

namespace Lockte.Web.Tests;

/// <summary>Parts of ค่าใช้จ่ายเงินสดย่อย (#232) that need no database.</summary>
public sealed class CashExpenseLineMathTests
{
    [Theory] // AC-02 and BR-002 #1/#3 (assumption ก)
    [InlineData("2", "40", "80.00")]
    [InlineData("3", "40", "120.00")] // BR-002 #3 differs from the legacy: editing the quantity works the amount out again
    [InlineData("25.01", "38.5", "962.89")] // 962.885 → half up
    [InlineData("0.5", "0.01", "0.01")] // 0.005 → half up, not to even
    public void The_amount_is_quantity_times_price_to_two_decimals_half_up(string qty, string price, string expected)
        => Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture),
            ExpenseLineMath.AmountOf(decimal.Parse(qty, System.Globalization.CultureInfo.InvariantCulture), decimal.Parse(price, System.Globalization.CultureInfo.InvariantCulture)));

    [Fact] // BR-002 #2: fuel is typed as an amount against price 0
    public void A_price_of_zero_leaves_the_amount_to_be_typed() => Assert.Null(ExpenseLineMath.AmountOf(25.01m, 0m));
}

/// <summary>บรรทัดค่าใช้จ่ายเงินสดย่อย (#232, SCR-03, BR-002). Types, vouchers and dates carry random parts: the database is shared by every test.</summary>
[Collection(LocalDbCollection.Name)]
public sealed class CashExpenseLineTests(LocalDbFixture db)
{
    private static string Unique(string name) => $"{name} {Guid.NewGuid():N}"[..(name.Length + 9)];

    /// <summary>A day nobody else uses, so a list of that day holds only this test's lines.</summary>
    private static DateOnly Day() => new DateOnly(2040, 1, 1).AddDays(Random.Shared.Next(0, 3000));

    private async Task<int> NewTypeAsync(string user = "qa1")
    {
        await using var scope = db.Scope(user);
        var result = await scope.Get<ExpenseTypeRegistry>().SaveAsync(new(null, Unique("ประเภททดสอบ"), null));
        Assert.True(result.Succeeded);
        return result.Id!.Value;
    }

    private async Task<int> AddAsync(DateOnly date, int typeId, string voucher = "09/36", string? name = "หัวสปริงเกอร์ดับเพลิง", decimal? qty = 2, decimal? price = 40, decimal? amount = null, string? unit = "ชุด", string user = "qa1")
    {
        await using var scope = db.Scope(user);
        var result = await scope.Get<ExpenseLineRegistry>().SaveAsync(new(null, date, voucher, name, qty, unit, price, amount, typeId, null));
        Assert.True(result.Succeeded, string.Join(", ", result.Errors));
        return result.Id!.Value;
    }

    private async Task<ExpenseLineDto> GetAsync(int id)
    {
        await using var scope = db.Scope();
        return (await scope.Get<ExpenseLineDirectory>().GetAsync(id))!;
    }

    [Fact]
    public async Task AC02_BR002_1_a_price_works_the_amount_out()
    {
        var (day, type) = (Day(), await NewTypeAsync());
        var line = await GetAsync(await AddAsync(day, type));
        Assert.Equal(80m, line.Amount);
        Assert.Equal((day, "09/36", 2m, 40m), (line.Date, line.VoucherNo, line.Qty, line.UnitPrice));
    }

    [Fact]
    public async Task BR002_2_a_typed_amount_against_price_zero_and_a_typed_amount_over_a_price_are_kept()
    {
        var (day, type) = (Day(), await NewTypeAsync());
        var fuel = await GetAsync(await AddAsync(day, type, name: "น้ำมันดีเซล", qty: 25.01m, price: 0, amount: 1000, unit: "ลิตร"));
        var typedOver = await GetAsync(await AddAsync(day, type, name: "ปากกา", qty: 2, price: 40, amount: 75));
        var nothing = await GetAsync(await AddAsync(day, type, name: "ของฟรี", qty: 1, price: 0, amount: null));
        Assert.Equal((25.01m, 1000m), (fuel.Qty, fuel.Amount));
        Assert.Equal(75m, typedOver.Amount);
        Assert.Equal(0m, nothing.Amount);
    }

    [Fact]
    public async Task AC03_BR002_4_a_new_item_is_offered_next_time_with_its_unit()
    {
        var (day, type) = (Day(), await NewTypeAsync());
        var name = Unique("ของใหม่");
        await AddAsync(day, type, name: name, unit: "กล่อง");

        await using var scope = db.Scope();
        var suggestions = await scope.Get<ExpenseLineDirectory>().SuggestionsAsync();
        Assert.Single(suggestions, s => s.Name == name && s.Unit == "กล่อง");
        Assert.Equal(suggestions.Count, suggestions.Select(s => (s.Name, s.Unit)).Distinct().Count()); // each pair once
    }

    [Fact]
    public async Task The_lines_of_a_day_come_in_the_order_keyed_and_a_range_by_date_then_voucher()
    {
        var (day, type) = (Day(), await NewTypeAsync());
        var b = await AddAsync(day, type, voucher: "09/02", name: "ข");
        var a = await AddAsync(day, type, voucher: "09/01", name: "ก");
        var other = await AddAsync(day.AddDays(1), type, voucher: "09/00", name: "ค");

        await using var scope = db.Scope();
        var directory = scope.Get<ExpenseLineDirectory>();
        Assert.Equal([b, a], (await directory.ListByDateAsync(day)).Select(l => l.Id));
        Assert.Equal([a, b, other], (await directory.ListAsync(day, day.AddDays(1))).Select(l => l.Id));
        Assert.Equal([a, other], (await directory.GetManyAsync([other, a])).Select(l => l.Id));
    }

    [Fact]
    public async Task A_line_missing_a_required_field_is_refused_with_a_message_naming_it_and_nothing_is_deleted()
    {
        var (day, type) = (Day(), await NewTypeAsync());
        var kept = await AddAsync(day, type);

        await using var scope = db.Scope();
        var registry = scope.Get<ExpenseLineRegistry>();
        string One(Lockte.SharedKernel.ServiceResult r) { Assert.False(r.Succeeded); return string.Join("|", r.Errors); }
        Assert.Contains("วันที่", One(await registry.SaveAsync(new(null, null, "09/37", "ก", 1, null, 1, null, type, null))));
        Assert.Contains("เลขที่", One(await registry.SaveAsync(new(null, day, " ", "ก", 1, null, 1, null, type, null))));
        Assert.Contains("รายการ", One(await registry.SaveAsync(new(null, day, "09/37", null, 1, null, 1, null, type, null))));
        Assert.Contains("ประเภท", One(await registry.SaveAsync(new(null, day, "09/37", "ก", 1, null, 1, null, null, null)))); // AC-04 adjusted: told, not silently dropped
        Assert.Contains("จำนวน", One(await registry.SaveAsync(new(null, day, "09/37", "ก", null, null, 1, null, type, null))));
        Assert.Contains("ปุ่มลบ", One(await registry.SaveAsync(new(null, day, "09/37", "ก", 0, null, 1, null, type, null)))); // quantity 0 is not a way to delete
        Assert.Equal([kept], (await scope.Get<ExpenseLineDirectory>().ListByDateAsync(day)).Select(l => l.Id));
    }

    [Fact]
    public async Task A_retired_type_cannot_be_chosen_but_a_line_that_has_it_can_still_be_edited()
    {
        var (day, type) = (Day(), await NewTypeAsync());
        var other = await NewTypeAsync();
        var id = await AddAsync(day, type);
        ExpenseTypeDto loadedType;
        await using (var scope = db.Scope()) loadedType = (await scope.Get<ExpenseTypeDirectory>().GetAsync(type))!;
        await using (var scope = db.Scope())
            Assert.True((await scope.Get<ExpenseTypeRegistry>().SetActiveAsync(type, false, loadedType.RowVersion)).Succeeded);

        await using (var scope = db.Scope())
        {
            var refused = await scope.Get<ExpenseLineRegistry>().SaveAsync(new(null, day, "09/40", "ก", 1, null, 1, null, type, null));
            Assert.False(refused.Succeeded);
            Assert.Contains("เลิกใช้", refused.Errors.Single());
        }

        var line = await GetAsync(id);
        await using (var scope = db.Scope()) // edit the amount, the retired type stays
            Assert.True((await scope.Get<ExpenseLineRegistry>().SaveAsync(new(id, day, line.VoucherNo, line.Name, line.Qty, line.Unit, line.UnitPrice, 95m, type, line.RowVersion))).Succeeded);
        Assert.Equal(95m, (await GetAsync(id)).Amount);
        line = await GetAsync(id);
        await using (var scope = db.Scope()) // move it to another type
            Assert.True((await scope.Get<ExpenseLineRegistry>().SaveAsync(new(id, day, line.VoucherNo, line.Name, line.Qty, line.Unit, line.UnitPrice, 95m, other, line.RowVersion))).Succeeded);
    }

    [Fact]
    public async Task A_type_a_line_uses_is_retired_not_deleted_and_the_line_still_shows_its_name()
    {
        var (day, type) = (Day(), await NewTypeAsync());
        var id = await AddAsync(day, type);
        ExpenseTypeDto loaded;
        await using (var scope = db.Scope()) loaded = (await scope.Get<ExpenseTypeDirectory>().GetAsync(type))!;

        await using (var scope = db.Scope())
            Assert.Equal(ExpenseTypeRemoval.Retired, await scope.Get<ExpenseTypeRegistry>().RemoveAsync(type, loaded.RowVersion));

        await using var read = db.Scope();
        var retired = (await read.Get<ExpenseTypeDirectory>().GetAsync(type))!;
        Assert.False(retired.IsActive);
        Assert.DoesNotContain(await read.Get<ExpenseTypeDirectory>().ListActiveAsync(), t => t.Id == type);
        Assert.Equal(retired.Name, (await read.Get<ExpenseLineDirectory>().GetAsync(id))!.ExpenseTypeName);
    }

    [Fact]
    public async Task Renaming_a_type_renames_it_on_every_line()
    {
        var (day, type) = (Day(), await NewTypeAsync());
        var id = await AddAsync(day, type);
        ExpenseTypeDto loaded;
        await using (var scope = db.Scope()) loaded = (await scope.Get<ExpenseTypeDirectory>().GetAsync(type))!;
        var renamed = Unique("ชื่อใหม่");
        await using (var scope = db.Scope())
            Assert.True((await scope.Get<ExpenseTypeRegistry>().SaveAsync(new(type, renamed, loaded.RowVersion))).Succeeded);

        Assert.Equal(renamed, (await GetAsync(id)).ExpenseTypeName);
    }

    [Fact]
    public async Task A_stale_edit_is_a_conflict_and_a_delete_removes_the_line()
    {
        var (day, type) = (Day(), await NewTypeAsync());
        var id = await AddAsync(day, type);
        var loaded = await GetAsync(id);
        await using (var scope = db.Scope())
            Assert.True((await scope.Get<ExpenseLineRegistry>().SaveAsync(new(id, day, loaded.VoucherNo, loaded.Name, loaded.Qty, loaded.Unit, loaded.UnitPrice, 81m, type, loaded.RowVersion))).Succeeded);
        await using (var scope = db.Scope())
            await Assert.ThrowsAsync<ConcurrencyConflictException>(() => scope.Get<ExpenseLineRegistry>().SaveAsync(new(id, day, loaded.VoucherNo, loaded.Name, loaded.Qty, loaded.Unit, loaded.UnitPrice, 82m, type, loaded.RowVersion)));
        await using (var scope = db.Scope())
            await Assert.ThrowsAsync<ConcurrencyConflictException>(() => scope.Get<ExpenseLineRegistry>().DeleteAsync(id, loaded.RowVersion));

        var current = await GetAsync(id);
        await using (var scope = db.Scope())
            Assert.True((await scope.Get<ExpenseLineRegistry>().DeleteAsync(id, current.RowVersion)).Succeeded);
        await using var read = db.Scope();
        Assert.Null(await read.Get<ExpenseLineDirectory>().GetAsync(id));
    }

    [Fact]
    public async Task Add_edit_and_delete_are_recorded_in_history_column_by_column()
    {
        var (day, type) = (Day(), await NewTypeAsync());
        var voucher = Unique("H");
        var id = await AddAsync(day, type, voucher: voucher, name: "ของบันทึกประวัติ", user: "qa1");
        var loaded = await GetAsync(id);
        await using (var scope = db.Scope("qa2"))
            Assert.True((await scope.Get<ExpenseLineRegistry>().SaveAsync(new(id, day, voucher, loaded.Name, loaded.Qty, loaded.Unit, loaded.UnitPrice, 95m, type, loaded.RowVersion))).Succeeded);
        var current = await GetAsync(id);
        await using (var scope = db.Scope("qa3"))
            Assert.True((await scope.Get<ExpenseLineRegistry>().DeleteAsync(id, current.RowVersion)).Succeeded);

        await using var read = db.Scope();
        var key = $"{day.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)} {voucher} ของบันทึกประวัติ";
        var rows = await read.Get<HistoryService>().SearchAsync(new(Table: "ค่าใช้จ่ายเงินสดย่อย", RowKey: key));
        Assert.Contains(rows, r => r.Action == HistoryActions.Added && r.UserName == "qa1");
        var change = rows.Single(r => r.Action == HistoryActions.Modified && r.Column == "จำนวนเงิน" && r.UserName == "qa2");
        Assert.Equal(80m, decimal.Parse(change.OldValue!, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(95m, decimal.Parse(change.NewValue!, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Contains(rows, r => r.Action == HistoryActions.Deleted && r.UserName == "qa3");
    }
}
