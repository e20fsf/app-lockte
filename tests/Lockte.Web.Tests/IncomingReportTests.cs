using Lockte.Org;
using Lockte.Purch;
using Lockte.Web.Reports;
using Lockte.Web.Reports.Purch;

namespace Lockte.Web.Tests;

/// <summary>RPT-05 / RPT-06 (#257) without a database.</summary>
public sealed class IncomingViewTests
{
    private static IncomingRow Row(string supplier, DateOnly date, string lot, bool failed, bool? certificate, bool onTime)
        => new(date, "690988", "R0019", "อลูมิเนียมก้อน", 400m, "กก.", 1, supplier, date.AddDays(onTime ? 1 : -1), lot, failed, certificate, onTime, null);

    private static async Task<string> RenderAsync<T>(IncomingReport report) where T : Microsoft.AspNetCore.Components.IComponent
        => System.Net.WebUtility.HtmlDecode(await ReportBody.RenderAsync<T>(new() { ["Report"] = report }));

    [Fact]
    public void The_totals_count_certificates_delivery_and_results()
    {
        var rows = new[]
        {
            Row("ก", new DateOnly(2026, 9, 11), "L1", false, true, true), Row("ก", new DateOnly(2026, 9, 11), "L2", true, false, false), Row("ข", new DateOnly(2026, 9, 11), "L3", false, null, true),
        };
        Assert.Equal(new IncomingTotals(3, 1, 1, 2, 1, 2, 1), IncomingTotals.Of(rows));
    }

    [Fact]
    public async Task The_record_lists_the_rows_with_results_and_totals()
    {
        var report = new IncomingReport(new DateOnly(2026, 9, 11), new DateOnly(2026, 9, 11), null, [Row("ผู้ขาย ก", new DateOnly(2026, 9, 11), "L1", false, true, true)]);
        var html = await RenderAsync<IncomingRecordView>(report);
        Assert.Contains("อลูมิเนียมก้อน", html);
        Assert.Contains("400.00 กก.", html);
        Assert.Contains("รวมทั้งหมด 1 รายการ", html);
        Assert.Contains("ชื่อผู้ขาย: ทั้งหมด", html);
    }

    [Fact]
    public async Task The_inspection_shows_the_lot_and_is_by_supplier()
    {
        var report = new IncomingReport(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), "ผู้ขาย ข",
            [Row("ผู้ขาย ข", new DateOnly(2026, 9, 12), "LOT-77", true, false, false), Row("ผู้ขาย ก", new DateOnly(2026, 9, 11), "LOT-11", false, true, true)]);
        var html = await RenderAsync<IncomingInspectionView>(report);
        Assert.Contains("LOT-77", html);
        Assert.True(html.IndexOf("LOT-11", StringComparison.Ordinal) < html.IndexOf("LOT-77", StringComparison.Ordinal)); // ก before ข
        Assert.Contains("ไม่ผ่าน", html);
        Assert.Contains("รวมทั้งหมด 2 รายการ", html);
    }

    [Fact]
    public async Task No_row_prints_no_data()
        => Assert.Contains("ไม่มีข้อมูล", await RenderAsync<IncomingRecordView>(new IncomingReport(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), null, [])));
}

