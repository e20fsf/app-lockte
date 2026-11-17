using Lockte.Migration;
using Lockte.Purch;

namespace Lockte.Web.Tests;

/// <summary>ด่านเทียบวัตถุดิบกับ Access (#271) without a file or a database: what counts as a match, what is accepted with its reason (ข้อมูล / ต่างจากเดิมตามที่ตกลง), and what fails.</summary>
public sealed class PurchComparisonRulesTests
{
    private static readonly DateOnly Day = new(2026, 9, 7);

    private static OutstandingReceiptLine Waiting(string no, int line, decimal outstanding) => new(no, Day, line, "วัตถุดิบทดสอบ", "ชิ้น", outstanding, Day.AddDays(10), "ผู้ขายทดสอบ", null);

    private static PurchBacklogRow Old(string no, int line, decimal qty, bool listed = true) => new(Day, no, line, "วัตถุดิบทดสอบ", "ชิ้น", qty, Day.AddDays(10), listed);

    [Fact] // RPT-04: a row with a วันที่รับ typed in but never confirmed is not in the old report, and is still waited for here (AC-30 ค้างรับ 37)
    public void The_backlog_matches_row_by_row_and_a_row_never_confirmed_is_accepted()
    {
        var result = PurchComparison.CompareBacklog([Old("690001", 1, 50), Old("690001", 2, 7, listed: false), Old("690002", 1, 3)],
            [Waiting("690001", 1, 50), Waiting("690001", 2, 7), Waiting("690002", 1, 3)]);

        Assert.True(result.Passed, string.Join("; ", result.Unaccepted.Select(d => d.Cause)));
        Assert.Equal(2, result.Matched);
        Assert.Contains(result.Differences, d => d.Origin == PurchDifference.Agreed && d.Key == "690001 ที่ 2");
    }

    [Fact]
    public void A_line_still_waited_for_only_here_or_with_another_quantity_fails()
    {
        var result = PurchComparison.CompareBacklog([Old("690001", 1, 50)], [Waiting("690001", 1, 40), Waiting("690003", 1, 1)]);

        Assert.False(result.Passed);
        Assert.Equal(2, result.Unaccepted.Count);
        Assert.All(result.Unaccepted, d => Assert.Equal(PurchDifference.Program, d.Origin));
    }

    private static ReceivedLine Received(string request, string order, int material, string name, decimal qty, decimal amount, bool failed = false)
        => new(1, Day, order, request, 1, material, "M" + material, name, "กก.", true, qty, amount / qty, amount, 1, "S1", "ผู้ขายทดสอบ", failed);

    [Fact] // RPT-20 (AC-26): grouped by name and unit as the old report, the heads counted once; one material under two names is shown, not counted against
    public void The_monthly_summary_compares_by_name_and_tells_the_grouping_by_material()
    {
        var lines = PurchReportRows.Purchases([Received("R1", "690001", 1, "อลูมิเนียม", 100, 1000), Received("R1", "690002", 1, "อลูมิเนียม ADC12", 10, 50),
            Received("R2", "690002-2", 2, "แก๊ส", 5, 25), Received("R3", "690004", 2, "แก๊ส", 5, 25, failed: true)], Day, Day);

        var result = PurchComparison.CompareMonthlySummary("M", [("อลูมิเนียม", "กก.", 100, 1000), ("อลูมิเนียม ADC12", "กก.", 10, 50), ("แก๊ส", "กก.", 5, 25)], (3, 2, 2), lines);

        Assert.True(result.Passed, string.Join("; ", result.Unaccepted.Select(d => d.Cause)));
        Assert.Equal(3, result.Matched);
        Assert.Contains(result.Differences, d => d.Origin == PurchDifference.Agreed && d.Cause.Contains("รหัสวัตถุดิบ", StringComparison.Ordinal));
    }

    [Fact] // RPT-22 (AC-25): the แผนก typed on the line is matched as the migration matched it
    public void The_department_totals_match_after_the_name_is_resolved()
    {
        var result = PurchComparison.CompareDepartments("M", [("ไดคาสท์", 1000m), ("ไดคาส", 5m), ("บรรจุ", 20m)], [("ไดคาสท์", 1005m), ("บรรจุ", 20m)], n => n == "ไดคาส" ? "ไดคาสท์" : n);
        Assert.True(result.Passed);

        var wrong = PurchComparison.CompareDepartments("M", [("บรรจุ", 20m)], [("บรรจุ", 21m)], n => n);
        Assert.False(wrong.Passed);
    }

