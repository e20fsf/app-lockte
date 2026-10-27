using System.IO.Compression;
using System.Xml.Linq;
using Lockte.Prod;
using Lockte.Sales;
using Lockte.Web.Reports;
using Lockte.Web.Reports.Order;

namespace Lockte.Web.Tests;

/// <summary>
/// สรุปการส่งออก (2) (#115): ตามชนิดสินค้า (RPT-09), ตามเขตการขายรายเดือน, ตามจังหวัด and ยอดขายของพนักงานขายแต่ละคน (RPT-09). Fictional data only; every test delivers in a
/// year of its own (far from the years of the other summaries) because the summaries are by date range and the tests of this collection share a database.
/// </summary>
[Collection(LocalDbCollection.Name)]
public sealed class ExportSummaryByTypeTests(LocalDbFixture db)
{
    private static int counter = Random.Shared.Next(0x100, 0x700); // "V" + 3 hex digits stays within the 4-character territory code
    private static int year = Random.Shared.Next(9100, 9800);

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

    private sealed record Setup(int Territory1, string Code1, int Territory2, string Code2, int A, string CodeA, int B, string CodeB, int C, string CodeC, int Pad, int Spring, int Other,
        string TypePad, string TypeSpring, string Province1, string Province2);

    private async Task<Setup> NewSetupAsync()
    {
        string TerritoryCode() => "V" + Interlocked.Increment(ref counter).ToString("X3");
        var (code1, code2) = (TerritoryCode(), TerritoryCode());
        var t1 = await AsAsync(s => SavedAsync(s.Get<SalesTerritoryRegistry>().SaveAsync(new SalesTerritoryInput(null, code1, "เขตทดสอบหนึ่ง", null, null))));
        var t2 = await AsAsync(s => SavedAsync(s.Get<SalesTerritoryRegistry>().SaveAsync(new SalesTerritoryInput(null, code2, "เขตทดสอบสอง", null, null))));
        var (province1, province2) = (Unique("จังหวัดก"), Unique("จังหวัดข"));
        var prefix = Unique("93-");
        var (codeA, codeB, codeC) = (prefix + "A", prefix + "B", prefix + "C");
        var a = await AsAsync(s => SavedAsync(s.Get<CustomerRegistry>().SaveAsync(new CustomerInput(null, codeA, "ลูกค้า เอ", null, Province: province1, SalesTerritoryId: t1))));
        var b = await AsAsync(s => SavedAsync(s.Get<CustomerRegistry>().SaveAsync(new CustomerInput(null, codeB, "ลูกค้า บี", null, Province: province2, SalesTerritoryId: t2))));
        var c = await AsAsync(s => SavedAsync(s.Get<CustomerRegistry>().SaveAsync(new CustomerInput(null, codeC, "ลูกค้า ซี", null, SalesTerritoryId: t1))));

        // two groups both match the pad code: the one with the longer fixed text is the ชนิด; the third product matches no group → no ชนิด
        var stem = "Q" + Guid.NewGuid().ToString("N")[..4].ToUpperInvariant();
        var (typePad, typeSpring) = (Unique("ก้ามทดสอบ"), Unique("สปริงทดสอบ"));
        await AsAsync(s => SavedAsync(s.Get<ProductListRegistry>().SaveGroupAsync(null, stem + "*", Unique("กว้าง"), null)));
        await AsAsync(s => SavedAsync(s.Get<ProductListRegistry>().SaveGroupAsync(null, stem + "P*", typePad, null)));
        await AsAsync(s => SavedAsync(s.Get<ProductListRegistry>().SaveGroupAsync(null, stem + "S*", typeSpring, null)));
        var pad = await AsAsync(s => SavedAsync(s.Get<ProductRegistry>().SaveAsync(new ProductInput(null, stem + "P1", "ก้ามทดสอบ", null, Unit: "คู่"))));
        var spring = await AsAsync(s => SavedAsync(s.Get<ProductRegistry>().SaveAsync(new ProductInput(null, stem + "S1", "สปริงทดสอบ", null, Unit: "ตัว"))));
        var other = await AsAsync(s => SavedAsync(s.Get<ProductRegistry>().SaveAsync(new ProductInput(null, Unique("NX"), "อื่นทดสอบ", null, Unit: "ชิ้น"))));
        return new Setup(t1, code1, t2, code2, a, codeA, b, codeB, c, codeC, pad, spring, other, typePad, typeSpring, province1, province2);
    }

