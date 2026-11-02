using System.Globalization;
using Lockte.Cash;
using Lockte.Migration;
using Lockte.Web.Seeding;
using Microsoft.Extensions.DependencyInjection;

namespace Lockte.Web.Tests;

/// <summary>ด่านเทียบเงินสดย่อยกับ Access (#248) without a file or a database: what counts as a match, what is accepted with its reason, and what fails.</summary>
public sealed class CashComparisonRulesTests
{
    private static ExpenseTypeTotal Total(string name, decimal total) => new(1, name, total);

    private static ExpenseLineDto Line(int id, string no, string name, decimal qty, decimal amount, int day = 11)
        => new(id, new DateOnly(2026, 9, day), no, name, qty, null, 0, amount, 1, "ประเภท", [1]);

    private static CashBaselineLine Old(int day, string no, string item, decimal? qty, decimal amount, string other = "ประเภท")
        => new(new DateOnly(2026, 9, day), no, "", item, qty, amount, other);

    [Fact] // AC-16 RPT-02: the types of the baseline equal the types of the new report to the satang
    public void The_summary_by_type_matches_when_every_type_equals()
    {
        var result = CashComparison.CompareExpenseSummary("R", [("ค่าขนส่ง", 70m), ("ค่าสวัสดิการ", 14620.5m)], [Total("ค่าขนส่ง", 70m), Total("ค่าสวัสดิการ", 14620.5m)], CodeMap.Empty);
        Assert.True(result.Passed);
        Assert.Equal(2, result.Matched);
    }

    [Fact] // two spellings that differ in blanks or a line break are one type; a blank TYPE is a line the migration left out; neither is hidden
    public void Names_cleaned_or_merged_by_the_map_and_blank_types_are_accepted_with_a_reason()
    {
        var map = new CodeMap([new CodeMapEntry(CodeKinds.CashExpenseType, "ค่าซ่อม (1กก 123)", "ค่าซ่อม (1 กก 123)", null, null)]);
        var result = CashComparison.CompareExpenseSummary("R",
            [("ค่าซ่อม (1 กก 123)", 100m), ("ค่าซ่อม (1กก 123)", 50m), ("ค่าอบรม\nค่าเดินทาง", 10m), ("", 5m)],
            [Total("ค่าซ่อม (1 กก 123)", 150m), Total("ค่าอบรม ค่าเดินทาง", 10m)], map);

        Assert.True(result.Passed, string.Join("; ", result.Unaccepted.Select(d => d.Cause)));
        Assert.Equal(2, result.Matched);
        Assert.Contains(result.Differences, d => d.Accepted && d.Cause.Contains("รวมชื่อดิบ", StringComparison.Ordinal));
        Assert.Contains(result.Differences, d => d.Accepted && d.Key == "(ว่าง)");
    }

    [Fact]
    public void A_total_that_differs_a_type_that_is_missing_and_a_type_that_is_extra_fail()
    {
        var result = CashComparison.CompareExpenseSummary("R", [("ก", 70m), ("ข", 5m)], [Total("ก", 71m), Total("ค", 1m)], CodeMap.Empty);
        Assert.False(result.Passed);
        Assert.Equal(["ประเภทเกินในรายงานใหม่", "ประเภทหายจากรายงานใหม่", "ยอดต่อประเภทไม่ตรง"], result.Unaccepted.Select(d => d.Cause).Order());
        Assert.All(result.Unaccepted, d => Assert.Equal("โปรแกรม", d.Origin));
    }

    [Fact] // AC-16 RPT-04: row by row, in the order of the old report (date, then the number as text)
    public void The_lines_match_row_by_row_and_in_order()
    {
        var result = CashComparison.CompareLines("R", [Old(11, "09/34", "แก๊ส", 31.53m, 633m), Old(11, "09/35", "ซีล", 1m, 90m), Old(11, "09/99", "ท้าย", 1m, 5m)],
            [Line(2, "09/34", "แก๊ส", 31.53m, 633m), Line(3, "09/35", "ซีล", 1m, 90m), Line(4, "09/99", "ท้าย", 1m, 5m)]);
        Assert.True(result.Passed);
        Assert.Equal(3, result.Matched);
    }

