using Lockte.Prod;
using Lockte.Sales;
using Lockte.SharedKernel.Persistence;
using Lockte.Web.Audit;

namespace Lockte.Web.Tests;

/// <summary>ออกเลข ORDER (SCR-04, BR-001) and รับ ORDER (SCR-05, BR-002, BR-004) through the public services of module ขายและส่งของ (#108).</summary>
[Collection(LocalDbCollection.Name)]
public sealed class SalesOrderEntryTests(LocalDbFixture db)
{
    private static int counter = Random.Shared.Next(0x100, 0x700);

    /// <summary>A territory code of its own (at most 4 characters), so the numbers of one test never meet another's.</summary>
    private static string TerritoryCode() => "N" + Interlocked.Increment(ref counter).ToString("X3");

    private static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    private async Task<T> AsAsync<T>(Func<TestScope, Task<T>> action, string user = "sales1")
    {
        await using var scope = db.Scope(user);
        return await action(scope);
    }

    private async Task<int> SavedAsync(Task<Lockte.SharedKernel.ServiceResult> save)
    {
        var result = await save;
        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        return result.Id!.Value;
    }

    private Task<int> NewTerritoryAsync(string code)
        => AsAsync(s => SavedAsync(s.Get<SalesTerritoryRegistry>().SaveAsync(new SalesTerritoryInput(null, code, "เขตทดสอบ ORDER", null, null))));

    private Task<int> NewCustomerAsync(int? territoryId = null, int? creditDays = null, string? remark = null)
        => AsAsync(s => SavedAsync(s.Get<CustomerRegistry>().SaveAsync(new CustomerInput(null, Unique("98-01-"), "ร้านทดสอบ ORDER", null,
            CreditDays: creditDays, SalesTerritoryId: territoryId, Remark: remark))));

    private Task<int> NewProductAsync(string? name = null)
        => AsAsync(s => SavedAsync(s.Get<ProductRegistry>().SaveAsync(new ProductInput(null, Unique("ZO"), name ?? "สินค้าทดสอบ ORDER", null, Unit: "คู่"))));

    private static SalesOrderLineInput L(int product, decimal? quantity, decimal? price) => new(product, quantity, price);

    private static SalesOrderInput Order(string orderNo, int customerId, int? territoryId, params SalesOrderLineInput[] lines)
        => new(orderNo, new DateOnly(2026, 10, 26), customerId, null, territoryId, new DateOnly(2026, 11, 2), "60 วัน", "หมายเหตุใบ", lines);

    private Task<OrderSaveResult> CreateAsync(SalesOrderInput input, OrderEntryMode mode = OrderEntryMode.Print, string user = "sales1")
        => AsAsync(s => s.Get<SalesOrderRegistry>().CreateAsync(input, mode), user);

    private async Task<SalesOrderDto> GetAsync(int id) => (await AsAsync(s => s.Get<SalesOrderDirectory>().GetAsync(id)))!;

    // ---------- ออกเลข (BR-001, AC-01, AC-02) ----------

    [Fact]
    public async Task Next_number_is_the_largest_of_the_territory_in_the_year_plus_one()
    {
        var code = TerritoryCode();
        var territory = await NewTerritoryAsync(code);
        var customer = await NewCustomerAsync(territory);
        var product = await NewProductAsync();
        foreach (var no in new[] { $"{code}-69-0419", $"{code}-69-0420", $"{code}-68-0538" })
            Assert.True((await CreateAsync(Order(no, customer, territory, L(product, 1, 1)))).Succeeded);

        var next = await AsAsync(s => s.Get<SalesOrderDirectory>().ProposeNumberAsync(code, new DateOnly(2026, 10, 26)));

        Assert.Equal($"{code}-69-0421", next); // BR-001 #1, AC-01
    }

    [Fact]
    public async Task A_new_year_starts_again_at_0001_whatever_the_regional_setting()
    {
        var code = TerritoryCode();
        var territory = await NewTerritoryAsync(code);
        var customer = await NewCustomerAsync(territory);
        var product = await NewProductAsync();
        Assert.True((await CreateAsync(Order($"{code}-68-0538", customer, territory, L(product, 1, 1)))).Succeeded);

        Assert.Equal($"{code}-69-0001", await AsAsync(s => s.Get<SalesOrderDirectory>().ProposeNumberAsync(code, new DateOnly(2026, 1, 15)))); // AC-02
        Assert.Equal($"{code}-68-0539", await AsAsync(s => s.Get<SalesOrderDirectory>().ProposeNumberAsync(code, new DateOnly(2025, 12, 31))));
        Assert.Equal("69", SalesOrderRules.YearPart(new DateOnly(2026, 1, 1)));
        Assert.Equal("00", SalesOrderRules.YearPart(new DateOnly(1957, 6, 1)));
    }

