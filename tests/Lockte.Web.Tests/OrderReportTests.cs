using Lockte.Prod;
using Lockte.Sales;
using Lockte.Web.Reports;
using Lockte.Web.Reports.Order;
using Microsoft.AspNetCore.Components;

namespace Lockte.Web.Tests;

/// <summary>
/// รายงาน ORDER ค้างส่ง (RPT-02/03/04) and สรุปการรับ ORDER (RPT-06) (#113): the rows and totals come from the module services and
/// the report builders, the views only lay them out (แบบกลาง B). Fictional data only; every test works in its own territory and customers
/// because the tests of this collection share a database.
/// </summary>
[Collection(LocalDbCollection.Name)]
public sealed class OrderReportTests(LocalDbFixture db)
{
    private static int counter = Random.Shared.Next(0x100, 0x700);

    private static string TerritoryCode() => "R" + Interlocked.Increment(ref counter).ToString("X3");

    private static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    private async Task<T> AsAsync<T>(Func<TestScope, Task<T>> action)
    {
        await using var scope = db.Scope("report1");
        return await action(scope);
    }

    private async Task<int> SavedAsync(Task<Lockte.SharedKernel.ServiceResult> save)
    {
        var result = await save;
        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        return result.Id!.Value;
    }

    private async Task<string> HtmlAsync<TView>(object report) where TView : IComponent
        => System.Net.WebUtility.HtmlDecode(await ReportBody.RenderAsync<TView>(new() { ["Report"] = report }));

    private sealed record Setup(int Territory, string TerritoryCode, int CustomerA, string CodeA, int CustomerB, string CodeB, int Pair, string PairCode, int Box, string BoxCode, int Model);

    private async Task<Setup> NewSetupAsync()
    {
        var code = TerritoryCode();
        var territory = await AsAsync(s => SavedAsync(s.Get<SalesTerritoryRegistry>().SaveAsync(new SalesTerritoryInput(null, code, "เขตรายงาน", null, null))));
        var codeA = Unique("93-01-");
        var codeB = Unique("93-02-");
        var customerA = await AsAsync(s => SavedAsync(s.Get<CustomerRegistry>().SaveAsync(new CustomerInput(null, codeA, "ลูกค้ารายงาน ก", null, SalesTerritoryId: territory))));
        var customerB = await AsAsync(s => SavedAsync(s.Get<CustomerRegistry>().SaveAsync(new CustomerInput(null, codeB, "ลูกค้ารายงาน ข", null, SalesTerritoryId: territory))));
        var model = await AsAsync(s => SavedAsync(s.Get<ProductListRegistry>().SaveModelAsync(null, Unique("รุ่น"), null)));
        var pairCode = Unique("ZR");
        var boxCode = Unique("ZB");
        var pair = await AsAsync(s => SavedAsync(s.Get<ProductRegistry>().SaveAsync(new ProductInput(null, pairCode, "ก้ามเบรกรายงาน", null, Unit: "คู่", ModelId: model))));
        var box = await AsAsync(s => SavedAsync(s.Get<ProductRegistry>().SaveAsync(new ProductInput(null, boxCode, "ตลับรายงาน", null, Unit: "ตลับ"))));
        return new Setup(territory, code, customerA, codeA, customerB, codeB, pair, pairCode, box, boxCode, model);
    }

    private async Task<int> OrderAsync(Setup f, string number, int customer, DateOnly date, OrderEntryMode mode, params SalesOrderLineInput[] lines)
    {
        var saved = await AsAsync(s => s.Get<SalesOrderRegistry>().CreateAsync(new SalesOrderInput(number, date, customer, null, f.Territory, date.AddDays(7), "30", null, lines), mode));
        Assert.True(saved.Succeeded, string.Join(" ", saved.Errors));
        return saved.Id!.Value;
    }

    private static SalesOrderLineInput L(int product, decimal quantity, decimal price) => new(product, quantity, price);

    private static string No(Setup f, int n) => $"{f.TerritoryCode}-69-{n:0000}";

