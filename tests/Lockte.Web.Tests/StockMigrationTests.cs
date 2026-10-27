using System.Runtime.Versioning;
using Lockte.Migration;
using Lockte.Prod;
using Lockte.Prod.Data;
using Lockte.Web.Audit;

namespace Lockte.Web.Tests;

/// <summary>
/// เครื่องมือย้ายข้อมูล: สมุดสต็อก ยอดยกมา และการใช้งานแม่พิมพ์ (#126) from a made-up <c>ผลผลิตและORDER.accdb</c> — every name, number and amount in it is fictional (the repo is public).
/// The master data (products, molds) is loaded by the same run, as in the real one.
/// </summary>
[Collection(MigrationDbCollection.Name)]
[SupportedOSPlatform("windows")]
public sealed class StockMigrationTests(MigrationDbFixture db) : IDisposable
{
    private readonly DirectoryInfo work = Directory.CreateTempSubdirectory("lockte-stock-migration-test-");

    public void Dispose() => work.Delete(recursive: true);

    private async Task<T> AsAsync<T>(Func<TestScope, Task<T>> action)
    {
        await using var scope = db.Scope("hr1");
        return await action(scope);
    }

    private Task<MigrationReport> RunAsync(string source, string output = "out", bool removeDuplicates = false)
        => OrderMigrationTool.RunAsync(db.Services, source, null, Path.Combine(work.FullName, output), removeDuplicates);

    private string Save(FakeLegacyFile file) => file.Save(Path.Combine(work.FullName, "ผลผลิตและORDER.accdb"));

    private static readonly DateTime June = new(2025, 6, 30);
    private static readonly DateTime December = new(2025, 12, 31);

    private static (string, object?)[] Entry(DateTime? date, string? no, string? line, string? product, double? quantity, string? mark = null, string? remark = null, string? employee = null)
        => [("DATE", date), ("REF_NO", no), ("SUF_NO", line), ("PD_C", product), ("QTY", quantity), ("ID", employee), ("MARK", mark), ("REM", remark)];

    private static FakeLegacyFile Standard()
    {
        var file = FakeOrderFile.Empty()
            .Row("P_NAME", ("PD_C", "G1001"), ("PD_N", "สินค้าหนึ่ง"), ("U_NAME", "ชิ้น"))
            .Row("P_NAME", ("PD_C", "G1002"), ("PD_N", "สินค้าสอง"), ("U_NAME", "ชิ้น"))
            .Row("รหัสพิมพ์", ("ID", "BF-001-1/1"), ("NAME", "แม่พิมพ์สมมุติ"), ("QTYS", 2));

        // ต้นงวดP_ST: the opening balances of two cuts (and a cancelled row)
        file.Row("ต้นงวดP_ST", Entry(June, "stock", null, "G1001", 100));
        file.Row("ต้นงวดP_ST", Entry(June, "stock", null, "G1002", 50));
        file.Row("ต้นงวดP_ST", Entry(December, "stock", null, "G1001", 120));
        file.Row("ต้นงวดP_ST", Entry(December, "stock", null, "G1002", 60));
        file.Row("ต้นงวดP_ST", Entry(June, "DE", null, "G1001", 9, mark: "de"));

        // P_ADDST: what the cuts closed (the opening of June is in ต้นงวดP_ST already; a cancelled row; a product not in the register; a row before 2025 is not read)
        file.Row("P_ADDST", Entry(new DateTime(2025, 3, 10), "D69001", "1", "G1001", 100, "pd"));
        file.Row("P_ADDST", Entry(June, "stock", null, "G1001", 100));
        file.Row("P_ADDST", Entry(new DateTime(2025, 9, 1), "T69001", "1", "G1001", -30, "tr"));
        file.Row("P_ADDST", Entry(new DateTime(2025, 9, 2), "D69002", "1", "G1001", 50, "pd"));
        file.Row("P_ADDST", Entry(new DateTime(2025, 9, 3), "T69002", "1", "G1001", -1, "de"));
        file.Row("P_ADDST", Entry(new DateTime(2025, 9, 4), "D69003", "1", "ZZ999", 7, "pd"));
        file.Row("P_ADDST", Entry(new DateTime(2025, 12, 10), "D69010", "1", "G1002", 25, "pd"));
        file.Row("P_ADDST", Entry(new DateTime(2024, 12, 31), "stock", null, "G1001", 999));

        // P_ST: the opening of the last cut, then the period now open; the production of 10 Dec is also in P_ADDST (Q-47)
        file.Row("P_ST", Entry(December, "stock", null, "G1001", 120));
        file.Row("P_ST", Entry(December, "stock", null, "G1002", 60));
        file.Row("P_ST", Entry(new DateTime(2025, 12, 10), "D69010", "1", "G1002", 25, "pd"));
        file.Row("P_ST", Entry(new DateTime(2026, 1, 5), "D69011", "1", "G1001", 10, "pd"));
        file.Row("P_ST", Entry(new DateTime(2026, 1, 6), "T69004", "1", "G1001", -4, "tr"));
        file.Row("P_ST", Entry(new DateTime(2026, 1, 7), "นับสต็อก", null, "G1002", 3, remark: "นับเพิ่ม"));
        file.Row("P_ST", Entry(new DateTime(2026, 1, 8), "ab", null, "G1002", 1, "ab"));
        file.Row("P_ST", Entry(new DateTime(2026, 1, 9), "X1", "1", ".G7045", 5, "pd"));
        return file;
    }

