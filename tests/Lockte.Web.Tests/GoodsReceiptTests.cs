using Lockte.Org;
using Lockte.Purch;
using Lockte.SharedKernel.Persistence;

namespace Lockte.Web.Tests;

/// <summary>ส่งทัน (BR-030) without a database.</summary>
public sealed class GoodsReceiptMathTests
{
    private static readonly DateOnly Wanted = new(2026, 9, 12);

    [Theory] // BR-030 #1–#4, AC-11
    [InlineData(11, true)]
    [InlineData(12, true)]
    [InlineData(13, false)]
    public void On_time_when_received_on_or_before_the_date_wanted(int day, bool expected)
        => Assert.Equal(expected, GoodsReceiptMath.OnTime(Wanted, new DateOnly(2026, 9, day)));

    [Fact]
    public void No_date_wanted_is_not_on_time()
        => Assert.False(GoodsReceiptMath.OnTime(null, new DateOnly(2026, 9, 11)));
}

/// <summary>การรับของ (#249, SCR-11, BR-030, BR-031, BR-033, AC-11..15, AC-37).</summary>
[Collection(LocalDbCollection.Name)]
public sealed class GoodsReceiptTests(LocalDbFixture db)
{
    private static readonly DateOnly Oct2026 = new(2026, 10, 1);

