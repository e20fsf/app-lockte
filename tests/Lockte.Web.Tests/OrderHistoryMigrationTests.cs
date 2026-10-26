using System.Runtime.Versioning;
using Lockte.Migration;
using Lockte.Sales;
using Lockte.Web.Audit;

namespace Lockte.Web.Tests;

/// <summary>
/// เครื่องมือย้ายข้อมูล: ORDER ค้างส่ง, ประวัติ ORDER และประวัติการส่งของ (#112) from a made-up <c>ผลผลิตและORDER.accdb</c> — every name,
/// number and amount in it is fictional (the repo is public). The master data is loaded by the same run, as in the real one.
/// </summary>
[Collection(MigrationDbCollection.Name)]
[SupportedOSPlatform("windows")]
public sealed class OrderHistoryMigrationTests(MigrationDbFixture db) : IDisposable
{
    private readonly DirectoryInfo work = Directory.CreateTempSubdirectory("lockte-order-history-test-");

    public void Dispose() => work.Delete(recursive: true);

    private async Task<T> AsAsync<T>(Func<TestScope, Task<T>> action)
    {
        await using var scope = db.Scope("hr1");
        return await action(scope);
    }

    private Task<MigrationReport> RunAsync(string source, string output = "out")
        => OrderMigrationTool.RunAsync(db.Services, source, null, Path.Combine(work.FullName, output));

    private string Save(FakeLegacyFile file) => file.Save(Path.Combine(work.FullName, "ผลผลิตและORDER.accdb"));

    private static readonly DateTime March = new(2026, 3, 1);

    private static (string, object?)[] Line(string no, string customer, int line, string product, double qty, double? price, double? amount, DateTime date, string sale = "08",
        string? name = null, params (string, object?)[] more)
        => [.. new (string, object?)[]
        {
            ("OD_DATE", date), ("CUS_ID", customer), ("CUS_N", name ?? "ชื่อลูกค้าสมมุติ"), ("REF_NO", no), ("SUF_NO", line), ("PD_C", product), ("PD_N", "ชื่อสินค้าสมมุติ"),
            ("QTY", qty), ("U_NAME", "คู่"), ("U_PRICE", price), ("AMOUNT", amount), ("SALE_ID", sale), ("DUERE_D", "30"), ("DATE_DL", date.AddDays(7)),
        }, .. more];