    private static TableCount Table(MigrationReport report, string legacyTable) => report.Tables.Single(t => t.LegacyTable == legacyTable);

    [Fact]
    public async Task Moves_the_ledger_closed_and_open_and_balances_every_count()
    {
        var report = await RunAsync(Save(Standard()));

        Assert.True(report.Tables.All(t => t.Balanced), string.Join("; ", report.Tables.Where(t => !t.Balanced)));
        var current = Table(report, "P_ST");
        Assert.Equal((8, 0, 1, 7, 7), (current.SourceRows, current.Excluded, current.Rejected, current.Loaded, current.InDatabase)); // the product '.G7045' is not in the register
        var closed = Table(report, "P_ADDST");
        Assert.Equal((8, 2, 2, 4, 4), (closed.SourceRows, closed.Excluded, closed.Rejected, closed.Loaded, closed.InDatabase)); // 2024 not read, the opening not twice; de and ZZ999 not moved
        var openings = Table(report, "ต้นงวดP_ST");
        Assert.Equal((5, 2, 1, 2, 2), (openings.SourceRows, openings.Excluded, openings.Rejected, openings.Loaded, openings.InDatabase)); // the cut of Dec is the opening in P_ST
        Assert.True(report.MonthlyTotals.All(m => m.Balanced), string.Join("; ", report.MonthlyTotals.Where(m => !m.Balanced)));

        var balances = (await AsAsync(s => s.Get<StockDirectory>().GetBalancesAsync())).ToDictionary(b => b.ProductCode, b => b.Quantity);
        Assert.Equal(126m, balances["G1001"]); // 120 + 10 − 4
        Assert.Equal(89m, balances["G1002"]);  // 60 + 25 (counted twice, Q-47: not removed) + 3 + 1
    }

    [Fact] // closed entries carry the cut that closed them; the opening of the earlier cut is closed by the next one, the latest is open
    public async Task Closed_entries_are_marked_with_the_cut_that_closed_them()
    {
        await RunAsync(Save(Standard()));

        var productId = (await AsAsync(s => s.Get<StockDirectory>().GetBalancesAsync())).Single(b => b.ProductCode == "G1001").ProductId;
        var all = await AsAsync(s => s.Get<StockDirectory>().GetEntriesAsync(productId, includeClosed: true));

        var production = all.Single(e => e.DocumentNo == "D69001");
        Assert.Equal((StockEntryKind.Production, new DateOnly(2025, 6, 30)), (production.Kind, production.ClosedOn));
        Assert.Equal(new DateOnly(2025, 12, 31), all.Single(e => e.DocumentNo == "T69001").ClosedOn);
        Assert.Equal(StockEntryKind.Withdrawal, all.Single(e => e.DocumentNo == "T69001").Kind);
        var openings = all.Where(e => e.Kind == StockEntryKind.OpeningBalance).OrderBy(e => e.Date).ToList();
        Assert.Equal([new DateOnly(2025, 6, 30), new DateOnly(2025, 12, 31)], openings.Select(e => e.Date!.Value));
        Assert.Equal([new DateOnly(2025, 12, 31), (DateOnly?)null], openings.Select(e => e.ClosedOn));
        Assert.Equal([100m, 120m], openings.Select(e => e.Quantity));
        Assert.DoesNotContain(all, e => e.Date == new DateOnly(2024, 12, 31)); // before 2025: not moved
    }

