using System.Runtime.Versioning;
using Lockte.Migration;
using Lockte.Purch;

namespace Lockte.Web.Tests;

/// <summary>เครื่องมือย้ายข้อมูล: สมุดสต็อกวัตถุดิบ (#267) — the balance of every material as in the old book, the kinds of row, the exceptions.</summary>
[Collection(MigrationDbCollection.Name)]
[SupportedOSPlatform("windows")]
public sealed class PurchStockMigrationTests(MigrationDbFixture db) : IDisposable
{
    private readonly DirectoryInfo work = Directory.CreateTempSubdirectory("lockte-purch-stock-test-");

    public void Dispose() => work.Delete(recursive: true);

    private async Task<T> AsAsync<T>(Func<TestScope, Task<T>> action)
    {
        await using var scope = db.Scope("hr1");
        return await action(scope);
    }

    private static string Unique() => Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();

    [Fact]
    public async Task The_balance_of_every_material_is_the_sum_of_its_rows_and_the_odd_rows_are_reported()
    {
        var tag = Unique();
        string code1 = "ST" + tag, code2 = "SU" + tag;
        var supplier = (await AsAsync(s => s.Get<SupplierRegistry>().SaveAsync(new SupplierInput(null, "SS" + tag, "ผู้ขายสต็อก " + tag, null)))).Id!.Value;
        var m1 = (await AsAsync(s => s.Get<MaterialRegistry>().SaveAsync(new MaterialInput(null, code1, "วัตถุดิบสต็อก 1", null, "กก.", IsMain: true)))).Id!.Value;
        var m2 = (await AsAsync(s => s.Get<MaterialRegistry>().SaveAsync(new MaterialInput(null, code2, "วัตถุดิบสต็อก 2", null, "กก.", IsMain: true)))).Id!.Value;

        var cut = new DateTime(2025, 10, 31);
        var file = FakePurchHistoryFile.Empty()
            .Row("Stock", ("RE_DATE", cut), ("INVOICE_NO", "stock"), ("MTR_CODE", code1), ("RE_QTY", 1000.0))
            .Row("Stock", ("RE_DATE", new DateTime(2026, 9, 9)), ("MARK", "st"), ("MTR_CODE", code1.ToLowerInvariant()), ("RE_QTY", 3356.0), ("EMP_ID", "SS" + tag), ("U_PRICE", 12.5)) // a small-letter code is matched
            .Row("Stock", ("RE_DATE", new DateTime(2026, 9, 20)), ("INVOICE_NO", "002/0098"), ("INV_SUF", "1"), ("MARK", "tr"), ("MTR_CODE", code1), ("RE_QTY", -1138.6), ("EMP_ID", "ญ" + tag)) // an employee code that is nobody's
            .Row("Stock", ("RE_DATE", cut), ("INVOICE_NO", "stock"), ("MTR_CODE", code2), ("RE_QTY", -13250.0))                                                                        // overdrawn at the cut
            .Row("Stock", ("RE_DATE", new DateTime(2027, 11, 22)), ("INVOICE_NO", "87/1"), ("MARK", "tr"), ("MTR_CODE", code2), ("RE_QTY", -5.0))                                         // a date in the future
            .Row("Stock", ("RE_DATE", new DateTime(2026, 9, 21)), ("MTR_CODE", code2), ("RE_QTY", 7.0))                                                                                 // typed by hand: an adjustment
            .Row("Stock", ("RE_DATE", new DateTime(2026, 9, 22)), ("MTR_CODE", "NOPE" + tag), ("RE_QTY", 40.0))                                                                          // a code nobody has
            .Row("Stock", ("MTR_CODE", code2), ("RE_QTY", 3.0))                                                                                                                         // no date
            .Row("ต้นงวดP_ST", ("DATE", new DateTime(2025, 10, 31)), ("REF_NO", "stock"), ("PD_C", code1), ("QTY", 1000.0))
            .Row("ต้นงวดP_ST", ("DATE", new DateTime(2025, 10, 31)), ("REF_NO", "stock"), ("PD_C", code2), ("QTY", -13250.0))
            .Row("ต้นงวดP_ST", ("DATE", new DateTime(2024, 5, 31)), ("REF_NO", "stock"), ("PD_C", code1), ("QTY", 9.0))                 // before 2025: not moved
            .Row("ต้นงวดP_ST", ("DATE", new DateTime(2025, 9, 30)), ("REF_NO", "stock"), ("PD_C", "NOPE" + tag), ("QTY", 1.0));        // a code nobody has
        var source = file.Save(Path.Combine(work.FullName, "วัตถุดิบ.accdb"));
        var map = Path.Combine(work.FullName, "map.csv");
        File.WriteAllLines(map, ["kind,legacy,code,name,prefix"]);
        try
        {
            var report = await PurchHistoryTool.RunAsync(db.Services, source, map, Path.Combine(work.FullName, "out"));

            var stock = report.Tables.Single(t => t.LegacyTable == "Stock");
            Assert.Equal((8, 0, 2, 6, 6), (stock.SourceRows, stock.Excluded, stock.Rejected, stock.Loaded, stock.InDatabase));
            var cuts = report.Tables.Single(t => t.LegacyTable == "ต้นงวดP_ST");
            Assert.Equal((4, 1, 1, 2, 2), (cuts.SourceRows, cuts.Excluded, cuts.Rejected, cuts.Loaded, cuts.InDatabase)); // 2024 is not moved (excluded), the unknown code is a DQ row
            Assert.True(report.Balanced, string.Join("; ", report.MonthlyTotals.Where(m => !m.Balanced).Select(m => m.Table + " " + m.Month + " " + m.Measure)));

            // AC-18: balance = Σ of the rows; the overdrawn one is not made 0
            Assert.Equal(1000m + 3356m - 1138.6m, await AsAsync(s => s.Get<StockDirectory>().GetBalanceAsync(m1)));
            Assert.Equal(-13250m - 5m + 7m, await AsAsync(s => s.Get<StockDirectory>().GetBalanceAsync(m2)));

            var entries = await AsAsync(s => s.Get<StockDirectory>().GetEntriesAsync(m1));
            Assert.Equal([StockEntryKind.OpeningBalance, StockEntryKind.Receipt, StockEntryKind.Issue], entries.Select(e => e.Kind));
            Assert.Equal(supplier, entries[1].SupplierId);
            Assert.Equal(12.5m, entries[1].UnitPrice);
            Assert.Null(entries[2].EmployeeId);
            Assert.Contains("ญ" + tag, entries[2].Note);
            Assert.Equal(StockEntryKind.Adjustment, (await AsAsync(s => s.Get<StockDirectory>().GetEntriesAsync(m2))).Single(e => e.Quantity == 7m).Kind);

            Assert.Contains(report.Exceptions, e => e.Rule == "DQ-08" && e.Key == code2);       // the negative balance, by code
            Assert.Contains(report.Exceptions, e => e.Rule == "DQ-07" && e.Key == "NOPE" + tag);
            Assert.Contains(report.Exceptions, e => e.Rule == "DQ-09" && e.Problem.Contains("2027-11-22"));
            Assert.Contains(report.Exceptions, e => e.Rule == "DQ-10");
            Assert.Contains(new DateOnly(2025, 10, 31), await AsAsync(s => s.Get<StockClosingDirectory>().GetCutDatesAsync())); // RPT-26 of that day can be printed again
        }
        finally
        {
            await AsAsync(async s => { await s.Get<PurchHistoryImport>().DeleteStockAsync(); return 0; });
        }
    }
}
