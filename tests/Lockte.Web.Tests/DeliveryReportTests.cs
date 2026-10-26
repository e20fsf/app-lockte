using Lockte.Prod;
using Lockte.Sales;
using Lockte.Web.Reports;
using Lockte.Web.Reports.Order;
using Microsoft.AspNetCore.Components;

namespace Lockte.Web.Tests;

/// <summary>
/// เอกสาร ISO การส่งของ (#136): ใบรายงานการส่งสินค้า (RPT-07), รายงานการตรวจสอบสินค้าที่จัดส่งให้ลูกค้า and รายงานส่งผู้จัดการโรงงาน (RPT-08). The rows come
/// from the module, the totals from the report builders, the views only lay them out. Fictional data only. Every test uses dates of its own year, because the
/// tests of this collection share a database and the reports are by date range.
/// </summary>
[Collection(LocalDbCollection.Name)]
public sealed class DeliveryReportTests(LocalDbFixture db)
{
    private static int counter = Random.Shared.Next(0x1000);
    private static int year = 2200;

    private static string TerritoryCode() => "V" + Interlocked.Increment(ref counter).ToString("X3");

    private static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    /// <summary>A month nobody else's test delivers in: the range of a report is the whole month.</summary>
    private static DateOnly NewMonth() => new(Interlocked.Increment(ref year), 3, 1);

    private async Task<T> AsAsync<T>(Func<TestScope, Task<T>> action)
    {
        await using var scope = db.Scope("store1");
        return await action(scope);
    }

    private async Task<int> SavedAsync(Task<Lockte.SharedKernel.ServiceResult> save)
    {
        var result = await save;
        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        return result.Id!.Value;
    }

    private static async Task<string> HtmlAsync<TView>(object report) where TView : IComponent
        => System.Net.WebUtility.HtmlDecode(await ReportBody.RenderAsync<TView>(new() { ["Report"] = report }));

    private sealed record Setup(int Territory, string TerritoryCode, int Customer, string CustomerCode, int Contract, string ContractCode, int Product);

    private async Task<Setup> NewSetupAsync()
    {
        var code = TerritoryCode();
        var territory = await AsAsync(s => SavedAsync(s.Get<SalesTerritoryRegistry>().SaveAsync(new SalesTerritoryInput(null, code, "เขตส่งของรายงาน", null, null))));
        var customerCode = Unique("92-");
        var contractCode = Unique("40-");
        var customer = await AsAsync(s => SavedAsync(s.Get<CustomerRegistry>().SaveAsync(new CustomerInput(null, customerCode, "ลูกค้าส่งของ ก", null, SalesTerritoryId: territory))));
        var contract = await AsAsync(s => SavedAsync(s.Get<CustomerRegistry>().SaveAsync(new CustomerInput(null, contractCode, "ลูกค้ารับจ้าง ข", null, SalesTerritoryId: territory))));
        var product = await AsAsync(s => SavedAsync(s.Get<ProductRegistry>().SaveAsync(new ProductInput(null, Unique("ZV"), "สินค้าส่งของ", null, Unit: "คู่"))));
        return new Setup(territory, code, customer, customerCode, contract, contractCode, product);
    }

    private async Task<string> OrderAsync(Setup f, string number, int customer, DateOnly orderDate, DateOnly due, params SalesOrderLineInput[] lines)
    {
        var saved = await AsAsync(s => s.Get<SalesOrderRegistry>().CreateAsync(new SalesOrderInput(number, orderDate, customer, null, f.Territory, due, "30", null, lines), OrderEntryMode.Print));
        Assert.True(saved.Succeeded, string.Join(" ", saved.Errors));
        return number;
    }

    private static SalesOrderLineInput L(int product, decimal quantity, decimal price) => new(product, quantity, price);

    /// <summary>Types the delivery on each line of the order (quantity, date, invoice, boxes) and saves, as SCR-08.</summary>
    private async Task DeliverAsync(string number, DateOnly date, string invoice, decimal quantity, decimal? boxes, int line = 1)
    {
        var lines = await AsAsync(s => s.Get<SalesDeliveryDirectory>().GetLinesAsync(number));
        var target = lines.Single(l => l.LineNo == line);
        await AsAsync(s => SavedAsync(s.Get<SalesDeliveryRegistry>().SetPendingAsync(target.LineId, target.RowVersion, new PendingDeliveryInput(date, invoice, line, quantity, null, boxes))));
        await AsAsync(s => s.Get<SalesDeliveryRegistry>().SaveAsync());
    }

    // ---------- RPT-07 ----------

