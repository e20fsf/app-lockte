using Lockte.Prod;
using Lockte.Sales;
using Lockte.SharedKernel.Persistence;
using Lockte.Web.Audit;

namespace Lockte.Web.Tests;

/// <summary>แก้ไข/ลบ ORDER (SCR-06, BR-012) through the public services of module ขายและส่งของ (#109): two sets of values per line, edited apart.</summary>
[Collection(LocalDbCollection.Name)]
public sealed class SalesOrderEditTests(LocalDbFixture db)
{
    private static int counter = Random.Shared.Next(0x1000);

    private static string TerritoryCode() => "E" + Interlocked.Increment(ref counter).ToString("X3");

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

    private static SalesOrderLineInput L(int product, decimal? quantity, decimal? price) => new(product, quantity, price);

    private sealed record Fixture(int Territory, int Customer, int Product1, int Product2, int Product3, string Number);

    private async Task<Fixture> NewFixtureAsync()
    {
        var code = TerritoryCode();
        var territory = await AsAsync(s => SavedAsync(s.Get<SalesTerritoryRegistry>().SaveAsync(new SalesTerritoryInput(null, code, "เขตแก้ไข ORDER", null, null))));
        var customer = await AsAsync(s => SavedAsync(s.Get<CustomerRegistry>().SaveAsync(new CustomerInput(null, Unique("96-01-"), "ร้านแก้ไข ORDER", null, SalesTerritoryId: territory))));
        int Product(string name) => AsAsync(s => SavedAsync(s.Get<ProductRegistry>().SaveAsync(new ProductInput(null, Unique("ZE"), name, null, Unit: "คู่")))).Result;
        return new Fixture(territory, customer, Product("สินค้า ก"), Product("สินค้า ข"), Product("สินค้า ค"), $"{code}-69-0189");
    }

    private async Task<SalesOrderDto> NewOrderAsync(Fixture f, OrderEntryMode mode = OrderEntryMode.Print, string? number = null)
    {
        var saved = await AsAsync(s => s.Get<SalesOrderRegistry>().CreateAsync(new SalesOrderInput(number ?? f.Number, new DateOnly(2026, 10, 26), f.Customer, null, f.Territory,
            new DateOnly(2026, 11, 2), "60 วัน", "หมายเหตุ", [L(f.Product1, 50, 10m), L(f.Product2, 100, 10m), L(f.Product3, 30, 2m)]), mode));
        Assert.True(saved.Succeeded, string.Join(" ", saved.Errors));
        return (await AsAsync(s => s.Get<SalesOrderDirectory>().GetAsync(saved.Id!.Value)))!;
    }

    private Task<SalesOrderDto> ReloadAsync(int id) => AsAsync(async s => (await s.Get<SalesOrderDirectory>().GetAsync(id))!);

    private static SalesOrderHeadEdit Head(SalesOrderDto o) => new(o.OrderDate, o.CustomerId, o.CustomerName, o.SalesTerritoryId, o.DeliveryDate, o.PaymentTerms, o.Remark);

    private static SalesOrderLineEdit Edit(SalesOrderLineDto l, decimal? quantity, decimal? price) => new(l.Id, l.ProductId, quantity, price, RowVersion: l.RowVersion);

    // ---------- แก้ (BR-012 #1) ----------

    [Fact]
    public async Task Editing_the_backorder_changes_its_quantity_and_amount_and_leaves_the_history_alone()
    {
        var f = await NewFixtureAsync();
        var order = await NewOrderAsync(f);

        var saved = await AsAsync(s => s.Get<SalesOrderEditor>().SaveAsync(order.Id, order.RowVersion, OrderSide.Outstanding, Head(order),
            [Edit(order.Lines[1], 80, 10m)]));

        Assert.True(saved.Succeeded);
        var after = await ReloadAsync(order.Id);
        Assert.Equal((80m, 800m), (after.Lines[1].OutstandingQuantity, after.Lines[1].OutstandingAmount)); // BR-012 #1: QTY 80, AMOUNT 800
        Assert.Equal((100m, 1000m), (after.Lines[1].ReceivedQuantity, after.Lines[1].ReceivedAmount)); // T-ORDER does not change
        Assert.Equal(order.Lines.Select(l => l.OutstandingQuantity).Where((_, i) => i != 1), after.Lines.Select(l => l.OutstandingQuantity).Where((_, i) => i != 1));
    }

