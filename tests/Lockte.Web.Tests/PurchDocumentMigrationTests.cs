using System.Runtime.Versioning;
using Lockte.Migration;
using Lockte.Org;
using Lockte.Purch;

namespace Lockte.Web.Tests;

/// <summary>เครื่องมือย้ายข้อมูล: ใบขอซื้อ ใบสั่งซื้อ การรับของ (#266) — the flat rows taken apart, the ordered quantity worked out, the exceptions.</summary>
[Collection(MigrationDbCollection.Name)]
[SupportedOSPlatform("windows")]
public sealed class PurchDocumentMigrationTests(MigrationDbFixture db) : IDisposable
{
    private readonly DirectoryInfo work = Directory.CreateTempSubdirectory("lockte-purch-documents-test-");

    public void Dispose() => work.Delete(recursive: true);

    private async Task<T> AsAsync<T>(Func<TestScope, Task<T>> action)
    {
        await using var scope = db.Scope("hr1");
        return await action(scope);
    }

    private static string Unique() => Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();

    [Fact]
    public async Task Takes_the_flat_rows_apart_and_works_out_what_was_ordered()
    {
        var tag = Unique();
        var departmentName = "แผนกเอกสาร " + tag;
        await AsAsync(async s =>
        {
            var org = s.Get<OrgRegistry>();
            var section = (await org.SaveSectionAsync(new SectionInput(null, "ฝ่ายเอกสาร" + tag, 1, null))).Id!.Value;
            return (await org.SaveDepartmentAsync(new DepartmentInput(null, "DC" + tag, departmentName, "d", 3, section, null, [], null))).Id!.Value;
        });
        string s1 = "DS1" + tag, s2 = "DS2" + tag, m1 = "DM1" + tag, m2 = "DM2" + tag;
        var supplier1 = (await AsAsync(s => s.Get<SupplierRegistry>().SaveAsync(new SupplierInput(null, s1, "ผู้ขายเอกสาร หนึ่ง", null, CreditDays: 30, Vat: true)))).Id!.Value;
        var supplier2 = (await AsAsync(s => s.Get<SupplierRegistry>().SaveAsync(new SupplierInput(null, s2, "ผู้ขายเอกสาร สอง", null, Vat: true)))).Id!.Value;
        var material1 = (await AsAsync(s => s.Get<MaterialRegistry>().SaveAsync(new MaterialInput(null, m1, "วัตถุดิบเอกสาร 1", null, "กก.", IsMain: true)))).Id!.Value;
        var material2 = (await AsAsync(s => s.Get<MaterialRegistry>().SaveAsync(new MaterialInput(null, m2, "วัตถุดิบเอกสาร 2", null, "ชิ้น")))).Id!.Value;

        var d = new DateTime(2026, 5, 4);
        (string, object?)[] Head(string pr, string po, float suf, string supplier, string material, double prQty, double poQty, string vat = "YES", string? dept = null) =>
        [
            ("PR_NO", pr), ("PR_SUF", 1f), ("PR_DATE", d), ("PR_QTY", prQty), ("MTR_CODE", material), ("MTR_NAME", "ชื่อในเอกสาร " + material), ("MTR_UNIT", "กก."), ("DELI_D", d.AddDays(10)),
            ("EMP_ID", dept ?? departmentName), ("PUR", "F"), ("REMARK", "เหตุผลสมมุติ"), ("PO_NO", po), ("PO_SUF", suf), ("PO_DATE", d.AddDays(1)), ("PO_QTY", poQty),
            ("SUP_CODE", supplier), ("SUP_NAME", "ผู้ขายในเอกสาร"), ("CREDIT_D", "30"), ("VAT", vat), ("U_PRICE", 10.0), ("TAX", 0.0),
        ];
        var file = FakePurchHistoryFile.Empty();
        // A: ordered 200, 50 received, 150 still waiting
        file.Row("PO_RECEI4", [.. Head("RA" + tag, "PA" + tag, 1, s1, m1, 200, 150), ("PASS", null)]);
        file.Row("PO_RECEI5", [.. Head("RA" + tag, "PA" + tag, 1, s1, m1, 200, 200), ("RE_DATE", d.AddDays(5)), ("RE_QTY", 50.0), ("NET_AMT", 500.0), ("SEND", "ทัน")]);
        // B: received in two deliveries, nothing waiting: ordered = what the first one found outstanding; one on time by the dates, the old text says the other way (DQ-11)
        file.Row("PO_RECEI5", [.. Head("RB" + tag, "PB" + tag, 1, s1, m1, 100, 100), ("RE_DATE", d.AddDays(3)), ("RE_QTY", 60.0), ("NET_AMT", 600.0), ("LOT", "L1"), ("PASS", "ผ่าน"), ("NOPASS", "มี"), ("SEND", "ไม่ทัน")]);
        file.Row("PO_RECEI5", [.. Head("RB" + tag, "PB" + tag, 1, s1, m1, 100, 40), ("RE_DATE", d.AddDays(20)), ("RE_QTY", 40.0), ("NET_AMT", 400.0), ("SEND", "ไม่ทัน")]);
        // C: the whole quantity came and failed: still waiting for all 100
        file.Row("PO_RECEI4", [.. Head("RC" + tag, "PC" + tag, 1, s1, m1, 100, 100)]);
        file.Row("PO_RECEI5", [.. Head("RC" + tag, "PC" + tag, 1, s1, m1, 100, 100), ("RE_DATE", d.AddDays(4)), ("RE_QTY", 100.0), ("NET_AMT", 1000.0), ("PASS", "ไม่ผ่าน")]);
        // D: one order number, two suppliers: two orders
        file.Row("PO_RECEI4", [.. Head("RD" + tag, "PD" + tag, 1, s1, m1, 10, 10)]);
        file.Row("PO_RECEI4", [.. Head("RD" + tag, "PD" + tag, 2, s2, m2, 20, 20)]);
        // exceptions: a leftover row, a material nobody has, a department nobody has
        file.Row("PO_RECEI5", ("PO_NO", "PX" + tag), ("PO_SUF", 1f));
        file.Row("PO_RECEI4", [.. Head("RE" + tag, "PE" + tag, 1, s1, "NOPE" + tag, 5, 5)]);
        file.Row("PO_RECEI4", [.. Head("RF" + tag, "PF" + tag, 1, s1, m1, 5, 5, dept: "แผนกที่ไม่มีในทะเบียน")]);
        var source = file.Save(Path.Combine(work.FullName, "วัตถุดิบ.accdb"));
        var map = Path.Combine(work.FullName, "map.csv");
        File.WriteAllLines(map, ["kind,legacy,code,name,prefix"]);
        try
        {
            var report = await PurchHistoryTool.RunAsync(db.Services, source, map, Path.Combine(work.FullName, "out"));

            var waiting = report.Tables.Single(t => t.LegacyTable == "PO_RECEI4");
            var history = report.Tables.Single(t => t.LegacyTable == "PO_RECEI5");
            Assert.Equal((6, 2, 4, 4), (waiting.SourceRows, waiting.Rejected, waiting.Loaded, waiting.InDatabase)); // A, C, D1, D2 are waiting lines; E and F cannot be loaded (B has no waiting row)
            Assert.Equal((5, 1, 4, 4), (history.SourceRows, history.Rejected, history.Loaded, history.InDatabase));        // the leftover row is not loaded
            Assert.True(report.Balanced, string.Join("; ", report.Tables.Where(t => !t.Balanced).Select(t => $"{t.LegacyTable} {t.SourceRows}/{t.Excluded}/{t.Rejected}/{t.Loaded}/{t.InDatabase}")
                .Concat(report.MonthlyTotals.Where(m => !m.Balanced).Select(m => m.Measure))));

            async Task<PurchaseOrderDto> OrderAsync(string no) => (await AsAsync(s => s.Get<PurchaseOrderDirectory>().FindByNoAsync(no)))!;
            var a = await OrderAsync("PA" + tag);
            Assert.Equal(200m, a.Lines.Single().Quantity); // 150 still waiting + 50 received
            Assert.Equal([50m], (await AsAsync(s => s.Get<GoodsReceiptDirectory>().GetOrderReceiptsAsync(a.Id))).Select(r => r.Quantity));
            var b = await OrderAsync("PB" + tag);
            Assert.Equal(100m, b.Lines.Single().Quantity);   // the first delivery found 100 outstanding
            var bReceipts = (await AsAsync(s => s.Get<GoodsReceiptDirectory>().GetOrderReceiptsAsync(b.Id))).OrderBy(r => r.ReceiveDate).ToList();
            Assert.Equal([60m, 40m], bReceipts.Select(r => r.Quantity));
            Assert.Equal(("L1", true), (bReceipts[0].Lot, bReceipts[0].HasCertificate));
            var c = await OrderAsync("PC" + tag);
            Assert.Equal(100m, c.Lines.Single().Quantity);   // the failed delivery did not reduce what is waiting
            Assert.True((await AsAsync(s => s.Get<GoodsReceiptDirectory>().GetOrderReceiptsAsync(c.Id))).Single().Failed);
            Assert.Single((await OrderAsync("PD" + tag)).Lines);
            Assert.Single((await OrderAsync("PD" + tag + "-2")).Lines);

            Assert.Contains(report.Exceptions, e => e.Rule == "DQ-05" && e.Key == "PX" + tag);
            Assert.Contains(report.Exceptions, e => e.Rule == "DQ-19" && e.Key == "PD" + tag);
            Assert.Contains(report.Exceptions, e => e.Rule == "DQ-20" && e.Key == "PE" + tag);
            Assert.Contains(report.Exceptions, e => e.Rule == "DQ-5" && e.Key == "PF" + tag);
            Assert.Contains(report.Exceptions, e => e.Rule == "DQ-11" && e.Key == "PB" + tag);
            Assert.DoesNotContain(report.Exceptions, e => e.Problem.Contains("ผู้ขายเอกสาร"));
            Assert.Equal(supplier2, (await OrderAsync("PD" + tag + "-2")).SupplierId);
        }
        finally
        {
            // the master-data tests of this database refuse a reload while documents exist
            await AsAsync(async s => { await s.Get<PurchHistoryImport>().DeleteDocumentsAsync(); return 0; });
        }
    }
}
