using Lockte.Prod;
using Lockte.Sales;
using Lockte.SharedKernel.Persistence;
using Lockte.Web.Audit;

namespace Lockte.Web.Tests;

/// <summary>บันทึกการส่งของ (SCR-08, BR-003, BR-004) and แก้ไข/ลบรายการส่ง (SCR-09, BR-014) through the public services of module ขายและส่งของ (#111).</summary>
[Collection(LocalDbCollection.Name)]
public sealed class SalesDeliveryTests(LocalDbFixture db)
{
    private static int counter = Random.Shared.Next(0x1000);

    private static string TerritoryCode() => "D" + Interlocked.Increment(ref counter).ToString("X3");

    private static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    private async Task<T> AsAsync<T>(Func<TestScope, Task<T>> action, string user = "store1")
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

    private sealed record Setup(int Territory, int Customer, int Product, string Number);

    private async Task<Setup> NewSetupAsync()
    {
        var code = TerritoryCode();
        var territory = await AsAsync(s => SavedAsync(s.Get<SalesTerritoryRegistry>().SaveAsync(new SalesTerritoryInput(null, code, "เขตส่งของ", null, null))));
        var customer = await AsAsync(s => SavedAsync(s.Get<CustomerRegistry>().SaveAsync(new CustomerInput(null, Unique("94-01-"), "ร้านส่งของ", null, SalesTerritoryId: territory))));
        var product = await AsAsync(s => SavedAsync(s.Get<ProductRegistry>().SaveAsync(new ProductInput(null, Unique("ZD"), "สินค้าส่งของ", null, Unit: "คู่"))));
        return new Setup(territory, customer, product, $"{code}-69-0001");
    }

    private async Task<SalesOrderDto> NewOrderAsync(Setup f, decimal quantity, decimal price, string? number = null, OrderEntryMode mode = OrderEntryMode.Print)
    {
        var saved = await AsAsync(s => s.Get<SalesOrderRegistry>().CreateAsync(new SalesOrderInput(number ?? f.Number, new DateOnly(2026, 10, 17), f.Customer, null, f.Territory,
            new DateOnly(2026, 10, 24), "30", null, [new SalesOrderLineInput(f.Product, quantity, price)]), mode));
        Assert.True(saved.Succeeded, string.Join(" ", saved.Errors));
        return (await AsAsync(s => s.Get<SalesOrderDirectory>().GetAsync(saved.Id!.Value)))!;
    }

    private Task<IReadOnlyList<DeliveryLineDto>> LinesAsync(string number) => AsAsync(s => s.Get<SalesDeliveryDirectory>().GetLinesAsync(number));

    private async Task TypeAsync(DeliveryLineDto line, decimal? quantity, string? invoice = "0103646", string? lot = null, DateOnly? date = null, int? invoiceLine = 1, decimal? boxes = null)
        => await AsAsync(s => SavedAsync(s.Get<SalesDeliveryRegistry>().SetPendingAsync(line.LineId, line.RowVersion,
            new PendingDeliveryInput(date ?? new DateOnly(2026, 10, 20), invoice, invoiceLine, quantity, lot, boxes))));

    // ---------- BR-003 ----------

    [Fact] // BR-003 #1, AC-04: sent in full → a "ครบ" delivery, the backorder line closed
    public async Task A_line_sent_in_full_is_recorded_as_complete_and_leaves_the_backorder()
    {
        var f = await NewSetupAsync();
        var order = await NewOrderAsync(f, 100, 10m);
        await TypeAsync((await LinesAsync(f.Number)).Single(), 100, lot: "ab1", boxes: 5);

        var result = await AsAsync(s => s.Get<SalesDeliveryRegistry>().SaveAsync());

        Assert.Equal((1, 0, 0), (result.Complete, result.Partial, result.Zero));
        var delivery = (await AsAsync(s => s.Get<SalesDeliveryDirectory>().SearchAsync(new DeliverySearch(OrderNo: f.Number)))).Single();
        Assert.Equal((100m, 1000m, true, "AB1", 5m, "0103646", 1, new DateOnly(2026, 10, 20)),
            (delivery.Quantity, delivery.Amount, delivery.Complete, delivery.Lot, delivery.Boxes, delivery.InvoiceNo, delivery.InvoiceLine, delivery.DeliveryDate));
        Assert.Equal((f.Number, 1, 10m, new DateOnly(2026, 10, 17), new DateOnly(2026, 10, 24), "สินค้าส่งของ"),
            (delivery.OrderNo, delivery.LineNo, delivery.UnitPrice, delivery.OrderDate, delivery.DueDate, delivery.ProductName)); // the copy of the line
        Assert.Empty(await LinesAsync(f.Number)); // gone from the backorder
        var after = (await AsAsync(s => s.Get<SalesOrderDirectory>().GetAsync(order.Id)))!;
        Assert.Equal((0m, null, 1000m), (after.Lines[0].OutstandingQuantity, after.Lines[0].OutstandingAmount, after.Lines[0].ReceivedAmount)); // the history of the order keeps its รับ
        Assert.Empty(await AsAsync(s => s.Get<SalesOrderDirectory>().SearchAsync(new SalesOrderSearch(OrderSide.Outstanding, OrderNo: f.Number))));
    }

