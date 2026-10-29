using Lockte.Org;
using Lockte.Purch;
using Lockte.Web.Reports;
using Lockte.Web.Reports.Purch;

namespace Lockte.Web.Tests;

/// <summary>RPT-25 รายงานการรับสินค้าซื้อมาขายไป (#261, BR-062) and RPT-11 (BR-063) without a database.</summary>
public sealed class ResaleReportTests
{
    private static readonly DateOnly From = new(2026, 8, 1);
    private static readonly DateOnly To = new(2026, 8, 31);

    private static ReceivedLine Line(int id, string code, decimal qty, decimal price, bool failed = false, DateOnly? date = null)
        => new(id, date ?? new DateOnly(2026, 8, 5), "P" + id, "R" + id, 1, id, code, "สินค้า " + code, "ชิ้น", false, qty, price, qty * price, 1, "S1", "ผู้ขาย", failed);

    [Fact]
    public void The_codes_go_to_their_group_by_prefix_ignoring_case_and_the_empty_groups_stay_blank_AC_27()
    {
        var report = ResaleAndSmallOrderReports.Resale(
        [
            Line(1, "SP-01", 100m, 10.5m),              // spring: 1,050
            Line(2, "sp-02", 50m, 11m, failed: true),   // the failed ones count too: 550
            Line(3, "CH-07", 10m, 100.25m),             // chain: 1,002.5 → 1,002 (half to even)
            Line(4, "XX-01", 5m, 5m),                   // in no group
            Line(5, "SP-03", 1m, 1m, date: new DateOnly(2026, 9, 1)), // outside the period
        ], PurchSettingsDefaults.ResaleGroups, From, To);
        Assert.Equal(PurchSettingsDefaults.ResaleGroups.Select(g => g.Name), report.Rows.Select(r => r.Name));
        var chain = report.Rows.Single(r => r.Name == PurchSettingsDefaults.ResaleGroups.Single(g => g.Prefix == "ch").Name);
        var spring = report.Rows.Single(r => r.Name == PurchSettingsDefaults.ResaleGroups.Single(g => g.Prefix == "sp").Name);
        Assert.Equal((10m, 1002m), (chain.Quantity, chain.Amount));
        Assert.Equal((150m, 1600m), (spring.Quantity, spring.Amount));
        Assert.Equal(3, report.Rows.Count(r => r.Quantity is null && r.Amount is null)); // blank, not 0 (BR-062 #2)
        Assert.Equal((160m, 2602m), (report.TotalQuantity, report.TotalAmount));
    }

    [Fact]
    public async Task The_views_print_blank_groups_and_the_total_and_the_small_orders_with_the_approver()
    {
        var resale = ResaleAndSmallOrderReports.Resale([Line(1, "SP-01", 100m, 10.5m)], PurchSettingsDefaults.ResaleGroups, From, To);
        var html = System.Net.WebUtility.HtmlDecode(await ReportBody.RenderAsync<ResaleView>(new() { ["Report"] = resale }));
        Assert.Contains("1,050", html);
        Assert.Contains("รวมทั้งหมด", html);
        Assert.DoesNotContain(">0<", html);

        var small = ResaleAndSmallOrderReports.SmallOrders(
            [new SmallOrderLine(new DateOnly(2026, 8, 3), "690977", 1, "ลวด", 2m, "กก.", 3317m, "ผู้ขาย ก"), new SmallOrderLine(new DateOnly(2026, 8, 3), "690977", 2, "น็อต", 1m, "ตัว", 100.5m, "ผู้ขาย ก")],
            " ผู้อนุมัติทดสอบ ", From, To);
        var smallHtml = System.Net.WebUtility.HtmlDecode(await ReportBody.RenderAsync<SmallOrderView>(new() { ["Report"] = small }));
        Assert.Contains("690977", smallHtml);
        Assert.Contains("ผู้อนุมัติทดสอบ", smallHtml);
        Assert.Contains("รวมทั้งหมด 2 รายการ เป็นเงิน 3,417.50 บาท", smallHtml);
        Assert.Contains("ไม่มีข้อมูล", await ReportBody.RenderAsync<SmallOrderView>(new() { ["Report"] = ResaleAndSmallOrderReports.SmallOrders([], null, From, To) }));
    }
}

/// <summary>RPT-11 (#261, BR-063): which orders the report reads.</summary>
[Collection(LocalDbCollection.Name)]
public sealed class SmallOrderDataTests(LocalDbFixture db)
{
    private static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..6];

    private async Task<T> AsAsync<T>(Func<TestScope, Task<T>> action)
    {
        await using var scope = db.Scope("buyer1");
        return await action(scope);
    }

    [Fact]
    public async Task An_order_within_the_limit_shows_5000_exactly_included_and_one_cent_over_not_BR_063()
    {
        var supplier = (await AsAsync(s => s.Get<SupplierRegistry>().SaveAsync(new SupplierInput(null, Unique("so").ToUpperInvariant(), "ผู้ขายเล็ก", null)))).Id!.Value;
        var material = (await AsAsync(s => s.Get<MaterialRegistry>().SaveAsync(new MaterialInput(null, Unique("sm").ToUpperInvariant(), "วัตถุดิบเล็ก", null, "กก.")))).Id!.Value;
        int department;
        await using (var scope = db.Scope("hr1"))
        {
            var org = scope.Get<OrgRegistry>();
            var section = (await org.SaveSectionAsync(new SectionInput(null, Unique("ฝ่าย"), 1, null))).Id!.Value;
            department = (await org.SaveDepartmentAsync(new DepartmentInput(null, Unique("D"), Unique("แผนก"), "d", 3, section, null, [], null))).Id!.Value;
        }
        var date = new DateOnly(2028, 3, Random.Shared.Next(1, 28));

        async Task<string> OrderAsync(decimal price)
        {
            var request = await AsAsync(s => s.Get<PurchaseRequestRegistry>().SaveAsync(new PurchaseRequestInput(Unique("R") + "71-0001", date, department, null, "F", null, null, [new PurchaseRequestLineInput(material, 1)])));
            var requestLine = (await AsAsync(s => s.Get<PurchaseRequestDirectory>().GetAsync(request.Id!.Value)))!.Lines.Single();
            var number = "71" + Random.Shared.Next(1000, 9999) + Guid.NewGuid().ToString("N")[..3];
            var order = await AsAsync(s => s.Get<PurchaseOrderRegistry>().IssueAsync(new PurchaseOrderInput(number, date, supplier, "ผู้ขายเล็ก", null, null, null, null, 30, false, "F", null, null,
                [new PurchaseOrderLineInput(requestLine.Id, 1m, price)])));
            Assert.True(order.Succeeded, string.Join(",", order.Errors));
            return number;
        }

        var exactly = await OrderAsync(5000m);
        var over = await OrderAsync(5000.01m);
        var small = await OrderAsync(3317m);

        var lines = await AsAsync(s => s.Get<PurchaseOrderDirectory>().GetSmallOrderLinesAsync(date, date, 5000m));
        Assert.Contains(lines, l => l.OrderNo == exactly && l.AmountWithTax == 5000m);
        Assert.Contains(lines, l => l.OrderNo == small && l.AmountWithTax == 3317m);
        Assert.DoesNotContain(lines, l => l.OrderNo == over);
        Assert.DoesNotContain(await AsAsync(s => s.Get<PurchaseOrderDirectory>().GetSmallOrderLinesAsync(date.AddDays(1), date.AddDays(2), 5000m)), l => l.OrderNo == exactly || l.OrderNo == small);
    }
}