    [Fact]
    public void A_line_that_is_missing_extra_or_out_of_order_fails_and_an_incomplete_line_is_left_out_with_a_reason()
    {
        var baseline = new[] { Old(11, "09/34", "ก", 1m, 10m), Old(11, "09/35", "ข", 1m, 20m), Old(11, "", "ไม่มีเลขที่", 1m, 7m) };
        var good = CashComparison.CompareLines("R", baseline, [Line(1, "09/34", "ก", 1m, 10m), Line(2, "09/35", "ข", 1m, 20m)]);
        Assert.True(good.Passed);
        Assert.Contains(good.Differences, d => d.Accepted && d.Cause.Contains("ไม่ครบ", StringComparison.Ordinal));

        var missing = CashComparison.CompareLines("R", baseline, [Line(1, "09/34", "ก", 1m, 10m)]);
        Assert.False(missing.Passed);
        Assert.Contains(missing.Unaccepted, d => d.Cause == "แถวหายจากรายงานใหม่");

        var changed = CashComparison.CompareLines("R", baseline, [Line(1, "09/34", "ก", 1m, 10m), Line(2, "09/35", "ข", 1m, 21m)]);
        Assert.Equal(2, changed.Unaccepted.Count); // the old amount is missing, the new one is extra

        var order = CashComparison.CompareLines("R", baseline, [Line(2, "09/35", "ข", 1m, 20m), Line(1, "09/34", "ก", 1m, 10m)]);
        Assert.False(order.Passed);
        Assert.Contains(order.Unaccepted, d => d.Cause.Contains("ลำดับ", StringComparison.Ordinal));
    }

    private static ScrapSaleRow Sale(string no, int day, string item, decimal amount) => new(new DateOnly(2026, 9, day), no, 1, item, 1m, "กก.", amount, amount, "คุณตัวอย่าง");

    [Fact] // RPT-09: the head of a receipt is its first line's, so a line may be on another date than the old report gave — accepted; the double blank in a name is cleaned — accepted
    public void Sales_match_and_the_accepted_differences_are_told()
    {
        var baseline = new[] { Old(8, "69029", "เศษเหล็ก", 1m, 100m), Old(9, "69029", "ถัง  เหล็ก", 1m, 50m), Old(9, "69030", "น้ำมัน", 1m, 7m) };
        var now = new[] { Sale("69029", 8, "เศษเหล็ก", 100m), Sale("69029", 8, "ถัง เหล็ก", 50m), Sale("69030", 9, "น้ำมัน", 7m) };
        var dates = new Dictionary<string, DateOnly> { ["69029"] = new DateOnly(2026, 9, 8), ["69030"] = new DateOnly(2026, 9, 9) };
        var all = new Dictionary<string, IReadOnlySet<DateOnly>> { ["69029"] = new HashSet<DateOnly> { new(2026, 9, 8), new(2026, 9, 9) }, ["69030"] = new HashSet<DateOnly> { new(2026, 9, 9) } };

        var result = CashComparison.CompareSales("R", baseline, now, dates, all);

        Assert.True(result.Passed, string.Join("; ", result.Unaccepted.Select(d => d.Cause + " " + d.Key)));
        Assert.Contains(result.Differences, d => d.Accepted && d.Cause.Contains("หัวต่างกัน", StringComparison.Ordinal));
        Assert.Contains(result.Differences, d => d.Accepted && d.Cause.Contains("ช่องว่างซ้อน", StringComparison.Ordinal));
    }

    [Fact]
    public void A_sold_line_that_is_gone_or_made_up_fails()
    {
        var baseline = new[] { Old(8, "69029", "เศษเหล็ก", 1m, 100m) };
        var result = CashComparison.CompareSales("R", baseline, [Sale("69029", 8, "เศษเหล็ก", 101m)], new Dictionary<string, DateOnly> { ["69029"] = new DateOnly(2026, 9, 8) });
        Assert.False(result.Passed);
        Assert.Equal(2, result.Unaccepted.Count);
    }

    private static ScrapReceiptDto Receipt(string no, params decimal[] amounts)
        => new(1, no, new DateOnly(2026, 9, 8), "คุณตัวอย่าง", null, null, null, false, null, null, null, amounts.Select((a, i) => new ScrapReceiptLineDto(i, i + 1, "ของ", null, null, 1, a, a)).ToList(), [1]);