    [Fact] // BR-003 #2, AC-05: 100, sent 40 at 10 → delivery 400, backorder 60 worth 600, the typed fields cleared
    public async Task A_part_delivery_leaves_the_rest_on_the_backorder()
    {
        var f = await NewSetupAsync();
        var order = await NewOrderAsync(f, 100, 10m);
        await TypeAsync((await LinesAsync(f.Number)).Single(), 40);

        var result = await AsAsync(s => s.Get<SalesDeliveryRegistry>().SaveAsync());

        Assert.Equal((0, 1, 0), (result.Complete, result.Partial, result.Zero));
        var delivery = (await AsAsync(s => s.Get<SalesDeliveryDirectory>().SearchAsync(new DeliverySearch(OrderNo: f.Number)))).Single();
        Assert.Equal((40m, 400m, false), (delivery.Quantity, delivery.Amount, delivery.Complete));
        var line = (await LinesAsync(f.Number)).Single();
        Assert.Equal((60m, null, null, null, null), (line.OutstandingQuantity, line.Quantity, line.InvoiceNo, line.DeliveryDate, line.Lot)); // the fields are cleared, the line goes on
        var after = (await AsAsync(s => s.Get<SalesOrderDirectory>().GetAsync(order.Id)))!;
        Assert.Equal((60m, 600m, 100m, 1000m), (after.Lines[0].OutstandingQuantity, after.Lines[0].OutstandingAmount, after.Lines[0].ReceivedQuantity, after.Lines[0].ReceivedAmount));
    }

    [Fact] // BR-003 #3 ⚠: nothing stops a delivery of more than the backorder
    public async Task Sending_more_than_the_backorder_is_not_stopped_and_makes_it_negative()
    {
        var f = await NewSetupAsync();
        var order = await NewOrderAsync(f, 100, 10m);
        await TypeAsync((await LinesAsync(f.Number)).Single(), 120);

        var result = await AsAsync(s => s.Get<SalesDeliveryRegistry>().SaveAsync());

        Assert.Equal((0, 1, 0), (result.Complete, result.Partial, result.Zero)); // "ไม่ครบ" (b)
        var line = (await LinesAsync(f.Number)).Single();
        Assert.Equal(-20m, line.OutstandingQuantity); // still on the backorder, as in Access
        var after = (await AsAsync(s => s.Get<SalesOrderDirectory>().GetAsync(order.Id)))!;
        Assert.Equal(-200m, after.Lines[0].OutstandingAmount);
    }

    [Fact] // BR-003 #4: a quantity of 0 is recorded and removed at once
    public async Task A_quantity_of_zero_records_nothing_and_changes_nothing()
    {
        var f = await NewSetupAsync();
        await NewOrderAsync(f, 100, 10m);
        await TypeAsync((await LinesAsync(f.Number)).Single(), 0);

        var result = await AsAsync(s => s.Get<SalesDeliveryRegistry>().SaveAsync());

        Assert.Equal((0, 0, 1), (result.Complete, result.Partial, result.Zero));
        Assert.Empty(await AsAsync(s => s.Get<SalesDeliveryDirectory>().SearchAsync(new DeliverySearch(OrderNo: f.Number))));
        var line = (await LinesAsync(f.Number)).Single();
        Assert.Equal((100m, null), (line.OutstandingQuantity, line.Quantity));
    }