/// <summary>RPT-05 / RPT-06 (#257): which receipts are listed (AC-39).</summary>
[Collection(LocalDbCollection.Name)]
public sealed class IncomingDataTests(LocalDbFixture db)
{
    private static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..6];

    private async Task<T> AsAsync<T>(Func<TestScope, Task<T>> action)
    {
        await using var scope = db.Scope("qa1");
        return await action(scope);
    }

    private async Task<(int Supplier, int Receipt)> ReceiveAsync(DateOnly date, string? lot, bool main = true, int? supplier = null)
    {
        supplier ??= (await AsAsync(s => s.Get<SupplierRegistry>().SaveAsync(new SupplierInput(null, Unique("ic").ToUpperInvariant(), "ผู้ขายรับ", null)))).Id!.Value;
        var material = (await AsAsync(s => s.Get<MaterialRegistry>().SaveAsync(new MaterialInput(null, Unique("im").ToUpperInvariant(), "วัตถุดิบรับ", null, "กก.", IsMain: main)))).Id!.Value;
        int department;
        await using (var scope = db.Scope("hr1"))
        {
            var org = scope.Get<OrgRegistry>();
            var section = (await org.SaveSectionAsync(new SectionInput(null, Unique("ฝ่าย"), 1, null))).Id!.Value;
            department = (await org.SaveDepartmentAsync(new DepartmentInput(null, Unique("D"), Unique("แผนก"), "d", 3, section, null, [], null))).Id!.Value;
        }
        var request = await AsAsync(s => s.Get<PurchaseRequestRegistry>().SaveAsync(new PurchaseRequestInput(Unique("I") + "69-0001", date, department, date.AddDays(1), "F", null, null, [new PurchaseRequestLineInput(material, 1)])));
        var requestLine = (await AsAsync(s => s.Get<PurchaseRequestDirectory>().GetAsync(request.Id!.Value)))!.Lines.Single();
        var order = await AsAsync(s => s.Get<PurchaseOrderRegistry>().IssueAsync(new PurchaseOrderInput("69" + Random.Shared.Next(1000, 9999) + Guid.NewGuid().ToString("N")[..3], date, supplier.Value, "ผู้ขายรับ", null, null, null, null, 30, true, "F", null, null,
            [new PurchaseOrderLineInput(requestLine.Id, 1m, 1m)])));
        var line = (await AsAsync(s => s.Get<PurchaseOrderDirectory>().GetAsync(order.Id!.Value)))!.Lines.Single();
        await AsAsync(s => s.Get<GoodsReceiptRegistry>().ReceiveAsync([new GoodsReceiptInput(line.Id, date, 1m)]));
        var receipt = (await AsAsync(s => s.Get<GoodsReceiptDirectory>().GetReceiptsAsync(line.Id))).Single();
        if (lot is not null) await AsAsync(s => s.Get<GoodsReceiptRegistry>().SaveQaAsync(new GoodsReceiptQaUpdate(receipt.Id, receipt.RowVersion, lot, false, true, null)));
        return (supplier.Value, receipt.Id);
    }

    [Fact] // BR-050 ⚠, AC-39: a receipt with no LOT is never listed
    public async Task Only_receipts_with_a_lot_in_the_period_are_listed_and_a_supplier_can_be_picked()
    {
        var day = new DateOnly(2027, 3, Random.Shared.Next(1, 28));
        var with = await ReceiveAsync(day, "LOT-A");
        var without = await ReceiveAsync(day, null);
        var elsewhere = await ReceiveAsync(day.AddMonths(2), "LOT-B");
        var notMain = await ReceiveAsync(day, "LOT-C", main: false);

        var all = await AsAsync(s => s.Get<GoodsReceiptDirectory>().GetIncomingAsync(day, day));
        Assert.Contains(all, r => r.SupplierId == with.Supplier && r.Lot == "LOT-A");
        Assert.DoesNotContain(all, r => r.SupplierId == without.Supplier);
        Assert.DoesNotContain(all, r => r.SupplierId == elsewhere.Supplier);
        Assert.Contains(all, r => r.SupplierId == notMain.Supplier); // all materials unless asked for only the main ones
        var mine = await AsAsync(s => s.Get<GoodsReceiptDirectory>().GetIncomingAsync(day, day, with.Supplier));
        Assert.Equal([with.Supplier], mine.Select(r => r.SupplierId).Distinct());
        Assert.DoesNotContain(await AsAsync(s => s.Get<GoodsReceiptDirectory>().GetIncomingAsync(day, day, mainOnly: true)), r => r.SupplierId == notMain.Supplier);
    }
}