    [Fact]
    public async Task Editing_the_history_changes_the_received_values_and_leaves_the_backorder_alone()
    {
        var f = await NewFixtureAsync();
        var order = await NewOrderAsync(f);

        await AsAsync(s => SavedAsync(s.Get<SalesOrderEditor>().SaveAsync(order.Id, order.RowVersion, OrderSide.History, Head(order),
            [Edit(order.Lines[0], 55, 12m)])));

        var after = await ReloadAsync(order.Id);
        Assert.Equal((55m, 660m), (after.Lines[0].ReceivedQuantity, after.Lines[0].ReceivedAmount));
        Assert.Equal((50m, 500m), (after.Lines[0].OutstandingQuantity, after.Lines[0].OutstandingAmount));
        Assert.Equal(12m, after.Lines[0].UnitPrice); // the price is the line's; the other side's amount is what it was
    }

    [Fact]
    public async Task The_head_is_shared_by_both_sides_and_the_number_does_not_change()
    {
        var f = await NewFixtureAsync();
        var order = await NewOrderAsync(f);

        await AsAsync(s => SavedAsync(s.Get<SalesOrderEditor>().SaveAsync(order.Id, order.RowVersion, OrderSide.Outstanding,
            Head(order) with { DeliveryDate = new DateOnly(2026, 11, 15), PaymentTerms = "เงินสด", CustomerName = "ร้านแก้ชื่อ" }, [])));

        var after = await ReloadAsync(order.Id);
        Assert.Equal((new DateOnly(2026, 11, 15), "เงินสด", "ร้านแก้ชื่อ", f.Number), (after.DeliveryDate, after.PaymentTerms, after.CustomerName, after.OrderNo));
    }

    [Fact]
    public async Task A_line_can_be_added_to_one_side_and_edits_are_checked()
    {
        var f = await NewFixtureAsync();
        var order = await NewOrderAsync(f);

        Assert.True((await AsAsync(s => s.Get<SalesOrderEditor>().SaveAsync(order.Id, order.RowVersion, OrderSide.Outstanding, Head(order),
            [new SalesOrderLineEdit(null, f.Product1, 7, 3m), new SalesOrderLineEdit(null, f.Product2, null, 3m)]))).Succeeded);
        var after = await ReloadAsync(order.Id);
        Assert.Equal(4, after.Lines.Count);
        Assert.Equal((4, 7m, 21m, null, null), (after.Lines[3].LineNo, after.Lines[3].OutstandingQuantity, after.Lines[3].OutstandingAmount, after.Lines[3].ReceivedQuantity, after.Lines[3].ReceivedAmount));

        var blank = await AsAsync(s => s.Get<SalesOrderEditor>().SaveAsync(order.Id, after.RowVersion, OrderSide.Outstanding, Head(after), [Edit(after.Lines[0], null, 10m)]));
        Assert.Equal(["บรรทัด 1: จำนวนว่าง — ใช้ปุ่มลบแถวเพื่อลบบรรทัด"], blank.Errors);
        var bad = await AsAsync(s => s.Get<SalesOrderEditor>().SaveAsync(order.Id, after.RowVersion, OrderSide.Outstanding,
            Head(after) with { CustomerId = int.MaxValue }, [Edit(after.Lines[0], -1, 1m), new SalesOrderLineEdit(null, int.MaxValue, 1, 1m)]));
        Assert.Equivalent(new[] { "กรุณาเลือกลูกค้าที่มีในทะเบียนลูกค้า", "จำนวนและราคาต้องไม่ติดลบ", "กรุณาเลือกสินค้าที่มีในทะเบียนสินค้าทุกบรรทัด" }, bad.Errors);
    }

