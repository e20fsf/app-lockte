using System.IO.Compression;
using System.Xml.Linq;
using Lockte.Prod;
using Lockte.Sales;
using Lockte.Web.Reports;
using Lockte.Web.Reports.Order;

namespace Lockte.Web.Tests;

/// <summary>
/// สรุปการส่งออก (RPT-08, #114) as tables: ตามลูกค้า, ตามสินค้า, ตามรุ่น and the year in 4 forms, printed or downloaded as Excel from the same table (INT-01). Fictional data only;
/// every test delivers in a year of its own because the summaries are by date range and the tests of this collection share a database.
/// </summary>
[Collection(LocalDbCollection.Name)]
public sealed class ExportSummaryTests(LocalDbFixture db)
{
    private static int counter = Random.Shared.Next(0x100, 0x700); // "E" + 3 hex digits must stay within the 4-character territory code
    private static int year = Random.Shared.Next(2600, 9000); // a range of its own per run: the database outlives the run

    private static string TerritoryCode() => "E" + Interlocked.Increment(ref counter).ToString("X3");

    private static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    private static int NewYear() => Interlocked.Increment(ref year);

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

    private static SalesOrderLineInput L(int product, decimal quantity, decimal price) => new(product, quantity, price);

    private sealed record Setup(int Territory, string TerritoryCode, int A, string CodeA, int B, string CodeB, int C, string CodeC, int Pair, string PairCode, int Box, string BoxCode, string Model);

    private async Task<Setup> NewSetupAsync()
    {
        var territoryCode = TerritoryCode();
        var territory = await AsAsync(s => SavedAsync(s.Get<SalesTerritoryRegistry>().SaveAsync(new SalesTerritoryInput(null, territoryCode, "เขตสรุปส่งออก", null, null))));
        var prefix = Unique("91-");
        var (codeA, codeB, codeC) = (prefix + "A", prefix + "B", prefix + "C");
        var a = await AsAsync(s => SavedAsync(s.Get<CustomerRegistry>().SaveAsync(new CustomerInput(null, codeA, "ลูกค้า เอ & บี", null, SalesTerritoryId: territory))));
        var b = await AsAsync(s => SavedAsync(s.Get<CustomerRegistry>().SaveAsync(new CustomerInput(null, codeB, "ลูกค้า บี", null, SalesTerritoryId: territory))));
        var c = await AsAsync(s => SavedAsync(s.Get<CustomerRegistry>().SaveAsync(new CustomerInput(null, codeC, "ลูกค้า ซี ไม่มียอด", null, SalesTerritoryId: territory))));
        var modelName = Unique("รุ่น");
        var model = await AsAsync(s => SavedAsync(s.Get<ProductListRegistry>().SaveModelAsync(null, modelName, null)));
        var pairCode = Unique("SB");
        var boxCode = Unique("SD");
        var pair = await AsAsync(s => SavedAsync(s.Get<ProductRegistry>().SaveAsync(new ProductInput(null, pairCode, "ก้ามเบรกสรุป", null, Unit: "คู่", ModelId: model))));
        var box = await AsAsync(s => SavedAsync(s.Get<ProductRegistry>().SaveAsync(new ProductInput(null, boxCode, "ตลับสรุป", null, Unit: "ตลับ"))));
        return new Setup(territory, territoryCode, a, codeA, b, codeB, c, codeC, pair, pairCode, box, boxCode, modelName);
    }

