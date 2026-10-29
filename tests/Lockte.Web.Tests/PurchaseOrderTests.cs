using Lockte.Org;
using Lockte.Purch;
using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;
using Lockte.Web.Audit;

namespace Lockte.Web.Tests;

/// <summary>ออกใบสั่งซื้อ (#222, SCR-05, BR-020, BR-021, BR-022, BR-023, AC-09, AC-10) และข้อที่ค้างของ #219 (แก้/ลบรายการที่สั่งไปแล้ว).</summary>
[Collection(LocalDbCollection.Name)]
public sealed class PurchaseOrderTests(LocalDbFixture db)
{
    private static readonly DateOnly Oct2026 = new(2026, 10, 29);

    private static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..6];

    private async Task<T> AsBuyerAsync<T>(Func<TestScope, Task<T>> action)
    {
        await using var scope = db.Scope("buyer1");
        return await action(scope);
    }

    private async Task<int> DepartmentAsync()
    {
        await using var scope = db.Scope("hr1");
        var org = scope.Get<OrgRegistry>();
        var section = (await org.SaveSectionAsync(new SectionInput(null, Unique("ฝ่าย"), 1, null))).Id!.Value;
        return (await org.SaveDepartmentAsync(new DepartmentInput(null, Unique("D"), Unique("แผนก"), "d", 3, section, null, [], null))).Id!.Value;
    }

    private async Task<int> MaterialAsync(decimal cost, string name)
        => (await AsBuyerAsync(s => s.Get<MaterialRegistry>().SaveAsync(new MaterialInput(null, Unique("po").ToUpperInvariant(), name, null, "กก.", Cost: cost)))).Id!.Value;

    private async Task<int> SupplierAsync(bool vat = true, int? credit = 30)
        => (await AsBuyerAsync(s => s.Get<SupplierRegistry>().SaveAsync(new SupplierInput(null, Unique("sp").ToUpperInvariant(), "ผู้ขายทดสอบ", null, Tel1: "02-111", CreditDays: credit, Vat: vat)))).Id!.Value;

    /// <summary>A request with one line per (cost, quantity); returns its lines as read back.</summary>
    private async Task<PurchaseRequestDto> RequestAsync(params (decimal Cost, decimal Quantity)[] items)
    {
        var department = await DepartmentAsync();
        var lines = new List<PurchaseRequestLineInput>();
        foreach (var (cost, quantity) in items) lines.Add(new PurchaseRequestLineInput(await MaterialAsync(cost, "วัตถุดิบ " + cost), quantity));
        var no = Unique("Q") + "69-0001";
        var saved = await AsBuyerAsync(s => s.Get<PurchaseRequestRegistry>().SaveAsync(new PurchaseRequestInput(no, Oct2026, department, new DateOnly(2026, 11, 20), "F", "เหตุผล", "หมายเหตุ", lines)));
        return (await AsBuyerAsync(s => s.Get<PurchaseRequestDirectory>().GetAsync(saved.Id!.Value)))!;
    }

    private static string NewOrderNo() => "69" + Random.Shared.Next(1000, 9999).ToString() + Guid.NewGuid().ToString("N")[..3];

    private Task<ServiceResult> IssueAsync(int supplier, string no, bool vat, params PurchaseOrderLineInput[] lines)
        => AsBuyerAsync(s => s.Get<PurchaseOrderRegistry>().IssueAsync(new PurchaseOrderInput(no, Oct2026, supplier, "ผู้ขายทดสอบ", "ที่อยู่ 1", "เขต จังหวัด", "02-111", "02-222", 30, vat, "F", "เหตุผล", null, lines)));

    private Task<PurchaseOrderDto?> FindAsync(string no) => AsBuyerAsync(s => s.Get<PurchaseOrderDirectory>().FindByNoAsync(no));

    [Fact]
    public async Task Two_items_with_vat_give_the_totals_of_BR_022_example_1_and_close_the_request()
    {
        var request = await RequestAsync((950m, 1m), (5430m, 1m));
        var supplier = await SupplierAsync();
        var no = NewOrderNo();
        var issued = await IssueAsync(supplier, no, true, request.Lines.Select(l => new PurchaseOrderLineInput(l.Id, 1m)).ToArray());
        Assert.True(issued.Succeeded);

        var order = (await FindAsync(no))!;
        Assert.Equal((6380.00m, 446.60m, 6826.60m), (order.Subtotal, order.Tax, order.Total));
        Assert.Equal([1016.50m, 5810.10m], order.Lines.Select(l => l.TaxIncludedAmount));
        Assert.Equal("หกพันแปดร้อยยี่สิบหกบาทหกสิบสตางค์", order.TotalInWords);
        Assert.Empty(await AsBuyerAsync(s => s.Get<PurchaseOrderDirectory>().GetOutstandingLinesAsync(request.Id))); // nothing left to order
    }

    [Fact]
    public async Task A_partial_order_leaves_the_rest_as_outstanding_AC_09_and_it_can_be_ordered_again_from_another_supplier()
    {
        var request = await RequestAsync((100m, 6000m));
        var line = request.Lines.Single();
        var first = await SupplierAsync();
        var second = await SupplierAsync();

        Assert.True((await IssueAsync(first, NewOrderNo(), true, new PurchaseOrderLineInput(line.Id, 3000m))).Succeeded);
        var open = Assert.Single(await AsBuyerAsync(s => s.Get<PurchaseOrderDirectory>().GetOutstandingLinesAsync(request.Id)));
        Assert.Equal((6000m, 3000m, 3000m), (open.Requested, open.Ordered, open.Outstanding));

        var no = NewOrderNo();
        Assert.True((await IssueAsync(second, no, false, new PurchaseOrderLineInput(line.Id, 3000m, 95m))).Succeeded);
        Assert.Empty(await AsBuyerAsync(s => s.Get<PurchaseOrderDirectory>().GetOutstandingLinesAsync(request.Id)));
        var order = (await FindAsync(no))!;
        Assert.Equal((285000m, 0m, 285000m, 6000m), (order.Total, order.Tax, order.Lines[0].NetAmount, order.Lines[0].RequestedQuantity));
    }

    [Fact]
    public async Task Ordering_more_than_requested_closes_the_line_AC_10()
    {
        var request = await RequestAsync((10m, 500m));
        Assert.True((await IssueAsync(await SupplierAsync(), NewOrderNo(), true, new PurchaseOrderLineInput(request.Lines.Single().Id, 1000m))).Succeeded);
        Assert.Empty(await AsBuyerAsync(s => s.Get<PurchaseOrderDirectory>().GetOutstandingLinesAsync(request.Id)));
        Assert.DoesNotContain(await AsBuyerAsync(s => s.Get<PurchaseOrderDirectory>().GetRequestsWithOutstandingAsync()), r => r.Id == request.Id);
    }

    [Fact]
    public async Task The_number_is_proposed_as_the_highest_of_the_year_plus_one_and_a_repeated_one_is_refused_BR_020()
    {
        var request = await RequestAsync((10m, 5m), (20m, 5m));
        var supplier = await SupplierAsync();
        var no = "69" + Random.Shared.Next(5000, 9000).ToString();
        Assert.True((await IssueAsync(supplier, no, true, new PurchaseOrderLineInput(request.Lines[0].Id, 1m))).Succeeded);

        var highest = (await AsBuyerAsync(s => s.Get<PurchaseOrderDirectory>().HighestNumberAsync(Oct2026)))!;
        var suggestion = await AsBuyerAsync(s => s.Get<PurchaseOrderDirectory>().SuggestNumberAsync(Oct2026));
        Assert.Equal("69" + (int.Parse(highest[2..]) + 1).ToString("0000"), suggestion);
        Assert.StartsWith("69", suggestion);

        var duplicate = await IssueAsync(supplier, no, true, new PurchaseOrderLineInput(request.Lines[1].Id, 1m));
        Assert.Equal([$"มีเลขที่ใบสั่งซื้อ '{no}' อยู่แล้ว"], duplicate.Errors);
    }

    [Fact]
    public async Task An_order_missing_its_number_a_quantity_or_a_supplier_is_not_saved_BR_023()
    {
        var request = await RequestAsync((10m, 5m));
        var lineId = request.Lines.Single().Id;
        var supplier = await SupplierAsync();
        Assert.Contains("กรุณากรอกเลขที่ใบสั่งซื้อ", (await IssueAsync(supplier, " ", true, new PurchaseOrderLineInput(lineId, 1m))).Errors);
        Assert.Contains("ทุกรายการต้องกรอกจำนวนสั่งซื้อ (มากกว่า 0)", (await IssueAsync(supplier, NewOrderNo(), true, new PurchaseOrderLineInput(lineId, null))).Errors);
        Assert.Contains("กรุณาเลือกผู้ขาย", (await IssueAsync(int.MaxValue, NewOrderNo(), true, new PurchaseOrderLineInput(lineId, 1m))).Errors);
        Assert.Contains("ไม่มีรายการที่จะสั่งซื้อ", (await IssueAsync(supplier, NewOrderNo(), true)).Errors);
        Assert.Contains("ราคาต้องไม่ติดลบ", (await IssueAsync(supplier, NewOrderNo(), true, new PurchaseOrderLineInput(lineId, 1m, -1m))).Errors);
        Assert.Single(await AsBuyerAsync(s => s.Get<PurchaseOrderDirectory>().GetOutstandingLinesAsync(request.Id))); // nothing was ordered
    }

    [Fact]
    public async Task Changing_the_material_of_an_order_line_takes_its_name_and_price_and_the_save_is_in_history()
    {
        var request = await RequestAsync((10m, 5m));
        var other = await MaterialAsync(42m, "วัตถุดิบอื่น");
        var no = NewOrderNo();
        Assert.True((await IssueAsync(await SupplierAsync(), no, true, new PurchaseOrderLineInput(request.Lines.Single().Id, 2m, MaterialId: other))).Succeeded);
        var line = (await FindAsync(no))!.Lines.Single();
        Assert.Equal(("วัตถุดิบอื่น", 42m, 84m), (line.MaterialName, line.UnitPrice, line.NetAmount));

        await using var scope = db.Scope();
        var rows = await scope.Get<HistoryService>().SearchAsync(new(Table: "ใบสั่งซื้อ", RowKey: no));
        Assert.Contains(rows, r => r is { Action: HistoryActions.Added, Column: "ยอดรวมทั้งสิ้น", UserName: "buyer1" });
    }

    [Fact]
    public async Task A_request_line_that_was_ordered_cannot_be_deleted_or_lowered_below_the_order_219()
    {
        var request = await RequestAsync((10m, 5m), (20m, 5m));
        var ordered = request.Lines[0];
        var untouched = request.Lines[1];
        Assert.True((await IssueAsync(await SupplierAsync(), NewOrderNo(), true, new PurchaseOrderLineInput(ordered.Id, 4m))).Succeeded);

        var deleted = await AsBuyerAsync(s => s.Get<PurchaseRequestRegistry>().DeleteLineAsync(ordered.Id, ordered.RowVersion));
        Assert.Equal(["รายการนี้ออกใบสั่งซื้อแล้ว ลบไม่ได้"], deleted.Errors);

        PurchaseRequestUpdate Update(decimal quantity) => new(request.Id, request.RowVersion, request.RequestNo, request.RequestDate, request.DepartmentId, request.DesiredDate, "F", request.Reason, request.Note,
            [new PurchaseRequestLineUpdate(ordered.Id, ordered.RowVersion, ordered.MaterialId, quantity)]);
        var lowered = await AsBuyerAsync(s => s.Get<PurchaseRequestRegistry>().UpdateAsync(Update(3m)));
        Assert.Single(lowered.Errors);
        Assert.Contains("ออกใบสั่งซื้อไปแล้ว 4", lowered.Errors[0]);
        Assert.True((await AsBuyerAsync(s => s.Get<PurchaseRequestRegistry>().UpdateAsync(Update(4m)))).Succeeded); // down to what was ordered is fine
        Assert.True((await AsBuyerAsync(s => s.Get<PurchaseRequestRegistry>().DeleteLineAsync(untouched.Id, untouched.RowVersion))).Succeeded); // the other one was never ordered
    }
}