    [Fact] // groups by customer, then date and invoice; ที่ starts at 1 at every invoice; totals of a group and of all
    public async Task The_delivery_report_groups_by_customer_and_totals_amount_boxes_invoices_and_orders()
    {
        var f = await NewSetupAsync();
        var month = NewMonth();
        var first = await OrderAsync(f, $"{f.TerritoryCode}-70-0001", f.Customer, month, month.AddDays(10), L(f.Product, 100, 10m), L(f.Product, 50, 20m));
        var second = await OrderAsync(f, $"{f.TerritoryCode}-70-0002", f.Customer, month, month.AddDays(10), L(f.Product, 30, 10m));
        var contract = await OrderAsync(f, $"{f.TerritoryCode}-70-0003", f.Contract, month, month.AddDays(10), L(f.Product, 400, 2m));
        await DeliverAsync(first, month.AddDays(5), "0100001", 100, 4, line: 1);
        await DeliverAsync(first, month.AddDays(5), "0100001", 50, 2, line: 2);
        await DeliverAsync(second, month.AddDays(6), "0100002", 30, 1);
        await DeliverAsync(contract, month.AddDays(6), "0100003", 400, null);

        var report = await AsAsync(s => DeliveryReports.DeliveryAsync(s.Get<SalesDeliveryReportDirectory>(), month, month.AddMonths(1).AddDays(-1)));

        Assert.Equal(new[] { f.CustomerCode, f.ContractCode }.Order(), report.Customers.Select(c => c.CustomerCode));
        var group = report.Customers.Single(c => c.CustomerCode == f.CustomerCode);
        Assert.Equal((3, 2300m, 7m), (group.Rows.Count, group.Amount, group.Boxes));
        Assert.Equal((4, 3100m, 7m, 3, 3), (report.Rows, report.Amount, report.Boxes, report.Invoices, report.Orders));

        var html = await HtmlAsync<DeliveryReportView>(report);
        Assert.Contains("รวม 3 รายการ", html);
        Assert.Contains("รวมทั้งหมด", html);
        Assert.Contains("บิล 3 ใบ", html);
        Assert.Contains("SALE ORDER 3 ใบ", html);
        Assert.Contains("ผู้รายงาน", html);
        Assert.Contains("ผู้จัดการฝ่าย", html);
        Assert.Equal(1, System.Text.RegularExpressions.Regex.Matches(html, f.CustomerCode).Count); // the head of the group, once
    }

    [Fact] // a customer of code 40… is shown the pieces and "ชิ้น" in the box column; Σกล่อง counts real boxes only
    public async Task A_customer_of_code_40_is_shown_pieces_instead_of_boxes()
    {
        var f = await NewSetupAsync();
        var month = NewMonth();
        var contract = await OrderAsync(f, $"{f.TerritoryCode}-70-0001", f.Contract, month, month.AddDays(10), L(f.Product, 400, 2m));
        await DeliverAsync(contract, month.AddDays(6), "0100003", 400, null);

        var report = await AsAsync(s => DeliveryReports.DeliveryAsync(s.Get<SalesDeliveryReportDirectory>(), month, month.AddMonths(1).AddDays(-1)));

        Assert.True(DeliveryReports.ShowsPieces(f.ContractCode));
        Assert.False(DeliveryReports.ShowsPieces(f.CustomerCode));
        Assert.Contains("400 ชิ้น", await HtmlAsync<DeliveryReportView>(report));
        Assert.Equal(0m, report.Boxes);
    }

    [Fact] // D-6: a range with no delivery says so
    public async Task A_range_without_deliveries_prints_no_data()
    {
        var month = NewMonth();

        var report = await AsAsync(s => DeliveryReports.DeliveryAsync(s.Get<SalesDeliveryReportDirectory>(), month, month.AddMonths(1).AddDays(-1)));

        Assert.Equal(0, report.Rows);
        Assert.Contains("ไม่มีข้อมูล", await HtmlAsync<DeliveryReportView>(report));
        Assert.Contains("ไม่มีข้อมูล", await HtmlAsync<ManagerDeliveryView>(await AsAsync(s => DeliveryReports.ManagerAsync(s.Get<SalesDeliveryReportDirectory>(), month, month.AddMonths(1).AddDays(-1)))));
        Assert.Contains("ไม่มีข้อมูล", await HtmlAsync<ShippingInspectionView>(await AsAsync(s => DeliveryReports.InspectionAsync(s.Get<SalesDeliveryReportDirectory>(), month, month.AddMonths(1).AddDays(-1)))));
    }

    // ---------- the quality check before delivery ----------