    private static readonly DateOnly October = new(2026, 10, 5);

    private OrderReportFilter Mine(Setup f) => new(SalesTerritoryId: f.Territory);

    // ---------- RPT-02, RPT-03, RPT-04: ค้างส่ง ----------

    [Fact] // RPT-02: by date → number → line; only what is still to deliver; the filter in the head (D-10)
    public async Task The_backorder_is_listed_by_date_and_number_with_totals_per_unit()
    {
        var f = await NewSetupAsync();
        await OrderAsync(f, No(f, 2), f.CustomerB, October.AddDays(2), OrderEntryMode.Print, L(f.Box, 30, 5m));
        await OrderAsync(f, No(f, 1), f.CustomerA, October, OrderEntryMode.Save, L(f.Pair, 100, 10m), L(f.Box, 20, 5m));

        var report = await AsAsync(s => OrderReports.BackorderAsync(s.Get<SalesOrderReportDirectory>(), s.Get<CustomerDirectory>(), s.Get<SalesTerritoryDirectory>(), Mine(f)));

        Assert.Equal([(No(f, 1), 1), (No(f, 1), 2), (No(f, 2), 1)], report.Lines.Select(l => (l.OrderNo, l.LineNo))); // the Save-mode order is on the list as well: it is a backorder
        Assert.Contains($"เขตการขาย {f.TerritoryCode}", report.Filter);
        var html = await HtmlAsync<BackorderListView>(report);
        Assert.Contains("รวม 3 บรรทัด", html);
        Assert.Contains("100 คู่", html);
        Assert.Contains("50 ตลับ", html); // D-11: 20 + 30 ตลับ, apart from the คู่
        Assert.Equal(1, System.Text.RegularExpressions.Regex.Matches(html, f.CodeA).Count); // the customer only on the first line of an order
        Assert.Contains("ชื่อลูกค้า", html);
    }

    [Fact] // a line that was delivered in full is no longer on any backorder report
    public async Task A_line_with_nothing_left_is_not_listed()
    {
        var f = await NewSetupAsync();
        var number = No(f, 1);
        await OrderAsync(f, number, f.CustomerA, October, OrderEntryMode.Print, L(f.Pair, 100, 10m));
        var line = (await AsAsync(s => s.Get<SalesDeliveryDirectory>().GetLinesAsync(number))).Single();
        await AsAsync(s => SavedAsync(s.Get<SalesDeliveryRegistry>().SetPendingAsync(line.LineId, line.RowVersion, new PendingDeliveryInput(October.AddDays(3), "0100001", 1, 100, null, 2))));
        await AsAsync(s => s.Get<SalesDeliveryRegistry>().SaveAsync());

        var report = await AsAsync(s => OrderReports.BackorderAsync(s.Get<SalesOrderReportDirectory>(), s.Get<CustomerDirectory>(), s.Get<SalesTerritoryDirectory>(), Mine(f)));

        Assert.Empty(report.Lines);
        Assert.Contains("ไม่มีข้อมูล", await HtmlAsync<BackorderListView>(report)); // D-6
    }

    [Fact] // RPT-03: the products summed, the most first, per unit
    public async Task The_backorder_is_summed_by_product_most_first()
    {
        var f = await NewSetupAsync();
        await OrderAsync(f, No(f, 1), f.CustomerA, October, OrderEntryMode.Print, L(f.Pair, 100, 10m), L(f.Box, 500, 5m));
        await OrderAsync(f, No(f, 2), f.CustomerB, October.AddDays(1), OrderEntryMode.Print, L(f.Pair, 50, 10m));

        var report = await AsAsync(s => OrderReports.BackorderByProductAsync(s.Get<SalesOrderReportDirectory>(), s.Get<CustomerDirectory>(), s.Get<SalesTerritoryDirectory>(), Mine(f)));

        Assert.Equal([(f.BoxCode, 500m), (f.PairCode, 150m)], report.Rows.Select(r => (r.Code!, r.Quantity)));
        var html = await HtmlAsync<BackorderByProductView>(report);
        Assert.Contains("รวม", html);
        Assert.True(html.IndexOf(f.BoxCode, StringComparison.Ordinal) < html.IndexOf(f.PairCode, StringComparison.Ordinal));
    }