    [Fact]
    public async Task A_territory_with_no_number_yet_gets_no_proposal_and_other_formats_are_not_counted()
    {
        var code = TerritoryCode();
        var territory = await NewTerritoryAsync(code);
        var customer = await NewCustomerAsync(territory);
        var product = await NewProductAsync();
        var ownNo = Unique("24/");
        var directory = (Func<TestScope, SalesOrderDirectory>)(s => s.Get<SalesOrderDirectory>());

        Assert.Null(await AsAsync(s => directory(s).ProposeNumberAsync(code, new DateOnly(2026, 10, 26)))); // BR-001 #3
        Assert.True((await CreateAsync(Order(ownNo, customer, territory, L(product, 1, 1)))).Succeeded); // a customer's own number
        Assert.Null(await AsAsync(s => directory(s).ProposeNumberAsync(code, new DateOnly(2026, 10, 26))));
    }

    // ---------- เลขห้ามซ้ำ (#28) ----------

    [Fact]
    public async Task A_number_already_used_is_refused_and_the_next_one_is_proposed()
    {
        var code = TerritoryCode();
        var territory = await NewTerritoryAsync(code);
        var customer = await NewCustomerAsync(territory);
        var product = await NewProductAsync();
        Assert.True((await CreateAsync(Order($"{code}-69-0001", customer, territory, L(product, 1, 1)))).Succeeded);

        var again = await CreateAsync(Order($"{code.ToLowerInvariant()}-69-0001", customer, territory, L(product, 1, 1)));

        Assert.False(again.Succeeded);
        Assert.Equal($"{code}-69-0002", again.NextNumber);
        Assert.Contains("มีอยู่แล้ว", again.Errors.Single());
        Assert.Contains($"{code}-69-0002", again.Errors.Single());
        var ownNo = Unique("24/");
        var own = await CreateAsync(Order(ownNo, customer, territory, L(product, 1, 1)));
        Assert.True(own.Succeeded);
        Assert.False((await CreateAsync(Order(ownNo, customer, null, L(product, 1, 1)))).Succeeded); // a customer's own number too
    }

    [Fact]
    public async Task Two_people_taking_the_same_number_at_once_get_one_order()
    {
        var code = TerritoryCode();
        var territory = await NewTerritoryAsync(code);
        var customer = await NewCustomerAsync(territory);
        var product = await NewProductAsync();
        var number = $"{code}-69-0001";

        var results = await Task.WhenAll(
            CreateAsync(Order(number, customer, territory, L(product, 1, 1)), user: "sales1"),
            CreateAsync(Order(number, customer, territory, L(product, 2, 2)), user: "sales2"));

        Assert.Single(results, r => r.Succeeded);
        var loser = results.Single(r => !r.Succeeded);
        Assert.Equal($"{code}-69-0002", loser.NextNumber);
        Assert.Single((await AsAsync(s => s.Get<SalesOrderDirectory>().FindByNumberAsync(number)))!.Lines);
    }

    // ---------- กรอกและพิมพ์ (BR-002, BR-004, AC-03) ----------