    /// <summary>One order of <paramref name="customer"/> with a line for each (month, quantity, product): delivered in that month at the price of 10.</summary>
    private async Task DeliverAsync(Setup f, int customer, int year, int number, params (int Month, decimal Quantity, int Product)[] deliveries)
    {
        var orderNo = $"{f.TerritoryCode}-70-{number:0000}";
        var created = await AsAsync(s => s.Get<SalesOrderRegistry>().CreateAsync(new SalesOrderInput(orderNo, new DateOnly(year, 1, 1), customer, null, f.Territory, new DateOnly(year, 12, 31),
            "30", null, deliveries.Select(d => L(d.Product, d.Quantity, 10m)).ToList()), OrderEntryMode.Print));
        Assert.True(created.Succeeded, string.Join(" ", created.Errors));
        var lines = await AsAsync(s => s.Get<SalesDeliveryDirectory>().GetLinesAsync(orderNo));
        for (var i = 0; i < deliveries.Length; i++)
        {
            var line = lines.Single(l => l.LineNo == i + 1);
            await AsAsync(s => SavedAsync(s.Get<SalesDeliveryRegistry>().SetPendingAsync(line.LineId, line.RowVersion,
                new PendingDeliveryInput(new DateOnly(year, deliveries[i].Month, 15), $"01{number:00000}", i + 1, deliveries[i].Quantity, null, 1))));
        }
        await AsAsync(s => s.Get<SalesDeliveryRegistry>().SaveAsync());
    }

    private static ExportFilter Year(int year, int? territory = null) => new(new DateOnly(year, 1, 1), new DateOnly(year, 12, 31), SalesTerritoryId: territory);

    private static async Task<string> HtmlAsync(SummaryTable table)
        => System.Net.WebUtility.HtmlDecode(await ReportBody.RenderAsync<SummaryTableView>(new() { ["Table"] = table }));

    private static object?[] RowOf(SummaryTable table, string code) => table.Rows.Single(r => (string?)r[0] == code);

    // ---------- สรุปทั้งปี (AC-23) ----------

    [Fact] // แบบ 200: Σ มูลค่า by month for every customer; the total is blank when any month is blank (⚠ Q-56 kept); a customer with none is a row of blanks
    public async Task The_year_summary_leaves_the_total_blank_when_any_month_is_blank()
    {
        var f = await NewSetupAsync();
        var year = NewYear();
        await DeliverAsync(f, f.A, year, 1, Enumerable.Range(1, 12).Select(m => (m, 100m * m, f.Pair)).ToArray()); // every month
        await DeliverAsync(f, f.B, year, 2, (1, 50m, f.Pair), (3, 70m, f.Box)); // two months

        var table = await AsAsync(s => ExportSummaries.YearAsync(200, s.Get<SalesDeliveryReportDirectory>(), s.Get<CustomerDirectory>(), s.Get<SalesTerritoryDirectory>(), Year(year)));

        var a = RowOf(table, f.CodeA);
        Assert.Equal(15, a.Length); // code, name, 12 months, total
        Assert.Equal(1000m, a[2]);  // January: 100 × 10
        Assert.Equal(12000m, a[13]); // December
        Assert.Equal(78000m, a[14]); // 100 × (1+…+12) × 10: all 12 months → a total
        var b = RowOf(table, f.CodeB);
        Assert.Equal(500m, b[2]);
        Assert.Null(b[3]); // February: nothing delivered → blank, not 0
        Assert.Equal(700m, b[4]);
        Assert.Null(b[14]); // the total is blank
        var c = RowOf(table, f.CodeC);
        Assert.All(c.Skip(2), cell => Assert.Null(cell));
        Assert.Equal("มกราคม", table.Columns[2].Header);
        Assert.Equal("จำนวนเงินรวม", table.Columns[14].Header);
    }

