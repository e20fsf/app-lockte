using Lockte.Org;
using Lockte.Purch;
using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;

namespace Lockte.Web.Tests;

/// <summary>ใบขออนุมัติสั่งซื้อ: ออกเลข (BR-010, AC-01..03, AC-34, Q-17) และกรอก (BR-011, BR-014, AC-04, AC-05).</summary>
[Collection(LocalDbCollection.Name)]
public sealed class PurchaseRequestTests(LocalDbFixture db)
{
    private static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..6];

    private static readonly DateOnly Oct2026 = new(2026, 10, 29);

    private async Task<T> AsBuyerAsync<T>(Func<TestScope, Task<T>> action)
    {
        await using var scope = db.Scope("buyer1");
        return await action(scope);
    }

    /// <summary>A new แผนก with the given prefix (upper case, unique per test).</summary>
    private async Task<int> DepartmentWithPrefixAsync(string prefix)
    {
        int id;
        await using (var scope = db.Scope("hr1"))
        {
            var org = scope.Get<OrgRegistry>();
            var section = (await org.SaveSectionAsync(new SectionInput(null, Unique("ฝ่าย"), 1, null))).Id!.Value;
            id = (await org.SaveDepartmentAsync(new DepartmentInput(null, Unique("D"), Unique("แผนก"), "d", 3, section, null, [], null))).Id!.Value;
        }
        Assert.True((await AsBuyerAsync(s => s.Get<RequestSetupRegistry>().SetPrefixAsync(id, prefix, null))).Succeeded);
        return id;
    }

    private static string NewPrefix() => "Z" + Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();

    private async Task<int> MaterialAsync(decimal cost, string name = "วัตถุดิบทดสอบ")
    {
        var code = Unique("rq").ToUpperInvariant();
        return (await AsBuyerAsync(s => s.Get<MaterialRegistry>().SaveAsync(new MaterialInput(null, code, name, null, "กก.", Cost: cost)))).Id!.Value;
    }

    private Task<ServiceResult> SaveAsync(string no, int departmentId, DateOnly date, params PurchaseRequestLineInput[] lines)
        => AsBuyerAsync(s => s.Get<PurchaseRequestRegistry>().SaveAsync(new PurchaseRequestInput(no, date, departmentId, null, "F", null, null, lines)));

    private Task<string?> ReserveAsync(int departmentId, DateOnly date)
        => AsBuyerAsync(s => s.Get<PurchaseRequestRegistry>().ReserveNumberAsync(departmentId, date));

    [Fact]
    public async Task The_next_number_is_one_after_the_highest_of_the_same_prefix_and_year_BR_010_1_AC_01()
    {
        var prefix = NewPrefix();
        var department = await DepartmentWithPrefixAsync(prefix);
        var material = await MaterialAsync(10);
        Assert.True((await SaveAsync($"{prefix}69-0086", department, Oct2026, new PurchaseRequestLineInput(material, 5))).Succeeded);

        Assert.Equal($"{prefix}69-0087", await ReserveAsync(department, Oct2026));
        Assert.Equal($"{prefix}69-0088", await ReserveAsync(department, Oct2026));
    }

    [Fact]
    public async Task A_new_year_starts_at_0001_BR_010_3_AC_02()
    {
        var prefix = NewPrefix();
        var department = await DepartmentWithPrefixAsync(prefix);
        var material = await MaterialAsync(10);
        await SaveAsync($"{prefix}68-0130", department, new DateOnly(2025, 12, 20), new PurchaseRequestLineInput(material, 1));

        Assert.Equal($"{prefix}69-0001", await ReserveAsync(department, new DateOnly(2026, 1, 5)));
    }

    [Fact]
    public async Task A_prefix_that_starts_another_departments_prefix_does_not_mix_numbers_AC_03()
    {
        var d = NewPrefix();
        var dm = d + "M";
        var dieCast = await DepartmentWithPrefixAsync(d);
        var molds = await DepartmentWithPrefixAsync(dm);
        var material = await MaterialAsync(10);
        await SaveAsync($"{d}69-0077", dieCast, Oct2026, new PurchaseRequestLineInput(material, 1));
        await SaveAsync($"{dm}69-0015", molds, Oct2026, new PurchaseRequestLineInput(material, 1));

        Assert.Equal($"{d}69-0078", await ReserveAsync(dieCast, Oct2026));
        Assert.Equal($"{dm}69-0016", await ReserveAsync(molds, Oct2026));
    }

    [Fact]
    public async Task The_year_is_the_buddhist_year_of_the_date_and_a_department_without_prefix_gets_no_number()
    {
        Assert.Equal(69, PurchaseRequestRegistry.ThaiYear2(Oct2026));
        Assert.Equal(70, PurchaseRequestRegistry.ThaiYear2(new DateOnly(2027, 1, 3)));
        int id;
        await using (var scope = db.Scope("hr1"))
        {
            var org = scope.Get<OrgRegistry>();
            var section = (await org.SaveSectionAsync(new SectionInput(null, Unique("ฝ่าย"), 1, null))).Id!.Value;
            id = (await org.SaveDepartmentAsync(new DepartmentInput(null, Unique("D"), Unique("แผนก"), "d", 3, section, null, [], null))).Id!.Value;
        }
        Assert.Null(await ReserveAsync(id, Oct2026)); // BR-010 #4: the user types the number
    }

    [Fact]
    public async Task January_2027_gives_70_0001_then_70_0002_never_the_same_number_again_Q_17()
    {
        var prefix = NewPrefix();
        var department = await DepartmentWithPrefixAsync(prefix);
        var jan2027 = new DateOnly(2027, 1, 4);
        Assert.Equal($"{prefix}70-0001", await ReserveAsync(department, jan2027));
        Assert.Equal($"{prefix}70-0002", await ReserveAsync(department, jan2027));
    }

    [Fact]
    public async Task Two_users_asking_at_the_same_time_get_different_numbers_AC_34()
    {
        var prefix = NewPrefix();
        var department = await DepartmentWithPrefixAsync(prefix);
        var numbers = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => ReserveAsync(department, Oct2026)));
        Assert.Equal(6, numbers.Distinct().Count());
        Assert.All(numbers, n => Assert.StartsWith($"{prefix}69-", n));
    }

    [Fact]
    public async Task A_number_cannot_repeat_and_can_be_typed_when_the_department_has_no_prefix()
    {
        var prefix = NewPrefix();
        var department = await DepartmentWithPrefixAsync(prefix);
        var material = await MaterialAsync(10);
        var no = $"{prefix}69-0001";
        Assert.True((await SaveAsync(no, department, Oct2026, new PurchaseRequestLineInput(material, 1))).Succeeded);
        Assert.Equal([$"มีเลขที่ใบขอซื้อ '{no}' อยู่แล้ว"], (await SaveAsync(no, department, Oct2026, new PurchaseRequestLineInput(material, 1))).Errors);
        Assert.Contains("กรุณากรอกเลขที่ใบขอซื้อ", (await SaveAsync(" ", department, Oct2026, new PurchaseRequestLineInput(material, 1))).Errors);
    }

    [Fact]
    public async Task Picking_a_material_fills_name_unit_and_latest_price_and_a_zero_quantity_line_is_not_kept_AC_04_AC_05()
    {
        var prefix = NewPrefix();
        var department = await DepartmentWithPrefixAsync(prefix);
        var r0019 = await MaterialAsync(96, "อลูมิเนียมก้อน");
        var w0365 = await MaterialAsync(20, "ลวด");
        var no = $"{prefix}69-0001";

        var saved = await SaveAsync(no, department, Oct2026, new PurchaseRequestLineInput(r0019, 3000), new PurchaseRequestLineInput(w0365, 0));
        Assert.True(saved.Succeeded);

        var request = (await AsBuyerAsync(s => s.Get<PurchaseRequestDirectory>().FindByNoAsync(no)))!;
        var line = Assert.Single(request.Lines);
        Assert.Equal(("อลูมิเนียมก้อน", "กก.", 96m, 288000m, 3000m), (line.MaterialName, line.Unit, line.UnitPrice, line.NetAmount, line.Quantity));
        Assert.Equal("F", request.PurchasingLocation);
    }

    [Fact]
    public async Task A_request_without_any_item_is_not_created_and_the_document_text_of_a_line_is_its_own_BR_014()
    {
        var prefix = NewPrefix();
        var department = await DepartmentWithPrefixAsync(prefix);
        var material = await MaterialAsync(50, "ทะเบียน");
        var empty = await SaveAsync($"{prefix}69-0001", department, Oct2026, new PurchaseRequestLineInput(material, 0));
        Assert.True(empty.Succeeded);
        Assert.Null(empty.Id);
        Assert.Null(await AsBuyerAsync(s => s.Get<PurchaseRequestDirectory>().FindByNoAsync($"{prefix}69-0001")));

        var no = $"{prefix}69-0002";
        await SaveAsync(no, department, Oct2026, new PurchaseRequestLineInput(material, 2, MaterialName: "ชื่อในใบ", Unit: "ถัง", UnitPrice: 55m, TankWeight: 25m));
        var line = (await AsBuyerAsync(s => s.Get<PurchaseRequestDirectory>().FindByNoAsync(no)))!.Lines.Single();
        Assert.Equal(("ชื่อในใบ", "ถัง", 110m, 25m), (line.MaterialName, line.Unit, line.NetAmount, line.TankWeight));
        Assert.Equal("ทะเบียน", (await AsBuyerAsync(s => s.Get<MaterialDirectory>().GetAsync(material)))!.Name); // the register is not touched
    }

    [Fact]
    public async Task Header_checks_and_a_used_material_or_prefix_cannot_be_removed()
    {
        var prefix = NewPrefix();
        var department = await DepartmentWithPrefixAsync(prefix);
        var material = await MaterialAsync(10);
        var line = new PurchaseRequestLineInput(material, 1);

        Assert.Contains("ไม่พบแผนกที่เลือก", (await SaveAsync($"{prefix}69-0001", int.MaxValue, Oct2026, line)).Errors);
        var noLocation = await AsBuyerAsync(s => s.Get<PurchaseRequestRegistry>().SaveAsync(new PurchaseRequestInput($"{prefix}69-0001", Oct2026, department, null, " ", null, null, [line])));
        Assert.Contains("กรุณาเลือกผู้จัดซื้อ", noLocation.Errors);
        Assert.Contains("ไม่พบวัตถุดิบที่เลือก", (await SaveAsync($"{prefix}69-0001", department, Oct2026, new PurchaseRequestLineInput(int.MaxValue, 1))).Errors);

        Assert.True((await SaveAsync($"{prefix}69-0001", department, Oct2026, line)).Succeeded);
        var materialDto = (await AsBuyerAsync(s => s.Get<MaterialDirectory>().GetAsync(material)))!;
        Assert.False((await AsBuyerAsync(s => s.Get<MaterialRegistry>().DeleteAsync(materialDto.Id, materialDto.RowVersion))).Succeeded); // ข้อสมมติ ง

        // #216: the prefix of a department that has requests can be changed but not removed, and the old numbers stay as they are
        var loaded = (await AsBuyerAsync(s => s.Get<RequestSetupDirectory>().GetDepartmentsAsync())).Single(d => d.DepartmentId == department);
        var removed = await AsBuyerAsync(s => s.Get<RequestSetupRegistry>().SetPrefixAsync(department, " ", loaded.RowVersion));
        Assert.Equal(["แผนกนี้มีใบขอซื้อแล้ว ลบ prefix ไม่ได้ (เปลี่ยนเป็น prefix อื่นได้)"], removed.Errors);
        var changed = NewPrefix();
        Assert.True((await AsBuyerAsync(s => s.Get<RequestSetupRegistry>().SetPrefixAsync(department, changed, loaded.RowVersion))).Succeeded);
        Assert.NotNull(await AsBuyerAsync(s => s.Get<PurchaseRequestDirectory>().FindByNoAsync($"{prefix}69-0001")));
        Assert.Equal($"{changed}69-0001", await ReserveAsync(department, Oct2026));
    }
}