    private static SmallOrderLine Small(string no, int line, decimal tax) => new(Day, no, line, "วัตถุดิบทดสอบ", 1, "ชิ้น", tax, "ผู้ขายทดสอบ");

    [Fact] // RPT-11 (AC-28): the received orders the old query misses (PUR_ID is an empty text) are told; a leftover row of the history is not moved
    public void Small_orders_compare_the_rows_the_query_means_and_tell_what_it_misses()
    {
        var result = PurchComparison.CompareSmallOrders("M",
            [new("690977", 1, 3317m, 3317m, Literal: false), new("690980", 1, 100m, 200m, Literal: true), new("690980", 1, 100m, 200m, Literal: false), new("630512", 1, 2200m, 2200m, false, Leftover: true)],
            [Small("690977", 1, 3317m), Small("690980", 1, 100m)], new HashSet<string>());

        Assert.True(result.Passed, string.Join("; ", result.Unaccepted.Select(d => d.Cause)));
        Assert.Equal(2, result.Matched);
        Assert.Contains(result.Differences, d => d.Origin == PurchDifference.Agreed && d.Cause.Contains("PUR_ID", StringComparison.Ordinal));
        Assert.Contains(result.Differences, d => d.Origin == PurchDifference.Agreed && d.Cause.Contains("ครั้งเดียว", StringComparison.Ordinal));
        Assert.Contains(result.Differences, d => d.Origin == PurchDifference.Data && d.Key == "630512 ที่ 1");

        Assert.False(PurchComparison.CompareSmallOrders("M", [new("690977", 1, 3317m, 3317m, true)], [Small("690977", 1, 3300m)], new HashSet<string>()).Passed);
    }

    private static StockPeriodBalanceDto Balance(string code, decimal qty, decimal cost) => new(new DateOnly(2025, 10, 31), 1, code, "วัตถุดิบ " + code, "ชิ้น", qty, cost);

    [Fact] // RPT-26 (AC-20/21): by code (the old join ignores case), quantity and price; a rehearsal cut may take in rows keyed after Access had cut
    public void The_stock_cut_matches_by_code_and_a_rehearsal_tells_the_rows_keyed_late()
    {
        Assert.True(PurchComparison.CompareStockCut("C", [("r0019", 1000m, 96m), ("CA002", 1200m, 4.165m)], [Balance("R0019", 1000m, 96m), Balance("CA002", 1200m, 4.165m), Balance("Z1", 0m, 1m)]).Passed);

        var rehearsed = PurchComparison.CompareStockCut("C", [("R0019", 1000m, 96m)], [Balance("R0019", 990m, 96m), Balance("R0020", -5m, 2m)],
            new Dictionary<string, decimal> { ["R0019"] = -10m, ["R0020"] = -5m });
        Assert.True(rehearsed.Passed, string.Join("; ", rehearsed.Unaccepted.Select(d => d.Cause)));
        Assert.All(rehearsed.Differences, d => Assert.Equal(PurchDifference.Data, d.Origin));

        Assert.False(PurchComparison.CompareStockCut("C", [("R0019", 1000m, 96m)], [Balance("R0019", 990m, 96m)]).Passed);
    }

    [Fact] // AC-18: a code outside the register and an undated row are left out by the migration (DQ), the rest must equal
    public void The_balances_leave_out_what_the_migration_does_not_move()
    {
        var registered = new HashSet<string> { "R0019", "R0020" };
        var result = PurchComparison.CompareBalances([("R0019", 3217.4m, 3, -2675m), ("R0020", 5m, 1, 0m), ("S[040", -339m, 1, 0m)],
            [new StockBalanceDto(1, "R0019", "", null, 5892.4m, 0), new StockBalanceDto(2, "R0020", "", null, 5m, 0)], registered);

        Assert.True(result.Passed, string.Join("; ", result.Unaccepted.Select(d => d.Cause)));
        Assert.Equal(2, result.Differences.Count(d => d.Origin == PurchDifference.Data));
    }

