using Lockte.Purch;
using Lockte.SharedKernel.Persistence;
using Lockte.Web.Audit;

namespace Lockte.Web.Tests;

/// <summary>ทะเบียนวัตถุดิบ (#215, SCR-17, BR-001, BR-003): upper-case unique code, name required, main flag, history, rowversion.</summary>
[Collection(LocalDbCollection.Name)]
public sealed class MaterialTests(LocalDbFixture db)
{
    private static string NewCode() => "tm" + Guid.NewGuid().ToString("N")[..8];

    private async Task<T> AsBuyerAsync<T>(Func<TestScope, Task<T>> action)
    {
        await using var scope = db.Scope("buyer1");
        return await action(scope);
    }

    private Task<Lockte.SharedKernel.ServiceResult> SaveAsync(MaterialInput input) => AsBuyerAsync(s => s.Get<MaterialRegistry>().SaveAsync(input));

    private Task<MaterialDto?> FindAsync(string code) => AsBuyerAsync(s => s.Get<MaterialDirectory>().FindByCodeAsync(code));

    [Fact]
    public async Task A_code_is_stored_upper_case_and_a_blank_main_flag_means_no()
    {
        var code = NewCode();
        Assert.True((await SaveAsync(new MaterialInput(null, code, " อลูมิเนียมก้อน ", null, "กก.", Cost: 85.5m, BuyBackPrice: 3))).Succeeded);

        var material = (await FindAsync(code))!;
        Assert.Equal((code.ToUpperInvariant(), "อลูมิเนียมก้อน", "กก.", 85.5m, false, 3m), (material.Code, material.Name, material.Unit, material.Cost, material.IsMain, material.BuyBackPrice));
        Assert.DoesNotContain(await AsBuyerAsync(s => s.Get<MaterialDirectory>().GetAllAsync(mainOnly: true)), m => m.Code == material.Code);
    }

    [Fact]
    public async Task A_repeated_or_blank_code_and_a_blank_name_are_rejected_but_a_repeated_name_is_fine()
    {
        var code = NewCode();
        await SaveAsync(new MaterialInput(null, code, "ชื่อซ้ำได้", null, IsMain: true));

        var duplicate = await SaveAsync(new MaterialInput(null, code.ToUpperInvariant(), "ซ้ำรหัส", null));
        Assert.Equal([$"มีรหัสวัตถุดิบ '{code.ToUpperInvariant()}' อยู่แล้ว"], duplicate.Errors);
        Assert.Equal(["กรุณากรอกรหัสวัตถุดิบ", "กรุณากรอกชื่อวัตถุดิบ"], (await SaveAsync(new MaterialInput(null, " ", "", null))).Errors);
        Assert.Equal(["กรุณากรอกชื่อวัตถุดิบ"], (await SaveAsync(new MaterialInput(null, NewCode(), "  ", null))).Errors);
        Assert.True((await SaveAsync(new MaterialInput(null, NewCode(), "ชื่อซ้ำได้", null))).Succeeded); // DQ-02: names may repeat
        Assert.Contains(await AsBuyerAsync(s => s.Get<MaterialDirectory>().GetAllAsync(mainOnly: true)), m => m.Code == code.ToUpperInvariant());
    }

    [Fact]
    public async Task A_name_over_the_advised_length_is_saved_and_an_unknown_product_is_rejected()
    {
        var long56 = new string('ก', MaterialRegistry.NameAdvice + 10);
        Assert.True((await SaveAsync(new MaterialInput(null, NewCode(), long56, null))).Succeeded);
        var result = await SaveAsync(new MaterialInput(null, NewCode(), "x", null, ProductId: int.MaxValue));
        Assert.Equal(["ไม่พบสินค้าที่เลือก"], result.Errors);
    }

    [Fact]
    public async Task Editing_records_old_and_new_value_a_stale_edit_conflicts_and_delete_removes()
    {
        var code = NewCode();
        await SaveAsync(new MaterialInput(null, code, "เดิม", null, Cost: 10));
        var material = (await FindAsync(code))!;
        Assert.True((await SaveAsync(new MaterialInput(material.Id, code, "เดิม", material.RowVersion, Cost: 12, IsMain: true))).Succeeded);
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => SaveAsync(new MaterialInput(material.Id, code, "จากหน้าเก่า", material.RowVersion)));

        await using (var scope = db.Scope())
        {
            var rows = await scope.Get<HistoryService>().SearchAsync(new(Table: "วัตถุดิบ", RowKey: code.ToUpperInvariant()));
            Assert.Contains(rows, r => r is { Action: HistoryActions.Modified, Column: "ราคา", UserName: "buyer1" } && decimal.Parse(r.OldValue!, System.Globalization.CultureInfo.InvariantCulture) == 10m && decimal.Parse(r.NewValue!, System.Globalization.CultureInfo.InvariantCulture) == 12m);
            Assert.Contains(rows, r => r is { Action: HistoryActions.Modified, Column: "วัตถุดิบหลัก", OldValue: "ไม่ใช่", NewValue: "ใช่" });
        }
        var fresh = (await FindAsync(code))!;
        Assert.True(fresh.IsMain);
        Assert.True((await AsBuyerAsync(s => s.Get<MaterialRegistry>().DeleteAsync(fresh.Id, fresh.RowVersion))).Succeeded);
        Assert.Null(await FindAsync(code));
    }

    [Fact]
    public async Task The_code_groups_start_as_the_legacy_ones_and_a_filter_by_prefix_works()
    {
        Assert.Equal(16, MaterialDirectory.DefaultGroups.Count);
        var prefix = "zq" + Guid.NewGuid().ToString("N")[..4];
        await SaveAsync(new MaterialInput(null, prefix + "1", "ในกลุ่ม", null));
        await SaveAsync(new MaterialInput(null, "zx" + prefix + "2", "นอกกลุ่ม", null));
        var shown = await AsBuyerAsync(s => s.Get<MaterialDirectory>().GetAllAsync(prefix + "*"));
        Assert.Equal([(prefix + "1").ToUpperInvariant()], shown.Select(m => m.Code));
    }
}