    private async Task DeliverAsync(string territoryCode, int territory, int customer, int year, int number, params (int Month, decimal Quantity, int Product)[] deliveries)
    {
        var orderNo = $"{territoryCode}-71-{number:0000}";
        var created = await AsAsync(s => s.Get<SalesOrderRegistry>().CreateAsync(new SalesOrderInput(orderNo, new DateOnly(year, 1, 1), customer, null, territory, new DateOnly(year, 12, 31),
            "30", null, deliveries.Select(d => new SalesOrderLineInput(d.Product, d.Quantity, 10m)).ToList()), OrderEntryMode.Print));
        Assert.True(created.Succeeded, string.Join(" ", created.Errors));
        var lines = await AsAsync(s => s.Get<SalesDeliveryDirectory>().GetLinesAsync(orderNo));
        for (var i = 0; i < deliveries.Length; i++)
        {
            var line = lines.Single(l => l.LineNo == i + 1);
            await AsAsync(s => SavedAsync(s.Get<SalesDeliveryRegistry>().SetPendingAsync(line.LineId, line.RowVersion,
                new PendingDeliveryInput(new DateOnly(year, deliveries[i].Month, 15), $"02{number:00000}", i + 1, deliveries[i].Quantity, null, 1))));
        }
        await AsAsync(s => s.Get<SalesDeliveryRegistry>().SaveAsync());
    }

    private static ExportFilter Range(int year, int fromMonth = 1, int toMonth = 12, int? territory = null)
        => new(new DateOnly(year, fromMonth, 1), new DateOnly(year, toMonth, 1).AddMonths(1).AddDays(-1), SalesTerritoryId: territory);

    private static async Task<string> HtmlAsync(SummaryTable table)
        => System.Net.WebUtility.HtmlDecode(await ReportBody.RenderAsync<SummaryTableView>(new() { ["Table"] = table }));

    // ---------- ตามชนิดสินค้า ----------

    [Fact] // the ชนิด is the group with the longest fixed text, the money share has 4 decimals and the total is 100; a code with no group of its own is "ไม่ระบุชนิด"
    public async Task The_type_summary_groups_by_the_most_specific_group_and_shows_the_share()
    {
        var f = await NewSetupAsync();
        var year = ExportSummaryByTypeTests.year++;
        await DeliverAsync(f.Code1, f.Territory1, f.A, year, 1, (1, 30m, f.Pad), (1, 10m, f.Spring), (1, 10m, f.Other)); // 300 + 100 + 100 money
        await DeliverAsync(f.Code2, f.Territory2, f.B, year, 2, (1, 20m, f.Pad));                                        // 200 more

        var table = await AsAsync(s => ExportSummaries.TypeSummaryAsync(s.Get<SalesDeliveryReportDirectory>(), s.Get<ProductDirectory>(), s.Get<CustomerDirectory>(),
            s.Get<SalesTerritoryDirectory>(), Range(year, 1, 1)));

        var pad = table.Rows.Single(r => (string)r[1]! == f.TypePad);
        Assert.Equal((50m, "คู่", 500m, 71.4286m), (pad[2], pad[3], pad[4], pad[5])); // 500 of 700
        Assert.Equal(14.2857m, table.Rows.Single(r => (string)r[1]! == f.TypeSpring)[5]);
        var none = table.Rows.Single(r => (string)r[1]! == ExportSummaries.NoType);
        Assert.Equal((10m, "ชิ้น", 100m), (none[2], none[3], none[4]));
        Assert.Equal(700m, table.TotalRow![4]);
        Assert.Equal("รวมทั้งหมด 3 ชนิด", table.TotalRow[1]);
        Assert.Contains($"มกราคม {year + 543}", table.Title);
        Assert.StartsWith("ยอดขายสินค้าแต่ละชนิดเดือน", table.Title);
        Assert.Equal("เปอร์เซ็นต์", table.Columns[5].Header);
        var html = await HtmlAsync(table);
        Assert.Contains("71.4286%", html);
        Assert.Contains("500.00", html);
    }

    [Fact] // the territory narrows the type summary
    public async Task The_type_summary_can_be_narrowed_to_one_territory()
    {
        var f = await NewSetupAsync();
        var year = ExportSummaryByTypeTests.year++;
        await DeliverAsync(f.Code1, f.Territory1, f.A, year, 1, (1, 30m, f.Pad));
        await DeliverAsync(f.Code2, f.Territory2, f.B, year, 2, (1, 20m, f.Spring));

        var table = await AsAsync(s => ExportSummaries.TypeSummaryAsync(s.Get<SalesDeliveryReportDirectory>(), s.Get<ProductDirectory>(), s.Get<CustomerDirectory>(),
            s.Get<SalesTerritoryDirectory>(), Range(year, territory: f.Territory2)));

        Assert.Equal([f.TypeSpring], table.Rows.Select(r => (string)r[1]!));
        Assert.Equal(100m, table.Rows[0][5]);
    }

    // ---------- ตามเขตการขาย ----------