    private static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..6];

    private async Task<T> AsAsync<T>(Func<TestScope, Task<T>> action)
    {
        await using var scope = db.Scope("buyer1");
        return await action(scope);
    }

    private async Task<int> SupplierAsync()
        => (await AsAsync(s => s.Get<SupplierRegistry>().SaveAsync(new SupplierInput(null, Unique("rc").ToUpperInvariant(), "ผู้ขายรับของ", null, Vat: true)))).Id!.Value;

    /// <summary>An order of one line of a new material: ordered <paramref name="quantity"/> at <paramref name="price"/>, wanted by <paramref name="wanted"/>.</summary>
    private async Task<(int OrderLineId, int MaterialId, int SupplierId, string OrderNo)> OrderAsync(decimal quantity, decimal price, bool main, DateOnly? wanted = null)
    {
        var material = (await AsAsync(s => s.Get<MaterialRegistry>().SaveAsync(new MaterialInput(null, Unique("rm").ToUpperInvariant(), "วัตถุดิบรับ", null, "กก.", Cost: 1m, IsMain: main)))).Id!.Value;
        int department;
        await using (var scope = db.Scope("hr1"))
        {
            var org = scope.Get<OrgRegistry>();
            var section = (await org.SaveSectionAsync(new SectionInput(null, Unique("ฝ่าย"), 1, null))).Id!.Value;
            department = (await org.SaveDepartmentAsync(new DepartmentInput(null, Unique("D"), Unique("แผนก"), "d", 3, section, null, [], null))).Id!.Value;
        }
        var request = await AsAsync(s => s.Get<PurchaseRequestRegistry>().SaveAsync(new PurchaseRequestInput(Unique("R") + "69-0001", Oct2026, department, wanted, "F", null, null,
            [new PurchaseRequestLineInput(material, quantity)])));
        var requestLine = (await AsAsync(s => s.Get<PurchaseRequestDirectory>().GetAsync(request.Id!.Value)))!.Lines.Single();
        var supplier = await SupplierAsync();
        var no = "69" + Random.Shared.Next(1000, 9999) + Guid.NewGuid().ToString("N")[..3];
        var order = await AsAsync(s => s.Get<PurchaseOrderRegistry>().IssueAsync(new PurchaseOrderInput(no, Oct2026, supplier, "ผู้ขายรับของ", null, null, null, null, 30, true, "F", null, null,
            [new PurchaseOrderLineInput(requestLine.Id, quantity, price)])));
        var orderLine = (await AsAsync(s => s.Get<PurchaseOrderDirectory>().GetAsync(order.Id!.Value)))!.Lines.Single();
        return (orderLine.Id, material, supplier, no);
    }

    private Task<Lockte.SharedKernel.ServiceResult> ReceiveAsync(params GoodsReceiptInput[] inputs) => AsAsync(s => s.Get<GoodsReceiptRegistry>().ReceiveAsync(inputs));

    private Task<IReadOnlyList<GoodsReceiptDto>> ReceiptsAsync(int line) => AsAsync(s => s.Get<GoodsReceiptDirectory>().GetReceiptsAsync(line));

    private Task<decimal> StockAsync(int material) => AsAsync(s => s.Get<StockDirectory>().GetBalanceAsync(material));

    [Fact]
    public async Task A_main_material_received_in_full_goes_into_the_stock_and_closes_the_line_BR_031_1_AC_12()
    {
        var order = await OrderAsync(400m, 96m, main: true);
        Assert.True((await ReceiveAsync(new GoodsReceiptInput(order.OrderLineId, Oct2026, 400m, "INV-1", "1"))).Succeeded);

        Assert.Equal(400m, await StockAsync(order.MaterialId));
        var stock = Assert.Single(await AsAsync(s => s.Get<StockDirectory>().GetEntriesAsync(order.MaterialId)));
        Assert.Equal((StockEntryKind.Receipt, "INV-1", 400m), (stock.Kind, stock.DocumentNo, stock.Quantity));
        Assert.DoesNotContain(await AsAsync(s => s.Get<GoodsReceiptDirectory>().GetOrdersAsync()), o => o.OrderNo == order.OrderNo);
        Assert.Empty(await AsAsync(async s => await s.Get<GoodsReceiptDirectory>().GetReceivableLinesAsync((await s.Get<PurchaseOrderDirectory>().FindByNoAsync(order.OrderNo))!.Id)));
    }

    [Fact]
    public async Task A_partial_receipt_leaves_the_rest_waiting_AC_13_and_a_later_one_closes_it()
    {
        var order = await OrderAsync(200m, 10m, main: false);
        Assert.True((await ReceiveAsync(new GoodsReceiptInput(order.OrderLineId, Oct2026, 50m))).Succeeded);
        var open = Assert.Single(await AsAsync(async s => await s.Get<GoodsReceiptDirectory>().GetReceivableLinesAsync((await s.Get<PurchaseOrderDirectory>().FindByNoAsync(order.OrderNo))!.Id)));
        Assert.Equal((200m, 50m, 150m), (open.Ordered, open.Received, open.Outstanding));

        Assert.True((await ReceiveAsync(new GoodsReceiptInput(order.OrderLineId, Oct2026.AddDays(3), 150m))).Succeeded);
        Assert.Equal([150m, 0m], (await ReceiptsAsync(order.OrderLineId)).Select(r => r.OutstandingAfter));
        Assert.DoesNotContain(await AsAsync(s => s.Get<GoodsReceiptDirectory>().GetOrdersAsync()), o => o.OrderNo == order.OrderNo);
    }

    [Fact]
    public async Task A_material_that_is_not_main_does_not_go_into_the_stock_AC_14()
    {
        var order = await OrderAsync(10m, 5m, main: false);
        Assert.True((await ReceiveAsync(new GoodsReceiptInput(order.OrderLineId, Oct2026, 1m))).Succeeded);
        Assert.Equal(0m, await StockAsync(order.MaterialId));
        Assert.Single(await ReceiptsAsync(order.OrderLineId));
    }

    [Fact]
    public async Task The_amount_follows_BR_030_5_and_6_and_an_over_receipt_leaves_a_negative_outstanding()
    {
        var order = await OrderAsync(200m, 1195.271m, main: false);
        Assert.True((await ReceiveAsync(new GoodsReceiptInput(order.OrderLineId, Oct2026, 50m))).Succeeded);
        var first = Assert.Single(await ReceiptsAsync(order.OrderLineId));
        Assert.Equal((59763.55m, 150m), (first.NetAmount, first.OutstandingAfter));

        var over = await OrderAsync(5000m, 96m, main: false);
        Assert.True((await ReceiveAsync(new GoodsReceiptInput(over.OrderLineId, Oct2026, 10042.8m))).Succeeded);
        var received = Assert.Single(await ReceiptsAsync(over.OrderLineId));
        Assert.Equal((964108.80m, -5042.8m), (received.NetAmount, received.OutstandingAfter));
    }

    [Fact]
    public async Task On_time_follows_the_wanted_date_and_is_recomputed_not_stored_DQ_11()
    {
        var order = await OrderAsync(10m, 1m, main: false, wanted: new DateOnly(2026, 9, 12));
        await ReceiveAsync(new GoodsReceiptInput(order.OrderLineId, new DateOnly(2026, 9, 11), 2m), new GoodsReceiptInput(order.OrderLineId, new DateOnly(2026, 9, 13), 2m));
        Assert.Equal([true, false], (await ReceiptsAsync(order.OrderLineId)).Select(r => r.OnTime));

        var noDate = await OrderAsync(10m, 1m, main: false);
        await ReceiveAsync(new GoodsReceiptInput(noDate.OrderLineId, Oct2026, 1m));
        Assert.False(Assert.Single(await ReceiptsAsync(noDate.OrderLineId)).OnTime);
    }

    [Fact]
    public async Task The_registers_latest_price_and_supplier_are_set_by_the_material_id_AC_15()
    {
        var order = await OrderAsync(10m, 96m, main: true);
        var twin = await AsAsync(s => s.Get<MaterialRegistry>().SaveAsync(new MaterialInput(null, Unique("tw").ToUpperInvariant(), "วัตถุดิบรับ", null, "กก.", Cost: 7m))); // the same name, another material (DQ-02)
        var other = await SupplierAsync();
        Assert.True((await ReceiveAsync(new GoodsReceiptInput(order.OrderLineId, Oct2026, 4m, UnitPrice: 99m, SupplierId: other))).Succeeded);

        var received = (await AsAsync(s => s.Get<MaterialDirectory>().GetAsync(order.MaterialId)))!;
        Assert.Equal((99m, other), (received.Cost, received.LatestSupplierId));
        var untouched = (await AsAsync(s => s.Get<MaterialDirectory>().GetAsync(twin.Id!.Value)))!;
        Assert.Equal((7m, null), (untouched.Cost, untouched.LatestSupplierId));
    }

    [Fact]
    public async Task Two_orders_received_one_after_the_other_and_an_empty_quantity_line_is_left_out_BR_031_4()
    {
        var first = await OrderAsync(5m, 1m, main: true);
        var second = await OrderAsync(8m, 1m, main: true);
        Assert.True((await ReceiveAsync(new GoodsReceiptInput(first.OrderLineId, Oct2026, 5m), new GoodsReceiptInput(second.OrderLineId, Oct2026, null))).Succeeded);
        Assert.True((await ReceiveAsync(new GoodsReceiptInput(second.OrderLineId, Oct2026, 8m))).Succeeded);
        Assert.Equal((5m, 8m), (await StockAsync(first.MaterialId), await StockAsync(second.MaterialId)));
        Assert.Single(await ReceiptsAsync(first.OrderLineId));
        Assert.Single(await ReceiptsAsync(second.OrderLineId));
    }

    [Fact]
    public async Task A_cancelled_line_leaves_the_outstanding_but_keeps_the_order_and_what_was_received_AC_37()
    {
        var order = await OrderAsync(10m, 1m, main: true);
        await ReceiveAsync(new GoodsReceiptInput(order.OrderLineId, Oct2026, 3m));
        var line = (await AsAsync(async s => await s.Get<GoodsReceiptDirectory>().GetReceivableLinesAsync((await s.Get<PurchaseOrderDirectory>().FindByNoAsync(order.OrderNo))!.Id))).Single();

        Assert.True((await AsAsync(s => s.Get<GoodsReceiptRegistry>().CancelOutstandingAsync(line.OrderLineId, line.RowVersion))).Succeeded);
        Assert.DoesNotContain(await AsAsync(s => s.Get<GoodsReceiptDirectory>().GetOrdersAsync()), o => o.OrderNo == order.OrderNo);
        Assert.NotNull(await AsAsync(s => s.Get<PurchaseOrderDirectory>().FindByNoAsync(order.OrderNo))); // the number is untouched
        Assert.Equal(3m, await StockAsync(order.MaterialId));
        Assert.Contains("รายการใบสั่งซื้อที่ยกเลิกการรับแล้ว รับของไม่ได้", (await ReceiveAsync(new GoodsReceiptInput(order.OrderLineId, Oct2026, 1m))).Errors);
    }

    [Fact]
    public async Task A_received_order_line_cannot_be_deleted_from_the_order_and_bad_input_is_refused()
    {
        var order = await OrderAsync(10m, 1m, main: false);
        await ReceiveAsync(new GoodsReceiptInput(order.OrderLineId, Oct2026, 1m));
        var dto = (await AsAsync(s => s.Get<PurchaseOrderDirectory>().FindByNoAsync(order.OrderNo)))!;
        var refused = await AsAsync(s => s.Get<PurchaseOrderRegistry>().DeleteLineAsync(dto.Lines[0].Id, dto.Lines[0].RowVersion));
        Assert.Equal(["รายการนี้มีการรับของแล้ว ลบไม่ได้"], refused.Errors);

        Assert.Contains("จำนวนรับต้องไม่ติดลบ", (await ReceiveAsync(new GoodsReceiptInput(order.OrderLineId, Oct2026, -1m))).Errors);
        Assert.Contains("ไม่พบรายการใบสั่งซื้อที่รับ (อาจถูกลบไปแล้ว)", (await ReceiveAsync(new GoodsReceiptInput(int.MaxValue, Oct2026, 1m))).Errors);
    }

    // ---------- #250: ไม่ผ่าน (BR-032) และแก้/ลบประวัติ (BR-034) ----------

    private Task<GoodsReceiptUpdate> UpdateOf(GoodsReceiptDto r, int supplier, decimal? quantity = null, decimal? price = null, bool? failed = null)
        => Task.FromResult(new GoodsReceiptUpdate(r.Id, r.RowVersion, r.ReceiveDate, quantity ?? r.Quantity, r.InvoiceNo, r.InvoiceLineNo, price ?? r.UnitPrice, supplier, r.MaterialId, r.MaterialName, r.Unit,
            failed ?? r.Failed, r.ReturnNote));

    [Fact]
    public async Task A_failed_receipt_is_kept_as_evidence_but_is_not_received_and_not_in_the_stock_BR_032_AC_16()
    {
        var order = await OrderAsync(1000m, 10m, main: true);
        Assert.True((await ReceiveAsync(new GoodsReceiptInput(order.OrderLineId, Oct2026, 1000m, Failed: true))).Succeeded);

        var receipt = Assert.Single(await ReceiptsAsync(order.OrderLineId));
        Assert.True(receipt.Failed);
        Assert.Equal(1000m, receipt.OutstandingAfter); // the order still waits for all of it
        Assert.Equal(0m, await StockAsync(order.MaterialId));
        var line = Assert.Single(await AsAsync(async s => await s.Get<GoodsReceiptDirectory>().GetReceivableLinesAsync((await s.Get<PurchaseOrderDirectory>().FindByNoAsync(order.OrderNo))!.Id)));
        Assert.Equal((0m, 1000m), (line.Received, line.Outstanding));
        var material = (await AsAsync(s => s.Get<MaterialDirectory>().GetAsync(order.MaterialId)))!;
        Assert.Equal(1m, material.Cost); // the register's price is not set by a failed delivery

        Assert.True((await ReceiveAsync(new GoodsReceiptInput(order.OrderLineId, Oct2026.AddDays(2), 1000m))).Succeeded); // the supplier delivers again, accepted
        Assert.Equal(1000m, await StockAsync(order.MaterialId));
        Assert.Equal([1000m, 0m], (await ReceiptsAsync(order.OrderLineId)).Select(r => r.OutstandingAfter));
    }

    [Fact]
    public async Task Editing_a_receipt_recomputes_the_amount_and_leaves_the_stock_and_the_order_alone_BR_034_1_AC_38()
    {
        var order = await OrderAsync(400m, 96m, main: true);
        await ReceiveAsync(new GoodsReceiptInput(order.OrderLineId, Oct2026, 400m));
        var receipt = Assert.Single(await ReceiptsAsync(order.OrderLineId));
        Assert.Equal(400m, await StockAsync(order.MaterialId));

        Assert.True((await AsAsync(async s => await s.Get<GoodsReceiptRegistry>().UpdateAsync(await UpdateOf(receipt, order.SupplierId, quantity: 380m, price: 100m)))).Succeeded);
        var edited = Assert.Single(await ReceiptsAsync(order.OrderLineId));
        Assert.Equal((380m, 38000m), (edited.Quantity, edited.NetAmount));
        Assert.Equal(400m, await StockAsync(order.MaterialId)); // the stock is still +400
        Assert.DoesNotContain(await AsAsync(s => s.Get<GoodsReceiptDirectory>().GetOrdersAsync()), o => o.OrderNo == order.OrderNo); // not back to ยอดค้างรับ
        Assert.Single(await AsAsync(s => s.Get<GoodsReceiptDirectory>().GetHistoryOrdersAsync()), o => o.OrderNo == order.OrderNo);

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            AsAsync(async s => await s.Get<GoodsReceiptRegistry>().UpdateAsync(await UpdateOf(receipt, order.SupplierId, quantity: 390m))));
    }

    [Fact]
    public async Task Deleting_a_receipt_does_not_take_the_stock_back_or_reopen_the_order_BR_034_2()
    {
        var order = await OrderAsync(100m, 5m, main: true);
        await ReceiveAsync(new GoodsReceiptInput(order.OrderLineId, Oct2026, 100m));
        var receipt = Assert.Single(await ReceiptsAsync(order.OrderLineId));

        Assert.True((await AsAsync(s => s.Get<GoodsReceiptRegistry>().DeleteAsync(receipt.Id, receipt.RowVersion))).Succeeded);
        Assert.Empty(await ReceiptsAsync(order.OrderLineId));
        Assert.Equal(100m, await StockAsync(order.MaterialId));
        Assert.DoesNotContain(await AsAsync(s => s.Get<GoodsReceiptDirectory>().GetOrdersAsync()), o => o.OrderNo == order.OrderNo);
        Assert.NotNull(await AsAsync(s => s.Get<PurchaseOrderDirectory>().FindByNoAsync(order.OrderNo))); // the order number stays
        Assert.True((await AsAsync(s => s.Get<GoodsReceiptRegistry>().DeleteAsync(receipt.Id, receipt.RowVersion))).Succeeded); // already gone
    }

    [Fact]
    public async Task A_partly_received_order_stays_waiting_when_a_receipt_is_corrected_and_the_material_can_be_changed()
    {
        var order = await OrderAsync(200m, 10m, main: false);
        await ReceiveAsync(new GoodsReceiptInput(order.OrderLineId, Oct2026, 50m));
        var receipt = Assert.Single(await ReceiptsAsync(order.OrderLineId));
        var other = (await AsAsync(s => s.Get<MaterialRegistry>().SaveAsync(new MaterialInput(null, Unique("ch").ToUpperInvariant(), "วัตถุดิบที่แก้ให้ถูก", null, "ใบ")))).Id!.Value;

        var update = new GoodsReceiptUpdate(receipt.Id, receipt.RowVersion, receipt.ReceiveDate, 30m, receipt.InvoiceNo, receipt.InvoiceLineNo, receipt.UnitPrice, order.SupplierId, other, null, null, false, "ส่งคืน 20");
        Assert.True((await AsAsync(s => s.Get<GoodsReceiptRegistry>().UpdateAsync(update))).Succeeded);
        var edited = Assert.Single(await ReceiptsAsync(order.OrderLineId));
        Assert.Equal((other, "วัตถุดิบที่แก้ให้ถูก", "ใบ", 170m, "ส่งคืน 20"), (edited.MaterialId, edited.MaterialName, edited.Unit, edited.OutstandingAfter, edited.ReturnNote));
        Assert.Contains(await AsAsync(s => s.Get<GoodsReceiptDirectory>().GetOrdersAsync()), o => o.OrderNo == order.OrderNo); // still waiting for the rest

        Assert.Contains("จำนวนรับต้องมากกว่า 0 (ถ้าไม่ต้องการรายการนี้ ใช้ปุ่มลบ)",
            (await AsAsync(s => s.Get<GoodsReceiptRegistry>().UpdateAsync(update with { RowVersion = edited.RowVersion, Quantity = 0m }))).Errors);
    }

    // ---------- #251: QA รับวัตถุดิบ (BR-035, AC-39) ----------

    [Fact]
    public async Task A_date_lists_everything_received_that_day_main_or_not_BR_035_1()
    {
        var day = new DateOnly(2026, 8, Random.Shared.Next(1, 28));
        var main = await OrderAsync(10m, 1m, main: true);
        var other = await OrderAsync(10m, 1m, main: false);
        var elsewhere = await OrderAsync(10m, 1m, main: true);
        await ReceiveAsync(new GoodsReceiptInput(main.OrderLineId, day, 4m), new GoodsReceiptInput(other.OrderLineId, day, 2m), new GoodsReceiptInput(elsewhere.OrderLineId, day.AddDays(1), 1m));

        var received = await AsAsync(s => s.Get<GoodsReceiptDirectory>().GetByDateAsync(day));
        Assert.Contains(received, r => r.OrderNo == main.OrderNo);
        Assert.Contains(received, r => r.OrderNo == other.OrderNo); // not filtered by วัตถุดิบหลัก (AC-39)
        Assert.DoesNotContain(received, r => r.OrderNo == elsewhere.OrderNo);
        Assert.All(received, r => Assert.Equal(day, r.ReceiveDate));
    }

    [Fact]
    public async Task QA_writes_the_lot_result_certificate_and_note_and_marking_failed_leaves_the_stock_BR_035_2_AC_39()
    {
        var order = await OrderAsync(10m, 1m, main: true);
        await ReceiveAsync(new GoodsReceiptInput(order.OrderLineId, Oct2026, 10m));
        var receipt = Assert.Single(await ReceiptsAsync(order.OrderLineId));
        Assert.Equal(10m, await StockAsync(order.MaterialId));

        Assert.True((await AsAsync(s => s.Get<GoodsReceiptRegistry>().SaveQaAsync(new GoodsReceiptQaUpdate(receipt.Id, receipt.RowVersion, " L-0911 ", false, true, "ตรวจแล้ว")))).Succeeded);
        var passed = Assert.Single(await ReceiptsAsync(order.OrderLineId));
        Assert.Equal(("L-0911", false, true, "ตรวจแล้ว"), (passed.Lot, passed.Failed, passed.HasCertificate, passed.QaNote));

        Assert.True((await AsAsync(s => s.Get<GoodsReceiptRegistry>().SaveQaAsync(new GoodsReceiptQaUpdate(passed.Id, passed.RowVersion, "L-0911", true, false, "ไม่ผ่าน")))).Succeeded);
        var failed = Assert.Single(await ReceiptsAsync(order.OrderLineId));
        Assert.True(failed.Failed);
        Assert.Equal(10m, await StockAsync(order.MaterialId)); // the stock is unchanged
        Assert.DoesNotContain(await AsAsync(s => s.Get<GoodsReceiptDirectory>().GetOrdersAsync()), o => o.OrderNo == order.OrderNo); // and the order does not go back to waiting

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            AsAsync(s => s.Get<GoodsReceiptRegistry>().SaveQaAsync(new GoodsReceiptQaUpdate(passed.Id, passed.RowVersion, "ซ้ำ", false, null, null))));
    }

    [Fact]
    public async Task QA_can_correct_the_material_name_and_the_supplier_on_the_document()
    {
        var order = await OrderAsync(10m, 1m, main: false);
        await ReceiveAsync(new GoodsReceiptInput(order.OrderLineId, Oct2026, 3m));
        var receipt = Assert.Single(await ReceiptsAsync(order.OrderLineId));
        var supplier = await SupplierAsync();
        Assert.True((await AsAsync(s => s.Get<GoodsReceiptRegistry>().SaveQaAsync(new GoodsReceiptQaUpdate(receipt.Id, receipt.RowVersion, null, false, null, null, MaterialName: "ชื่อที่ QA แก้", Unit: "ถัง", SupplierId: supplier)))).Succeeded);
        var edited = Assert.Single(await ReceiptsAsync(order.OrderLineId));
        Assert.Equal(("ชื่อที่ QA แก้", "ถัง", supplier), (edited.MaterialName, edited.Unit, edited.SupplierId));
        Assert.Contains("ไม่พบผู้ขายที่เลือก", (await AsAsync(s => s.Get<GoodsReceiptRegistry>().SaveQaAsync(new GoodsReceiptQaUpdate(edited.Id, edited.RowVersion, null, false, null, null, SupplierId: int.MaxValue)))).Errors);
    }
}