    private static FakeLegacyFile Standard()
    {
        var file = FakeOrderFile.Empty()
            .Row("เซล", ("ID", "08"), ("NAME", "เขตสมมุติหนึ่ง"))
            .Row("เซล", ("ID", "09"), ("NAME", "เขตสมมุติสอง"))
            .Row("P_CUS", ("CODE", "08-01-001"), ("NAME", "ร้านเอ"), ("CREDIT", "30"), ("SALE_ID", "08"))
            .Row("P_CUS", ("CODE", "08-01-002"), ("NAME", "ร้านบี"), ("CREDIT", "30"), ("SALE_ID", "08"))
            .Row("P_CUS", ("CODE", "09-01-001"), ("NAME", "ร้านซี"), ("CREDIT", "30"), ("SALE_ID", "09"))
            .Row("P_NAME", ("PD_C", "G1001"), ("PD_N", "สินค้าหนึ่ง"), ("U_NAME", "คู่"))
            .Row("P_NAME", ("PD_C", "G1002"), ("PD_N", "สินค้าสอง"), ("U_NAME", "ชิ้น"));

        // ใบ 1: two lines in the history; line 1 still has 40 to deliver, line 2 was delivered in full
        file.Row("ORDER", Line("08-69-0001", "08-01-001", 1, "G1001", 100, 10, 1000, March.AddDays(4)));
        file.Row("ORDER", Line("08-69-0001", "08-01-001", 2, "G1002", 50, 10, 500, March.AddDays(4)));
        file.Row("ODD", Line("08-69-0001", "08-01-001", 1, "G1001", 40, 10, 400, March.AddDays(4)));
        // ใบ 2: one number, two customers (A first by date)
        file.Row("ORDER", Line("08-69-0002", "08-01-001", 1, "G1001", 10, 10, 100, March.AddDays(9)));
        file.Row("ORDER", Line("08-69-0002", "08-01-002", 1, "G1002", 5, 10, 50, March.AddDays(11)));
        // ใบ 3: one number, one customer, two dates = one order
        file.Row("ORDER", Line("08-69-0003", "09-01-001", 1, "G1001", 20, 10, 200, March, sale: "09"));
        file.Row("ORDER", Line("08-69-0003", "09-01-001", 2, "G1002", 30, 10, 300, March.AddDays(1), sale: "09"));
        // ใบ 4: a backorder that is not in the history
        file.Row("ODD", Line("08-69-0004", "08-01-002", 1, "G1002", 7, 10, 70, new DateTime(2026, 2, 1)));
        // a stored amount that is not จำนวน × ราคา is moved as stored (Q-39)
        file.Row("ORDER", Line("08-69-0006", "08-01-002", 1, "G1001", 10, 10, 99, March.AddDays(20)));
        // rows that cannot be taken
        file.Row("ORDER", Line("08-69-0007", "99-99-999", 1, "G1001", 1, 10, 10, March)); // unknown customer
        file.Row("ORDER", Line("08-69-0008", "08-01-001", 1, "ZZ999", 1, 10, 10, March)); // unknown product
        file.Row("ORDER", Line("08-69-0005", "08-01-001", 1, "G1001", 1, 10, 10, new DateTime(2568, 1, 1))); // a Buddhist year in the date
        file.Row("ORDER", Line("07-67-0100", "08-01-001", 1, "G1001", 9, 10, 90, new DateTime(2024, 12, 31))); // before 1 Jan 2025: not moved
        // a backorder with nothing left: not moved
        file.Row("ODD", Line("08-69-0009", "08-01-001", 1, "G1001", 0, 10, 0, March));

        // deliveries
        file.Row("ODDส่งออก", Line("08-69-0001", "08-01-001", 1, "G1001", 60, 10, 600, March.AddDays(4), more: [("DL_DATE", March.AddDays(19)), ("INV_NO", "0100001"), ("INV_SUF", 1), ("DL_QTY", 60.0), ("DL_NET", 600.0), ("DISC_PS", 3.0), ("MARK", "b"), ("LOT", "L1")]));
        file.Row("ODDส่งออก", Line("08-69-0001", "08-01-001", 2, "G1002", 50, 10, 500, March.AddDays(4), more: [("DL_DATE", March.AddDays(19)), ("INV_NO", "0100001"), ("INV_SUF", 2), ("DL_QTY", 50.0), ("DL_NET", 500.0), ("DISC_PS", 2.0), ("MARK", "a")]));
        file.Row("ODDส่งออก", Line("08-69-0002", "08-01-002", 1, "G1002", 5, 10, 50, March.AddDays(11), more: [("DL_DATE", March.AddDays(25)), ("INV_NO", "0100002"), ("INV_SUF", 1), ("DL_QTY", 5.0), ("DL_NET", 50.0), ("DISC_PS", 1.0), ("MARK", "a")]));
        file.Row("ODDส่งออก", Line("08-69-0002", "08-01-001", 1, "G1001", 10, 10, 100, March.AddDays(9), more: [("DL_DATE", March.AddDays(26)), ("INV_NO", "0100003"), ("INV_SUF", 1), ("DL_QTY", 10.0), ("DL_NET", 100.0), ("MARK", "a")]));
        file.Row("ODDส่งออก", Line("07-67-0100", "08-01-001", 1, "G1001", 9, 10, 90, new DateTime(2024, 12, 1), more: [("DL_DATE", new DateTime(2025, 2, 1)), ("INV_NO", "0100004"), ("INV_SUF", 1), ("DL_QTY", 9.0), ("DL_NET", 90.0)])); // an order before 2025
        file.Row("ODDส่งออก", Line("07-67-0101", "08-01-001", 1, "G1001", 9, 10, 90, new DateTime(2024, 11, 1), more: [("DL_DATE", new DateTime(2024, 12, 1)), ("DL_QTY", 9.0), ("DL_NET", 90.0), ("MARK", "a")])); // delivered before 2025: not moved
        file.Row("ODDส่งออก", Line("08-69-0010", "08-01-001", 1, "ZZ999", 1, 10, 10, March, more: [("DL_DATE", March), ("DL_QTY", 1.0), ("DL_NET", 10.0)])); // unknown product
        return file;
    }

    private static TableCount Table(MigrationReport report, string legacyTable) => report.Tables.Single(t => t.LegacyTable == legacyTable);

    private Task<SalesOrderDto?> OrderAsync(string number) => AsAsync(s => s.Get<SalesOrderDirectory>().FindByNumberAsync(number));