    [Fact] // RPT-04: by customer — a group each, the products of the customer
    public async Task The_backorder_by_customer_has_a_group_per_customer()
    {
        var f = await NewSetupAsync();
        await OrderAsync(f, No(f, 1), f.CustomerA, October, OrderEntryMode.Print, L(f.Pair, 100, 10m), L(f.Box, 20, 5m));
        await OrderAsync(f, No(f, 2), f.CustomerA, October.AddDays(1), OrderEntryMode.Print, L(f.Pair, 25, 10m));
        await OrderAsync(f, No(f, 3), f.CustomerB, October.AddDays(1), OrderEntryMode.Print, L(f.Box, 7, 5m));

        var report = await AsAsync(s => OrderReports.BackorderByCustomerAsync(s.Get<SalesOrderReportDirectory>(), s.Get<CustomerDirectory>(), s.Get<SalesTerritoryDirectory>(), Mine(f)));

        Assert.Equal(new[] { f.CodeA, f.CodeB }.Order(), report.Groups.Select(g => g.CustomerCode));
        var a = report.Groups.Single(g => g.CustomerCode == f.CodeA);
        Assert.Equal(new[] { (f.BoxCode, 20m), (f.PairCode, 125m) }.Order(), a.Rows.Select(r => (r.Code!, r.Quantity)).Order());
        Assert.Contains($"รหัสลูกค้า {f.CodeA}", await HtmlAsync<BackorderByCustomerView>(report));
    }

    [Fact] // RPT-04: of a territory — the territory is the head, and the filter of the other fields too
    public async Task The_backorder_of_a_territory_names_it_in_the_head()
    {
        var f = await NewSetupAsync();
        await OrderAsync(f, No(f, 1), f.CustomerA, October, OrderEntryMode.Print, L(f.Pair, 100, 10m));

        var report = await AsAsync(s => OrderReports.BackorderOfTerritoryAsync(s.Get<SalesOrderReportDirectory>(), s.Get<CustomerDirectory>(), s.Get<SalesTerritoryDirectory>(),
            Mine(f) with { From = October }));

        Assert.Equal($"ORDER ค้างส่งของเขตการขาย {f.TerritoryCode} เขตรายงาน", report.Heading);
        Assert.StartsWith("วันที่ ORDER ตั้งแต่", report.Filter);
        Assert.DoesNotContain("เขตการขาย", report.Filter); // already in the heading
        Assert.Single(report.Lines);
    }

    [Fact] // RPT-04: by model — the model of the product, "ไม่ระบุรุ่น" for a product with none
    public async Task The_backorder_by_model_uses_the_model_of_the_product()
    {
        var f = await NewSetupAsync();
        await OrderAsync(f, No(f, 1), f.CustomerA, October, OrderEntryMode.Print, L(f.Pair, 100, 10m), L(f.Box, 20, 5m));
        await OrderAsync(f, No(f, 2), f.CustomerB, October, OrderEntryMode.Print, L(f.Pair, 40, 10m));

        var report = await AsAsync(s => OrderReports.BackorderByModelAsync(s.Get<SalesOrderReportDirectory>(), s.Get<ProductDirectory>(), s.Get<CustomerDirectory>(),
            s.Get<SalesTerritoryDirectory>(), Mine(f)));

        Assert.Equal(2, report.Rows.Count);
        Assert.Contains(report.Rows, r => r.Model == "ไม่ระบุรุ่น" && r.Quantity == 20 && r.Unit == "ตลับ");
        Assert.Contains(report.Rows, r => r.Model != "ไม่ระบุรุ่น" && r.Quantity == 140 && r.Unit == "คู่");
    }