    [Fact]
    public async Task A_stale_edit_of_the_order_or_of_a_line_is_a_conflict()
    {
        var f = await NewFixtureAsync();
        var order = await NewOrderAsync(f);
        await AsAsync(s => SavedAsync(s.Get<SalesOrderEditor>().SaveAsync(order.Id, order.RowVersion, OrderSide.Outstanding, Head(order) with { Remark = "คนแรกแก้" }, [Edit(order.Lines[0], 49, 10m)])));

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => AsAsync(s => s.Get<SalesOrderEditor>().SaveAsync(order.Id, order.RowVersion,
            OrderSide.Outstanding, Head(order), [])));
        var fresh = await ReloadAsync(order.Id);
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => AsAsync(s => s.Get<SalesOrderEditor>().SaveAsync(order.Id, fresh.RowVersion,
            OrderSide.Outstanding, Head(fresh), [Edit(order.Lines[0], 40, 10m)]))); // the line as loaded before the first edit
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => AsAsync(s => s.Get<SalesOrderEditor>().DeleteLineAsync(order.Id, order.Lines[0].Id, OrderSide.Outstanding, order.Lines[0].RowVersion)));
    }

    // ---------- ลบบรรทัด ----------

    [Fact]
    public async Task Deleting_a_line_empties_one_side_and_the_line_goes_when_both_are_empty()
    {
        var f = await NewFixtureAsync();
        var order = await NewOrderAsync(f);

        Assert.True((await AsAsync(s => s.Get<SalesOrderEditor>().DeleteLineAsync(order.Id, order.Lines[2].Id, OrderSide.Outstanding, order.Lines[2].RowVersion))).Succeeded);
        var step1 = await ReloadAsync(order.Id);
        Assert.Equal((0m, null, 30m), (step1.Lines[2].OutstandingQuantity, step1.Lines[2].OutstandingAmount, step1.Lines[2].ReceivedQuantity)); // the history keeps it

        Assert.True((await AsAsync(s => s.Get<SalesOrderEditor>().DeleteLineAsync(order.Id, order.Lines[1].Id, OrderSide.History, step1.Lines[1].RowVersion))).Succeeded);
        var step2 = await ReloadAsync(order.Id);
        Assert.Equal((null, 100m), (step2.Lines[1].ReceivedQuantity, step2.Lines[1].OutstandingQuantity)); // the backorder keeps it

        Assert.True((await AsAsync(s => s.Get<SalesOrderEditor>().DeleteLineAsync(order.Id, order.Lines[1].Id, OrderSide.Outstanding, step2.Lines[1].RowVersion))).Succeeded);
        var step3 = await ReloadAsync(order.Id);
        Assert.Equal([1, 3], step3.Lines.Select(l => l.LineNo)); // empty on both sides: gone
        Assert.True((await AsAsync(s => s.Get<SalesOrderEditor>().DeleteLineAsync(order.Id, order.Lines[1].Id, OrderSide.Outstanding, order.Lines[1].RowVersion))).Succeeded); // already gone
    }

    // ---------- ลบทั้งใบ (BR-012 #3, AC-06 as #28/#29 changed it) ----------

    [Fact]
    public async Task Deleting_the_whole_backorder_closes_it_but_the_order_stays_in_the_history_and_its_number_stays_used()
    {
        var f = await NewFixtureAsync();
        var order = await NewOrderAsync(f);

        Assert.True((await AsAsync(s => s.Get<SalesOrderEditor>().DeleteOrderAsync(order.Id, OrderSide.Outstanding, order.RowVersion))).Succeeded);

        var after = await ReloadAsync(order.Id);
        Assert.All(after.Lines, l => Assert.Equal((0m, null), (l.OutstandingQuantity, l.OutstandingAmount)));
        Assert.Equal(1560m, after.TotalReceivedAmount); // 50×10 + 100×10 + 30×2: still counted in the receipts
        Assert.True(after.InHistory);
        Assert.Empty(await AsAsync(s => s.Get<SalesOrderDirectory>().SearchAsync(new SalesOrderSearch(OrderSide.Outstanding, OrderNo: f.Number))));
        Assert.Single(await AsAsync(s => s.Get<SalesOrderDirectory>().SearchAsync(new SalesOrderSearch(OrderSide.History, OrderNo: f.Number))));

        var again = await AsAsync(s => s.Get<SalesOrderRegistry>().CreateAsync(new SalesOrderInput(f.Number, new DateOnly(2026, 10, 27), f.Customer, null, f.Territory, null, null, null,
            [L(f.Product1, 1, 1m)]), OrderEntryMode.Print));
        Assert.False(again.Succeeded); // the number is used
    }

    [Fact]
    public async Task An_order_left_with_no_line_on_either_side_is_deleted_and_its_number_can_be_used_again()
    {
        var f = await NewFixtureAsync();
        var onlyBackorder = await NewOrderAsync(f, OrderEntryMode.Save, number: f.Number + "-S");
        Assert.False(onlyBackorder.InHistory);

        // a backorder that was never in the history: deleting it deletes the order, and the number is free
        Assert.True((await AsAsync(s => s.Get<SalesOrderEditor>().DeleteOrderAsync(onlyBackorder.Id, OrderSide.Outstanding, onlyBackorder.RowVersion))).Succeeded);
        Assert.Null(await AsAsync(s => s.Get<SalesOrderDirectory>().FindByNumberAsync(onlyBackorder.OrderNo)));
        Assert.True((await AsAsync(s => s.Get<SalesOrderRegistry>().CreateAsync(new SalesOrderInput(onlyBackorder.OrderNo, new DateOnly(2026, 10, 27), f.Customer, null, f.Territory,
            null, null, null, [L(f.Product1, 1, 1m)]), OrderEntryMode.Print))).Succeeded);

        // an order in the history: close the backorder, then delete the history — nothing left, so the order and its number go
        var order = await NewOrderAsync(f);
        await AsAsync(s => SavedAsync(s.Get<SalesOrderEditor>().DeleteOrderAsync(order.Id, OrderSide.Outstanding, order.RowVersion)));
        var closed = await ReloadAsync(order.Id);
        Assert.True((await AsAsync(s => s.Get<SalesOrderEditor>().DeleteOrderAsync(order.Id, OrderSide.History, closed.RowVersion))).Succeeded);
        Assert.Null(await AsAsync(s => s.Get<SalesOrderDirectory>().FindByNumberAsync(f.Number)));
        Assert.True((await AsAsync(s => s.Get<SalesOrderRegistry>().CreateAsync(new SalesOrderInput(f.Number, new DateOnly(2026, 10, 27), f.Customer, null, f.Territory,
            null, null, null, [L(f.Product1, 1, 1m)]), OrderEntryMode.Print))).Succeeded);
        Assert.True((await AsAsync(s => s.Get<SalesOrderEditor>().DeleteOrderAsync(order.Id, OrderSide.History, closed.RowVersion))).Succeeded); // already gone
    }

    [Fact]
    public async Task Emptying_the_history_of_an_order_takes_it_out_of_the_receipts_but_keeps_the_backorder()
    {
        var f = await NewFixtureAsync();
        var order = await NewOrderAsync(f);

        await AsAsync(s => SavedAsync(s.Get<SalesOrderEditor>().DeleteOrderAsync(order.Id, OrderSide.History, order.RowVersion)));

        var after = await ReloadAsync(order.Id);
        Assert.False(after.InHistory);
        Assert.Equal(1560m, after.TotalOutstandingAmount);
        Assert.Empty(await AsAsync(s => s.Get<SalesOrderDirectory>().SearchAsync(new SalesOrderSearch(OrderSide.History, OrderNo: f.Number))));
    }

    // ---------- ค้น (SCR-06 3 แบบ) ----------

    [Fact]
    public async Task Orders_are_found_by_number_by_date_and_by_customer_on_each_side()
    {
        var f = await NewFixtureAsync();
        var a = await NewOrderAsync(f, number: f.Number + "A");
        var b = await NewOrderAsync(f, OrderEntryMode.Save, number: f.Number + "B");

        async Task<int[]> Ids(SalesOrderSearch search) => (await AsAsync(s => s.Get<SalesOrderDirectory>().SearchAsync(search))).Select(i => i.Id).ToArray();

        Assert.Equal([a.Id, b.Id], await Ids(new(OrderSide.Outstanding, OrderNo: f.Number)));
        Assert.Equal([b.Id], await Ids(new(OrderSide.Outstanding, OrderNo: f.Number + "B")));
        Assert.Equal([a.Id], await Ids(new(OrderSide.History, CustomerId: f.Customer))); // b was never printed
        Assert.Equal([a.Id, b.Id], await Ids(new(OrderSide.Outstanding, Date: new DateOnly(2026, 10, 26), CustomerId: f.Customer)));
        Assert.Empty(await Ids(new(OrderSide.Outstanding, Date: new DateOnly(1999, 1, 1), CustomerId: f.Customer)));
        var item = (await AsAsync(s => s.Get<SalesOrderDirectory>().SearchAsync(new(OrderSide.Outstanding, OrderNo: f.Number + "A")))).Single();
        Assert.Equal((3, 1560m, "ร้านแก้ไข ORDER"), (item.Lines, item.Amount, item.CustomerName));
    }

    [Fact]
    public async Task Edits_and_deletes_are_in_the_change_history()
    {
        var f = await NewFixtureAsync();
        var order = await NewOrderAsync(f);
        await AsAsync(s => SavedAsync(s.Get<SalesOrderEditor>().SaveAsync(order.Id, order.RowVersion, OrderSide.Outstanding, Head(order), [Edit(order.Lines[1], 80, 10m)])), "sales7");
        var fresh = await ReloadAsync(order.Id);
        await AsAsync(s => SavedAsync(s.Get<SalesOrderEditor>().DeleteOrderAsync(order.Id, OrderSide.History, fresh.RowVersion)), "sales7");
        var noHistory = await ReloadAsync(order.Id);
        await AsAsync(s => SavedAsync(s.Get<SalesOrderEditor>().DeleteOrderAsync(order.Id, OrderSide.Outstanding, noHistory.RowVersion)), "sales7");

        await AsAsync(async s =>
        {
            var lines = await s.Get<HistoryService>().SearchAsync(new(Table: "บรรทัด ORDER", RowKey: f.Number + " #2"));
            Assert.Contains(lines, r => r is { Action: HistoryActions.Modified, Column: "ยอดค้างส่ง", UserName: "sales7" } && r.OldValue == "100.0000" && r.NewValue == "80");
            Assert.Contains(lines, r => r.Action == HistoryActions.Deleted);
            Assert.Contains(await s.Get<HistoryService>().SearchAsync(new(Table: "ORDER", RowKey: f.Number)), r => r.Action == HistoryActions.Deleted);
            return 0;
        });
    }
}

