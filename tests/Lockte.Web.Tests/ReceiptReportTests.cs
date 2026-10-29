using Lockte.Org;
using Lockte.Purch;
using Lockte.Web.Reports;
using Lockte.Web.Reports.Purch;

namespace Lockte.Web.Tests;

/// <summary>รายงานการรับวัตถุดิบ RPT-12, 13, 15, 18, 19 (#258) without a database: the grouping by id and the totals.</summary>
public sealed class ReceiptReportGroupingTests
{
    private static readonly DateOnly From = new(2026, 8, 1);
    private static readonly DateOnly To = new(2026, 8, 31);

    private static ReceivedLine Line(int id, int department, int material, string name, decimal qty, decimal price, int supplier, string request, bool failed = false, int day = 5)
        => new(id, new DateOnly(2026, 8, day), "6909" + id, request, department, material, "M" + material, name, "กก.", true, qty, price, qty * price, supplier, "S" + supplier, "ผู้ขาย " + supplier, failed);

    private static readonly ReceivedLine[] Lines =
    [
        Line(1, 1, 10, "อลูมิเนียม", 100m, 10m, 7, "B69-0001"),
        Line(2, 1, 10, "อลูมิเนียม (ชื่อในใบต่างกัน)", 50m, 12m, 7, "B69-0002"), // the same material, another name on the document: still one row
        Line(3, 1, 11, "ลวด", 10m, 5m, 8, "B69-0002"),
        Line(4, 1, 11, "ลวด", 999m, 5m, 8, "B69-0003", failed: true), // failed: not in the purchases
        Line(5, 2, 10, "อลูมิเนียม", 20m, 10m, 9, "T69-0001"),
    ];

    [Fact]
    public void A_department_groups_by_material_id_by_amount_descending_and_counts_the_requests_RPT_12()
    {
        var report = ReceiptReports.Department(Lines, "อัดผ้าเบรก", 1, From, To);
        Assert.Equal(["M10", "M11"], report.Rows.Select(r => r.Code));
        Assert.Equal((150m, 1600m), (report.Rows[0].Quantity, report.Rows[0].Amount)); // 100×10 + 50×12
        Assert.Equal(1600m / 150m, report.Rows[0].AveragePrice);
        Assert.Equal(50m, report.Rows[1].Amount); // the failed delivery of 999 is left out
        Assert.Equal(1650m, report.Total);
        Assert.Equal(2, report.RequestCount); // B69-0001 and B69-0002 (the failed line's request does not count)
    }

    [Fact]
    public void The_department_summary_totals_each_department_without_the_failed_ones_RPT_13()
    {
        var report = ReceiptReports.DepartmentSummary(Lines, new Dictionary<int, string> { [1] = "อัดผ้าเบรก", [2] = "ตกแต่ง" }, From, To);
        Assert.Equal([("ตกแต่ง", 200m), ("อัดผ้าเบรก", 1650m)], report.Rows.Select(r => (r.Department, r.Amount)).OrderBy(r => r.Item1, StringComparer.Ordinal));
        Assert.Equal(1850m, report.Total);
    }

    [Fact]
    public void One_material_lists_its_deliveries_by_date_with_the_average_price_RPT_15()
    {
        var report = ReceiptReports.Material(Lines.Append(Line(6, 1, 10, "อลูมิเนียม", 10m, 20m, 7, "B69-0004", day: 1)).ToArray(), 10, "อลูมิเนียม", From, To);
        Assert.Equal(4, report.Rows.Count);
        Assert.Equal(new DateOnly(2026, 8, 1), report.Rows[0].ReceiveDate);
        Assert.Equal((180m, 2000m, 2000m / 180m), (report.TotalQuantity, report.TotalAmount, report.AveragePrice)); // 1000 + 600 + 200 + 200 over all departments
    }

    [Fact]
    public void Suppliers_are_grouped_by_id_and_the_failed_deliveries_are_kept_RPT_18_RPT_19()
    {
        var all = ReceiptReports.Supplier(Lines, null, From, To);
        Assert.Equal(["S7", "S8", "S9"], all.Suppliers.Select(s => s.Code));
        Assert.Equal(1600m + 50m + 4995m + 200m, all.TotalAmount);
        Assert.Equal(5045m, all.Suppliers[1].Amount); // supplier 8: 10×5 + the failed 999×5 (this report does not filter by result)
        var one = ReceiptReports.Supplier(Lines, 7, From, To);
        Assert.Single(one.Suppliers);
        Assert.Equal(["M10"], one.Suppliers[0].Materials.Select(m => m.MaterialCode));
    }