    [Fact] // AC-23, RPT-08
    public void The_waiting_receipts_match_the_old_waiting_table()
    {
        var (report, gate) = CashComparison.CompareWaiting([("69027", 1360m, 2), ("69028", 1000m, 1)], [Receipt("69027", 1000m, 360m), Receipt("69028", 1000m)]);
        Assert.True(report.Passed);
        Assert.True(gate.Passed);

        var (bad, badGate) = CashComparison.CompareWaiting([("69027", 1360m, 2), ("69028", 1000m, 1)], [Receipt("69027", 1000m, 360m), Receipt("69029", 5m)]);
        Assert.False(bad.Passed);
        Assert.False(badGate.Passed);
    }

    [Fact] // AC-24: equal totals; a sum beyond two decimals may differ with its reason; any other difference fails
    public void The_total_of_a_receipt_may_differ_only_where_the_sum_goes_beyond_two_decimals()
    {
        var now = new[] { Receipt("1", 100m), Receipt("2", 50.13m, 50m), Receipt("3", 10m) };
        var result = CashComparison.CompareReceiptTotals([("1", 1, 100.0, 100m), ("2", 2, 100.125, 100.12m), ("3", 1, 11.0, 11m)], now);

        Assert.Equal(1, result.Matched);
        Assert.Single(result.Differences, d => d.Accepted && d.Key == "2");
        Assert.Single(result.Unaccepted, d => d.Key == "3");
        Assert.False(result.Passed);
    }

    [Fact] // AC-01
    public void The_sizes_must_equal_what_the_baseline_could_load()
    {
        var sizes = new Dictionary<string, string>
        {
            ["expense_lines_all"] = "19040", ["expense_lines_loadable"] = "19037", ["expense_amount_loadable"] = "11730270.00", ["receipt_numbers"] = "765", ["receipt_lines_loadable"] = "1859",
        };
        Assert.All(CashComparison.CompareSizes(sizes, 19037, 11730270m, 765, 1859), g => Assert.True(g.Passed));
        Assert.Contains(CashComparison.CompareSizes(sizes, 19036, 11730270m, 765, 1859), g => g.Passed == false);
    }
}

/// <summary>The gate run end to end on the demo set (#248): a baseline written from what the demo set must give passes; one changed value fails and is told. A database of its own.</summary>
public sealed class CashComparisonRunTests : IAsyncLifetime
{
    private readonly LocalDbFixture db = new();
    private readonly DirectoryInfo work = Directory.CreateTempSubdirectory("lockte-cash-compare-test-");

    private AsyncServiceScope NewScope()
    {
        var scope = db.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TestCurrentUser>().UserName = "seed";
        return scope;
    }

    public async Task InitializeAsync()
    {
        await db.InitializeAsync();
        await new CashFakeDataSeeder(NewScope).SeedAsync();
    }

    public async Task DisposeAsync()
    {
        await db.DisposeAsync();
        TestFiles.DeleteScratch(work);
    }

