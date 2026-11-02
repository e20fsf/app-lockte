using Lockte.Calib;
using Lockte.Purch;

namespace Lockte.Web.Tests;

/// <summary>บริษัทที่สอบเทียบ (#197, SCR-06, BR-007): suppliers of the register of วัตถุดิบ, added through its public service.</summary>
[Collection(LocalDbCollection.Name)]
public sealed class CalibrationProviderTests(LocalDbFixture db)
{
    private static string NewName() => "บริษัทสอบเทียบ " + Guid.NewGuid().ToString("N")[..8];

    private async Task<T> AsQaAsync<T>(Func<TestScope, Task<T>> action)
    {
        await using var scope = db.Scope("qa1");
        return await action(scope);
    }

    private Task<Lockte.SharedKernel.ServiceResult> AddAsync(string? name) => AsQaAsync(s => s.Get<CalibrationProviderRegistry>().AddAsync(name));

    private Task<IReadOnlyList<CalibrationProviderDto>> ListAsync() => AsQaAsync(s => s.Get<CalibrationProviderDirectory>().ListAsync());

    [Fact] // BR-007 #1
    public async Task A_new_company_becomes_a_supplier_and_a_choice_in_order_of_name()
    {
        var late = "ฮ" + NewName();
        var early = "ก" + NewName();
        Assert.True((await AddAsync(late)).Succeeded);
        Assert.True((await AddAsync($"  {early}  ")).Succeeded);

        var list = await ListAsync();
        Assert.True(list.Select(p => p.Name).ToList().IndexOf(early) < list.Select(p => p.Name).ToList().IndexOf(late));
        var provider = list.Single(p => p.Name == early);
        Assert.StartsWith(CalibrationProviderRegistry.CodePrefix, provider.SupplierCode);
        var supplier = await AsQaAsync(s => s.Get<SupplierDirectory>().GetAsync(provider.SupplierId));
        Assert.Equal(early, supplier!.Name); // in the one register of suppliers
    }

    [Fact] // BR-007 #2: blank is not saved; Q-10: a repeated name is refused (case and blanks ignored)
    public async Task A_blank_or_repeated_name_is_refused()
    {
        var before = (await ListAsync()).Count;
        Assert.Equal(["กรุณากรอกชื่อบริษัทที่สอบเทียบ"], (await AddAsync("  ")).Errors);
        Assert.Equal(before, (await ListAsync()).Count);

        var name = "Quality Calibration " + Guid.NewGuid().ToString("N")[..6];
        Assert.True((await AddAsync(name)).Succeeded);
        var again = await AddAsync($" {name.ToUpperInvariant()} ");
        Assert.Equal([$"มีบริษัทที่สอบเทียบ '{name.ToUpperInvariant()}' อยู่แล้ว"], again.Errors);
        Assert.Single(await ListAsync(), p => p.Name == name);
    }

    [Fact] // a supplier that already exists is marked, not duplicated
    public async Task A_name_that_is_already_a_supplier_marks_that_supplier()
    {
        var name = NewName();
        var code = "SU" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var supplier = (await AsQaAsync(s => s.Get<SupplierRegistry>().SaveAsync(new SupplierInput(null, code, name, null)))).Id!.Value;

        Assert.True((await AddAsync(name)).Succeeded);

        var provider = Assert.Single(await ListAsync(), p => p.Name == name);
        Assert.Equal((supplier, code), (provider.SupplierId, provider.SupplierCode));
        Assert.Single(await AsQaAsync(s => s.Get<SupplierDirectory>().SearchAsync(name)));
    }

    [Fact]
    public async Task Only_marked_suppliers_are_calibration_companies()
    {
        var plain = (await AsQaAsync(s => s.Get<SupplierRegistry>().SaveAsync(new SupplierInput(null, "SP" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(), NewName(), null)))).Id!.Value;
        Assert.DoesNotContain(await ListAsync(), p => p.SupplierId == plain);
        Assert.Null(await AsQaAsync(s => s.Get<CalibrationProviderDirectory>().GetAsync(plain)));
    }
}