/// <summary>The data of the printed ใบ SALE ORDER (RPT-01, #110): the ค้างส่ง values only, in line order (⚠ RPT-01: printed from T-ODD).</summary>
[Collection(LocalDbCollection.Name)]
public sealed class SaleOrderPrintDataTests(LocalDbFixture db)
{
    private static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

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

    [Fact]
    public async Task The_print_has_the_backorder_lines_with_their_values_and_nothing_for_an_order_delivered_in_full()
    {
        var code = "P" + Guid.NewGuid().ToString("N")[..3].ToUpperInvariant();
        var territory = await AsAsync(s => SavedAsync(s.Get<SalesTerritoryRegistry>().SaveAsync(new SalesTerritoryInput(null, code, "เขตพิมพ์", null, null))));
        var customer = await AsAsync(s => SavedAsync(s.Get<CustomerRegistry>().SaveAsync(new CustomerInput(null, Unique("95-"), "ร้านพิมพ์ ORDER", null, SalesTerritoryId: territory))));
        var p1 = await AsAsync(s => SavedAsync(s.Get<ProductRegistry>().SaveAsync(new ProductInput(null, Unique("ZP"), "สินค้า พิมพ์ ก", null, Unit: "คู่"))));
        var p2 = await AsAsync(s => SavedAsync(s.Get<ProductRegistry>().SaveAsync(new ProductInput(null, Unique("ZP"), "สินค้า พิมพ์ ข", null, Unit: "ชิ้น"))));
        var number = $"{code}-69-0001";
        var saved = await AsAsync(s => s.Get<SalesOrderRegistry>().CreateAsync(new SalesOrderInput(number, new DateOnly(2026, 10, 17), customer, null, territory, null, "30", "หมายเหตุพิมพ์",
            [new SalesOrderLineInput(p1, 50, 65m), new SalesOrderLineInput(p2, 10, 2.5m)]), OrderEntryMode.Print));
        var order = (await AsAsync(s => s.Get<SalesOrderDirectory>().GetAsync(saved.Id!.Value)))!;

        var print = (await AsAsync(s => s.Get<SalesOrderDirectory>().GetPrintAsync(order.Id)))!;
        Assert.Equal((number, "30", code, "หมายเหตุพิมพ์", 3275m), (print.OrderNo, print.PaymentTerms, print.SalesTerritoryCode, print.Remark, print.Total));
        Assert.Equal([(1, "คู่", 50m, 3250m), (2, "ชิ้น", 10m, 25m)], print.Lines.Select(l => (l.LineNo, l.Unit, l.Quantity, l.Amount)));

        // one line delivered in full (its ค้างส่ง closed): it is not on the sheet, the history still has it
        await AsAsync(s => SavedAsync(s.Get<SalesOrderEditor>().DeleteLineAsync(order.Id, order.Lines[0].Id, OrderSide.Outstanding, order.Lines[0].RowVersion)));
        var after = (await AsAsync(s => s.Get<SalesOrderDirectory>().GetPrintAsync(order.Id)))!;
        Assert.Equal([2], after.Lines.Select(l => l.LineNo));
        Assert.Equal(25m, after.Total);

        var closed = (await AsAsync(s => s.Get<SalesOrderDirectory>().GetAsync(order.Id)))!;
        await AsAsync(s => SavedAsync(s.Get<SalesOrderEditor>().DeleteOrderAsync(order.Id, OrderSide.Outstanding, closed.RowVersion)));
        Assert.Null(await AsAsync(s => s.Get<SalesOrderDirectory>().GetPrintAsync(order.Id)));
        Assert.Null(await AsAsync(s => s.Get<SalesOrderDirectory>().GetPrintAsync(int.MaxValue)));
    }
}
