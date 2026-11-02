using Lockte.Cash;
using Lockte.SharedKernel.Persistence;
using Lockte.Web.Audit;

namespace Lockte.Web.Tests;

/// <summary>module เงินสดย่อย (#228): migrates with the others, and its saves land in ประวัติการแก้ไข like every module's.</summary>
[Collection(LocalDbCollection.Name)]
public sealed class CashModuleTests(LocalDbFixture db)
{
    [Fact]
    public async Task Schema_cash_is_migrated_with_the_other_modules()
        => Assert.True(await CashModule.IsUpToDateAsync(db.Services));

    [Fact]
    public async Task Saving_the_settings_records_old_and_new_value_in_history()
    {
        // The settings are one row shared by the whole database: make sure it exists, then change the number.
        await using (var scope = db.Scope("cs1"))
        {
            var current = await scope.Get<CashSettingsDirectory>().GetAsync();
            if (current.RowVersion is null) Assert.True((await scope.Get<CashSettingsRegistry>().SaveAsync(new(current.LastRemittedReceiptNo, null))).Succeeded);
        }
        CashSettingsDto saved;
        await using (var scope = db.Scope("cs1")) saved = await scope.Get<CashSettingsDirectory>().GetAsync();
        var number = saved.LastRemittedReceiptNo == "T001" ? "T002" : "T001";

        await using (var scope = db.Scope("cs2"))
            Assert.True((await scope.Get<CashSettingsRegistry>().SaveAsync(new(number, saved.RowVersion))).Succeeded);

        await using var read = db.Scope();
        var rows = await read.Get<HistoryService>().SearchAsync(new(Table: "ค่าตั้งค่าเงินสดย่อย", RowKey: "ค่าตั้งค่า"));
        var change = rows.Where(r => r.Action == HistoryActions.Modified && r.Column == "เลขที่ใบเสร็จสุดท้ายที่ตัดยอดนำส่งแล้ว" && r.UserName == "cs2")
            .OrderByDescending(r => r.Id).First();
        Assert.Equal(number, change.NewValue);
        Assert.Equal(saved.LastRemittedReceiptNo, change.OldValue);
    }

    [Fact]
    public async Task A_stale_settings_save_is_a_conflict_and_a_too_long_number_is_rejected()
    {
        CashSettingsDto first;
        await using (var scope = db.Scope()) first = await scope.Get<CashSettingsDirectory>().GetAsync();
        await using (var scope = db.Scope())
            if (first.RowVersion is null) Assert.True((await scope.Get<CashSettingsRegistry>().SaveAsync(new(first.LastRemittedReceiptNo, null))).Succeeded);
        await using (var scope = db.Scope()) first = await scope.Get<CashSettingsDirectory>().GetAsync();

        await using (var scope = db.Scope())
            Assert.True((await scope.Get<CashSettingsRegistry>().SaveAsync(new("A" + Guid.NewGuid().ToString("N")[..8], first.RowVersion))).Succeeded); // a value that really changes, or the rowversion does not move
        await using (var scope = db.Scope())
            await Assert.ThrowsAsync<ConcurrencyConflictException>(() => scope.Get<CashSettingsRegistry>().SaveAsync(new("B" + Guid.NewGuid().ToString("N")[..8], first.RowVersion)));

        await using var bad = db.Scope();
        Assert.False((await bad.Get<CashSettingsRegistry>().SaveAsync(new(new string('9', 51), null))).Succeeded);
    }
}