    [Fact] // BR-003: the typed fields are kept as soon as they are typed — and "บันทึก" processes every order, not the chosen one
    public async Task Typed_deliveries_are_kept_and_the_button_processes_every_order()
    {
        var f = await NewSetupAsync();
        var second = f with { Number = f.Number.Replace("-0001", "-0002") };
        await NewOrderAsync(f, 100, 10m);
        await NewOrderAsync(second, 30, 2m, second.Number);
        await TypeAsync((await LinesAsync(f.Number)).Single(), 25, lot: "x9");
        await TypeAsync((await LinesAsync(second.Number)).Single(), 30);

        // a new session sees what was typed
        var typed = (await LinesAsync(f.Number)).Single();
        Assert.Equal((25m, 250m, "X9", "0103646"), (typed.Quantity, typed.Amount, typed.Lot, typed.InvoiceNo));
        Assert.True(await AsAsync(s => s.Get<SalesDeliveryDirectory>().CountPendingAsync()) >= 2);

        var result = await AsAsync(s => s.Get<SalesDeliveryRegistry>().SaveAsync());

        Assert.True(result.Total >= 2);
        Assert.Equal(25m, (await AsAsync(s => s.Get<SalesDeliveryDirectory>().SearchAsync(new DeliverySearch(OrderNo: f.Number)))).Single().Quantity);
        Assert.Equal(30m, (await AsAsync(s => s.Get<SalesDeliveryDirectory>().SearchAsync(new DeliverySearch(OrderNo: second.Number)))).Single().Quantity);
        Assert.Equal(0, await AsAsync(s => s.Get<SalesDeliveryDirectory>().CountPendingAsync()));
    }

    [Fact] // BR-004: DL_NET follows DL_QTY (and the price) as the quantity is typed
    public async Task The_value_of_a_delivery_follows_the_quantity_typed()
    {
        var f = await NewSetupAsync();
        await NewOrderAsync(f, 100, 45.5m);
        var line = (await LinesAsync(f.Number)).Single();
        await TypeAsync(line, 40);
        var again = (await LinesAsync(f.Number)).Single();
        Assert.Equal(1820m, again.Amount);
        await AsAsync(s => SavedAsync(s.Get<SalesDeliveryRegistry>().SetPendingAsync(again.LineId, again.RowVersion, new PendingDeliveryInput(null, null, null, null, null, null))));
        Assert.Null((await LinesAsync(f.Number)).Single().Amount);
    }

