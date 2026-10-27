using System.Runtime.Versioning;
using Lockte.Migration;
using Lockte.Prod;
using Lockte.Web.Audit;

namespace Lockte.Web.Tests;

/// <summary>
/// เครื่องมือย้ายข้อมูล: ใบรายงานผลผลิต 7 แผนก (#122) from a made-up <c>ผลผลิตและORDER.accdb</c> — every name, number and amount in it is fictional (the repo is public).
/// The master data (products, molds) is loaded by the same run, as in the real one.
/// </summary>
[Collection(MigrationDbCollection.Name)]
[SupportedOSPlatform("windows")]
public sealed class ProductionReportMigrationTests(MigrationDbFixture db) : IDisposable
{
    private readonly DirectoryInfo work = Directory.CreateTempSubdirectory("lockte-production-migration-test-");

    public void Dispose() => work.Delete(recursive: true);

    private async Task<T> AsAsync<T>(Func<TestScope, Task<T>> action)
    {
        await using var scope = db.Scope("hr1");
        return await action(scope);
    }

    private Task<MigrationReport> RunAsync(string source, string output = "out")
        => OrderMigrationTool.RunAsync(db.Services, source, null, Path.Combine(work.FullName, output));

    private string Save(FakeLegacyFile file) => file.Save(Path.Combine(work.FullName, "ผลผลิตและORDER.accdb"));

    private static readonly DateTime May = new(2026, 5, 1);

    private static (string, object?)[] Row(string no, int? line, string product, double? good, double? reject, DateTime? date = null, string? lot = "L1", string? shift = "A",
        string? total = null, string? mould = null, string? employee = null)
        => [("DATE", date ?? May), ("REF_NO", no), ("SUF_NO", line), ("MC_NO", "D01"), ("SHIFT", shift), ("TT_T", 8.0), ("LOT_MTR1", "M1"), ("LOT_MTR2", null), ("PD_C", product), ("PD_N", "ชื่อสมมุติ"),
            ("U_NAME", "ชิ้น"), ("QTY", good), ("NG", reject), ("EMP_ID", employee), ("TOTAL", total is null ? null : double.Parse(total)), ("MOULD", mould), ("LOT", lot)];

    private static FakeLegacyFile Standard()
    {
        var file = FakeOrderFile.Empty()
            .Row("P_NAME", ("PD_C", "G1001"), ("PD_N", "สินค้าหนึ่ง"), ("U_NAME", "ชิ้น"))
            .Row("P_NAME", ("PD_C", "G1002"), ("PD_N", "สินค้าสอง"), ("U_NAME", "ชิ้น"))
            .Row("รหัสพิมพ์", ("ID", "BF-001-1/1"), ("NAME", "แม่พิมพ์สมมุติ"), ("QTYS", 2));

        // ตกแต่ง/ไดคาสท์: D69001 is in the copy (stocked), D69002 is not; D69001 has its line 2 twice; a deleted line (no จำนวนดี); a row before 2025
        file.Row("P_REPไดคาสท์", Row("D69001", 1, "G1001", 100, 5, total: "105", mould: "BF-001-1/1"));
        file.Row("P_REPไดคาสท์", Row("D69001", 2, "G1001", 40, 0, total: "40"));
        file.Row("P_REPไดคาสท์", Row("D69001", 2, "G1002", 30, 1)); // the same number and line again: kept, renumbered; total empty → worked out
        file.Row("P_REPไดคาสท์", Row("D69001", 4, "G1001", null, null)); // no จำนวนดี: a deleted line
        file.Row("P_REPไดคาสท์", Row("D69002", 1, "ZZ999", 70, 2, date: May.AddDays(1), shift: "ก")); // product not in the register, a malformed shift
        file.Row("P_REPไดคาสท์", Row("D68999", 1, "G1001", 9, 0, date: new DateTime(2024, 12, 31))); // before 2025: not moved
        file.Row("P_REPตกแต่ง", Row("T69001", 1, "G1002", 500, 10, date: May.AddDays(2)));
        file.Row("P_REPบรรจุ", Row("P69001", 1, "G1001", 80, 0, date: new DateTime(2026, 6, 3)));

        // the transferred copy: the rows of D69001 and T69001 as they were, one changed after the transfer (500 → 450), and one that is not in any source
        file.Row("P_REP", Row("D69001", 1, "G1001", 100, 5, total: "105", mould: "BF-001-1/1"));
        file.Row("P_REP", Row("D69001", 2, "G1001", 40, 0, total: "40"));
        file.Row("P_REP", Row("T69001", 1, "G1002", 450, 10, date: May.AddDays(2)));
        file.Row("P_REP", Row("D69777", 1, "G1001", 1, 0));
        return file;
    }

    private static TableCount Table(MigrationReport report, string legacyTable) => report.Tables.Single(t => t.LegacyTable == legacyTable);

