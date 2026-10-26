using Lockte.Prod;
using Lockte.SharedKernel.Persistence;
using Lockte.Web.Audit;

namespace Lockte.Web.Tests;

/// <summary>module ผลิต (#103): migrates with the others and its saves land in ประวัติการแก้ไข like every module's.</summary>
[Collection(LocalDbCollection.Name)]
public sealed class ProdModuleTests(LocalDbFixture db)
{
    private static string NewCode() => "BF-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant() + "-1/1";

    private async Task<T> AsQaAsync<T>(Func<TestScope, Task<T>> action)
    {
        await using var scope = db.Scope("qa1");
        return await action(scope);
    }

    [Fact]
    public async Task Schema_prod_is_migrated_with_the_other_modules()
        => Assert.True(await ProdModule.IsUpToDateAsync(db.Services));

    [Fact]
    public async Task Editing_a_mold_records_old_and_new_value_of_each_changed_column()
    {
        var code = NewCode();
        var added = await AsQaAsync(s => s.Get<MoldRegistry>().SaveAsync(new MoldInput(null, code.ToLowerInvariant(), " แม่พิมพ์ก้ามเบรก ", null)));
        Assert.True(added.Succeeded);
        var mold = (await AsQaAsync(s => s.Get<MoldDirectory>().FindByCodeAsync(code)))!;
        Assert.Equal((code, "แม่พิมพ์ก้ามเบรก"), (mold.Code, mold.Name));

        var edited = await AsQaAsync(s => s.Get<MoldRegistry>().SaveAsync(new MoldInput(mold.Id, code, "แม่พิมพ์ก้ามเบรกหลัง", mold.RowVersion)));
        Assert.True(edited.Succeeded);

        await using var scope = db.Scope();
        var rows = await scope.Get<HistoryService>().SearchAsync(new(Table: "แม่พิมพ์", RowKey: code));
        string?[][] expected =
        [
            [HistoryActions.Added, "รหัสแม่พิมพ์", null, code], [HistoryActions.Added, "ชื่อแม่พิมพ์", null, "แม่พิมพ์ก้ามเบรก"],
            [HistoryActions.Modified, "ชื่อแม่พิมพ์", "แม่พิมพ์ก้ามเบรก", "แม่พิมพ์ก้ามเบรกหลัง"],
        ];
        Assert.Equivalent(expected, rows.Select(r => new[] { r.Action, r.Column, r.OldValue, r.NewValue }), strict: true);
        Assert.All(rows, r => Assert.Equal("qa1", r.UserName));
    }

    [Fact]
    public async Task Duplicate_code_is_rejected_and_a_stale_edit_is_a_conflict()
    {
        var code = NewCode();
        await AsQaAsync(s => s.Get<MoldRegistry>().SaveAsync(new MoldInput(null, code, "ชุดแรก", null)));
        var mold = (await AsQaAsync(s => s.Get<MoldDirectory>().FindByCodeAsync(code)))!;

        var duplicate = await AsQaAsync(s => s.Get<MoldRegistry>().SaveAsync(new MoldInput(null, code, "ซ้ำ", null)));
        Assert.Equal([$"มีรหัสแม่พิมพ์ '{code}' อยู่แล้ว"], duplicate.Errors);

        await AsQaAsync(s => s.Get<MoldRegistry>().SaveAsync(new MoldInput(mold.Id, code, "แก้ครั้งแรก", mold.RowVersion)));
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            AsQaAsync(s => s.Get<MoldRegistry>().SaveAsync(new MoldInput(mold.Id, code, "แก้จากหน้าเก่า", mold.RowVersion))));
    }
}