    private static async Task<string> RenderAsync<T>(object report) where T : Microsoft.AspNetCore.Components.IComponent
        => System.Net.WebUtility.HtmlDecode(await ReportBody.RenderAsync<T>(new() { ["Report"] = report }));

    [Fact]
    public async Task The_views_print_the_rows_and_the_totals_and_no_data_when_empty()
    {
        var html = await RenderAsync<DepartmentReceiptView>(ReceiptReports.Department(Lines, "อัดผ้าเบรก", 1, From, To));
        Assert.Contains("แผนก อัดผ้าเบรก", html);
        Assert.Contains("ใบขออนุมัติซื้อ 2 ใบ", html);
        Assert.Contains("1,650.00", html);
        Assert.Contains("ชื่อผู้ขาย ผู้ขาย 7", await RenderAsync<SupplierReceiptView>(ReceiptReports.Supplier(Lines, null, From, To)));
        Assert.Contains("ไม่มีข้อมูล", await RenderAsync<DepartmentReceiptSummaryView>(ReceiptReports.DepartmentSummary([], new Dictionary<int, string>(), From, To)));
        Assert.Contains("ไม่มีข้อมูล", await RenderAsync<MaterialReceiptView>(ReceiptReports.Material([], 10, "x", From, To)));
        Assert.Contains("ไม่มีข้อมูล", await RenderAsync<SupplierReceiptSummaryView>(ReceiptReports.Supplier([], null, From, To)));
    }
}

/// <summary>RPT-12…19 (#258): the receipts the reports read.</summary>
[Collection(LocalDbCollection.Name)]
public sealed class ReceiptReportDataTests(LocalDbFixture db)
{
    private static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..6];

    private async Task<T> AsAsync<T>(Func<TestScope, Task<T>> action)
    {
        await using var scope = db.Scope("buyer1");
        return await action(scope);
    }

    [Fact]
    public async Task The_received_lines_carry_the_ids_department_request_and_amount()
    {
        var supplier = (await AsAsync(s => s.Get<SupplierRegistry>().SaveAsync(new SupplierInput(null, Unique("rr").ToUpperInvariant(), "ผู้ขายรายงาน", null)))).Id!.Value;
        var material = (await AsAsync(s => s.Get<MaterialRegistry>().SaveAsync(new MaterialInput(null, Unique("rm").ToUpperInvariant(), "วัตถุดิบรายงาน", null, "กก.")))).Id!.Value;
        int department;
        await using (var scope = db.Scope("hr1"))
        {
            var org = scope.Get<OrgRegistry>();
            var section = (await org.SaveSectionAsync(new SectionInput(null, Unique("ฝ่าย"), 1, null))).Id!.Value;
            department = (await org.SaveDepartmentAsync(new DepartmentInput(null, Unique("D"), Unique("แผนก"), "d", 3, section, null, [], null))).Id!.Value;
        }
        var date = new DateOnly(2027, 6, Random.Shared.Next(1, 28));
        var requestNo = Unique("R") + "70-0001";
        var request = await AsAsync(s => s.Get<PurchaseRequestRegistry>().SaveAsync(new PurchaseRequestInput(requestNo, date, department, null, "F", null, null, [new PurchaseRequestLineInput(material, 5)])));
        var requestLine = (await AsAsync(s => s.Get<PurchaseRequestDirectory>().GetAsync(request.Id!.Value)))!.Lines.Single();
        var order = await AsAsync(s => s.Get<PurchaseOrderRegistry>().IssueAsync(new PurchaseOrderInput("70" + Random.Shared.Next(1000, 9999) + Guid.NewGuid().ToString("N")[..3], date, supplier, "ผู้ขายรายงาน", null, null, null, null, 30, true, "F", null, null,
            [new PurchaseOrderLineInput(requestLine.Id, 5m, 12m)])));
        var line = (await AsAsync(s => s.Get<PurchaseOrderDirectory>().GetAsync(order.Id!.Value)))!.Lines.Single();
        await AsAsync(s => s.Get<GoodsReceiptRegistry>().ReceiveAsync([new GoodsReceiptInput(line.Id, date, 5m)]));

        var received = (await AsAsync(s => s.Get<GoodsReceiptDirectory>().GetReceivedLinesAsync(date, date))).Single(l => l.MaterialId == material);
        Assert.Equal((department, requestNo, supplier, 60m, false), (received.DepartmentId, received.RequestNo, received.SupplierId, received.Amount, received.Failed));
        Assert.Empty((await AsAsync(s => s.Get<GoodsReceiptDirectory>().GetReceivedLinesAsync(date.AddDays(1), date.AddDays(2)))).Where(l => l.MaterialId == material));
    }
}
