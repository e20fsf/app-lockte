using System.Runtime.Versioning;
using Lockte.Migration;
using Lockte.Purch;

namespace Lockte.Web.Tests;

/// <summary>The history tables of a made-up <c>วัตถุดิบ.accdb</c> (#268…) with the legacy column types. Every name and number in it is fictional — the repo is public.</summary>
[SupportedOSPlatform("windows")]
public static class FakePurchHistoryFile
{
    public static Dictionary<string, (string Column, string Type)[]> Schema { get; } = new()
    {
        ["ประเมินผู้ขาย"] =
        [
            ("EXPR1", "LONG"), ("EXPR2", "LONG"), ("EXPR3", "LONG"), ("EXPR4", "LONG"), ("EXPR5", "LONG"), ("EXPR6", "LONG"),
            ("FROM_DATE", "DATETIME"), ("TO_DATE", "DATETIME"), ("SUP", "TEXT(255)"), ("POINT1", "LONG"), ("POINT2", "LONG"), ("TERM", "TEXT(50)"),
        ],
        ["Stock"] =
        [
            ("RE_DATE", "DATETIME"), ("INVOICE_NO", "TEXT(255)"), ("INV_SUF", "TEXT(255)"), ("MTR_CODE", "TEXT(255)"), ("RE_QTY", "DOUBLE"), ("EMP_ID", "TEXT(255)"),
            ("MARK", "TEXT(255)"), ("U_PRICE", "DOUBLE"),
        ],
        ["ต้นงวดP_ST"] =
        [
            ("DATE", "DATETIME"), ("REF_NO", "TEXT(10)"), ("SUF_NO", "TEXT(2)"), ("PD_C", "TEXT(6)"), ("QTY", "DOUBLE"), ("ID", "TEXT(50)"), ("MARK", "TEXT(2)"), ("U_PRICE", "DOUBLE"),
        ],
    };

    public static FakeLegacyFile Empty() => new(Schema);
}

/// <summary>เครื่องมือย้ายข้อมูล: ผลประเมินผู้ขาย (#268) — the names matched to the suppliers' codes, the exceptions, the totals.</summary>
[Collection(MigrationDbCollection.Name)]
[SupportedOSPlatform("windows")]
public sealed class PurchHistoryMigrationTests(MigrationDbFixture db) : IDisposable
{
    private readonly DirectoryInfo work = Directory.CreateTempSubdirectory("lockte-purch-history-test-");

    public void Dispose() => work.Delete(recursive: true);

    private async Task<T> AsAsync<T>(Func<TestScope, Task<T>> action)
    {
        await using var scope = db.Scope("hr1");
        return await action(scope);
    }

    private static string Unique() => Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    private async Task<int> SupplierAsync(string code, string name) => (await AsAsync(s => s.Get<SupplierRegistry>().SaveAsync(new SupplierInput(null, code, name, null)))).Id!.Value;