    [Fact] // แบบ 300: the quantity; แบบ 100: only the customers of the เขตการขาย, even with nothing delivered, with the territory at the end
    public async Task The_other_forms_of_the_year_summary()
    {
        var f = await NewSetupAsync();
        var year = NewYear();
        await DeliverAsync(f, f.A, year, 1, Enumerable.Range(1, 12).Select(m => (m, 10m, f.Pair)).ToArray());
        await DeliverAsync(f, f.B, year, 2, (2, 5m, f.Box));

        var quantity = await AsAsync(s => ExportSummaries.YearAsync(300, s.Get<SalesDeliveryReportDirectory>(), s.Get<CustomerDirectory>(), s.Get<SalesTerritoryDirectory>(), Year(year)));
        var territory = await AsAsync(s => ExportSummaries.YearAsync(100, s.Get<SalesDeliveryReportDirectory>(), s.Get<CustomerDirectory>(), s.Get<SalesTerritoryDirectory>(), Year(year, f.Territory)));

        Assert.Equal(120m, RowOf(quantity, f.CodeA)[14]);
        Assert.Equal("จำนวนรวม", quantity.Columns[14].Header);
        Assert.Equal(new[] { f.CodeA, f.CodeB, f.CodeC }.Order(), territory.Rows.Select(r => (string)r[0]!)); // all three customers of the territory, nobody else
        var a = RowOf(territory, f.CodeA);
        Assert.Equal((f.TerritoryCode, "เขตสรุปส่งออก"), (a[15], a[16]));
        Assert.Equal("รหัสเซล", territory.Columns[15].Header);
        Assert.Contains(f.TerritoryCode, territory.Filter);
    }

    [Fact] // แบบ 400: by product — the quantity by month, then the total quantity and the total money (blank by the same rule)
    public async Task The_product_form_has_the_quantity_by_month_and_both_totals()
    {
        var f = await NewSetupAsync();
        var year = NewYear();
        await DeliverAsync(f, f.A, year, 1, Enumerable.Range(1, 12).Select(m => (m, 10m, f.Pair)).ToArray());
        await DeliverAsync(f, f.A, year, 2, (4, 7m, f.Box));

        var table = await AsAsync(s => ExportSummaries.YearAsync(400, s.Get<SalesDeliveryReportDirectory>(), s.Get<CustomerDirectory>(), s.Get<SalesTerritoryDirectory>(), Year(year)));

        var pair = RowOf(table, f.PairCode);
        Assert.Equal((120m, 1200m), (pair[14], pair[15]));
        var box = RowOf(table, f.BoxCode);
        Assert.Equal(7m, box[5]); // April
        Assert.Null(box[2]);
        Assert.Equal((null, null), (box[14], box[15]));
    }

    // ---------- ตามลูกค้า, ตามสินค้า, ตามรุ่น ----------

    [Fact] // the customer's report: the products with Σ จำนวน + หน่วย and Σ จำนวนเงิน; the head names the customer and the range (D-10)
    public async Task A_customer_report_lists_the_products_with_the_customer_and_range_in_the_head()
    {
        var f = await NewSetupAsync();
        var year = NewYear();
        await DeliverAsync(f, f.A, year, 1, (1, 100m, f.Pair), (2, 20m, f.Pair), (3, 5m, f.Box));
        await DeliverAsync(f, f.B, year, 2, (1, 999m, f.Pair));

        var table = await AsAsync(s => ExportSummaries.CustomerDetailAsync(s.Get<SalesDeliveryReportDirectory>(), s.Get<CustomerDirectory>(), s.Get<SalesTerritoryDirectory>(),
            Year(year) with { CustomerId = f.A }));

        Assert.Equal(2, table.Rows.Count);
        Assert.Equal(new[] { f.PairCode, f.BoxCode }.Order(), table.Rows.Select(r => (string)r[1]!));
        Assert.Equal((120m, "คู่", 1200m), (table.Rows.Single(r => (string)r[1]! == f.PairCode)[3], table.Rows.Single(r => (string)r[1]! == f.PairCode)[4], table.Rows.Single(r => (string)r[1]! == f.PairCode)[5]));
        Assert.Equal(1250m, table.TotalRow![5]);
        Assert.Contains(f.CodeA, table.Filter);
        Assert.Contains("ตั้งแต่วันที่", table.Filter);
        var html = await HtmlAsync(table);
        Assert.Contains("รวม", html);
        Assert.Contains("1,250.00", html);
    }

