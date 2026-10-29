using Lockte.Org;
using Lockte.Purch;
using Lockte.Web.Reports;

namespace Lockte.Web.Tests;

/// <summary>RPT-03 / RPT-04 (#256) without a database: what the two lists print.</summary>
public sealed class PurchBacklogViewTests
{
    private static async Task<string> RenderAsync<T>(object lines) where T : Microsoft.AspNetCore.Components.IComponent
        => System.Net.WebUtility.HtmlDecode(await ReportBody.RenderAsync<T>(new() { ["Lines"] = lines }));

    [Fact]
    public async Task The_request_list_prints_the_quantity_left_to_order()
    {
        var line = new OutstandingRequestLine(1, "D69-0074", new DateOnly(2026, 9, 1), 3, 5, "AL001", "อลูมิเนียม", "กก.", 6000m, 3000m, 3000m, 96m, null, new DateOnly(2026, 9, 20), 1, null, null, "F");
        var html = await RenderAsync<Lockte.Web.Reports.Purch.PurchRequestBacklogView>(new List<OutstandingRequestLine> { line });
        Assert.Contains("D69-0074", html);
        Assert.Contains("3,000.00", html); // ยอดค้างสั่ง AC-09
        Assert.Contains("อลูมิเนียม", html);
    }

    [Fact]
    public async Task The_order_list_prints_the_quantity_left_to_receive_with_the_suppliers_phone()
    {
        var line = new OutstandingReceiptLine("690817", new DateOnly(2026, 8, 1), 1, "แก๊ส", "ถัง", 150m, new DateOnly(2026, 8, 20), "ผู้ขายแก๊ส", "02-111-1111");
        var html = await RenderAsync<Lockte.Web.Reports.Purch.PurchOrderBacklogView>(new List<OutstandingReceiptLine> { line });
        Assert.Contains("690817", html);
        Assert.Contains("150.00", html); // ยอดค้างรับ AC-13
        Assert.Contains("ผู้ขายแก๊ส · 02-111-1111", html);
    }

    [Fact]
    public async Task Nothing_open_prints_no_data()
    {
        Assert.Contains("ไม่มีข้อมูล", await RenderAsync<Lockte.Web.Reports.Purch.PurchRequestBacklogView>(new List<OutstandingRequestLine>()));
        Assert.Contains("ไม่มีข้อมูล", await RenderAsync<Lockte.Web.Reports.Purch.PurchOrderBacklogView>(new List<OutstandingReceiptLine>()));
    }
}