    [Fact]
    public async Task Moves_the_reports_of_the_seven_departments_into_one_table_and_balances_every_count()
    {
        var report = await RunAsync(Save(Standard()));

        Assert.True(report.Tables.All(t => t.Balanced), string.Join("; ", report.Tables.Where(t => !t.Balanced)));
        var die = Table(report, "P_REPไดคาสท์");
        Assert.Equal((6, 2, 0, 4, 4), (die.SourceRows, die.Excluded, die.Rejected, die.Loaded, die.InDatabase)); // one before 2025 and one deleted line are not moved
        Assert.Equal((1, 1, 0, 1), (Table(report, "P_REPตกแต่ง").SourceRows, Table(report, "P_REPตกแต่ง").Loaded, Table(report, "P_REPตกแต่ง").Rejected, Table(report, "P_REPตกแต่ง").InDatabase));
        Assert.Equal(1, Table(report, "P_REPบรรจุ").Loaded);
        Assert.True(report.MonthlyTotals.All(m => m.Balanced), string.Join("; ", report.MonthlyTotals.Where(m => !m.Balanced)));

        var first = (await AsAsync(s => s.Get<ProductionReportDirectory>().FindByNumberAsync("D69001")))!;
        Assert.Equal((3, new DateOnly(2026, 5, 1), "D"), (first.Lines.Count, first.ReportDate, first.DepartmentCode));
        Assert.Equal([1, 2, 3], first.Lines.Select(l => l.LineNo)); // the repeated line number got the next free one, nothing was dropped
        Assert.Equal([105m, 40m, 31m], first.Lines.Select(l => l.TotalQuantity)); // จำนวนรวม = ดี + เสีย, also where the legacy total was empty
        Assert.NotNull(first.Lines[0].MoldId);
        Assert.Contains(report.Notes, n => n.Contains("เลขที่+ลำดับซ้ำ") || n.Contains("ลำดับซ้ำ"));
    }

    [Fact] // เข้าสต็อกแล้ว comes from the copy; a copy that differs and a copy with no source are counted; a source row with no copy
    public async Task The_stocked_status_comes_from_the_transferred_copy()
    {
        var report = await RunAsync(Save(Standard()));

        var stocked = await AsAsync(async s => new[] { "D69001", "D69002", "T69001", "P69001" }.Select(n => s.Get<ProductionReportDirectory>().FindByNumberAsync(n).Result!.InStock).ToArray());
        Assert.Equal([true, false, false, false], stocked); // T69001 was edited after the transfer (500 → 450 in the copy): no exact match, so not stocked
        Assert.Contains(report.Notes, n => n.StartsWith("สำเนา P_REP (ถึงสต็อก) 4 แถว: ตรงกับต้นทาง 2 แถว") && n.Contains("ไม่ตรงกับต้นทาง (แก้หลังโอนหรือไม่มีต้นทางตั้งแต่ปี 2568) 2 แถว") && n.Contains("แถวต้นทาง 4 แถวไม่อยู่ในสำเนา"));
    }

    [Fact] // a product that is not in the register or a malformed shift: the line is kept, the field blank, and reported
    public async Task Values_that_are_not_in_the_registers_are_left_blank_and_reported()
    {
        var report = await RunAsync(Save(Standard()));

        var second = (await AsAsync(s => s.Get<ProductionReportDirectory>().FindByNumberAsync("D69002")))!;
        var line = Assert.Single(second.Lines);
        Assert.Equal((null, null, 70m), (line.ProductId, line.ShiftId, line.GoodQuantity));
        Assert.Contains(report.Exceptions, e => e.Table == "P_REPไดคาสท์" && e.Problem.Contains("ZZ999") && e.Action == "โหลดโดยเว้นช่องนี้ว่าง");
        Assert.Contains(report.Exceptions, e => e.Table == "P_REPไดคาสท์" && e.Problem.Contains("กะ 'ก'"));
    }

    [Fact] // run again: the same reports, once
    public async Task Runs_again_with_the_same_result()
    {
        var source = Save(Standard());
        await RunAsync(source, "first");

        var report = await RunAsync(source, "second");

        Assert.True(report.Tables.All(t => t.Balanced));
        var again = (await AsAsync(s => s.Get<ProductionReportDirectory>().FindByNumberAsync("D69001")))!;
        Assert.Equal(3, again.Lines.Count);
    }

    [Fact] // a report with several machines: the one named most often is the report's, and the report says the line-level machines are not kept (decided by the users)
    public async Task A_report_with_several_machines_is_listed()
    {
        var file = Standard();
        var other = Row("D69003", 1, "G1001", 10, 0);
        other = [.. other.Where(c => c.Item1 != "MC_NO"), ("MC_NO", "D02")];
        file.Row("P_REPไดคาสท์", Row("D69003", 2, "G1001", 20, 0));
        file.Row("P_REPไดคาสท์", Row("D69003", 3, "G1001", 30, 0));
        file.Row("P_REPไดคาสท์", other);

        var report = await RunAsync(Save(file));

        Assert.Contains(report.Exceptions, e => e.Table == "P_REPไดคาสท์" && e.Key == "D69003" && e.Problem.Contains("หลายเครื่องในใบเดียว") && e.Action == "โหลดโดยใช้เครื่องเดียวของใบ");
        Assert.Contains(report.Notes, n => n.Contains("หลายเครื่องในใบเดียวกัน") && n.Contains("ไม่เก็บเครื่องระดับบรรทัด"));
        Assert.True(report.Tables.All(t => t.Balanced));
    }

    [Fact] // MARK1 = Y (any case) is the QA's mark (SCR-12): kept as the user left it, counted in a note
    public async Task The_qa_mark_is_kept()
    {
        var file = Standard();
        file.Row("P_REPไดคาสท์", [.. Row("D69004", 1, "G1001", 10, 0, date: new DateTime(2026, 7, 7)), ("MARK1", "y")]);
        file.Row("P_REPไดคาสท์", [.. Row("D69004", 2, "G1002", 20, 0, date: new DateTime(2026, 7, 7)), ("MARK1", null)]);

        var report = await RunAsync(Save(file));

        var lines = await AsAsync(s => s.Get<QaSelectionDirectory>().GetLinesAsync("D", new DateOnly(2026, 7, 7)));
        Assert.Equal([true, false], lines.Select(l => l.Selected));
        Assert.Contains(report.Notes, n => n.Contains("1 แถวยังติดเครื่องหมายสุ่มตรวจ"));
    }
}
