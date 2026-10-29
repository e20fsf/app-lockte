using Lockte.Purch;
using Lockte.SharedKernel.Persistence;
using Lockte.Web.Audit;

namespace Lockte.Web.Tests;

/// <summary>ทะเบียนผู้ขาย (#214, SCR-02, BR-002): upper-case unique code, name required, history, rowversion.</summary>
[Collection(LocalDbCollection.Name)]
public sealed class SupplierTests(LocalDbFixture db)
{
    private static string NewCode() => "tx-" + Guid.NewGuid().ToString("N")[..8];

    private async Task<T> AsBuyerAsync<T>(Func<TestScope, Task<T>> action)
    {
        await using var scope = db.Scope("buyer1");
        return await action(scope);
    }

    private Task<Lockte.SharedKernel.ServiceResult> SaveAsync(SupplierInput input) => AsBuyerAsync(s => s.Get<SupplierRegistry>().SaveAsync(input));

    [Fact]
    public async Task A_code_is_stored_upper_case_and_found_in_any_case()
    {
        var code = NewCode();
        Assert.True((await SaveAsync(new SupplierInput(null, " " + code + " ", " ผู้ขายทดสอบ ", null, Tel1: "02-000-0000", CreditDays: 30, Vat: true, UsedInProduction: true))).Succeeded);

        var supplier = (await AsBuyerAsync(s => s.Get<SupplierDirectory>().FindByCodeAsync(code)))!;
        Assert.Equal((code.ToUpperInvariant(), "ผู้ขายทดสอบ", "30", true), (supplier.Code, supplier.Name, supplier.CreditText, supplier.Vat));
    }

    [Fact]
    public async Task A_repeated_or_blank_code_and_a_blank_name_are_rejected()
    {
        var code = NewCode();
        await SaveAsync(new SupplierInput(null, code, "รายแรก", null));

        var duplicate = await SaveAsync(new SupplierInput(null, code.ToUpperInvariant(), "ซ้ำ", null));
        Assert.Equal([$"มีรหัสผู้ขาย '{code.ToUpperInvariant()}' อยู่แล้ว"], duplicate.Errors);
        Assert.Equal(["กรุณากรอกรหัสผู้ขาย", "กรุณากรอกชื่อผู้ขาย"], (await SaveAsync(new SupplierInput(null, "  ", "", null))).Errors);
        Assert.Contains("เครดิตต้องไม่ติดลบ (เงินสด = 0)", (await SaveAsync(new SupplierInput(null, NewCode(), "x", null, CreditDays: -1))).Errors);
    }

    [Fact]
    public async Task Cash_is_zero_days_and_editing_records_old_and_new_value()
    {
        var code = NewCode();
        await SaveAsync(new SupplierInput(null, code, "ผู้ขายเงินสด", null, CreditDays: 0));
        var supplier = (await AsBuyerAsync(s => s.Get<SupplierDirectory>().FindByCodeAsync(code)))!;
        Assert.Equal("เงินสด", supplier.CreditText);

        Assert.True((await SaveAsync(new SupplierInput(supplier.Id, code, "ผู้ขายเงินสด", supplier.RowVersion, CreditDays: 45))).Succeeded);

        await using var scope = db.Scope();
        var rows = await scope.Get<HistoryService>().SearchAsync(new(Table: "ผู้ขาย", RowKey: code.ToUpperInvariant()));
        var change = Assert.Single(rows, r => r.Action == HistoryActions.Modified);
        Assert.Equal(("เครดิต (วัน)", "0", "45", "buyer1"), (change.Column, change.OldValue, change.NewValue, change.UserName));
    }

    [Fact]
    public async Task A_stale_edit_is_a_conflict_and_a_deleted_supplier_is_gone()
    {
        var code = NewCode();
        await SaveAsync(new SupplierInput(null, code, "เดิม", null));
        var supplier = (await AsBuyerAsync(s => s.Get<SupplierDirectory>().FindByCodeAsync(code)))!;
        await SaveAsync(new SupplierInput(supplier.Id, code, "แก้ครั้งแรก", supplier.RowVersion));
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => SaveAsync(new SupplierInput(supplier.Id, code, "แก้จากหน้าเก่า", supplier.RowVersion)));

        var fresh = (await AsBuyerAsync(s => s.Get<SupplierDirectory>().FindByCodeAsync(code)))!;
        Assert.True((await AsBuyerAsync(s => s.Get<SupplierRegistry>().DeleteAsync(fresh.Id, fresh.RowVersion))).Succeeded);
        Assert.Null(await AsBuyerAsync(s => s.Get<SupplierDirectory>().FindByCodeAsync(code)));
    }

    [Fact]
    public async Task The_choice_lists_start_as_the_legacy_ones_and_can_be_edited()
    {
        Assert.Equal([30, 45, 60, 90, 120, 0], SupplierDirectory.DefaultCreditDays);
        Assert.Equal(4, SupplierDirectory.DefaultBusinessTypes.Count);
        // The lists are shared by the whole test database: this test only checks a save round-trips.
        var credit = await AsBuyerAsync(s => s.Get<SupplierDirectory>().GetCreditOptionsAsync());
        var types = await AsBuyerAsync(s => s.Get<SupplierDirectory>().GetBusinessTypesAsync());
        Assert.True((await AsBuyerAsync(s => s.Get<SupplierRegistry>().SaveOptionsAsync(credit, types))).Succeeded);
        Assert.Equal(credit.OrderBy(d => d == 0 ? int.MaxValue : d), await AsBuyerAsync(s => s.Get<SupplierDirectory>().GetCreditOptionsAsync()));
    }
}
