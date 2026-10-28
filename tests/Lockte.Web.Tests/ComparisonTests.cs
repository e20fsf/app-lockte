using System.Runtime.Versioning;
using Lockte.Migration;
using Lockte.Org;
using Lockte.Web.Accounts;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Web.Tests;

/// <summary>
/// ด่านเทียบกับ baseline (#140): the rules of the comparison on small lists, the command line, the files of the baseline, and a run on a database that was migrated from a made-up file.
/// Every code and number is fictional.
/// </summary>
[Collection(MigrationDbCollection.Name)]
[SupportedOSPlatform("windows")]
public sealed class ComparisonTests(MigrationDbFixture db) : IDisposable
{
    private readonly DirectoryInfo work = Directory.CreateTempSubdirectory("lockte-comparison-test-");

    public void Dispose() => work.Delete(recursive: true);

    private static CompareItem Item(string key, decimal value, params string[] refs) => new(key, [value], refs.Length == 0 ? [""] : refs);

    private static CompareResult Run(IReadOnlyList<CompareItem> baseline, IReadOnlyList<CompareItem> database, bool unique = false, Func<CompareItem, string?>? explain = null)
        => Comparison.Compare("T", "ทดสอบ", "AC-00", ["จำนวน"], baseline, database, unique, explain ?? (_ => null));

    [Fact] // the same keys with the same numbers match; a rounding of less than half a satang is not a difference
    public void Rows_that_agree_match()
    {
        var result = Run([Item("A", 10m), Item("B", 20.004m)], [Item("A", 10m), Item("B", 20m)]);

        Assert.True(result.Passed);
        Assert.Equal(2, result.Matched);
    }

    [Fact] // a different number is listed with both sides
    public void A_different_value_fails_and_shows_both_sides()
    {
        var result = Run([Item("A", 10m)], [Item("A", 11m)]);

        Assert.False(result.Passed);
        var detail = Assert.Single(result.Different);
        Assert.Equal([10m], detail.Baseline);
        Assert.Equal([11m], detail.New);
    }

    [Fact] // not loaded for a reason of the redesign: set aside with the reason and the totals; any other loss, or a row only in the database, fails
    public void Rows_not_loaded_for_a_reason_are_set_aside_and_the_rest_must_match()
    {
        var baseline = new[] { Item("A", 10m, "G1"), Item("B", 5m, "ZZ"), Item("C", 7m, "G1") };
        var database = new[] { Item("A", 10m), Item("D", 1m) };
        string? Explain(CompareItem item) => item.Refs[0] == "ZZ" ? "รหัสสินค้าไม่มีในทะเบียนสินค้า (ไม่โหลด)" : null;

        var result = Run(baseline, database, explain: Explain);

        Assert.False(result.Passed);
        Assert.Equal(["B"], result.BaselineOnlyExplained.Select(d => d.Key));
        Assert.Equal(5m, result.BaselineOnlyExplained[0].Baseline[0]);
        Assert.Equal(["C"], result.BaselineOnlyUnexplained.Select(d => d.Key));
        Assert.Equal(["D"], result.NewOnly.Select(d => d.Key));
    }

    [Fact] // a unique key repeated in the source: the first row is the one loaded (decision: duplicate → first row); a non-unique key is added up
    public void A_repeated_key_keeps_the_first_row_only_where_the_key_is_unique()
    {
        var baseline = new[] { Item("A", 10m), Item("A", 99m) };

        var unique = Run(baseline, [Item("A", 10m)], unique: true);
        var summed = Run(baseline, [Item("A", 109m)], unique: false);

        Assert.True(unique.Passed);
        Assert.Equal("คีย์ซ้ำในต้นทาง — ใช้แถวแรก", Assert.Single(unique.BaselineOnlyExplained).Reason);
        Assert.True(summed.Passed);
    }

    [Fact] // Access compares text without case; the migration keeps the code as in the register: the keys are compared the same way
    public void Keys_are_compared_without_case()
    {
        var result = Run([Item("Sx023|2025-01-09", 40m)], [Item("SX023|2025-01-09", 40m)]);

        Assert.True(result.Passed);
    }

    [Fact] // an amount the migration drops by rule inside a key that is loaded (the cancelled stock rows) is shown, and the rest must match
    public void An_amount_dropped_by_rule_is_shown_and_the_rest_must_match()
    {
        var baseline = new[] { new CompareItem("P1", [100m], ["P1"], [-30m], "รายการยกเลิก (MARK de) ไม่ย้าย") };

        var result = Run(baseline, [Item("P1", 130m)]);
        var wrong = Run(baseline, [Item("P1", 100m)]);

        Assert.True(result.Passed);
        Assert.Equal([-30m], Assert.Single(result.BaselineOnlyExplained).Baseline);
        Assert.False(wrong.Passed);
    }

    [Fact] // a difference inside the tolerance of the key (the unit price kept with 4 decimals) is a match
    public void A_tolerance_of_the_key_is_honoured()
    {
        var item = Item("A", 1365m);
        var result = Comparison.Compare("T", "ทดสอบ", "AC-00", ["มูลค่า"], [item with { Values = [12199.01m] }], [item with { Values = [12199.00m] }], false, _ => null, tolerance: _ => [0.02m]);

        Assert.True(result.Passed);
    }