    [Fact] // the money of each territory in the month columns; a month with nothing is blank and so is the total then (Q-56)
    public async Task The_territory_summary_has_the_money_by_month()
    {
        var f = await NewSetupAsync();
        var year = ExportSummaryByTypeTests.year++;
        await DeliverAsync(f.Code1, f.Territory1, f.A, year, 1, (1, 10m, f.Pad), (3, 5m, f.Pad));
        await DeliverAsync(f.Code1, f.Territory1, f.C, year, 2, (3, 5m, f.Spring));

        var table = await AsAsync(s => ExportSummaries.TerritoryMonthsAsync(s.Get<SalesDeliveryReportDirectory>(), s.Get<CustomerDirectory>(), s.Get<SalesTerritoryDirectory>(), Range(year)));

        var first = table.Rows.Single(r => (string)r[0]! == f.Code1);
        Assert.Equal((100m, null, 100m), (first[2], first[3], first[4])); // Jan, Feb (blank), Mar: 5 × 10 + 5 × 10
        Assert.Null(first[14]);
        var second = table.Rows.Single(r => (string)r[0]! == f.Code2);
        Assert.All(second.Skip(2), cell => Assert.Null(cell));
        Assert.Equal("มกราคม", table.Columns[2].Header);
    }

    // ---------- ตามจังหวัด ----------

    [Fact] // Σ จำนวนเงิน by the province of the customer; a customer with none is "ไม่ระบุจังหวัด"
    public async Task The_province_report_adds_the_money_by_province_of_the_customer()
    {
        var f = await NewSetupAsync();
        var year = ExportSummaryByTypeTests.year++;
        await DeliverAsync(f.Code1, f.Territory1, f.A, year, 1, (1, 10m, f.Pad));
        await DeliverAsync(f.Code1, f.Territory1, f.A, year, 2, (2, 5m, f.Pad));
        await DeliverAsync(f.Code2, f.Territory2, f.B, year, 3, (1, 7m, f.Spring));
        await DeliverAsync(f.Code1, f.Territory1, f.C, year, 4, (1, 3m, f.Other));

        var table = await AsAsync(s => ExportSummaries.ProvinceAsync(s.Get<SalesDeliveryReportDirectory>(), s.Get<CustomerDirectory>(), s.Get<SalesTerritoryDirectory>(), Range(year)));

        Assert.Equal(150m, table.Rows.Single(r => (string)r[1]! == f.Province1)[2]);
        Assert.Equal(70m, table.Rows.Single(r => (string)r[1]! == f.Province2)[2]);
        Assert.Equal(30m, table.Rows.Single(r => (string)r[1]! == "ไม่ระบุจังหวัด")[2]);
        Assert.Equal(250m, table.TotalRow![2]);
        Assert.Equal("รายงานส่งออกตามจังหวัด", table.Title);
    }

    // ---------- ยอดขายของพนักงานขายแต่ละคน ----------

    [Fact] // from the deliveries in the range: per territory then per SALE ORDER number, and after each territory "n ใบ" with the money
    public async Task The_sales_person_report_lists_the_orders_with_a_total_after_each_territory()
    {
        var f = await NewSetupAsync();
        var year = ExportSummaryByTypeTests.year++;
        await DeliverAsync(f.Code1, f.Territory1, f.A, year, 1, (1, 10m, f.Pad), (1, 5m, f.Pad)); // one order, two lines: 150
        await DeliverAsync(f.Code1, f.Territory1, f.C, year, 2, (1, 4m, f.Spring));              // 40
        await DeliverAsync(f.Code1, f.Territory1, f.C, year, 3, (6, 9m, f.Spring));              // outside the range (June)
        await DeliverAsync(f.Code2, f.Territory2, f.B, year, 4, (1, 6m, f.Pad));                 // 60

        var table = await AsAsync(s => ExportSummaries.SalesPersonAsync(s.Get<SalesDeliveryReportDirectory>(), s.Get<CustomerDirectory>(), s.Get<SalesTerritoryDirectory>(), Range(year, 1, 3)));

        var first = table.Rows.Where(r => (string)r[0]! == f.Code1).ToList();
        Assert.Equal(3, first.Count); // two orders and the total line
        Assert.Equal(new object?[] { 1, 2, null }, first.Select(r => r[1]).ToArray());
        Assert.Equal([150m, 40m, 190m], first.Select(r => (decimal)r[3]!));
        Assert.Equal("2 ใบ", first[^1][2]);
        var second = table.Rows.Where(r => (string)r[0]! == f.Code2).ToList();
        Assert.Equal(["1 ใบ"], second.Where(r => r[1] is null).Select(r => (string)r[2]!));
        Assert.StartsWith("ยอดขายของพนักงานขายแต่ละคนเดือน", table.Title);
        Assert.Contains("ใบ", await HtmlAsync(table));
    }

    [Fact] // the percent format is a number in the Excel file, with its own format
    public void The_percent_column_is_a_number_in_the_excel_file()
    {
        var table = new SummaryTable("ทดสอบ", null, [new("ชนิด"), new("เปอร์เซ็นต์", SummaryFormat.Percent)], [new object?[] { "ก", 54.978m }]);

        using var zip = new ZipArchive(new MemoryStream(XlsxWriter.Write(table)));
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var sheet = XDocument.Load(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open());
        var cell = sheet.Descendants(ns + "c").Single(c => (string?)c.Attribute("s") == "5");

        Assert.Equal("54.978", (string)cell.Element(ns + "v")!);
        Assert.Equal("54.9780%", SummaryTable.Show(table.Columns[1], 54.978m));
    }
}