    [Fact] // AC-24: a count changed after the evaluation is the data; ทัน/ไม่ทัน worked out from the dates is agreed (DQ-11); any other difference fails
    public void Evaluation_counts_tell_the_data_the_agreed_and_the_faults()
    {
        PurchEvaluationCounts C(string name, int passed, int failed, int onTime, int late) => new(name, passed, failed, 0, passed + failed, onTime, late);
        var kept = new[] { C("ผู้ขาย ก", 10, 0, 10, 0), C("ผู้ขาย ข", 5, 1, 6, 0) };
        var today = new[] { C("ผู้ขาย ก", 11, 0, 11, 0), C("ผู้ขาย ข", 5, 1, 6, 0) };

        var accepted = PurchComparison.CompareEvaluations(kept, today, [C("ผู้ขาย ก", 11, 0, 11, 0), C("ผู้ขาย ข", 5, 1, 5, 1)]);
        Assert.True(accepted.Passed, string.Join("; ", accepted.Unaccepted.Select(d => d.Cause)));
        Assert.Contains(accepted.Differences, d => d.Origin == PurchDifference.Data);
        Assert.Contains(accepted.Differences, d => d.Origin == PurchDifference.Agreed && d.Cause.Contains("DQ-11", StringComparison.Ordinal));

        Assert.False(PurchComparison.CompareEvaluations(kept, today, [C("ผู้ขาย ก", 11, 0, 11, 0), C("ผู้ขาย ข", 4, 2, 6, 0)]).Passed);
    }

    private static PurchOrderRow Order(string no, int line, decimal price, decimal qty, decimal total, decimal? net = null, decimal? tax = null)
        => new(true, no, line, null, price, qty, 0, net ?? price * qty, tax ?? 0, total, true, false);

    [Fact] // AC-07: BR-022 on the legacy rows, by print; #4 (NET_AMT not worked out again), several numbers printed at once, the rest to a person
    public void Order_totals_match_the_formula_or_tell_why_not()
    {
        var result = PurchComparison.CompareOrderTotals(
        [
            Order("690956", 1, 129.20m, 1, 274.67m), Order("690956", 2, 127.50m, 1, 274.67m), // BR-022 #2: 256.70 + VAT = CutDecimal(274.669)
            Order("690974", 1, 1m, 30000, 33705m, net: 31500m),                                  // BR-021 #4: the quantity edited after the price
            Order("690990", 1, 100m, 1, 321m), Order("690991", 1, 200m, 1, 321m),                // two numbers printed at once
            Order("690992", 1, 100m, 1, 999m),                                                    // nothing explains it
        ], 0.07m, out var explained);

        Assert.Equal(1, result.Matched);
        Assert.Equal(3, explained);
        Assert.Contains(result.Differences, d => d.Origin == PurchDifference.Agreed && d.Cause.Contains("#4", StringComparison.Ordinal));
        Assert.Equal(2, result.Differences.Count(d => d.Cause.Contains("ในครั้งเดียว", StringComparison.Ordinal)));
        Assert.Single(result.Differences, d => d.Origin == PurchDifference.Unknown);
    }

    [Fact] // TOTAL is a Single: above 131,072 one step is more than a satang
    public void A_total_is_the_same_within_a_satang_or_one_step_of_a_single()
    {
        Assert.True(PurchComparison.SameTotal(255_787.99m, 255_788m));
        Assert.True(PurchComparison.SameTotal(6_826.60m, 6_826.60009765625m));
        Assert.False(PurchComparison.SameTotal(100.00m, 100.02m));
    }

    [Fact]
    public void A_rehearsal_cut_must_name_the_database()
    {
        string[] args = ["--program", "compare-purch", "--baseline", "b", "--out", "o", "--connection", "Server=x;Database=LockteMatTest", "--rehearse-cut", "2025-10-31"];
        Assert.Null(MigrationOptions.Parse(args).Options);
        var (options, error) = MigrationOptions.Parse([.. args, "--confirm", "LockteMatTest"]);
        Assert.Null(error);
        Assert.Equal(new DateOnly(2025, 10, 31), options!.CutDate);
        Assert.Null(MigrationOptions.Parse(args[..^2]).Options!.CutDate); // without it the comparison only reads
    }
}
