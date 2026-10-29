using Lockte.Purch;
using Lockte.SharedKernel.Persistence;
using Lockte.Web.Audit;

namespace Lockte.Web.Tests;

/// <summary>สมุดสต็อกวัตถุดิบ (#225, SCR-16, BR-041, BR-043, AC-40): the balance as the sum of open rows, manual rows keep their sign, history.</summary>
[Collection(LocalDbCollection.Name)]
public sealed class StockLedgerTests(LocalDbFixture db)
{
    private static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..6];

    private async Task<T> AsStoreAsync<T>(Func<TestScope, Task<T>> action)
    {
        await using var scope = db.Scope("store1");
        return await action(scope);
    }

    private async Task<int> MaterialAsync()
        => (await AsStoreAsync(s => s.Get<MaterialRegistry>().SaveAsync(new MaterialInput(null, Unique("sk").ToUpperInvariant(), "วัตถุดิบสต็อก", null, "กก.", IsMain: true)))).Id!.Value;

    private Task<Lockte.SharedKernel.ServiceResult> AddAsync(int material, decimal quantity, DateOnly? date = null, string? document = null, string? note = null)
        => AsStoreAsync(s => s.Get<StockLedger>().SaveAsync(new StockEntryInput(null, date ?? new DateOnly(2026, 9, 9), material, quantity, null, document, "1", null, note)));

    private Task<decimal> BalanceAsync(int material) => AsStoreAsync(s => s.Get<StockDirectory>().GetBalanceAsync(material));

    [Fact]
    public async Task The_balance_is_the_sum_of_the_rows_and_a_negative_one_is_shown_as_it_is_BR_041()
    {
        var material = await MaterialAsync();
        await AddAsync(material, 1000m, new DateOnly(2025, 10, 31), "stock");
        await AddAsync(material, 3356m, new DateOnly(2026, 9, 9));
        await AddAsync(material, -1138.6m, new DateOnly(2026, 9, 10), "002/0098");
        Assert.Equal(3217.4m, await BalanceAsync(material));
        var rows = await AsStoreAsync(s => s.Get<StockDirectory>().GetEntriesAsync(material));
        Assert.Equal([1000m, 3356m, -1138.6m], rows.Select(r => r.Quantity)); // by date
        Assert.Contains(await AsStoreAsync(s => s.Get<StockDirectory>().GetBalancesAsync()), b => b.MaterialId == material && b.Balance == 3217.4m);

        var overdrawn = await MaterialAsync();
        await AddAsync(overdrawn, -13250m, new DateOnly(2025, 10, 31), "stock"); // DB006: carried in negative
        Assert.Equal(-13250m, await BalanceAsync(overdrawn));
    }

    [Fact]
    public async Task A_manual_row_keeps_its_sign_and_is_kind_adjustment_AC_40()
    {
        var material = await MaterialAsync();
        await AddAsync(material, 100m);
        await AddAsync(material, -20m, note: "ตรวจนับได้น้อยกว่าระบบ");
        await AddAsync(material, 20m, note: "tr"); // typed 'tr' in the note: no longer taken as a kind (it was reversed at the next issue)
        var rows = await AsStoreAsync(s => s.Get<StockDirectory>().GetEntriesAsync(material));
        Assert.Equal(100m, await BalanceAsync(material));
        Assert.All(rows, r => Assert.Equal(StockEntryKind.Adjustment, r.Kind));
        Assert.Contains(rows, r => r is { Quantity: 20m, Note: "tr" });
    }

    [Fact]
    public async Task A_row_needs_a_register_material_a_quantity_and_a_date_and_nothing_checks_the_balance()
    {
        var material = await MaterialAsync();
        Assert.Contains("กรุณาเลือกรหัสวัตถุดิบที่มีในทะเบียน", (await AddAsync(int.MaxValue, 1m)).Errors); // DQ-07: no typo'd code
        Assert.Contains("กรุณากรอกจำนวน (บวก = เข้า, ลบ = ออก)", (await AddAsync(material, 0m)).Errors);
        Assert.Contains("กรุณากรอกวันที่", (await AsStoreAsync(s => s.Get<StockLedger>().SaveAsync(new StockEntryInput(null, default, material, 5m, null)))).Errors);
        Assert.True((await AddAsync(material, -500m)).Succeeded); // with nothing in stock: allowed (ข้อสมมติ ข)
        Assert.Equal(-500m, await BalanceAsync(material));
    }

    [Fact]
    public async Task Editing_and_deleting_a_row_change_the_balance_with_history_and_a_stale_edit_conflicts()
    {
        var material = await MaterialAsync();
        var id = (await AddAsync(material, 50m, document: "A-1")).Id!.Value;
        var row = (await AsStoreAsync(s => s.Get<StockDirectory>().GetAsync(id)))!;
        Assert.True((await AsStoreAsync(s => s.Get<StockLedger>().SaveAsync(new StockEntryInput(id, row.EntryDate, material, 80m, row.RowVersion, "A-1", "1")))).Succeeded);
        Assert.Equal(80m, await BalanceAsync(material));
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            AsStoreAsync(s => s.Get<StockLedger>().SaveAsync(new StockEntryInput(id, row.EntryDate, material, 99m, row.RowVersion, "A-1", "1"))));

        await using (var scope = db.Scope())
        {
            var history = await scope.Get<HistoryService>().SearchAsync(new(Table: "สมุดสต็อกวัตถุดิบ"));
            Assert.Contains(history, h => h is { Action: HistoryActions.Modified, Column: "จำนวน", OldValue: "50", NewValue: "80", UserName: "store1" });
        }

        var fresh = (await AsStoreAsync(s => s.Get<StockDirectory>().GetAsync(id)))!;
        Assert.True((await AsStoreAsync(s => s.Get<StockLedger>().DeleteAsync(id, fresh.RowVersion))).Succeeded);
        Assert.Equal(0m, await BalanceAsync(material));
        Assert.True((await AsStoreAsync(s => s.Get<StockLedger>().DeleteAsync(id, fresh.RowVersion))).Succeeded); // already gone
    }

    [Fact]
    public async Task A_material_with_stock_rows_cannot_be_deleted()
    {
        var material = await MaterialAsync();
        await AddAsync(material, 1m);
        var dto = (await AsStoreAsync(s => s.Get<MaterialDirectory>().GetAsync(material)))!;
        Assert.False((await AsStoreAsync(s => s.Get<MaterialRegistry>().DeleteAsync(dto.Id, dto.RowVersion))).Succeeded);
    }
}
