using Lockte.Org;
using Lockte.Purch;
using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;

namespace Lockte.Web.Tests;

/// <summary>แก้ไข / ลบรายการ / ค้นใบสั่งซื้อ (#223, SCR-07, SCR-09, BR-024, BR-025, AC-35).</summary>
[Collection(LocalDbCollection.Name)]
public sealed class PurchaseOrderEditTests(LocalDbFixture db)
{
    private static readonly DateOnly Oct2026 = new(2026, 10, 29);

    private static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..6];

    private async Task<T> AsBuyerAsync<T>(Func<TestScope, Task<T>> action)
    {
        await using var scope = db.Scope("buyer1");
        return await action(scope);
    }

    private async Task<int> SupplierAsync()
        => (await AsBuyerAsync(s => s.Get<SupplierRegistry>().SaveAsync(new SupplierInput(null, Unique("se").ToUpperInvariant(), "ผู้ขายทดสอบ", null, Vat: true)))).Id!.Value;

    /// <summary>An order of three items (950, 5,430 and 100 baht, quantity 1) for the given supplier, read back.</summary>
    private async Task<(PurchaseOrderDto Order, PurchaseRequestDto Request)> NewOrderAsync(int supplier)
    {
        int department;
        await using (var scope = db.Scope("hr1"))
        {
            var org = scope.Get<OrgRegistry>();
            var section = (await org.SaveSectionAsync(new SectionInput(null, Unique("ฝ่าย"), 1, null))).Id!.Value;
            department = (await org.SaveDepartmentAsync(new DepartmentInput(null, Unique("D"), Unique("แผนก"), "d", 3, section, null, [], null))).Id!.Value;
        }
        var lines = new List<PurchaseRequestLineInput>();
        foreach (var cost in new[] { 950m, 5430m, 100m })
        {
            var material = (await AsBuyerAsync(s => s.Get<MaterialRegistry>().SaveAsync(new MaterialInput(null, Unique("oe").ToUpperInvariant(), "วัตถุดิบ " + cost, null, "ตัว", Cost: cost)))).Id!.Value;
            lines.Add(new PurchaseRequestLineInput(material, 1));
        }
        var saved = await AsBuyerAsync(s => s.Get<PurchaseRequestRegistry>().SaveAsync(new PurchaseRequestInput(Unique("W") + "69-0001", Oct2026, department, null, "F", null, null, lines)));
        var request = (await AsBuyerAsync(s => s.Get<PurchaseRequestDirectory>().GetAsync(saved.Id!.Value)))!;
        var no = "69" + Random.Shared.Next(1000, 9999) + Guid.NewGuid().ToString("N")[..3];
        var issued = await AsBuyerAsync(s => s.Get<PurchaseOrderRegistry>().IssueAsync(new PurchaseOrderInput(no, Oct2026, supplier, "ผู้ขายทดสอบ", null, null, null, null, 30, true, "F", null, null,
            request.Lines.Select(l => new PurchaseOrderLineInput(l.Id, 1m)).ToList())));
        Assert.True(issued.Succeeded);
        return ((await AsBuyerAsync(s => s.Get<PurchaseOrderDirectory>().GetAsync(issued.Id!.Value)))!, request);
    }

    private static PurchaseOrderUpdate Update(PurchaseOrderDto o, int? supplier = null, bool? vat = null, string? no = null, Func<PurchaseOrderLineDto, PurchaseOrderLineUpdate>? line = null)
        => new(o.Id, o.RowVersion, no ?? o.OrderNo, o.OrderDate, supplier ?? o.SupplierId, o.SupplierName, o.SupplierAddress1, o.SupplierAddress2, o.SupplierTel, o.SupplierFax, o.CreditDays,
            vat ?? o.ChargesVat, o.PurchasingLocation ?? "F", o.Reason, o.Note,
            o.Lines.Select(l => line?.Invoke(l) ?? new PurchaseOrderLineUpdate(l.Id, l.RowVersion, l.Quantity)).ToList());

    private Task<PurchaseOrderDto> ReadAsync(int id) => AsBuyerAsync(async s => (await s.Get<PurchaseOrderDirectory>().GetAsync(id))!);

    [Fact]
    public async Task Editing_the_price_recomputes_the_stored_totals_BR_024_1_and_the_order_is_found_by_number()
    {
        var (order, _) = await NewOrderAsync(await SupplierAsync());
        Assert.Contains(await AsBuyerAsync(s => s.Get<PurchaseOrderDirectory>().SearchAsync(order.OrderNo[2..])), o => o.OrderNo == order.OrderNo);
        Assert.Equal((6480.00m, 453.60m, 6933.60m), (order.Subtotal, order.Tax, order.Total)); // 950 + 5,430 + 100

        Assert.True((await AsBuyerAsync(s => s.Get<PurchaseOrderRegistry>().UpdateAsync(
            Update(order, line: l => new PurchaseOrderLineUpdate(l.Id, l.RowVersion, l.Quantity, l.NetAmount == 950m ? 1000m : l.UnitPrice))))).Succeeded);

        var edited = await ReadAsync(order.Id);
        Assert.Equal((6530m, 457.10m, 6987.10m), (edited.Subtotal, edited.Tax, edited.Total)); // not the old totals (the legacy kept them)
        Assert.Equal(1070.00m, edited.Lines.Single(l => l.UnitPrice == 1000m).TaxIncludedAmount);
    }

    [Fact]
    public async Task Changing_the_supplier_and_the_vat_and_the_quantity_is_saved_with_the_new_total()
    {
        var first = await SupplierAsync();
        var second = await SupplierAsync();
        var (order, _) = await NewOrderAsync(first);

        Assert.True((await AsBuyerAsync(s => s.Get<PurchaseOrderRegistry>().UpdateAsync(
            Update(order, supplier: second, vat: false, line: l => new PurchaseOrderLineUpdate(l.Id, l.RowVersion, l.UnitPrice == 100m ? 3m : l.Quantity))))).Succeeded);
        var edited = await ReadAsync(order.Id);
        Assert.Equal((second, 0m, 6680m), (edited.SupplierId, edited.Tax, edited.Total));
    }

    [Fact]
    public async Task A_repeated_number_a_zero_quantity_and_a_stale_edit_are_refused()
    {
        var supplier = await SupplierAsync();
        var (order, _) = await NewOrderAsync(supplier);
        var (other, _) = await NewOrderAsync(supplier);

        Assert.Equal([$"มีเลขที่ใบสั่งซื้อ '{other.OrderNo}' อยู่แล้ว"], (await AsBuyerAsync(s => s.Get<PurchaseOrderRegistry>().UpdateAsync(Update(order, no: other.OrderNo)))).Errors);
        var zero = await AsBuyerAsync(s => s.Get<PurchaseOrderRegistry>().UpdateAsync(Update(order, line: l => new PurchaseOrderLineUpdate(l.Id, l.RowVersion, 0))));
        Assert.Contains("ทุกรายการต้องกรอกจำนวนสั่งซื้อ (มากกว่า 0) — ถ้าไม่ต้องการรายการนี้ ใช้ปุ่มลบรายการ", zero.Errors);

        Assert.True((await AsBuyerAsync(s => s.Get<PurchaseOrderRegistry>().UpdateAsync(Update(order, vat: false)))).Succeeded);
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => AsBuyerAsync(s => s.Get<PurchaseOrderRegistry>().UpdateAsync(Update(order, vat: true))));
    }

    [Fact]
    public async Task Deleting_items_keeps_the_number_until_the_last_and_does_not_hand_the_request_back_BR_025_AC_35()
    {
        var supplier = await SupplierAsync();
        var (order, request) = await NewOrderAsync(supplier);
        var registry = (TestScope s) => s.Get<PurchaseOrderRegistry>();

        var first = order.Lines[0];
        Assert.True((await AsBuyerAsync(s => registry(s).DeleteLineAsync(first.Id, first.RowVersion))).Succeeded);
        var after = await ReadAsync(order.Id);
        Assert.Equal(2, after.Lines.Count);
        Assert.Equal(after.Lines.Sum(l => l.NetAmount), after.Subtotal); // the totals follow
        Assert.Empty(await AsBuyerAsync(s => s.Get<PurchaseOrderDirectory>().GetOutstandingLinesAsync(request.Id))); // the request is not offered again (BR-025 ⚠)

        foreach (var line in after.Lines) Assert.True((await AsBuyerAsync(s => registry(s).DeleteLineAsync(line.Id, line.RowVersion))).Succeeded);
        Assert.Null(await AsBuyerAsync(s => s.Get<PurchaseOrderDirectory>().FindByNoAsync(order.OrderNo)));
        Assert.True((await AsBuyerAsync(s => registry(s).DeleteLineAsync(first.Id, first.RowVersion))).Succeeded); // already gone: fine
        Assert.Empty(await AsBuyerAsync(s => s.Get<PurchaseOrderDirectory>().GetOutstandingLinesAsync(request.Id)));
    }
}