    [Fact] // the history of the cuts is what RPT-14 reads
    public async Task The_opening_balance_of_each_cut_is_kept_as_history()
    {
        await RunAsync(Save(Standard()));

        var dates = await AsAsync(s => s.Get<StockClosingDirectory>().GetCutDatesAsync());
        var (cutDates, rows) = await AsAsync(s => s.Get<StockClosingDirectory>().GetOpeningHistoryAsync(new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31)));

        Assert.Equal([new DateOnly(2025, 12, 31), new DateOnly(2025, 6, 30)], dates);
        Assert.Equal([new DateOnly(2025, 6, 30), new DateOnly(2025, 12, 31)], cutDates);
        var first = rows.Single(r => r.ProductCode == "G1001");
        Assert.Equal((100m, 120m), (first.Balances[new DateOnly(2025, 6, 30)], first.Balances[new DateOnly(2025, 12, 31)]));
    }

    [Fact] // a kind that is not pd/tr/opening becomes a hand entry with what was typed kept in the remark; a hand entry keeps its remark
    public async Task The_kind_of_an_entry_is_kept_apart_from_the_remark()
    {
        var report = await RunAsync(Save(Standard()));

        var productId = (await AsAsync(s => s.Get<StockDirectory>().GetBalancesAsync())).Single(b => b.ProductCode == "G1002").ProductId;
        var entries = await AsAsync(s => s.Get<StockDirectory>().GetEntriesAsync(productId));

        var hand = entries.Single(e => e.DocumentNo == "นับสต็อก");
        Assert.Equal((StockEntryKind.Manual, "นับเพิ่ม"), (hand.Kind, hand.Remark));
        var odd = entries.Single(e => e.DocumentNo == "ab");
        Assert.Equal(StockEntryKind.Manual, odd.Kind);
        Assert.Contains("ab", odd.Remark);
        Assert.Contains(report.Exceptions, e => e.Table == "P_ST" && e.Problem.Contains("'ab'"));
        Assert.Contains(report.Exceptions, e => e.Table == "P_ST" && e.Problem.Contains(".G7045"));
        Assert.Contains(report.Exceptions, e => e.Table == "P_ADDST" && e.Problem.Contains("de"));
    }

    [Fact] // Q-47: found and reported every time; left out only when asked
    public async Task Production_counted_twice_is_reported_and_removed_only_when_asked()
    {
        var source = Save(Standard());
        var report = await RunAsync(source);
        Assert.Contains(report.Notes, n => n.StartsWith("Q-47: พบผลผลิต") && n.Contains("1 แถว") && n.Contains("25") && n.Contains("ไม่ตัด"));
        Assert.Single(report.Exceptions, e => e.Rule == "Q-47");

        var removed = await RunAsync(source, "removed", removeDuplicates: true);

        Assert.True(removed.Tables.All(t => t.Balanced), string.Join("; ", removed.Tables.Where(t => !t.Balanced)));
        Assert.Contains(removed.Notes, n => n.StartsWith("Q-47: พบผลผลิต") && n.Contains("ตัดแถวซ้ำออก"));
        var balances = (await AsAsync(s => s.Get<StockDirectory>().GetBalancesAsync())).ToDictionary(b => b.ProductCode, b => b.Quantity);
        Assert.Equal(64m, balances["G1002"]); // 60 + 3 + 1: the production of 10 Dec only once (it is in the opening balance already)
        Assert.Equal(6, Table(removed, "P_ST").Loaded);
    }

    [Fact] // the usage of the open period and the total at each cut date; a temporary row, a mold not in the register and a repeated cut row are listed
    public async Task Moves_the_mold_usage_and_the_totals_of_the_cuts()
    {
        var file = Standard();
        file.Row("การใช้งานของพิมพ์0", ("DAY", December), ("MOULD_ID", "BF-001-1/1"), ("PD_QTY", 5000), ("MARK", "ST"));
        file.Row("การใช้งานของพิมพ์0", ("DAY", new DateTime(2026, 1, 3)), ("MOULD_ID", "BF-001-1/1"), ("PD_QTY", 200), ("MARK", null));
        file.Row("การใช้งานของพิมพ์0", ("DAY", new DateTime(2026, 1, 4)), ("MOULD_ID", "BF-001-1/1"), ("PD_QTY", 300), ("MARK", "de"));
        file.Row("การใช้งานของพิมพ์0", ("DAY", new DateTime(2026, 1, 4)), ("MOULD_ID", "ZZ-999"), ("PD_QTY", 10), ("MARK", null));
        file.Row("ตันงวดการใช้งานของพิมพ์0", ("DAY", June), ("MOULD_ID", "BF-001-1/1"), ("PD_QTY", 3000), ("MARK", null));
        file.Row("ตันงวดการใช้งานของพิมพ์0", ("DAY", December), ("MOULD_ID", "BF-001-1/1"), ("PD_QTY", 4900), ("MARK", null));
        file.Row("ตันงวดการใช้งานของพิมพ์0", ("DAY", December), ("MOULD_ID", "BF-001-1/1"), ("PD_QTY", 5000), ("MARK", null));

        var report = await RunAsync(Save(file));

        Assert.True(report.Tables.All(t => t.Balanced), string.Join("; ", report.Tables.Where(t => !t.Balanced)));
        var usage = Table(report, "การใช้งานของพิมพ์0");
        Assert.Equal((4, 2, 2), (usage.SourceRows, usage.Loaded, usage.Rejected));
        var cutoffs = Table(report, "ตันงวดการใช้งานของพิมพ์0");
        Assert.Equal((3, 2, 1, 2), (cutoffs.SourceRows, cutoffs.Loaded, cutoffs.Rejected, cutoffs.InDatabase));
        Assert.True(report.MonthlyTotals.All(m => m.Balanced), string.Join("; ", report.MonthlyTotals.Where(m => !m.Balanced)));

        var dates = await AsAsync(s => s.Get<MoldUsageDirectory>().GetCutDatesAsync());
        Assert.Equal([new DateOnly(2025, 12, 31), new DateOnly(2025, 6, 30)], dates);
        var total = (await AsAsync(s => s.Get<MoldUsageDirectory>().GetCumulativeAsync(new DateOnly(2025, 12, 31)))).Single(l => l.MoldCode == "BF-001-1/1");
        Assert.Equal(5000, total.Cumulative); // the last row of the day
        var moldId = (await AsAsync(s => s.Get<MoldDirectory>().GetAllAsync())).Single(m => m.Code == "BF-001-1/1").Id;
        var rows = await AsAsync(s => s.Get<MoldUsageDirectory>().GetUsageAsync(moldId, new DateOnly(2025, 1, 1), new DateOnly(2026, 12, 31)));
        Assert.Equal([(5000, true), (200, false)], rows.Select(r => (r.Shots, r.IsOpening)));
    }

    [Fact] // run again: the same ledger, once
    public async Task Runs_again_with_the_same_result()
    {
        var source = Save(Standard());
        await RunAsync(source, "first");

        var report = await RunAsync(source, "second");

        Assert.True(report.Tables.All(t => t.Balanced));
        var balances = (await AsAsync(s => s.Get<StockDirectory>().GetBalancesAsync())).ToDictionary(b => b.ProductCode, b => b.Quantity);
        Assert.Equal(126m, balances["G1001"]);
    }
}