    [Fact]
    public async Task Merges_the_backorder_with_the_history_into_one_line_with_two_sets_of_values()
    {
        var report = await RunAsync(Save(Standard()));

        // ใบ 1: line 1 has รับ 100 and ค้างส่ง 40; line 2 was delivered in full (ค้างส่ง 0); it is in the history
        var one = (await OrderAsync("08-69-0001"))!;
        Assert.True(one.InHistory);
        Assert.Equal(("08-01-001", "08", new DateOnly(2026, 3, 5), (DateOnly?)new DateOnly(2026, 3, 12), "30"), (one.CustomerCode, one.SalesTerritoryCode, one.OrderDate, one.DeliveryDate, one.PaymentTerms));
        Assert.Equal([(1, 100m, 1000m, 40m, 400m), (2, 50m, 500m, 0m, (decimal?)null)],
            one.Lines.Select(l => (l.LineNo, l.ReceivedQuantity!.Value, l.ReceivedAmount!.Value, l.OutstandingQuantity, l.OutstandingAmount)));

        // ใบ 4: only a backorder — not in the history, only the ค้างส่ง set
        var four = (await OrderAsync("08-69-0004"))!;
        Assert.False(four.InHistory);
        Assert.Equal((null, 7m, 70m), (four.Lines.Single().ReceivedQuantity, four.Lines.Single().OutstandingQuantity, four.Lines.Single().OutstandingAmount));

        // the stored amount is kept as stored, and counted
        Assert.Equal(99m, (await OrderAsync("08-69-0006"))!.Lines.Single().ReceivedAmount);
        Assert.Contains(report.Notes, n => n.StartsWith("ORDER: มูลค่าที่เก็บไว้ไม่ตรงจำนวน×ราคา 1 แถว"));
    }

    [Fact]
    public async Task A_number_several_customers_used_keeps_one_order_and_renumbers_the_others()
    {
        var report = await RunAsync(Save(Standard()));

        var first = (await OrderAsync("08-69-0002"))!;
        var second = (await OrderAsync("08-69-0002-2"))!;
        Assert.Equal(("08-01-001", (string?)null), (first.CustomerCode, first.OriginalNo));
        Assert.Equal(("08-01-002", "08-69-0002"), (second.CustomerCode, second.OriginalNo));
        Assert.Contains(report.Exceptions, e => e.Rule == "DQ-2" && e.Table == "ORDER" && e.Problem.Contains("08-69-0002-2") && e.Action == "โหลดโดยเปลี่ยนเลขที่");

        // one number, one customer, two dates = one order (the earliest date)
        var three = (await OrderAsync("08-69-0003"))!;
        Assert.Equal((new DateOnly(2026, 3, 1), 2), (three.OrderDate, three.Lines.Count));
        Assert.Contains(report.Notes, n => n.Contains("แถวมีวันที่หลายวัน"));
    }

    [Fact]
    public async Task A_delivery_is_tied_to_the_order_of_its_number_and_customer()
    {
        await RunAsync(Save(Standard()));

        var deliveries = await AsAsync(s => s.Get<SalesDeliveryDirectory>().SearchAsync(new DeliverySearch()));
        Assert.Equal(5, deliveries.Count);
        var ofFirst = deliveries.Single(d => d.CustomerCode == "08-01-001" && d.OrderNo == "08-69-0002");
        var ofSecond = deliveries.Single(d => d.CustomerCode == "08-01-002");
        Assert.Equal(("08-69-0002-2", 5m, true), (ofSecond.OrderNo, ofSecond.Quantity, ofSecond.Complete));
        Assert.Equal(10m, ofFirst.Quantity);

        var partial = deliveries.Single(d => d.OrderNo == "08-69-0001" && d.LineNo == 1);
        Assert.Equal((60m, 600m, 3m, "0100001", 1, "L1", false, new DateOnly(2026, 3, 20)),
            (partial.Quantity, partial.Amount ?? 0, partial.Boxes ?? 0, partial.InvoiceNo, partial.InvoiceLine ?? 0, partial.Lot, partial.Complete, partial.DeliveryDate ?? default));
        // an order from before 2025 is not in the new data: its delivery keeps its number
        Assert.Contains(deliveries, d => d.OrderNo == "07-67-0100" && d.DeliveryDate == new DateOnly(2025, 2, 1) && !d.Complete);
    }

