using Lockte.Hr;
using Lockte.Sales;
using Lockte.SharedKernel.Persistence;
using Lockte.Web.Audit;

namespace Lockte.Web.Tests;

/// <summary>ลูกค้า (SCR-02) and เขตการขาย/พนักงานขาย (SCR-07) through the public services of module ขายและส่งของ (#106).</summary>
[Collection(LocalDbCollection.Name)]
public sealed class CustomerTerritoryTests(LocalDbFixture db)
{
    private static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    private static int territoryCounter = Random.Shared.Next(0x100, 0x700);

    /// <summary>Territory codes are at most 4 characters.</summary>
    private static string TerritoryCode() => "X" + Interlocked.Increment(ref territoryCounter).ToString("X3");

    private async Task<T> AsAsync<T>(Func<TestScope, Task<T>> action)
    {
        await using var scope = db.Scope("sales1");
        return await action(scope);
    }

    private async Task<int> SavedAsync(Task<Lockte.SharedKernel.ServiceResult> save)
    {
        var result = await save;
        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        return result.Id!.Value;
    }

    private Task<int> NewTerritoryAsync(string? code = null, int? employeeId = null)
        => AsAsync(s => SavedAsync(s.Get<SalesTerritoryRegistry>().SaveAsync(new SalesTerritoryInput(null, code ?? TerritoryCode(), "เขตทดสอบ", employeeId, null))));

    private async Task<CustomerDto> GetAsync(int id) => (await AsAsync(s => s.Get<CustomerDirectory>().GetAsync(id)))!;

    private async Task<int> NewEmployeeAsync()
    {
        var code = "S" + Random.Shared.Next(100, 1000);
        // Employee codes are a letter and three digits and must be unique: retry a few times on a clash.
        for (var i = 0; i < 20; i++)
        {
            var result = await AsAsync(s => s.Get<EmployeeRegistry>().SaveAsync(new EmployeeInput { Code = code, Name = "พนักงานขายทดสอบ" }));
            if (result.Succeeded) return result.Id!.Value;
            code = "S" + Random.Shared.Next(100, 1000);
        }
        throw new InvalidOperationException("no free employee code");
    }

    [Fact]
    public async Task Customer_keeps_every_field_of_SCR_02()
    {
        var territoryId = await NewTerritoryAsync();
        var code = Unique("08-01-");
        var id = await AsAsync(s => SavedAsync(s.Get<CustomerRegistry>().SaveAsync(new CustomerInput(null, " " + code.ToLowerInvariant() + " ", " ร้านทดสอบ ", null,
            CreditDays: 60, Address: "1 หมู่ 2", District: "เมือง", Province: "จังหวัดทดสอบ", PostalCode: "10000", TransportName: "ขนส่ง ก",
            TransportAddress: "ถนนทดสอบ", TransportPhone: "02-000-0000", Phone1: "081-000-0001", Phone2: "081-000-0002",
            SalesTerritoryId: territoryId, Remark: "ร้านทดสอบ 1 หมู่ 2"))));

        var c = await GetAsync(id);
        Assert.Equal((code, "ร้านทดสอบ", 60, "60 วัน"), (c.Code, c.Name, c.CreditDays, c.CreditText));
        Assert.Equal(("1 หมู่ 2", "เมือง", "จังหวัดทดสอบ", "10000", "ขนส่ง ก", "ถนนทดสอบ", "02-000-0000"),
            (c.Address, c.District, c.Province, c.PostalCode, c.TransportName, c.TransportAddress, c.TransportPhone));
        Assert.Equal(("081-000-0001", "081-000-0002", territoryId, "ร้านทดสอบ 1 หมู่ 2"), (c.Phone1, c.Phone2, c.SalesTerritoryId, c.Remark));
        Assert.NotNull(c.SalesTerritoryCode);
        var found = await AsAsync(s => s.Get<CustomerDirectory>().SearchAsync("ร้านทดสอบ"));
        Assert.Contains(found, x => x.Id == id);
    }