    [Fact]
    public async Task Matches_the_names_to_the_codes_and_reports_the_rest()
    {
        var tag = Unique();
        var one = await SupplierAsync("H1" + tag, "ผู้ขายประเมิน " + tag);
        var renamed = await SupplierAsync("H2" + tag, "ผู้ขายชื่อใหม่ " + tag);
        await SupplierAsync("H3" + tag, "ชื่อซ้ำ " + tag);
        await SupplierAsync("H4" + tag, "ชื่อซ้ำ " + tag);
        var from = new DateTime(2026, 4, 1);
        var to = new DateTime(2026, 6, 30);
        var file = FakePurchHistoryFile.Empty()
            .Row("ประเมินผู้ขาย", ("EXPR1", 5), ("EXPR2", 1), ("EXPR3", 4), ("EXPR4", 2), ("EXPR5", 5), ("EXPR6", 1), ("FROM_DATE", from), ("TO_DATE", to), ("SUP", "ผู้ขายประเมิน " + tag), ("POINT1", 4), ("POINT2", 5), ("TERM", "2"))
            .Row("ประเมินผู้ขาย", ("EXPR1", 9), ("EXPR2", 0), ("EXPR3", 9), ("EXPR4", 0), ("EXPR5", 9), ("EXPR6", 0), ("FROM_DATE", from), ("TO_DATE", to), ("SUP", "ชื่อเก่าของผู้ขาย " + tag), ("POINT1", 3), ("POINT2", 3), ("TERM", "2")) // decided in the map
            .Row("ประเมินผู้ขาย", ("EXPR1", 1), ("FROM_DATE", from), ("TO_DATE", to), ("SUP", "ชื่อซ้ำ " + tag), ("TERM", "2"))      // two suppliers have the name
            .Row("ประเมินผู้ขาย", ("EXPR1", 1), ("FROM_DATE", from), ("TO_DATE", to), ("SUP", "ไม่มีผู้ขายชื่อนี้ " + tag), ("TERM", "2")) // nobody has it
            .Row("ประเมินผู้ขาย", ("EXPR1", 99), ("FROM_DATE", from), ("TO_DATE", to), ("SUP", "*"), ("TERM", "2"))                      // all the suppliers added up
            .Row("ประเมินผู้ขาย", ("EXPR1", 2), ("FROM_DATE", from), ("TO_DATE", to), ("SUP", "ผู้ขายประเมิน " + tag), ("TERM", "2"))   // the same supplier and period again
            .Row("ประเมินผู้ขาย", ("EXPR1", 3), ("FROM_DATE", new DateTime(2026, 1, 1)), ("TO_DATE", new DateTime(2026, 3, 31)), ("SUP", "ผู้ขายประเมิน " + tag), ("TERM", "Y")); // TERM is not a quarter
        var source = file.Save(Path.Combine(work.FullName, "วัตถุดิบ.accdb"));
        var map = Path.Combine(work.FullName, "map.csv");
        File.WriteAllLines(map, ["kind,legacy,code,name,prefix", $"supplier-name,ชื่อเก่าของผู้ขาย {tag},H2{tag},,"]);
        try
        {
            var report = await PurchHistoryTool.RunAsync(db.Services, source, map, Path.Combine(work.FullName, "out"));

            var table = report.Tables.Single(t => t.LegacyTable == "ประเมินผู้ขาย");
            Assert.Equal((7, 1, 3, 3), (table.SourceRows, table.Excluded, table.Rejected, table.Loaded));
            Assert.True(report.Balanced, string.Join("; ", report.MonthlyTotals.Where(m => !m.Balanced).Select(m => m.Measure)));
            Assert.Equal(["DQ-16", "DQ-16", "DQ-17"], report.Exceptions.Where(e => e.Rule is "DQ-16" or "DQ-17").Select(e => e.Rule).Order().ToArray());
            Assert.DoesNotContain(report.Exceptions, e => e.Problem.Contains(tag) && e.Rule == "DQ-16" && e.Problem.Contains("ผู้ขายประเมิน")); // the report tells the codes, not the names

            var evaluations = await AsAsync(s => s.Get<SupplierEvaluationDirectory>().ListAsync());
            var mine = evaluations.Where(e => e.SupplierId == one || e.SupplierId == renamed).OrderBy(e => e.FromDate).ThenBy(e => e.SupplierCode).ToList();
            Assert.Equal(3, mine.Count);
            var first = mine.First(e => e.SupplierId == one && e.Quarter == 2);
            Assert.Equal((1, 4, 5), (first.Counts.Failed, first.Point1, first.Point2));
            Assert.Equal(9, mine.Single(e => e.SupplierId == renamed).Counts.Passed);
            Assert.Equal(1, mine.Single(e => e.FromDate == new DateOnly(2026, 1, 1)).Quarter); // TERM "Y" → the quarter of the start
        }
        finally
        {
            // the master-data tests of this database refuse a reload while evaluations exist
            await AsAsync(async s => { await s.Get<PurchHistoryImport>().DeleteEvaluationsAsync(); await s.Get<PurchHistoryImport>().DeleteStockAsync(); return 0; });
        }
    }
}

/// <summary>The part of the purch-history program that needs no database.</summary>
public sealed class PurchHistoryMigrationPlainTests
{
    [Fact]
    public void The_quarter_of_a_period_start()
    {
        Assert.Equal([1, 1, 2, 3, 4], new[] { new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 31), new DateOnly(2026, 4, 1), new DateOnly(2026, 7, 15), new DateOnly(2026, 12, 31) }.Select(PurchHistoryMigration.QuarterOf));
    }
}