/// <summary>RPT-03 / RPT-04 (#256): the live lists follow the requests, orders and receipts (AC-09, AC-13).</summary>
[Collection(LocalDbCollection.Name)]
public sealed class PurchBacklogDataTests(LocalDbFixture db)
{
    private static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..6];

    private async Task<T> AsAsync<T>(Func<TestScope, Task<T>> action)
    {
        await using var scope = db.Scope("buyer1");
        return await action(scope);
    }

    private async Task<(string RequestNo, int RequestLine, string OrderNo, int OrderLine)> OrderedAsync(decimal requested, decimal ordered)
    {
        var material = (await AsAsync(s => s.Get<MaterialRegistry>().SaveAsync(new MaterialInput(null, Unique("bl").ToUpperInvariant(), "วัตถุดิบค้าง", null, "กก.")))).Id!.Value;
        var supplier = (await AsAsync(s => s.Get<SupplierRegistry>().SaveAsync(new SupplierInput(null, Unique("bs").ToUpperInvariant(), "ผู้ขายค้าง", null, Tel1: "02-999")))).Id!.Value;
        int department;
        await using (var scope = db.Scope("hr1"))
        {
            var org = scope.Get<OrgRegistry>();
            var section = (await org.SaveSectionAsync(new SectionInput(null, Unique("ฝ่าย"), 1, null))).Id!.Value;
            department = (await org.SaveDepartmentAsync(new DepartmentInput(null, Unique("D"), Unique("แผนก"), "d", 3, section, null, [], null))).Id!.Value;
        }
        var requestNo = Unique("B") + "69-0001";
        var request = await AsAsync(s => s.Get<PurchaseRequestRegistry>().SaveAsync(new PurchaseRequestInput(requestNo, new DateOnly(2026, 9, 1), department, new DateOnly(2026, 9, 20), "F", null, null, [new PurchaseRequestLineInput(material, requested)])));
        var requestLine = (await AsAsync(s => s.Get<PurchaseRequestDirectory>().GetAsync(request.Id!.Value)))!.Lines.Single();
        var orderNo = "69" + Random.Shared.Next(1000, 9999) + Guid.NewGuid().ToString("N")[..3];
        var order = await AsAsync(s => s.Get<PurchaseOrderRegistry>().IssueAsync(new PurchaseOrderInput(orderNo, new DateOnly(2026, 9, 2), supplier, "ผู้ขายค้าง", null, null, "02-999", null, 30, true, "F", null, null,
            [new PurchaseOrderLineInput(requestLine.Id, ordered, 10m)])));
        var orderLine = (await AsAsync(s => s.Get<PurchaseOrderDirectory>().GetAsync(order.Id!.Value)))!.Lines.Single();
        return (requestNo, requestLine.Id, orderNo, orderLine.Id);
    }

    [Fact] // AC-09: asked 6,000, ordered 3,000 → 3,000 still to order, 3,000 still to receive
    public async Task A_partial_order_is_on_both_lists_with_what_is_left_AC_09()
    {
        var a = await OrderedAsync(6000m, 3000m);
        var requests = await AsAsync(s => s.Get<PurchaseOrderDirectory>().GetAllOutstandingLinesAsync());
        Assert.Equal(3000m, requests.Single(r => r.RequestNo == a.RequestNo).Outstanding);
        var orders = await AsAsync(s => s.Get<GoodsReceiptDirectory>().GetAllOutstandingAsync());
        var open = orders.Single(o => o.OrderNo == a.OrderNo);
        Assert.Equal((3000m, "02-999"), (open.Outstanding, open.SupplierTel));
    }

    [Fact] // AC-13: ordered 200, received 50 → 150 left; a closed line is on neither list
    public async Task A_partial_receipt_leaves_the_rest_and_a_closed_line_drops_off_AC_13()
    {
        var a = await OrderedAsync(200m, 200m);
        await AsAsync(s => s.Get<GoodsReceiptRegistry>().ReceiveAsync([new GoodsReceiptInput(a.OrderLine, new DateOnly(2026, 9, 5), 50m)]));
        Assert.Equal(150m, (await AsAsync(s => s.Get<GoodsReceiptDirectory>().GetAllOutstandingAsync())).Single(o => o.OrderNo == a.OrderNo).Outstanding);
        Assert.DoesNotContain(await AsAsync(s => s.Get<PurchaseOrderDirectory>().GetAllOutstandingLinesAsync()), r => r.RequestNo == a.RequestNo); // fully ordered

        await AsAsync(s => s.Get<GoodsReceiptRegistry>().ReceiveAsync([new GoodsReceiptInput(a.OrderLine, new DateOnly(2026, 9, 8), 150m)]));
        Assert.DoesNotContain(await AsAsync(s => s.Get<GoodsReceiptDirectory>().GetAllOutstandingAsync()), o => o.OrderNo == a.OrderNo);
    }

    [Fact]
    public async Task The_request_list_is_by_date_then_number()
    {
        var first = await OrderedAsync(10m, 4m);
        var second = await OrderedAsync(10m, 4m);
        var list = (await AsAsync(s => s.Get<PurchaseOrderDirectory>().GetAllOutstandingLinesAsync())).Select(r => r.RequestNo).ToList();
        Assert.True(list.Contains(first.RequestNo) && list.Contains(second.RequestNo));
        Assert.Equal(list.OrderBy(n => n, StringComparer.Ordinal).Where(n => n == first.RequestNo || n == second.RequestNo), list.Where(n => n == first.RequestNo || n == second.RequestNo).OrderBy(n => n, StringComparer.Ordinal));
    }
}