    [Fact]
    public async Task Customer_with_a_duplicate_code_or_bad_values_is_rejected_and_a_stale_edit_is_a_conflict()
    {
        var code = Unique("09-01-");
        var id = await AsAsync(s => SavedAsync(s.Get<CustomerRegistry>().SaveAsync(new CustomerInput(null, code, "แรก", null))));

        var duplicate = await AsAsync(s => s.Get<CustomerRegistry>().SaveAsync(new CustomerInput(null, code.ToLowerInvariant(), "ซ้ำ", null)));
        Assert.Equal([$"มีรหัสลูกค้า '{code}' อยู่แล้ว"], duplicate.Errors);
        var bad = await AsAsync(s => s.Get<CustomerRegistry>().SaveAsync(new CustomerInput(null, " ", "", null, CreditDays: -1, SalesTerritoryId: int.MaxValue)));
        Assert.Equivalent(new[] { "กรุณากรอกรหัสลูกค้า", "กรุณากรอกชื่อลูกค้า", "เครดิตต้องเป็น 0 (เงินสด) ถึง 999 วัน", "ไม่พบเขตการขายที่เลือก" }, bad.Errors);

        var customer = await GetAsync(id);
        await AsAsync(s => SavedAsync(s.Get<CustomerRegistry>().SaveAsync(new CustomerInput(id, code, "แก้ครั้งแรก", customer.RowVersion))));
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            AsAsync(s => s.Get<CustomerRegistry>().SaveAsync(new CustomerInput(id, code, "จากหน้าเก่า", customer.RowVersion))));
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            AsAsync(s => s.Get<CustomerRegistry>().DeleteAsync(id, customer.RowVersion)));
    }

    [Theory]
    [InlineData("08-01-003", "08")]
    [InlineData("10-07-015", "00")] // ⚠ even though territory 10 exists (BR-023)
    [InlineData("30-01-013", "00")]
    [InlineData("00-00-001", "00")]
    [InlineData("09-99-999", "09")]
    public async Task Territory_proposed_for_a_new_customer_follows_BR_023(string customerCode, string expected)
    {
        // Territories 00–11 may or may not exist already in this database: make sure the expected one does.
        if (await AsAsync(s => s.Get<SalesTerritoryDirectory>().FindByCodeAsync(expected)) is null)
            await NewTerritoryAsync(expected);

        var proposed = await AsAsync(s => s.Get<SalesTerritoryDirectory>().DefaultForCustomerCodeAsync(customerCode));

        Assert.Equal(expected, proposed!.Code);
    }

    [Fact]
    public async Task No_territory_is_proposed_for_a_short_code_or_a_missing_territory()
    {
        Assert.Null(await AsAsync(s => s.Get<SalesTerritoryDirectory>().DefaultForCustomerCodeAsync("8")));
        Assert.Null(await AsAsync(s => s.Get<SalesTerritoryDirectory>().DefaultForCustomerCodeAsync("07-01-001"))); // no territory 07 here
    }

    [Theory]
    [InlineData("เงินสด", 0)]
    [InlineData(" สด ", 0)]
    [InlineData("60 วัน", 60)]
    [InlineData("60วัน", 60)]
    [InlineData("30", 30)]
    [InlineData("เครดิต 45 วัน", 45)]
    [InlineData("90 วันหลังวางบิล", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Legacy_credit_text_becomes_days_or_is_left_unconverted(string? text, int? expected)
        => Assert.Equal(expected, CreditTerm.Parse(text));

    [Fact]
    public void Credit_days_are_shown_as_cash_or_days()
    {
        Assert.Equal("เงินสด", CreditTerm.Format(0));
        Assert.Equal("30 วัน", CreditTerm.Format(30));
        Assert.Equal("", CreditTerm.Format(null));
    }

    [Fact]
    public async Task Territory_refers_to_an_employee_of_the_hr_register_and_may_have_none()
    {
        var employeeId = await NewEmployeeAsync();
        var withEmployee = await NewTerritoryAsync(employeeId: employeeId);
        var without = await NewTerritoryAsync();

        var all = await AsAsync(s => s.Get<SalesTerritoryDirectory>().GetAllAsync());
        var t = all.Single(x => x.Id == withEmployee);
        Assert.Equal((employeeId, "พนักงานขายทดสอบ"), (t.EmployeeId, t.EmployeeName));
        Assert.StartsWith("S", t.EmployeeCode);
        Assert.Null(all.Single(x => x.Id == without).EmployeeId);

        var unknown = await AsAsync(s => s.Get<SalesTerritoryRegistry>().SaveAsync(new SalesTerritoryInput(null, TerritoryCode(), "x", int.MaxValue, null)));
        Assert.Equal(["ไม่พบพนักงานขายที่เลือกในทะเบียนพนักงาน"], unknown.Errors);
    }

    [Fact]
    public async Task Territory_needs_unique_code_and_name_and_a_stale_edit_is_a_conflict()
    {
        var code = TerritoryCode();
        var id = await NewTerritoryAsync(code);
        var duplicate = await AsAsync(s => s.Get<SalesTerritoryRegistry>().SaveAsync(new SalesTerritoryInput(null, code, "ซ้ำ", null, null)));
        Assert.Equal([$"มีรหัสเขตการขาย '{code}' อยู่แล้ว"], duplicate.Errors);
        var blank = await AsAsync(s => s.Get<SalesTerritoryRegistry>().SaveAsync(new SalesTerritoryInput(null, "", " ", null, null)));
        Assert.Equivalent(new[] { "กรุณากรอกรหัสเขตการขาย", "กรุณากรอกชื่อเขตการขาย" }, blank.Errors);

        var territory = (await AsAsync(s => s.Get<SalesTerritoryDirectory>().GetAsync(id)))!;
        await AsAsync(s => SavedAsync(s.Get<SalesTerritoryRegistry>().SaveAsync(new SalesTerritoryInput(id, code, "แก้แล้ว", null, territory.RowVersion))));
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            AsAsync(s => s.Get<SalesTerritoryRegistry>().SaveAsync(new SalesTerritoryInput(id, code, "จากหน้าเก่า", null, territory.RowVersion))));
    }

    [Fact]
    public async Task Territory_used_by_a_customer_cannot_be_deleted_and_an_unused_customer_or_territory_can()
    {
        var territoryId = await NewTerritoryAsync();
        var territory = (await AsAsync(s => s.Get<SalesTerritoryDirectory>().GetAsync(territoryId)))!;
        var code = Unique("01-01-");
        var customerId = await AsAsync(s => SavedAsync(s.Get<CustomerRegistry>().SaveAsync(new CustomerInput(null, code, "ลูกค้าในเขต", null, SalesTerritoryId: territoryId))));
        var customer = await GetAsync(customerId);

        var refused = await AsAsync(s => s.Get<SalesTerritoryRegistry>().DeleteAsync(territoryId, territory.RowVersion));
        Assert.Equal([$"ลบเขตการขาย '{territory.Code}' ไม่ได้ เพราะมีลูกค้าหรือรายการอื่นอ้างถึงอยู่"], refused.Errors);

        Assert.True((await AsAsync(s => s.Get<CustomerRegistry>().DeleteAsync(customerId, customer.RowVersion))).Succeeded);
        Assert.Null(await AsAsync(s => s.Get<CustomerDirectory>().FindByCodeAsync(code)));
        Assert.True((await AsAsync(s => s.Get<CustomerRegistry>().DeleteAsync(customerId, customer.RowVersion))).Succeeded); // already gone
        Assert.True((await AsAsync(s => s.Get<SalesTerritoryRegistry>().DeleteAsync(territoryId, territory.RowVersion))).Succeeded);

        await using var scope = db.Scope();
        var rows = await scope.Get<HistoryService>().SearchAsync(new(Table: "ลูกค้า", RowKey: code));
        Assert.Contains(rows, r => r.Action == HistoryActions.Deleted && r.Column == "รหัสลูกค้า");
    }

    [Fact]
    public async Task Editing_a_customer_records_each_changed_field_in_the_history()
    {
        var code = Unique("05-01-");
        var id = await AsAsync(s => SavedAsync(s.Get<CustomerRegistry>().SaveAsync(new CustomerInput(null, code, "เดิม", null, CreditDays: 30))));
        var c = await GetAsync(id);

        await AsAsync(s => SavedAsync(s.Get<CustomerRegistry>().SaveAsync(new CustomerInput(id, code, "เดิม", c.RowVersion, CreditDays: 0, Province: "ใหม่"))));

        await using var scope = db.Scope();
        var rows = await scope.Get<HistoryService>().SearchAsync(new(Table: "ลูกค้า", RowKey: code));
        Assert.Equivalent(new (string, string?, string?)[] { ("เครดิต (วัน)", "30", "0"), ("จังหวัด", null, "ใหม่") },
            rows.Where(r => r.Action == HistoryActions.Modified).Select(r => (r.Column, r.OldValue, r.NewValue)));
    }
}
