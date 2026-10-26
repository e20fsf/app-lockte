using Lockte.Prod;
using Lockte.Sales;
using Lockte.SharedKernel.Persistence;
using Lockte.Web.Audit;

namespace Lockte.Web.Tests;

/// <summary>ทะเบียนสินค้า (SCR-03) and the lists it chooses from, through the public services of module ผลิต (#105).</summary>
[Collection(LocalDbCollection.Name)]
public sealed class ProductTests(LocalDbFixture db)
{
    private static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    private async Task<T> AsAsync<T>(Func<TestScope, Task<T>> action)
    {
        await using var scope = db.Scope("qa1");
        return await action(scope);
    }

    private async Task<int> SavedAsync(Task<Lockte.SharedKernel.ServiceResult> save)
    {
        var result = await save;
        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        return result.Id!.Value;
    }

    private Task<int> NewTypeAsync() => AsAsync(s => SavedAsync(s.Get<ProductListRegistry>().SaveProductTypeAsync(null, Unique("ประเภท"), null)));

    private Task<int> NewMoldAsync() => AsAsync(s => SavedAsync(s.Get<MoldRegistry>().SaveAsync(new MoldInput(null, Unique("M-"), "แม่พิมพ์", null))));

    private async Task<ProductDto> GetAsync(int id) => (await AsAsync(s => s.Get<ProductDirectory>().GetAsync(id)))!;

    [Fact]
    public async Task Product_keeps_every_field_and_the_customer_is_read_through_the_sales_service()
    {
        var typeId = await NewTypeAsync();
        var moldId = await NewMoldAsync();
        var customerCode = "40-" + Unique("");
        var customerId = await AsAsync(s => SavedAsync(s.Get<CustomerRegistry>().SaveAsync(new CustomerInput(null, customerCode, "ลูกค้าทดสอบ ก", null))));
        var modelId = await AsAsync(s => SavedAsync(s.Get<ProductListRegistry>().SaveModelAsync(null, Unique("C100 "), null)));
        var brakeId = await AsAsync(s => SavedAsync(s.Get<ProductListRegistry>().SaveBrakeTypeAsync(null, Unique("T"), Unique("ดรัม"), null)));
        var code = Unique("G5");

        var id = await AsAsync(s => SavedAsync(s.Get<ProductRegistry>().SaveAsync(new ProductInput(null, " " + code.ToLowerInvariant() + " ", " ผ้าเบรกทดสอบ ", null,
            Unit: "คู่", Pack: "กล่อง", AluminumWeight: 0.125m, BrakePadWeight: 0.2m, CorkWeight: 0.05m, SteelCost: 12.5m, PairWeight: 0.75m,
            SellingPrice: 99.5m, UnitCost: 40.25m, ProductTypeId: typeId, MoldId: moldId, CustomerId: customerId,
            ProducedOn: new DateOnly(2025, 6, 1), ModelId: modelId, Barcode: "88593144001", StandardNumber: "มอก.0000",
            BrakeTypeId: brakeId, Size: "10x20x5", Formula: "A", StandardModel: "C100"))));

        var p = await GetAsync(id);
        Assert.Equal((code, "ผ้าเบรกทดสอบ", "คู่", "กล่อง"), (p.Code, p.Name, p.Unit, p.Pack));
        Assert.Equal((0.125m, 0.2m, 0.05m, 12.5m, 0.75m, 99.5m, 40.25m),
            (p.AluminumWeight, p.BrakePadWeight, p.CorkWeight, p.SteelCost, p.PairWeight, p.SellingPrice, p.UnitCost));
        Assert.Equal((typeId, moldId, customerId, customerCode, "ลูกค้าทดสอบ ก"), (p.ProductTypeId, p.MoldId, p.CustomerId, p.CustomerCode, p.CustomerName));
        Assert.Equal((new DateOnly(2025, 6, 1), modelId, "88593144001", "มอก.0000", brakeId, "10x20x5", "A", "C100"),
            (p.ProducedOn, p.ModelId, p.Barcode, p.StandardNumber, p.BrakeTypeId, p.Size, p.Formula, p.StandardModel));
        Assert.NotNull(p.MoldCode);
        Assert.NotNull(p.ProductTypeName);
    }

    [Fact]
    public async Task Other_modules_find_and_search_products_by_code_or_name_and_by_customer()
    {
        var customerId = await AsAsync(s => SavedAsync(s.Get<CustomerRegistry>().SaveAsync(new CustomerInput(null, Unique("40-"), "ลูกค้า ข", null))));
        var name = Unique("ชื่อค้น");
        var code = Unique("SB");
        var id = await AsAsync(s => SavedAsync(s.Get<ProductRegistry>().SaveAsync(new ProductInput(null, code, name, null, Unit: "คู่", CustomerId: customerId))));
        await AsAsync(s => SavedAsync(s.Get<ProductRegistry>().SaveAsync(new ProductInput(null, Unique("SB"), "สินค้าอื่น", null))));

        var byCode = await AsAsync(s => s.Get<ProductDirectory>().FindByCodeAsync(code.ToLowerInvariant()));
        Assert.Equal(id, byCode!.Id);
        var byName = await AsAsync(s => s.Get<ProductDirectory>().SearchAsync(name));
        Assert.Equal([new ProductListItem(id, code, name, "คู่")], byName);
        var byCustomer = await AsAsync(s => s.Get<ProductDirectory>().SearchAsync(null, customerId));
        Assert.Equal([id], byCustomer.Select(i => i.Id));
    }