    [Fact] // RPT-04: ยอดค้างส่งของลูกค้า is per product (D-8) with the customers that wait; สินค้าที่ลูกค้าสั่งซื้อ is from the deliveries
    public async Task The_customers_of_a_product_are_listed_for_the_backorder_and_for_the_deliveries()
    {
        var f = await NewSetupAsync();
        var number = No(f, 1);
        await OrderAsync(f, number, f.CustomerA, October, OrderEntryMode.Print, L(f.Pair, 100, 10m));
        await OrderAsync(f, No(f, 2), f.CustomerB, October, OrderEntryMode.Print, L(f.Pair, 30, 10m), L(f.Box, 5, 5m));
        var line = (await AsAsync(s => s.Get<SalesDeliveryDirectory>().GetLinesAsync(number))).Single();
        await AsAsync(s => SavedAsync(s.Get<SalesDeliveryRegistry>().SetPendingAsync(line.LineId, line.RowVersion, new PendingDeliveryInput(October.AddDays(3), "0100002", 1, 60, null, 1))));
        await AsAsync(s => s.Get<SalesDeliveryRegistry>().SaveAsync());

        var waiting = await AsAsync(s => OrderReports.BackorderOfProductsAsync(s.Get<SalesOrderReportDirectory>(), s.Get<CustomerDirectory>(), s.Get<SalesTerritoryDirectory>(), Mine(f), f.Pair));
        var ordered = await AsAsync(s => OrderReports.OrderedProductsAsync(s.Get<SalesOrderReportDirectory>(), s.Get<CustomerDirectory>(), f.Pair, f.CustomerA));

        var group = waiting.Groups.Single();
        Assert.Equal(f.PairCode, group.ProductCode);
        Assert.Equal(new[] { (f.CodeA, 40m), (f.CodeB, 30m) }.Order(), group.Rows.Select(r => (r.CustomerCode, r.Quantity ?? 0)).Order()); // A has 40 left of 100 after a delivery of 60
        Assert.Equal("ลูกค้าที่ค้างส่งแยกตามสินค้า", waiting.Heading);
        var delivered = ordered.Groups.Single();
        Assert.Equal(f.CodeA, delivered.Rows.Single().CustomerCode);
        Assert.False(ordered.ShowQuantity);
        Assert.DoesNotContain("จำนวน", await HtmlAsync<ProductCustomersView>(ordered));
    }

    // ---------- RPT-06: สรุปการรับ ORDER ----------

    [Fact] // every territory is listed, even with no order; distinct orders; Σ of every line; only the orders in the history; a closed order still counts
    public async Task The_receipt_summary_lists_every_territory_and_counts_only_the_orders_in_the_history()
    {
        var f = await NewSetupAsync();
        var empty = TerritoryCode();
        await AsAsync(s => SavedAsync(s.Get<SalesTerritoryRegistry>().SaveAsync(new SalesTerritoryInput(null, empty, "เขตไม่มี ORDER", null, null))));
        await OrderAsync(f, No(f, 1), f.CustomerA, October, OrderEntryMode.Print, L(f.Pair, 100, 10m), L(f.Box, 20, 5m)); // 1,100.00
        await OrderAsync(f, No(f, 2), f.CustomerB, October.AddDays(1), OrderEntryMode.Print, L(f.Pair, 50, 10m)); // 500.00
        await OrderAsync(f, No(f, 3), f.CustomerB, October.AddDays(1), OrderEntryMode.Save, L(f.Pair, 999, 10m)); // never printed: not counted
        await OrderAsync(f, No(f, 4), f.CustomerB, October.AddMonths(-3), OrderEntryMode.Print, L(f.Pair, 7, 10m)); // before the range
        var delete = (await AsAsync(s => s.Get<SalesDeliveryDirectory>().GetLinesAsync(No(f, 2)))).Single();
        await AsAsync(s => SavedAsync(s.Get<SalesDeliveryRegistry>().SetPendingAsync(delete.LineId, delete.RowVersion, new PendingDeliveryInput(October.AddDays(2), "0100003", 1, 50, null, 1))));
        await AsAsync(s => s.Get<SalesDeliveryRegistry>().SaveAsync()); // closes the backorder of order 2: it still counts

        var report = await AsAsync(s => OrderReports.ReceiptSummaryAsync(s.Get<SalesOrderReportDirectory>(), s.Get<SalesTerritoryDirectory>(), October.AddDays(-2), October.AddDays(20)));

        var mine = report.Rows.Single(r => r.Code == f.TerritoryCode);
        Assert.Equal((2, 1600m), (mine.Orders, mine.Amount));
        var none = report.Rows.Single(r => r.Code == empty);
        Assert.Equal((0, 0m), (none.Orders, none.Amount));
        var html = await HtmlAsync<ReceiptSummaryView>(report);
        Assert.Contains("รวมทั้งหมด", html);
        Assert.Contains("1,600.00", html);
        Assert.Equal(report.Rows.Sum(r => r.Orders), report.TotalOrders);
    }