    [Fact]
    public async Task Printing_a_new_order_drops_the_line_without_a_quantity_and_records_it_in_the_history()
    {
        var code = TerritoryCode();
        var territory = await NewTerritoryAsync(code);
        var customer = await NewCustomerAsync(territory, remark: "ที่อยู่เดิมของลูกค้า");
        var product1 = await NewProductAsync("สินค้า ก");
        var product2 = await NewProductAsync("สินค้า ข");
        var product3 = await NewProductAsync("สินค้า ค");
        var number = $"{code}-69-0001";

        var saved = await CreateAsync(Order(number, customer, territory,
            L(product1, 100, 45.5m), L(product2, 20, 10m), L(product3, 3, 0.25m), L(product1, null, 99m)));

        Assert.True(saved.Succeeded);
        var order = await GetAsync(saved.Id!.Value);
        Assert.Equal(3, order.Lines.Count); // AC-03: the line without a quantity is gone, numbered 1..3
        Assert.Equal([1, 2, 3], order.Lines.Select(l => l.LineNo));
        Assert.True(order.InHistory);
        Assert.Equal(new decimal?[] { 4550m, 200m, 0.75m }, order.Lines.Select(l => l.ReceivedAmount)); // BR-004: จำนวน × ราคา
        Assert.Equal(order.Lines.Select(l => l.ReceivedQuantity), order.Lines.Select(l => (decimal?)l.OutstandingQuantity)); // ค้างส่ง = จำนวน
        Assert.Equal(order.Lines.Select(l => l.ReceivedAmount), order.Lines.Select(l => l.OutstandingAmount));
        Assert.Equal(4750.75m, order.TotalReceivedAmount);
        Assert.Equal(4750.75m, order.TotalOutstandingAmount);
        Assert.Equal(("สินค้า ก", "คู่"), (order.Lines[0].ProductName, order.Lines[0].Unit)); // filled from the product
        Assert.Equal((number, "ร้านทดสอบ ORDER", "60 วัน", "หมายเหตุใบ"), (order.OrderNo, order.CustomerName, order.PaymentTerms, order.Remark));
        Assert.Equal(new DateOnly(2026, 11, 2), order.DeliveryDate);

        // BR-002 step 6: with "พิมพ์" the order's หมายเหตุ goes back to the customer's
        Assert.Equal("หมายเหตุใบ", (await AsAsync(s => s.Get<CustomerDirectory>().GetAsync(customer)))!.Remark);
        // the number is in use now
        Assert.Equal($"{code}-69-0002", await AsAsync(s => s.Get<SalesOrderDirectory>().ProposeNumberAsync(code, new DateOnly(2026, 10, 26))));
    }

    [Fact]
    public async Task Leaving_without_printing_keeps_a_backorder_that_is_not_in_the_history()
    {
        var code = TerritoryCode();
        var territory = await NewTerritoryAsync(code);
        var customer = await NewCustomerAsync(territory, remark: "ที่อยู่เดิม");
        var product = await NewProductAsync();

        var saved = await CreateAsync(Order($"{code}-69-0001", customer, territory, L(product, 5, 2m)), OrderEntryMode.Save);

        var order = await GetAsync(saved.Id!.Value);
        Assert.False(order.InHistory); // AC-22 as #29 changed it: not printed = not in the history
        Assert.Equal((null, null, 5m, 10m), (order.Lines[0].ReceivedQuantity, order.Lines[0].ReceivedAmount, order.Lines[0].OutstandingQuantity, order.Lines[0].OutstandingAmount));
        Assert.Equal("ที่อยู่เดิม", (await AsAsync(s => s.Get<CustomerDirectory>().GetAsync(customer)))!.Remark);
        Assert.False((await CreateAsync(Order($"{code}-69-0001", customer, territory, L(product, 1, 1)), OrderEntryMode.Save)).Succeeded); // the number is used
    }

    [Fact]
    public async Task An_old_order_entered_from_the_customers_own_number_goes_to_the_history_but_not_to_the_customer()
    {
        var customer = await NewCustomerAsync(remark: "ที่อยู่เดิม");
        var product = await NewProductAsync();

        var saved = await CreateAsync(Order(Unique("30/"), customer, null, L(product, 8, 3m)), OrderEntryMode.SaveToHistory);

        var order = await GetAsync(saved.Id!.Value);
        Assert.True(order.InHistory);
        Assert.Equal(24m, order.TotalReceivedAmount);
        Assert.Null(order.SalesTerritoryId);
        Assert.Equal("ที่อยู่เดิม", (await AsAsync(s => s.Get<CustomerDirectory>().GetAsync(customer)))!.Remark);
    }

