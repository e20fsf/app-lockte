using Lockte.Purch;
using Lockte.SharedKernel.Persistence;
using Lockte.Web.Audit;

namespace Lockte.Web.Tests;

/// <summary>module จัดซื้อและวัตถุดิบ (#212): migrates with the others, and its saves land in ประวัติการแก้ไข like every module's.</summary>
[Collection(LocalDbCollection.Name)]
public sealed class PurchModuleTests(LocalDbFixture db)
{
    private static PurchSettingsInput Input(PurchSettingsDto from, decimal? vat = null, string? approver = null, IReadOnlyList<ResaleGroupDto>? groups = null)
        => new(vat ?? from.VatRate, from.SmallOrderLimit, approver ?? from.SmallOrderApprover, string.Join(",", from.NoCarryPrefixes),
            from.GradeA, from.GradeB, from.GradeC, groups ?? from.ResaleGroups, from.RowVersion);

    [Fact]
    public async Task Schema_purch_is_migrated_with_the_other_modules()
        => Assert.True(await PurchModule.IsUpToDateAsync(db.Services));

    [Fact]
    public void The_defaults_are_the_values_the_legacy_hard_coded()
    {
        Assert.Equal(0.07m, PurchSettingsDefaults.VatRate);
        Assert.Equal(5000m, PurchSettingsDefaults.SmallOrderLimit);
        Assert.Equal(["ba", "br", "ch", "sl", "sp"], PurchSettingsDefaults.ResaleGroups.Select(g => g.Prefix));
        Assert.Equal(["A", "B", "C", "D"], new[] { 80m, 70m, 60m, 59.99m }.Select(new PurchSettingsDto(0, 0, null, [], 80, 70, 60, [], null).GradeOf));
    }

    [Fact]
    public async Task Saving_the_settings_records_old_and_new_value_in_history()
    {
        // The settings are one row shared by the whole database: this test sets the VAT rate it needs and leaves the rest.
        await using (var scope = db.Scope("buyer1"))
        {
            var current = await scope.Get<PurchSettingsDirectory>().GetAsync();
            Assert.True((await scope.Get<PurchSettingsRegistry>().SaveAsync(Input(current, vat: 0.07m))).Succeeded);
        }
        PurchSettingsDto saved;
        await using (var scope = db.Scope("buyer1")) saved = await scope.Get<PurchSettingsDirectory>().GetAsync();
        Assert.NotNull(saved.RowVersion);
        var approver = "ผู้อนุมัติ " + Guid.NewGuid().ToString("N")[..6];

        await using (var scope = db.Scope("buyer2"))
            Assert.True((await scope.Get<PurchSettingsRegistry>().SaveAsync(Input(saved, approver: approver))).Succeeded);

        await using var read = db.Scope();
        var rows = await read.Get<HistoryService>().SearchAsync(new(Table: "ค่าตั้งค่าจัดซื้อ", RowKey: "ค่าตั้งค่า"));
        var change = Assert.Single(rows, r => r.Action == HistoryActions.Modified && r.Column == "ผู้อนุมัติใบสั่งซื้อเล็ก" && r.NewValue == approver);
        Assert.Equal("buyer2", change.UserName);
    }

    [Fact]
    public async Task A_stale_settings_save_is_a_conflict_and_bad_values_are_rejected()
    {
        PurchSettingsDto first;
        await using (var scope = db.Scope()) first = await scope.Get<PurchSettingsDirectory>().GetAsync();
        await using (var scope = db.Scope())
            if (first.RowVersion is null) Assert.True((await scope.Get<PurchSettingsRegistry>().SaveAsync(Input(first))).Succeeded);
        await using (var scope = db.Scope()) first = await scope.Get<PurchSettingsDirectory>().GetAsync();

        await using (var scope = db.Scope())
            Assert.True((await scope.Get<PurchSettingsRegistry>().SaveAsync(Input(first, approver: "คนแรก"))).Succeeded);
        await using (var scope = db.Scope())
            await Assert.ThrowsAsync<ConcurrencyConflictException>(() => scope.Get<PurchSettingsRegistry>().SaveAsync(Input(first, approver: "คนที่สองจากหน้าเก่า")));

        await using var bad = db.Scope();
        var current = await bad.Get<PurchSettingsDirectory>().GetAsync();
        var result = await bad.Get<PurchSettingsRegistry>().SaveAsync(Input(current, vat: 7m, groups: [new("ba", "x"), new("BA", "ซ้ำ")]));
        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Contains("VAT"));
        Assert.Contains(result.Errors, e => e.Contains("ซ้ำ"));
    }
}