    [Fact]
    public async Task Product_needs_code_and_name_and_existing_choices_and_a_unique_code()
    {
        var code = Unique("G1");
        await AsAsync(s => SavedAsync(s.Get<ProductRegistry>().SaveAsync(new ProductInput(null, code, "แรก", null))));

        var duplicate = await AsAsync(s => s.Get<ProductRegistry>().SaveAsync(new ProductInput(null, code.ToLowerInvariant(), "ซ้ำ", null)));
        Assert.Equal([$"มีรหัสสินค้า '{code}' อยู่แล้ว"], duplicate.Errors);

        var blank = await AsAsync(s => s.Get<ProductRegistry>().SaveAsync(new ProductInput(null, " ", "", null)));
        Assert.Equivalent(new[] { "กรุณากรอกรหัสสินค้า", "กรุณากรอกชื่อสินค้า" }, blank.Errors);

        var unknown = await AsAsync(s => s.Get<ProductRegistry>().SaveAsync(new ProductInput(null, Unique("G1"), "x", null,
            AluminumWeight: -1, SellingPrice: -1, ProductTypeId: int.MaxValue, MoldId: int.MaxValue, CustomerId: int.MaxValue,
            ModelId: int.MaxValue, BrakeTypeId: int.MaxValue)));
        Assert.Equivalent(new[]
        {
            "น้ำหนักและค่าเหล็กต้องไม่ติดลบ", "ราคาขายและต้นทุนต่อหน่วยต้องไม่ติดลบ", "ไม่พบประเภทสินค้าที่เลือก", "ไม่พบรุ่นที่เลือก",
            "ไม่พบชนิดเบรกที่เลือก", "ไม่พบแม่พิมพ์ที่เลือก", "ไม่พบลูกค้าที่เลือก",
        }, unknown.Errors);
    }