    [Fact]
    public async Task Reports_the_rows_it_cannot_take_and_balances_every_count()
    {
        var report = await RunAsync(Save(Standard()));

        var history = Table(report, "ORDER");
        var backorder = Table(report, "ODD");
        var delivery = Table(report, "ODDส่งออก");
        Assert.True(report.Tables.All(t => t.Balanced), string.Join("; ", report.Tables.Where(t => !t.Balanced)));
        // ORDER: 11 rows in the file, one before 1 Jan 2025 (not moved), three it cannot take, seven loaded
        Assert.Equal((11, 1, 3, 7), (history.SourceRows, history.Excluded, history.Rejected, history.Loaded));
        Assert.Equal((3, 0, 1, 2), (backorder.SourceRows, backorder.Excluded, backorder.Rejected, backorder.Loaded));
        Assert.Equal((7, 1, 1, 5), (delivery.SourceRows, delivery.Excluded, delivery.Rejected, delivery.Loaded));

        Assert.Contains(report.Exceptions, e => e.Table == "ORDER" && e.Problem.Contains("99-99-999"));
        Assert.Contains(report.Exceptions, e => e.Table == "ORDER" && e.Problem.Contains("ZZ999"));
        Assert.Contains(report.Exceptions, e => e.Table == "ORDER" && e.Rule == "DQ-4" && e.Problem.Contains("ไม่น่าเป็นไปได้"));
        Assert.Contains(report.Exceptions, e => e.Table == "ODD" && e.Problem.Contains("เป็น 0"));
        Assert.Contains(report.Exceptions, e => e.Table == "ODDส่งออก" && e.Problem.Contains("ZZ999"));
        Assert.Null(await OrderAsync("08-69-0005")); // the Buddhist year: no usable date, not moved
        Assert.Null(await OrderAsync("08-69-0009")); // nothing left to deliver and not in the history
    }

    [Fact]
    public async Task The_monthly_totals_match_the_source_and_the_database()
    {
        var report = await RunAsync(Save(Standard()));

        Assert.True(report.MonthlyTotals.All(m => m.Balanced), string.Join("; ", report.MonthlyTotals.Where(m => !m.Balanced)));
        var march = report.MonthlyTotals.Where(m => m.Table == "ODDส่งออก" && m.Month == "2026-03").ToDictionary(m => m.Measure);
        Assert.Equal((4, 125, 1250), ((int)march["จำนวนรายการส่ง"].InDatabase, march["Σ จำนวนส่ง"].InDatabase, march["Σ มูลค่าส่ง"].InDatabase));
        Assert.Equal(6, march["Σ กล่อง"].InDatabase);
        // AC-17: orders and Σ มูลค่าที่รับ per territory and month
        var received = report.MonthlyTotals.Single(m => m.Table == "ORDER" && m.Month == "2026-03" && m.Measure == "จำนวนใบ เขต 08");
        Assert.True(received.Expected > 0 && received.Balanced);
        // AC-19: the ค้างส่ง lines (Feb: 08-69-0004, Mar: 08-69-0001)
        Assert.Equal(1d, report.MonthlyTotals.Single(m => m.Table == "ODD" && m.Month == "2026-02" && m.Measure == "ค้างส่ง จำนวนบรรทัด").InDatabase);
        Assert.Equal(40d, report.MonthlyTotals.Single(m => m.Table == "ODD" && m.Month == "2026-03" && m.Measure == "ค้างส่ง จำนวน").InDatabase);
    }

    [Fact]
    public async Task Runs_again_with_the_same_result_and_writes_no_row_history()
    {
        var source = Save(Standard());
        await RunAsync(source, "first");
        var firstCount = (await AsAsync(s => s.Get<SalesDeliveryDirectory>().SearchAsync(new DeliverySearch()))).Count;

        var report = await RunAsync(source, "second");

        Assert.Equal(firstCount, (await AsAsync(s => s.Get<SalesDeliveryDirectory>().SearchAsync(new DeliverySearch()))).Count);
        Assert.Equal(40m, (await OrderAsync("08-69-0001"))!.Lines[0].OutstandingQuantity);
        Assert.True(report.Tables.All(t => t.Balanced));
        // the load is not in the change history (#39)
        await using (var scope = db.Scope("hr1"))
        {
            var history = scope.Get<HistoryService>();
            Assert.Empty(await history.SearchAsync(new(Table: "ORDER", RowKey: "08-69-0001")));
            Assert.Empty(await history.SearchAsync(new(Table: "การส่งของ", RowKey: "08-69-0001 #1 0100001")));
        }
        Assert.True(File.Exists(Path.Combine(work.FullName, "second", MigrationReport.MonthlyFile)));
    }
}