    [Fact] // สรุปส่งออก1: per customer, the most money first, with Σ จำนวน; the territory narrows it
    public async Task The_customer_summary_is_most_money_first()
    {
        var f = await NewSetupAsync();
        var year = NewYear();
        await DeliverAsync(f, f.A, year, 1, (1, 10m, f.Pair));
        await DeliverAsync(f, f.B, year, 2, (1, 30m, f.Pair), (2, 5m, f.Box));

        var table = await AsAsync(s => ExportSummaries.CustomerSummaryAsync(s.Get<SalesDeliveryReportDirectory>(), s.Get<CustomerDirectory>(), s.Get<SalesTerritoryDirectory>(), Year(year, f.Territory)));

        Assert.Equal([f.CodeB, f.CodeA], table.Rows.Select(r => (string)r[1]!));
        Assert.Equal((35m, 350m), (table.Rows[0][3], table.Rows[0][4]));
        Assert.Equal(450m, table.TotalRow![4]);
    }

    [Fact] // the product summary: the code pattern narrows it, sorted by code or by name; the month in the head comes from the dates (D-5)
    public async Task The_product_summary_is_narrowed_by_code_and_sorted_by_code_or_name()
    {
        var f = await NewSetupAsync();
        var year = NewYear();
        await DeliverAsync(f, f.A, year, 1, (1, 10m, f.Pair), (1, 5m, f.Box));
        var january = new ExportFilter(new DateOnly(year, 1, 1), new DateOnly(year, 1, 31));

        var all = await AsAsync(s => ExportSummaries.ProductSummaryAsync(s.Get<SalesDeliveryReportDirectory>(), s.Get<CustomerDirectory>(), s.Get<SalesTerritoryDirectory>(), january with { CustomerId = f.A }));
        var onlyPairs = await AsAsync(s => ExportSummaries.ProductSummaryAsync(s.Get<SalesDeliveryReportDirectory>(), s.Get<CustomerDirectory>(), s.Get<SalesTerritoryDirectory>(),
            january with { CustomerId = f.A, CodePattern = "SB*" }));
        var byName = await AsAsync(s => ExportSummaries.ProductSummaryAsync(s.Get<SalesDeliveryReportDirectory>(), s.Get<CustomerDirectory>(), s.Get<SalesTerritoryDirectory>(),
            january with { CustomerId = f.A, SortByName = true }));

        Assert.Equal(new[] { f.PairCode, f.BoxCode }.Order(), all.Rows.Select(r => (string)r[1]!));
        Assert.Equal([f.PairCode], onlyPairs.Rows.Select(r => (string)r[1]!));
        Assert.Equal(["ก้ามเบรกสรุป", "ตลับสรุป"], byName.Rows.Select(r => (string)r[2]!)); // ก before ต
        Assert.Contains($"มกราคม {year + 543}", all.Title);
        Assert.Contains("รหัสสินค้า SB*", onlyPairs.Filter);
    }

    [Fact] // the model summary: the model of the product and the unit
    public async Task The_model_summary_groups_by_the_model_of_the_product()
    {
        var f = await NewSetupAsync();
        var year = NewYear();
        await DeliverAsync(f, f.A, year, 1, (1, 10m, f.Pair), (2, 5m, f.Box));

        var table = await AsAsync(s => ExportSummaries.ModelSummaryAsync(s.Get<SalesDeliveryReportDirectory>(), s.Get<ProductDirectory>(), s.Get<CustomerDirectory>(),
            s.Get<SalesTerritoryDirectory>(), Year(year)));

        Assert.Contains(table.Rows, r => (string)r[1]! == f.Model && (decimal)r[2]! == 10m && (string?)r[3] == "คู่");
        Assert.Contains(table.Rows, r => (string)r[1]! == "ไม่ระบุรุ่น" && (decimal)r[2]! == 5m && (string?)r[3] == "ตลับ");
    }