    /// <summary>What the old reports would have given on the demo set, written in the shape of <c>export_cash_baseline.py</c>; the sums are those of acceptance.md.</summary>
    private async Task<string> WriteBaselineAsync(string name)
    {
        var folder = Path.Combine(work.FullName, name);
        Directory.CreateDirectory(folder);
        await using var scope = NewScope();
        var lines = await scope.ServiceProvider.GetRequiredService<ExpenseLineDirectory>().ListAsync(DateOnly.MinValue, DateOnly.MaxValue);
        var receipts = await scope.ServiceProvider.GetRequiredService<ScrapReceiptDirectory>().ListAsync(DateOnly.MinValue, DateOnly.MaxValue);
        var waiting = await scope.ServiceProvider.GetRequiredService<ScrapRemittanceDirectory>().UnremittedAsync();
        string D(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        string N(decimal v) => v.ToString("0.####", CultureInfo.InvariantCulture);
        Csv.Write(Path.Combine(folder, "cash-ranges.csv"), ["id", "from", "to"], [["Y2026", "2026-01-01", "2026-12-31"], ["AC07", "2026-09-01", "2026-09-03"]]);
        Csv.Write(Path.Combine(folder, "cash-summary.csv"), ["metric", "value"], new object?[][]
        {
            ["expense_lines_all", lines.Count], ["expense_amount_all", N(lines.Sum(l => l.Amount))], ["expense_lines_loadable", lines.Count], ["expense_amount_loadable", N(lines.Sum(l => l.Amount))],
            ["receipt_numbers", receipts.Count], ["receipt_lines_all", receipts.Sum(r => r.Lines.Count)], ["receipt_lines_loadable", receipts.Sum(r => r.Lines.Count)],
            ["waiting_lines", waiting.Sum(r => r.Lines.Count)], ["waiting_receipts", waiting.Count],
        });
        var rows02 = new List<object?[]>();
        var rows04 = new List<object?[]>();
        var rows09 = new List<object?[]>();
        foreach (var (id, from, to) in new[] { ("Y2026", new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)), ("AC07", new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 3)) })
        {
            rows02.AddRange(lines.Where(l => l.Date >= from && l.Date <= to).GroupBy(l => l.ExpenseTypeName).Select(g => new object?[] { id, g.Key, N(g.Sum(l => l.Amount)) }));
            rows04.AddRange(lines.Where(l => l.Date >= from && l.Date <= to).OrderBy(l => l.Date).ThenBy(l => l.VoucherNo, StringComparer.Ordinal).ThenBy(l => l.Id)
                .Select(l => new object?[] { id, D(l.Date), l.VoucherNo, l.Name, N(l.Qty), l.Unit, N(l.Amount), l.ExpenseTypeName }));
            rows09.AddRange(receipts.Where(r => r.Date >= from && r.Date <= to).OrderBy(r => r.Date).ThenBy(r => r.No, StringComparer.Ordinal)
                .SelectMany(r => r.Lines.Select(l => new object?[] { id, D(r.Date), r.No, l.Seq, l.ItemName, N(l.Qty), l.Unit, N(l.UnitPrice), N(l.Amount), r.BuyerName })));
        }
        Csv.Write(Path.Combine(folder, "cash-rpt02.csv"), ["range", "type", "total"], rows02);
        Csv.Write(Path.Combine(folder, "cash-rpt04.csv"), ["range", "date", "suf", "name", "qty", "unit", "amount", "type"], rows04);
        Csv.Write(Path.Combine(folder, "cash-rpt09.csv"), ["range", "date", "no", "suf", "names", "qty", "unit", "price", "amount", "named"], rows09);
        Csv.Write(Path.Combine(folder, "cash-rpt08.csv"), ["no", "total", "lines"], waiting.Select(r => new object?[] { r.No, N(r.Total), r.Lines.Count }));
        Csv.Write(Path.Combine(folder, "cash-receipts.csv"), ["no", "lines", "sum", "access_total"], receipts.Select(r => new object?[] { r.No, r.Lines.Count, N(r.Lines.Sum(l => l.Amount)), N(r.Total) }));
        return folder;
    }

    [Fact]
    public async Task A_baseline_that_equals_the_demo_set_passes_and_the_reports_are_written()
    {
        var folder = await WriteBaselineAsync("good");

        var result = await CashComparison.RunAsync(db.Services, folder, CodeMap.Empty);

        Assert.True(result.Passed, string.Join("; ", result.Ranges.SelectMany(r => r.Unaccepted).Select(d => $"{d.Report} {d.Key} {d.Cause}")) + string.Join("; ", result.Gate.Where(g => g.Passed == false).Select(g => g.Check)));
        Assert.Contains(result.Gate, g => g.Id == "AC-23" && g.Passed == true);
        Assert.Contains(result.Gate, g => g.Id == "AC-24" && g.Passed == true);
        var output = Path.Combine(work.FullName, "out");
        CashComparison.WriteTo(output, result);
        foreach (var file in new[] { "cash-compare.csv", "cash-compare-details.csv", "cash-gate.csv", "cash-summary.md" }) Assert.True(File.Exists(Path.Combine(output, file)), file);
    }

    [Fact]
    public async Task One_changed_value_in_the_baseline_fails_the_gate_and_is_listed_as_a_fault_of_the_program()
    {
        var folder = await WriteBaselineAsync("bad");
        var path = Path.Combine(folder, "cash-rpt02.csv");
        var text = (await File.ReadAllTextAsync(path)).Replace("70", "71", StringComparison.Ordinal);
        await File.WriteAllTextAsync(path, text);

        var result = await CashComparison.RunAsync(db.Services, folder, CodeMap.Empty);

        Assert.False(result.Passed);
        Assert.Contains(result.Gate, g => g.Id == "AC-16" && g.Check.StartsWith("RPT-02", StringComparison.Ordinal) && g.Passed == false);
        var output = Path.Combine(work.FullName, "out-bad");
        CashComparison.WriteTo(output, result);
        Assert.Contains("โปรแกรม", await File.ReadAllTextAsync(Path.Combine(output, "cash-compare-details.csv")));
    }
}