    [Fact] // "มากกว่า 3000": the limit is on the quantity, not on the money
    public async Task The_over_3000_summary_counts_the_quantity_not_the_money()
    {
        var f = await NewSetupAsync();
        await OrderAsync(f, No(f, 1), f.CustomerA, October, OrderEntryMode.Print, L(f.Pair, 3000, 0.5m)); // 3,000 pieces, 1,500.00 baht: in
        await OrderAsync(f, No(f, 2), f.CustomerB, October, OrderEntryMode.Print, L(f.Pair, 2999, 1000m)); // 2,999 pieces, 2,999,000.00 baht: out

        var report = await AsAsync(s => OrderReports.ReceiptOver3000Async(s.Get<SalesOrderReportDirectory>(), October.AddDays(-1), October.AddDays(30)));

        var mine = report.Rows.Where(r => r.CustomerCode == f.CodeA || r.CustomerCode == f.CodeB).ToList();
        Assert.Equal([(f.CodeA, 3000m, 1500m)], mine.Select(r => (r.CustomerCode, r.Quantity, r.Amount)));
        Assert.Contains("ไม่ใช่เงิน", await HtmlAsync<ReceiptOver3000View>(report));
    }

    [Fact] // the receipts of a territory (and of a customer): by order, with the total of each and of all
    public async Task The_receipts_of_a_territory_are_listed_by_order_with_totals()
    {
        var f = await NewSetupAsync();
        await OrderAsync(f, No(f, 1), f.CustomerA, October, OrderEntryMode.Print, L(f.Pair, 100, 10m), L(f.Box, 20, 5m));
        await OrderAsync(f, No(f, 2), f.CustomerB, October.AddDays(1), OrderEntryMode.Print, L(f.Pair, 50, 10m));

        var ofTerritory = await AsAsync(s => OrderReports.ReceiptOrdersAsync(s.Get<SalesOrderReportDirectory>(), s.Get<CustomerDirectory>(), s.Get<SalesTerritoryDirectory>(),
            October, October.AddDays(10), f.Territory, null));
        var ofCustomer = await AsAsync(s => OrderReports.ReceiptOrdersAsync(s.Get<SalesOrderReportDirectory>(), s.Get<CustomerDirectory>(), s.Get<SalesTerritoryDirectory>(),
            October, October.AddDays(10), null, f.CustomerB));

        Assert.Equal([No(f, 1), No(f, 2)], ofTerritory.Orders.Select(o => o.OrderNo));
        Assert.Equal([1100m, 500m], ofTerritory.Orders.Select(o => o.Amount));
        Assert.Equal(1600m, ofTerritory.Amount);
        Assert.StartsWith("รายงานการรับ ORDER ของเขตการขาย " + f.TerritoryCode, ofTerritory.Heading);
        Assert.StartsWith("รายงานการรับ ORDER ตามชื่อลูกค้า", ofCustomer.Heading);
        Assert.Equal([No(f, 2)], ofCustomer.Orders.Select(o => o.OrderNo));
        var html = await HtmlAsync<ReceiptOrdersView>(ofTerritory);
        Assert.Contains("รวมทั้งหมด 2 ใบ", html);
        Assert.Contains("1,600.00", html);
    }
}
