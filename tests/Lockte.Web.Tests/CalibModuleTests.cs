using Lockte.Calib;
using Lockte.SharedKernel.Persistence;
using Lockte.Web.Audit;

namespace Lockte.Web.Tests;

/// <summary>module สอบเทียบ (#192): migrates with the others, and its saves land in ประวัติการแก้ไข like every module's.</summary>
[Collection(LocalDbCollection.Name)]
public sealed class CalibModuleTests(LocalDbFixture db)
{
    [Fact]
    public async Task Schema_calib_is_migrated_with_the_other_modules()
        => Assert.True(await CalibModule.IsUpToDateAsync(db.Services));

    [Fact]
    public void The_sticker_defaults_are_the_size_of_the_print_sample()
    {
        Assert.Equal(4.0m, CalibSettingsDefaults.StickerWidthCm);
        Assert.Equal(2.1m, CalibSettingsDefaults.StickerHeightCm);
    }

    [Fact]
    public async Task Saving_the_settings_records_old_and_new_value_in_history()
    {
        // The settings are one row shared by the whole database: make sure it exists, then change the width.
        await using (var scope = db.Scope("qa1"))
        {
            var current = await scope.Get<CalibSettingsDirectory>().GetAsync();
            if (current.RowVersion is null) Assert.True((await scope.Get<CalibSettingsRegistry>().SaveAsync(new(current.StickerWidthCm, current.StickerHeightCm, null))).Succeeded);
        }
        CalibSettingsDto saved;
        await using (var scope = db.Scope("qa1")) saved = await scope.Get<CalibSettingsDirectory>().GetAsync();
        var width = saved.StickerWidthCm == 3.4m ? 4.0m : 3.4m;

        await using (var scope = db.Scope("qa2"))
            Assert.True((await scope.Get<CalibSettingsRegistry>().SaveAsync(new(width, saved.StickerHeightCm, saved.RowVersion))).Succeeded);

        await using var read = db.Scope();
        var rows = await read.Get<HistoryService>().SearchAsync(new(Table: "ค่าตั้งค่าสอบเทียบ", RowKey: "ค่าตั้งค่า"));
        var change = rows.Where(r => r.Action == HistoryActions.Modified && r.Column == "ความกว้างสติกเกอร์ (ซม.)" && r.UserName == "qa2")
            .OrderByDescending(r => r.Id).First();
        Assert.Equal(saved.StickerWidthCm, decimal.Parse(change.OldValue!, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(width, decimal.Parse(change.NewValue!, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task A_stale_settings_save_is_a_conflict_and_bad_sizes_are_rejected()
    {
        CalibSettingsDto first;
        await using (var scope = db.Scope()) first = await scope.Get<CalibSettingsDirectory>().GetAsync();
        await using (var scope = db.Scope())
            if (first.RowVersion is null) Assert.True((await scope.Get<CalibSettingsRegistry>().SaveAsync(new(first.StickerWidthCm, first.StickerHeightCm, null))).Succeeded);
        await using (var scope = db.Scope()) first = await scope.Get<CalibSettingsDirectory>().GetAsync();

        await using (var scope = db.Scope())
            Assert.True((await scope.Get<CalibSettingsRegistry>().SaveAsync(new(first.StickerWidthCm, first.StickerHeightCm, first.RowVersion))).Succeeded);
        await using (var scope = db.Scope())
            await Assert.ThrowsAsync<ConcurrencyConflictException>(() => scope.Get<CalibSettingsRegistry>().SaveAsync(new(first.StickerWidthCm, first.StickerHeightCm, first.RowVersion)));

        await using var bad = db.Scope();
        var result = await bad.Get<CalibSettingsRegistry>().SaveAsync(new(0m, 30m, null));
        Assert.False(result.Succeeded);
        Assert.Equal(2, result.Errors.Count);
    }
}