    [Fact]
    public async Task Lines_are_listed_by_invoice_number_and_the_input_is_checked()
    {
        var f = await NewSetupAsync();
        var saved = await AsAsync(s => s.Get<SalesOrderRegistry>().CreateAsync(new SalesOrderInput(f.Number, new DateOnly(2026, 10, 17), f.Customer, null, f.Territory, null, null, null,
            [new SalesOrderLineInput(f.Product, 5, 1m), new SalesOrderLineInput(f.Product, 6, 1m), new SalesOrderLineInput(f.Product, 7, 1m)]), OrderEntryMode.Print));
        Assert.True(saved.Succeeded);
        var lines = await LinesAsync(f.Number);
        await TypeAsync(lines[0], 1, invoice: "0000009");
        await TypeAsync(lines[1], 1, invoice: "0000002");

        var listed = await LinesAsync(f.Number);
        // lines with no invoice first, then by invoice number
        Assert.Equal([3, 2, 1], listed.Select(l => l.LineNo));

        var bad = await AsAsync(s => s.Get<SalesDeliveryRegistry>().SetPendingAsync(listed[0].LineId, listed[0].RowVersion, new PendingDeliveryInput(null, "123456789012", null, -1, null, null)));
        Assert.Equivalent(new[] { "เลขที่บิลยาวได้ไม่เกิน 11 ตัว", "จำนวนส่งและจำนวนกล่องต้องไม่ติดลบ" }, bad.Errors);
        var line = listed[0];
        await TypeAsync(line, 2); // a typed row changes its rowversion: the old copy is stale
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => AsAsync(s => s.Get<SalesDeliveryRegistry>().SetPendingAsync(line.LineId, line.RowVersion,
            new PendingDeliveryInput(null, null, null, 3, null, null))));
    }

    [Fact] // BR-003 step 6 + BR-012: an order that was never printed and is delivered in full disappears, its deliveries stay
    public async Task A_backorder_not_in_the_history_that_is_delivered_in_full_goes_but_its_deliveries_stay()
    {
        var f = await NewSetupAsync();
        var order = await NewOrderAsync(f, 10, 3m, mode: OrderEntryMode.Save);
        await TypeAsync((await LinesAsync(f.Number)).Single(), 10);

        await AsAsync(s => s.Get<SalesDeliveryRegistry>().SaveAsync());

        Assert.Null(await AsAsync(s => s.Get<SalesOrderDirectory>().FindByNumberAsync(f.Number)));
        Assert.Equal(30m, (await AsAsync(s => s.Get<SalesDeliveryDirectory>().SearchAsync(new DeliverySearch(OrderNo: f.Number)))).Single().Amount);
        var customer = (await AsAsync(s => s.Get<CustomerDirectory>().GetAsync(f.Customer)))!;
        var refused = await AsAsync(s => s.Get<CustomerRegistry>().DeleteAsync(f.Customer, customer.RowVersion)); // the history of deliveries refers to it
        Assert.False(refused.Succeeded);
        Assert.NotNull(order);
    }

    // ---------- BR-014 ----------

    [Fact] // BR-014 #1, #2: the value follows the quantity; deleting does not give the backorder back
    public async Task A_delivery_can_be_edited_and_deleted_without_touching_the_backorder()
    {
        var f = await NewSetupAsync();
        var order = await NewOrderAsync(f, 100, 10m);
        await TypeAsync((await LinesAsync(f.Number)).Single(), 40);
        await AsAsync(s => s.Get<SalesDeliveryRegistry>().SaveAsync());
        var delivery = (await AsAsync(s => s.Get<SalesDeliveryDirectory>().SearchAsync(new DeliverySearch(InvoiceNo: "0103646", OrderNo: f.Number)))).Single();

        DeliveryEdit Edit(DeliveryDto d, decimal quantity, string? lot = null) => new(d.OrderNo, d.OrderDate, d.CustomerId, d.CustomerName, d.SalesTerritoryId, d.ProductId,
            d.ProductName, d.Unit, d.UnitPrice, d.DueDate, d.DeliveryDate, d.InvoiceNo, d.InvoiceLine, lot ?? d.Lot, quantity, d.Boxes, d.RowVersion);

        Assert.True((await AsAsync(s => s.Get<SalesDeliveryEditor>().SaveAsync(delivery.Id, Edit(delivery, 50, "lot-z")))).Succeeded);
        var edited = (await AsAsync(s => s.Get<SalesDeliveryDirectory>().GetAsync(delivery.Id)))!;
        Assert.Equal((50m, 500m, "LOT-Z"), (edited.Quantity, edited.Amount, edited.Lot)); // BR-014 #1: DL_NET 500
        var backorder = (await LinesAsync(f.Number)).Single();
        Assert.Equal(60m, backorder.OutstandingQuantity); // the order does not follow (⚠ Q-57)

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => AsAsync(s => s.Get<SalesDeliveryEditor>().SaveAsync(delivery.Id, Edit(delivery, 55))));
        var bad = await AsAsync(s => s.Get<SalesDeliveryEditor>().SaveAsync(delivery.Id, Edit(edited, -1) with { CustomerId = int.MaxValue, ProductId = int.MaxValue }));
        Assert.Equivalent(new[] { "จำนวน ราคา และจำนวนกล่องต้องไม่ติดลบ", "กรุณาเลือกลูกค้าที่มีในทะเบียนลูกค้า", "กรุณาเลือกสินค้าที่มีในทะเบียนสินค้า" }, bad.Errors);

        Assert.True((await AsAsync(s => s.Get<SalesDeliveryEditor>().DeleteAsync(delivery.Id, edited.RowVersion))).Succeeded);
        Assert.Empty(await AsAsync(s => s.Get<SalesDeliveryDirectory>().SearchAsync(new DeliverySearch(OrderNo: f.Number))));
        Assert.Equal(60m, (await LinesAsync(f.Number)).Single().OutstandingQuantity); // BR-014 #2: the backorder is not given back
        Assert.True((await AsAsync(s => s.Get<SalesDeliveryEditor>().DeleteAsync(delivery.Id, edited.RowVersion))).Succeeded); // already gone
        Assert.NotNull(order);
    }

    [Fact]
    public async Task Deliveries_are_found_by_invoice_by_order_and_by_day_and_are_in_the_change_history()
    {
        var f = await NewSetupAsync();
        await NewOrderAsync(f, 100, 10m);
        var invoice = Unique("I")[..7];
        var line = (await LinesAsync(f.Number)).Single();
        await TypeAsync(line, 40, invoice: invoice, date: new DateOnly(2026, 11, 2));
        await AsAsync(s => s.Get<SalesDeliveryRegistry>().SaveAsync());

        Task<IReadOnlyList<DeliveryDto>> Find(DeliverySearch search) => AsAsync(s => s.Get<SalesDeliveryDirectory>().SearchAsync(search));
        Assert.Single(await Find(new(InvoiceNo: invoice)));
        Assert.Single(await Find(new(OrderNo: f.Number, Date: new DateOnly(2026, 11, 2))));
        Assert.Empty(await Find(new(InvoiceNo: invoice, Date: new DateOnly(2026, 11, 3))));

        await AsAsync(async s =>
        {
            var rows = await s.Get<HistoryService>().SearchAsync(new(Table: "การส่งของ", RowKey: $"{f.Number} #1 {invoice}"));
            Assert.Contains(rows, r => r is { Action: HistoryActions.Added, Column: "จำนวนส่ง", UserName: "store1" });
            return 0;
        });
    }
}