    [Fact] // the command line: needs the baseline folder and the cut date, and does not ask for --confirm (it only reads)
    public void The_command_line_of_the_comparison_needs_the_baseline_and_the_cut_date()
    {
        string[] args(params string[] extra) => ["--program", "compare", "--connection", "Server=x;Database=LockteTemp;", "--out", "out", .. extra];

        var ok = MigrationOptions.Parse(args("--baseline", "folder", "--cut", "2025-12-31"));
        var withoutBaseline = MigrationOptions.Parse(args("--cut", "2025-12-31"));
        var withoutCut = MigrationOptions.Parse(args("--baseline", "folder"));

        Assert.Equal(("folder", new DateOnly(2025, 12, 31)), (ok.Options!.BaselineFolder, ok.Options.CutDate!.Value));
        Assert.Contains("--baseline", withoutBaseline.Error);
        Assert.Contains("--cut", withoutCut.Error);
    }

    [Fact] // the CSV of the exporter: quoted fields with a comma or a quote; a missing file says which tool to run
    public void The_baseline_files_are_read_and_a_missing_one_names_the_tool()
    {
        File.WriteAllText(Path.Combine(work.FullName, "t.csv"), "A,B\n\"x,1\",\"he said \"\"hi\"\"\"\n2,\n", new System.Text.UTF8Encoding(true));

        var rows = Comparison.Read(work.FullName, "t.csv").ToList();
        var missing = Assert.Throws<FileNotFoundException>(() => Comparison.Read(work.FullName, "nope.csv").ToList());

        Assert.Equal(("x,1", "he said \"hi\""), (rows[0]["A"], rows[0]["B"]));
        Assert.Equal(("2", ""), (rows[1]["A"], rows[1]["B"]));
        Assert.Contains("export_baseline.py", missing.Message);
    }

    private async Task<string> ConnectionAsync()
    {
        await using var scope = db.Scope("hr1");
        return scope.Get<AccountsDbContext>().Database.GetConnectionString()!;
    }

    private void Baseline(string file, string header, params string[] rows)
    {
        Directory.CreateDirectory(Path.Combine(work.FullName, "baseline"));
        File.WriteAllText(Path.Combine(work.FullName, "baseline", file), header + "\n" + string.Join("\n", rows) + (rows.Length == 0 ? "" : "\n"), new System.Text.UTF8Encoding(true));
    }

    /// <summary>A migrated made-up database and a baseline of the same data: all eight checks pass; the baseline row of a product that is not in the register is set aside with its reason; a baseline row nobody explains fails.</summary>
    [Fact]
    public async Task A_migrated_database_is_compared_with_its_baseline()
    {
        var employee = FakeLegacyFile.Standard().Save(Path.Combine(work.FullName, "พนักงาน.accdb"));
        var order = FakeOrderFile.Empty()
            .Row("P_NAME", ("PD_C", "G1001"), ("PD_N", "สินค้าหนึ่ง"), ("U_NAME", "ชิ้น"), ("FML", 2.5))
            .Row("รหัสพิมพ์", ("ID", "BF-001-1/1"), ("NAME", "แม่พิมพ์สมมุติ"), ("QTYS", 2))
            .Save(Path.Combine(work.FullName, "ผลผลิตและORDER.accdb"));
        var options = new MigrationOptions("", await ConnectionAsync(), null, Path.Combine(work.FullName, "migration"), MigrationOptions.AllProgram, false, employee, order, null);
        await FullMigrationTool.RunAsync(db.Services, options);
        Baseline("backlog.csv", "REF_NO,SUF_NO,CUS_ID,PD_C,QTY", "08-69-0001,1,08-01-001,ZZ999,10");
        Baseline("receipts.csv", "REF_NO,SUF_NO,OD_DATE,CUS_ID,SALE_ID,PD_C,QTY,AMOUNT");
        Baseline("deliveries.csv", "INV_NO,INV_SUF,REF_NO,CUS_ID,PD_C,DL_DATE,DL_QTY,DL_NET,DISC_PS");
        Baseline("stock-opening.csv", "PD_C,QTY,FML");
        Baseline("stock-balance.csv", "PD_C,QTY");
        Baseline("mold-cumulative.csv", "MOULD_ID,PD_QTY");
        Baseline("yield.csv", "TABLE,REF_NO,SUF_NO,DATE,PD_C,QTY,NG,LOT");
        var folder = Path.Combine(work.FullName, "baseline");

        var results = await Comparison.RunAsync(db.Services, options.ConnectionString, folder, new DateOnly(2025, 12, 31));
        Comparison.WriteReport(Path.Combine(work.FullName, "compare"), results, new DateOnly(2025, 12, 31));

        Assert.Equal(["RPT-02", "RPT-06", "RPT-07", "RPT-07x", "RPT-13", "STOCK", "RPT-15", "RPT-16"], results.Select(r => r.Id));
        Assert.All(results, r => Assert.True(r.Passed, r.Id));
        var backlog = results[0];
        Assert.Equal(("ลูกค้าไม่มีในทะเบียนลูกค้า (ไม่โหลด)", 1), (backlog.BaselineOnlyExplained[0].Reason, backlog.BaselineOnlyExplained.Count));
        var summary = File.ReadAllText(Path.Combine(work.FullName, "compare", "compare-summary.md"));
        Assert.Contains("ผ่าน", summary);
        Assert.Contains("หักออก: ลูกค้าไม่มีในทะเบียนลูกค้า (ไม่โหลด) — 1 แถว", summary);
        Assert.True(File.Exists(Path.Combine(work.FullName, "compare", "compare-details.csv")));

        // the stock the baseline has and the database has not: nobody explains it, the check fails
        Baseline("stock-balance.csv", "PD_C,QTY", "G1001,500");
        var failing = await Comparison.RunAsync(db.Services, options.ConnectionString, folder, new DateOnly(2025, 12, 31));
        Assert.False(failing.Single(r => r.Id == "STOCK").Passed);
        Assert.Single(failing.Single(r => r.Id == "STOCK").BaselineOnlyUnexplained);
    }
}