    [Fact] // a strip for each customer delivered to; the boxes are Σกล่อง of RPT-07; the sample is at most 5
    public async Task The_inspection_has_a_strip_for_each_customer_with_boxes_and_sample()
    {
        var f = await NewSetupAsync();
        var month = NewMonth();
        var big = await OrderAsync(f, $"{f.TerritoryCode}-70-0001", f.Customer, month, month.AddDays(10), L(f.Product, 100, 10m));
        var small = await OrderAsync(f, $"{f.TerritoryCode}-70-0002", f.Contract, month, month.AddDays(10), L(f.Product, 10, 10m));
        await DeliverAsync(big, month.AddDays(3), "0100001", 60, 35);
        await DeliverAsync(big, month.AddDays(9), "0100002", 40, 5);
        await DeliverAsync(small, month.AddDays(4), "0100003", 10, 1);

        var report = await AsAsync(s => DeliveryReports.InspectionAsync(s.Get<SalesDeliveryReportDirectory>(), month, month.AddMonths(1).AddDays(-1)));

        Assert.Equal(2, report.Strips.Count);
        var bigStrip = report.Strips.Single(s => s.CustomerCode == f.CustomerCode);
        Assert.Equal((40, 5, month.AddDays(9)), (bigStrip.Boxes, bigStrip.Sample, bigStrip.DeliveryDate));
        Assert.Equal((1, 1), (report.Strips.Single(s => s.CustomerCode == f.ContractCode).Boxes, report.Strips.Single(s => s.CustomerCode == f.ContractCode).Sample));
        var html = await HtmlAsync<ShippingInspectionView>(report);
        Assert.Contains("Attribute Data", html);
        Assert.Contains("จำนวน 40 กล่อง", html);
        Assert.Contains("ผู้จัดการฝ่ายสโตร์", html);
    }

    // ---------- RPT-08 ----------

    [Fact] // ทัน / ล่าช้า against the due date of the order; the percentage and the month in the head from the dates (D-5)
    public async Task The_manager_report_judges_each_order_against_its_due_date()
    {
        var f = await NewSetupAsync();
        var month = NewMonth();
        var inTime = await OrderAsync(f, $"{f.TerritoryCode}-70-0001", f.Customer, month, month.AddDays(10), L(f.Product, 10, 10m));
        var late = await OrderAsync(f, $"{f.TerritoryCode}-70-0002", f.Customer, month, month.AddDays(5), L(f.Product, 10, 20m));
        await DeliverAsync(inTime, month.AddDays(10), "0100001", 10, 1);
        await DeliverAsync(late, month.AddDays(8), "0100002", 10, 1);

        var report = await AsAsync(s => DeliveryReports.ManagerAsync(s.Get<SalesDeliveryReportDirectory>(), month, month.AddMonths(1).AddDays(-1)));

        Assert.Equal([(true, 100m), (false, 200m)], report.Rows.OrderBy(r => r.DeliveryDate).Select(r => (r.OnTime!.Value, r.Amount)).OrderByDescending(x => x.Item1));
        Assert.Equal((1, 1, 50m, 300m), (report.OnTime, report.Late, report.PercentOnTime, report.Amount));
        var html = await HtmlAsync<ManagerDeliveryView>(report);
        Assert.Contains("ล่าช้า", html); // spelt right
        Assert.DoesNotContain("ล้าช้า", html);
        Assert.Contains("ส่งทัน 50.00 %", html);
        Assert.Contains("Prepared by", html);
        Assert.Contains("Approved by", html);
        Assert.Equal($"รายงานการส่งสินค้าเดือน มีนาคม {month.Year + 543}", DeliveryReports.ManagerTitle(month, month.AddMonths(1).AddDays(-1)));
    }

    // ---------- เลขเอกสารควบคุม ----------

    [Fact] // the numbers as the samples print them, kept as master data (#52)
    public async Task The_control_numbers_are_the_ones_the_samples_print()
    {
        var printed = await AsAsync(async s =>
        {
            var numbers = s.Get<DocumentNumberService>();
            return new[] { DocumentKeys.DeliveryReport, DocumentKeys.ShippingInspection, DocumentKeys.ManagerDelivery, DocumentKeys.DeliverySchedule }
                .Select(key => numbers.PrintedTextAsync(key).Result).ToArray();
        });
        Assert.Equal(["WI-ST005-1/2551 ฉบับที่2", "WI-QA033-1/2547 ฉบับที่ 3", "WI-ST005-3/2551 ฉบับที่ 1", "PC-PD003-3/2547"], printed);
    }
}