    [Fact]
    public async Task The_head_starts_from_the_customer_and_the_date()
    {
        var territory = await NewTerritoryAsync(TerritoryCode());
        var customer = await NewCustomerAsync(territory, creditDays: 60, remark: "ชื่อ + ที่อยู่สำหรับพิมพ์");
        var cash = await NewCustomerAsync(creditDays: 0);

        var defaults = (await AsAsync(s => s.Get<SalesOrderDirectory>().DefaultsForCustomerAsync(customer)))!;

        Assert.Equal(("ร้านทดสอบ ORDER", territory, "60 วัน", "ชื่อ + ที่อยู่สำหรับพิมพ์"), (defaults.CustomerName, defaults.SalesTerritoryId, defaults.PaymentTerms, defaults.Remark));
        Assert.Equal("เงินสด", (await AsAsync(s => s.Get<SalesOrderDirectory>().DefaultsForCustomerAsync(cash)))!.PaymentTerms);
        Assert.Null(await AsAsync(s => s.Get<SalesOrderDirectory>().DefaultsForCustomerAsync(int.MaxValue)));
        Assert.Equal(new DateOnly(2026, 11, 2), SalesOrderRules.DefaultDeliveryDate(new DateOnly(2026, 10, 26)));
        Assert.Equal(new DateOnly(2027, 1, 3), SalesOrderRules.DefaultDeliveryDate(new DateOnly(2026, 12, 27)));
        Assert.Null(SalesOrderRules.Amount(5, null));
        Assert.Equal(4550m, SalesOrderRules.Amount(100, 45.5m));
    }

    [Fact]
    public async Task An_order_needs_registered_customer_and_products_a_number_and_a_line()
    {
        var customer = await NewCustomerAsync();
        var product = await NewProductAsync();

        var none = await CreateAsync(Order(" ", int.MaxValue, int.MaxValue, L(int.MaxValue, 1, 1)));
        Assert.Equivalent(new[] { "กรุณากรอกเลขที่ ORDER", "กรุณาเลือกลูกค้าที่มีในทะเบียนลูกค้า", "ไม่พบเขตการขายที่เลือก", "กรุณาเลือกสินค้าที่มีในทะเบียนสินค้าทุกบรรทัด" }, none.Errors);
        var empty = await CreateAsync(Order(Unique("E/"), customer, null, L(product, null, 5m)));
        Assert.Equal(["ORDER ต้องมีอย่างน้อยหนึ่งบรรทัดที่มีจำนวน"], empty.Errors);
        var negative = await CreateAsync(Order(Unique("E/"), customer, null, L(product, -1, 5m)));
        Assert.Equal(["จำนวนและราคาต้องไม่ติดลบ"], negative.Errors);
    }

    [Fact]
    public async Task A_saved_order_is_in_the_change_history_and_stops_the_customer_and_territory_from_being_deleted()
    {
        var code = TerritoryCode();
        var territory = await NewTerritoryAsync(code);
        var customerCode = Unique("97-01-");
        var customer = await AsAsync(s => SavedAsync(s.Get<CustomerRegistry>().SaveAsync(new CustomerInput(null, customerCode, "ร้านที่มี ORDER", null, SalesTerritoryId: territory))));
        var product = await NewProductAsync();
        var number = $"{code}-69-0001";
        var saved = await CreateAsync(Order(number, customer, territory, L(product, 2, 7m)), user: "sales9");

        await AsAsync(async s =>
        {
            var rows = await s.Get<HistoryService>().SearchAsync(new(Table: "ORDER", RowKey: number));
            Assert.Contains(rows, r => r is { Action: HistoryActions.Added, Column: "เลขที่ ORDER", NewValue: var n } && n == number);
            Assert.All(rows, r => Assert.Equal("sales9", r.UserName));
            Assert.NotEmpty(await s.Get<HistoryService>().SearchAsync(new(Table: "บรรทัด ORDER", RowKey: $"{number} #1")));
            return 0;
        });

        var c = (await AsAsync(s => s.Get<CustomerDirectory>().GetAsync(customer)))!;
        var refused = await AsAsync(s => s.Get<CustomerRegistry>().DeleteAsync(customer, c.RowVersion));
        Assert.Equal([$"ลบลูกค้า '{customerCode}' ไม่ได้ เพราะมีรายการอื่นอ้างถึงอยู่"], refused.Errors);
        var t = (await AsAsync(s => s.Get<SalesTerritoryDirectory>().GetAsync(territory)))!;
        Assert.False((await AsAsync(s => s.Get<SalesTerritoryRegistry>().DeleteAsync(territory, t.RowVersion))).Succeeded);
        Assert.NotNull(saved.Id);
    }
}