    [Fact] // D-6: nothing delivered in the range prints "ไม่มีข้อมูล"
    public async Task A_range_with_nothing_prints_no_data()
    {
        var year = NewYear();

        var table = await AsAsync(s => ExportSummaries.CustomerSummaryAsync(s.Get<SalesDeliveryReportDirectory>(), s.Get<CustomerDirectory>(), s.Get<SalesTerritoryDirectory>(), Year(year)));

        Assert.Empty(table.Rows);
        Assert.Contains("ไม่มีข้อมูล", await HtmlAsync(table));
    }

    // ---------- Excel (INT-01) ----------

    [Fact] // the file is the table cell for cell: text as text, numbers as numbers, a blank stays blank, & is escaped, the heading row first
    public void The_excel_file_holds_the_same_cells_as_the_table()
    {
        var table = new SummaryTable("สรุป & ทดสอบ <1>", "ตั้งแต่วันที่ 1/1/2570",
            [new("รหัส"), new("ชื่อ"), new("จำนวน", SummaryFormat.Quantity), new("เงิน", SummaryFormat.Money)],
            [new object?[] { "A1", "เอ & บี", 12.5m, 1234.5m }, new object?[] { "A2", "ซี", null, 0m }], [null, "รวมทั้งหมด", null, 1234.5m]);

        var bytes = XlsxWriter.Write(table);

        using var zip = new ZipArchive(new MemoryStream(bytes));
        Assert.Contains(zip.Entries, e => e.FullName == "xl/worksheets/sheet1.xml");
        Assert.Contains(zip.Entries, e => e.FullName == "[Content_Types].xml");
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var sheet = XDocument.Load(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open());
        var cells = sheet.Descendants(ns + "c").ToDictionary(c => (string)c.Attribute("r")!, c => c);
        string Text(string reference) => string.Concat(cells[reference].Descendants(ns + "t").Select(t => t.Value));
        Assert.Equal("สรุป & ทดสอบ <1>", Text("A1"));
        Assert.Equal("ตั้งแต่วันที่ 1/1/2570", Text("A2"));
        Assert.Equal(["รหัส", "ชื่อ", "จำนวน", "เงิน"], new[] { "A4", "B4", "C4", "D4" }.Select(Text));
        Assert.Equal("เอ & บี", Text("B5"));
        Assert.Equal("12.5", (string)cells["C5"].Element(ns + "v")!);
        Assert.Equal("1234.5", (string)cells["D5"].Element(ns + "v")!);
        Assert.DoesNotContain("C6", cells.Keys); // a blank is no cell
        Assert.Equal("0", (string)cells["D6"].Element(ns + "v")!);
        Assert.Equal("รวมทั้งหมด", Text("B7"));
        Assert.Equal("D", System.Text.RegularExpressions.Regex.Match(XlsxWriter.Ref(3, 1), "[A-Z]+").Value);
        Assert.Equal("AA10", XlsxWriter.Ref(26, 10));
    }

    [Fact] // what is shown on the screen is what the Excel gets: the same table
    public async Task The_table_of_a_summary_goes_to_excel_as_it_is_printed()
    {
        var f = await NewSetupAsync();
        var year = NewYear();
        await DeliverAsync(f, f.A, year, 1, (1, 10m, f.Pair));
        var table = await AsAsync(s => ExportSummaries.CustomerSummaryAsync(s.Get<SalesDeliveryReportDirectory>(), s.Get<CustomerDirectory>(), s.Get<SalesTerritoryDirectory>(), Year(year)));

        using var zip = new ZipArchive(new MemoryStream(XlsxWriter.Write(table)));
        var xml = new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open()).ReadToEnd();

        Assert.Contains("ลูกค้า เอ &amp; บี", xml);
        Assert.Contains("<v>100</v>", xml);
    }
}