    [Fact]
    public async Task A_stale_edit_is_a_conflict_and_the_history_records_each_changed_field()
    {
        var code = Unique("G2");
        var id = await AsAsync(s => SavedAsync(s.Get<ProductRegistry>().SaveAsync(new ProductInput(null, code, "เดิม", null, UnitCost: 10))));
        var p = await GetAsync(id);

        Assert.True((await AsAsync(s => s.Get<ProductRegistry>().SaveAsync(new ProductInput(id, code, "เดิม", p.RowVersion, UnitCost: 12.5m)))).Succeeded);

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            AsAsync(s => s.Get<ProductRegistry>().SaveAsync(new ProductInput(id, code, "จากหน้าเก่า", p.RowVersion))));
        await using var scope = db.Scope();
        var rows = await scope.Get<HistoryService>().SearchAsync(new(Table: "สินค้า", RowKey: code));
        Assert.Equivalent(new (string, string?, string?)[] { ("ต้นทุนต่อหน่วย", "10.0000", "12.5") },
            rows.Where(r => r.Action == HistoryActions.Modified).Select(r => (r.Column, r.OldValue, r.NewValue)));
    }

    [Fact]
    public async Task Deleting_a_product_works_and_a_mold_or_list_item_that_a_product_uses_cannot_be_deleted()
    {
        var typeId = await NewTypeAsync();
        var moldId = await NewMoldAsync();
        var mold = (await AsAsync(s => s.Get<MoldDirectory>().GetAsync(moldId)))!;
        var type = (await AsAsync(s => s.Get<ProductDirectory>().GetProductTypesAsync())).Single(t => t.Id == typeId);
        var code = Unique("G3");
        var id = await AsAsync(s => SavedAsync(s.Get<ProductRegistry>().SaveAsync(new ProductInput(null, code, "ใช้แม่พิมพ์", null, ProductTypeId: typeId, MoldId: moldId))));

        var moldRefused = await AsAsync(s => s.Get<MoldRegistry>().DeleteAsync(moldId, mold.RowVersion));
        Assert.Equal([$"ลบแม่พิมพ์ '{mold.Code}' ไม่ได้ เพราะมีรายการอื่นอ้างถึงอยู่"], moldRefused.Errors);
        var typeRefused = await AsAsync(s => s.Get<ProductListRegistry>().DeleteProductTypeAsync(typeId, type.RowVersion));
        Assert.Equal([$"ลบประเภทสินค้า '{type.Name}' ไม่ได้ เพราะมีสินค้าใช้อยู่"], typeRefused.Errors);

        var product = await GetAsync(id);
        Assert.True((await AsAsync(s => s.Get<ProductRegistry>().DeleteAsync(id, product.RowVersion))).Succeeded);
        Assert.Null(await AsAsync(s => s.Get<ProductDirectory>().FindByCodeAsync(code)));
        Assert.True((await AsAsync(s => s.Get<ProductRegistry>().DeleteAsync(id, product.RowVersion))).Succeeded); // already gone

        Assert.True((await AsAsync(s => s.Get<MoldRegistry>().DeleteAsync(moldId, mold.RowVersion))).Succeeded);
        Assert.True((await AsAsync(s => s.Get<ProductListRegistry>().DeleteProductTypeAsync(typeId, type.RowVersion))).Succeeded);
    }

    [Fact]
    public async Task Group_patterns_pick_the_groups_a_code_belongs_to_with_overlaps_kept()
    {
        var prefix = Unique("Z");
        await AsAsync(s => SavedAsync(s.Get<ProductListRegistry>().SaveGroupAsync(null, prefix + "1*", "ก้ามเบรก", null)));
        await AsAsync(s => SavedAsync(s.Get<ProductListRegistry>().SaveGroupAsync(null, prefix + "3*", "ก้ามเบรก", null)));
        await AsAsync(s => SavedAsync(s.Get<ProductListRegistry>().SaveGroupAsync(null, prefix + "EX", "สำเร็จรูปซื้อมาขาย", null)));
        await AsAsync(s => SavedAsync(s.Get<ProductListRegistry>().SaveGroupAsync(null, prefix + "#?", "สองตัวท้าย", null)));
        await AsAsync(s => SavedAsync(s.Get<ProductListRegistry>().SaveGroupAsync(null, prefix + "*", "ทั้งหมดของชุดนี้", null)));

        async Task<string[]> NamesAsync(string code)
            => (await AsAsync(s => s.Get<ProductDirectory>().GroupsOfAsync(code))).Where(g => g.Pattern.StartsWith(prefix)).Select(g => g.Name).Order().ToArray();

        Assert.Equal(new[] { "ก้ามเบรก", "ทั้งหมดของชุดนี้" }, await NamesAsync(prefix.ToLowerInvariant() + "1029"));
        Assert.Equal(new[] { "ก้ามเบรก", "ทั้งหมดของชุดนี้", "สองตัวท้าย" }.Order().ToArray(), await NamesAsync(prefix + "3A"));
        Assert.Equal(new[] { "ทั้งหมดของชุดนี้" }, await NamesAsync(prefix + "2900"));
        Assert.Equal(new[] { "สำเร็จรูปซื้อมาขาย", "ทั้งหมดของชุดนี้" }.Order().ToArray(), await NamesAsync(prefix + "EX"));
        Assert.Equal(new[] { "ทั้งหมดของชุดนี้" }, await NamesAsync(prefix + "EXTRA")); // EX has no * — exact only
        Assert.Equal(new[] { "ทั้งหมดของชุดนี้", "สองตัวท้าย" }, await NamesAsync(prefix + "5b"));
        Assert.Empty(await NamesAsync("Q" + prefix));
    }

    [Fact]
    public async Task Lists_reject_duplicates_blanks_and_stale_edits()
    {
        var name = Unique("รุ่น");
        var id = await AsAsync(s => SavedAsync(s.Get<ProductListRegistry>().SaveModelAsync(null, name, null)));
        var duplicate = await AsAsync(s => s.Get<ProductListRegistry>().SaveModelAsync(null, name, null));
        Assert.Equal([$"มีรุ่น '{name}' อยู่แล้ว"], duplicate.Errors);
        Assert.Equal(["กรุณากรอกชื่อรุ่น"], (await AsAsync(s => s.Get<ProductListRegistry>().SaveModelAsync(null, " ", null))).Errors);

        var brakeCode = Unique("T");
        var brakeId = await AsAsync(s => SavedAsync(s.Get<ProductListRegistry>().SaveBrakeTypeAsync(null, brakeCode.ToLowerInvariant(), Unique("ชนิด"), null)));
        var brake = (await AsAsync(s => s.Get<ProductDirectory>().GetBrakeTypesAsync())).Single(b => b.Id == brakeId);
        Assert.Equal(brakeCode, brake.Code);
        Assert.Equal([$"มีรหัสชนิดเบรก '{brakeCode}' อยู่แล้ว"], (await AsAsync(s => s.Get<ProductListRegistry>().SaveBrakeTypeAsync(null, brakeCode, Unique("ชนิด"), null))).Errors);

        var model = (await AsAsync(s => s.Get<ProductDirectory>().GetModelsAsync())).Single(m => m.Id == id);
        await AsAsync(s => SavedAsync(s.Get<ProductListRegistry>().SaveModelAsync(id, name + "x", model.RowVersion)));
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            AsAsync(s => s.Get<ProductListRegistry>().SaveModelAsync(id, name + "y", model.RowVersion)));
    }
}
