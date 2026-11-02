using System.Globalization;
using System.Text;
using Lockte.Cash;
using Microsoft.Extensions.DependencyInjection;

namespace Lockte.Migration;

/// <summary>
/// A cell or a row that differs between what Access gave (the baseline) and what the new system gives from the migrated data. <see cref="Cause"/> says why; <see cref="Accepted"/> is true
/// only for what the tickets accept (data the migration leaves out and lists in the DQ report, names merged by the code map, the head of a receipt taken from its first line, a sum beyond two
/// decimals rounded line by line) — a difference nobody accepts is a fault of the program or of the load, and fails the gate. <see cref="Origin"/> tells which: <c>ข้อมูล</c> or <c>โปรแกรม</c>.
/// </summary>
public sealed record CashDifference(string Report, string Range, string Key, string Baseline, string New, string Cause, bool Accepted)
{
    public string Origin => Accepted ? "ข้อมูล" : "โปรแกรม";
}

/// <summary>The comparison of one report over one range: rows each side had, rows that matched, the accepted exceptions and the rest.</summary>
public sealed record CashRangeComparison(string Report, string Range, int BaselineRows, int NewRows, int Matched, bool SameOrder, IReadOnlyList<CashDifference> Differences)
{
    public IReadOnlyList<CashDifference> Unaccepted => Differences.Where(d => !d.Accepted).ToList();

    public bool Passed => Unaccepted.Count == 0 && SameOrder;
}

/// <summary>One line of the gate: what was expected, what the migrated data gave, and whether it passed (or is left to a person).</summary>
public sealed record CashGateLine(string Id, string Check, string Expected, string Actual, bool? Passed, string Covers);

/// <summary>A line of the baseline of RPT-04 / RPT-09, as typed (text) before it is compared.</summary>
public sealed record CashBaselineLine(DateOnly? Date, string No, string Seq, string Item, decimal? Qty, decimal Amount, string Other);

/// <summary>
/// ด่านเทียบผลเงินสดย่อยกับ Access (#248, AC-01, AC-16, AC-23, AC-24): the baseline is what the queries of the old reports gave on a copy of the file
/// (<c>tools/baseline/export_cash_baseline.py</c>: RPT-02, RPT-04, RPT-08, RPT-09 over many ranges, the totals of every receipt, the sizes); the new system calculates the same reports from the
/// migrated data and the two are put side by side, row by row. What matches is counted; what differs is classified, never adjusted to match: a row the migration left out and listed (a
/// line the legacy would have deleted when the screen was left, BR-003/BR-014), names merged by the code map (shown from both sides: by the raw name, and by the type after merging), a
/// receipt whose head came from its first line (DQ-Q16), a total beyond two decimals (AC-24) — all accepted with their reason; everything else fails the gate. Real data stays in the details
/// file outside git. The documents are checked by eye at their own level (the voucher and the receipt are level A: laid over the sample, signed by the user).
/// </summary>
public static class CashComparison
{
    private const decimal Tolerance = 0.005m;

    public sealed record Result(IReadOnlyList<CashRangeComparison> Ranges, IReadOnlyList<CashGateLine> Gate)
    {
        public bool Passed => Ranges.All(r => r.Passed) && Gate.All(g => g.Passed != false);
    }

    // ---------- RPT-02 สรุปค่าใช้จ่ายตามประเภท ----------

    /// <summary>
    /// RPT-02 of one range. The baseline groups by the raw TYPE; here the names are cleaned (blanks, line breaks) and merged by the code map, and each merged group must equal the type of the
    /// new system to the satang. A blank TYPE is a line the migration did not load. Pure: no file, no database.
    /// </summary>
    public static CashRangeComparison CompareExpenseSummary(string range, IReadOnlyList<(string Type, decimal Total)> baseline, IReadOnlyList<ExpenseTypeTotal> now, CodeMap map)
    {
        var differences = new List<CashDifference>();
        var merged = new Dictionary<string, (decimal Total, List<string> Raw)>(StringComparer.OrdinalIgnoreCase);
        foreach (var (raw, total) in baseline)
        {
            if (CashMigrationRules.Name(raw) is not { } clean)
            {
                differences.Add(new("RPT-02", range, "(ว่าง)", Money(total), "ไม่โหลด", "บรรทัดไม่มีประเภท ไม่ย้าย (ระบบเดิมลบแถวนี้ BR-003)", true));
                continue;
            }
            var name = map.Find(CodeKinds.CashExpenseType, clean)?.Code is { } code ? CashMigrationRules.Name(code) ?? clean : clean;
            var (sum, names) = merged.GetValueOrDefault(name, (0m, []));
            names.Add(raw);
            merged[name] = (sum + total, names);
        }
        foreach (var (name, (_, raw)) in merged.Where(m => m.Value.Raw.Count > 1))
            differences.Add(new("RPT-02", range, name, $"{raw.Count.ToString(CultureInfo.InvariantCulture)} ชื่อดิบ", "1 ประเภท", "รวมชื่อดิบเป็นประเภทเดียว (ตัดช่องว่าง/ขึ้นบรรทัดใหม่/ไฟล์จับคู่)", true));

        var byName = now.GroupBy(t => t.TypeName, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.Sum(t => t.Total), StringComparer.OrdinalIgnoreCase);
        var matched = 0;
        foreach (var (name, (total, _)) in merged)
        {
            if (!byName.TryGetValue(name, out var current)) differences.Add(new("RPT-02", range, name, Money(total), "ไม่มีประเภทนี้", "ประเภทหายจากรายงานใหม่", false));
            else if (Math.Abs(current - total) >= Tolerance) differences.Add(new("RPT-02", range, name, Money(total), Money(current), "ยอดต่อประเภทไม่ตรง", false));
            else matched++;
        }
        foreach (var name in byName.Keys.Where(n => !merged.ContainsKey(n))) differences.Add(new("RPT-02", range, name, "ไม่มี", Money(byName[name]), "ประเภทเกินในรายงานใหม่", false));
        return new CashRangeComparison("RPT-02", range, merged.Count, byName.Count, matched, true, differences);
    }

    // ---------- RPT-04 รายการซื้อของ ----------

    /// <summary>RPT-04 of one range: the lines row by row — what is in both, what is in one only, and the order (date, then the number as text).</summary>
    public static CashRangeComparison CompareLines(string range, IReadOnlyList<CashBaselineLine> baseline, IReadOnlyList<ExpenseLineDto> now)
    {
        var differences = new List<CashDifference>();
        var loadable = new List<CashBaselineLine>();
        foreach (var line in baseline)
        {
            if (line.Date is null || line.No.Length == 0 || line.Item.Length == 0 || CashMigrationRules.Name(line.Other) is null)
                differences.Add(new("RPT-04", range, Key(line), Money(line.Amount), "ไม่โหลด", "บรรทัดไม่ครบวันที่/เลขที่/รายการ/ประเภท ไม่ย้าย (ระบบเดิมลบแถวนี้ BR-003)", true));
            else loadable.Add(line);
        }
        var left = loadable.GroupBy(LineKey).ToDictionary(g => g.Key, g => g.Count());
        var right = now.GroupBy(l => ((DateOnly?)l.Date, l.VoucherNo.Trim(), l.Name.Trim(), (decimal?)Round(l.Qty, 3), Round(l.Amount, 2))).ToDictionary(g => g.Key, g => g.Count());
        var matched = 0;
        foreach (var (key, count) in left)
        {
            var same = Math.Min(count, right.GetValueOrDefault(key));
            matched += same;
            if (count > same) differences.Add(new("RPT-04", range, KeyText(key), $"{count.ToString(CultureInfo.InvariantCulture)} แถว", $"{same.ToString(CultureInfo.InvariantCulture)} แถว", "แถวหายจากรายงานใหม่", false));
        }
        foreach (var (key, count) in right)
            if (count > left.GetValueOrDefault(key)) differences.Add(new("RPT-04", range, KeyText(key), "ไม่มี", $"{(count - left.GetValueOrDefault(key)).ToString(CultureInfo.InvariantCulture)} แถว", "แถวเกินในรายงานใหม่", false));

        var order = loadable.Select(l => (l.Date!.Value, l.No)).ToList();
        var nowOrder = now.Select(l => (l.Date, l.VoucherNo.Trim())).ToList();
        var sameOrder = order.SequenceEqual(nowOrder);
        if (!sameOrder && order.Count == nowOrder.Count)
            differences.Add(new("RPT-04", range, "(ลำดับ)", "เรียงตามวันที่ แล้วเลขที่แบบข้อความ ตามเดิม", "ต่าง", "ลำดับแถวไม่ตรงรายงานเดิม", false));
        return new CashRangeComparison("RPT-04", range, baseline.Count, now.Count, matched, sameOrder || order.Count != nowOrder.Count, differences);
    }

    private static (DateOnly?, string, string, decimal?, decimal) LineKey(CashBaselineLine l) => (l.Date, l.No.Trim(), l.Item.Trim(), l.Qty is { } q ? Round(q, 3) : null, Round(l.Amount, 2));

    private static string Key(CashBaselineLine l) => $"{(l.Date is { } d ? CashLineRules.Iso(d) : "(ไม่มีวันที่)")} {l.No} {l.Item}".Trim();

    private static string KeyText((DateOnly? Date, string No, string Item, decimal? Qty, decimal Amount) k) => $"{(k.Date is { } d ? CashLineRules.Iso(d) : "")} {k.No} {k.Item}".Trim();

    // ---------- RPT-09 ยอดขายของเก่า ----------

    /// <summary>
    /// RPT-09 of one range: the sold lines row by row. A line of a receipt whose first line has another date (DQ-Q16) is not on the date the old report gave — accepted with its reason when the
    /// receipt is in the new system under the date of its head.
    /// </summary>
    public static CashRangeComparison CompareSales(string range, IReadOnlyList<CashBaselineLine> baseline, IReadOnlyList<ScrapSaleRow> now, IReadOnlyDictionary<string, DateOnly> receiptDates,
        IReadOnlyDictionary<string, IReadOnlySet<DateOnly>>? baselineDates = null)
    {
        var differences = new List<CashDifference>();
        var loadable = new List<CashBaselineLine>();
        foreach (var line in baseline)
        {
            if (line.No.Length == 0 || line.Item.Length == 0 || line.Qty is null)
                differences.Add(new("RPT-09", range, Key(line), Money(line.Amount), "ไม่โหลด", "บรรทัดไม่มีเลขที่/รายการ/จำนวน ไม่ย้าย (ระบบเดิมลบแถวนี้ BR-014)", true));
            else
            {
                if (CashMigrationRules.Name(line.Item) != line.Item.Trim())
                    differences.Add(new("RPT-09", range, $"{line.No} {CashMigrationRules.Name(line.Item)}", "ช่องว่างซ้อนในชื่อรายการ", "ตัดเหลือช่องว่างเดียว", "ชื่อรายการตัดช่องว่างซ้อน (ย้ายข้อมูลทำเหมือนหน้าจอ)", true));
                loadable.Add(line);
            }
        }
        var left = loadable.GroupBy(SaleKey).ToDictionary(g => g.Key, g => g.ToList());
        var right = now.GroupBy(r => ((DateOnly?)r.Date, r.No.Trim(), r.ItemName.Trim(), (decimal?)Round(r.Qty, 3), Round(r.Amount, 2))).ToDictionary(g => g.Key, g => g.Count());
        var matched = 0;
        foreach (var (key, lines) in left)
        {
            var same = Math.Min(lines.Count, right.GetValueOrDefault(key));
            matched += same;
            if (lines.Count == same) continue;
            if (receiptDates.TryGetValue(key.Item2, out var head) && head != key.Item1)
                differences.Add(new("RPT-09", range, $"{key.Item2} {key.Item3}", $"วันที่ {CashLineRules.Iso(key.Item1!.Value)}", $"วันที่ {CashLineRules.Iso(head)}", "ใบที่หัวต่างกัน ใช้วันที่ของบรรทัดแรก (DQ-Q16)", true));
            else differences.Add(new("RPT-09", range, $"{key.Item2} {key.Item3}", $"{lines.Count.ToString(CultureInfo.InvariantCulture)} แถว", $"{same.ToString(CultureInfo.InvariantCulture)} แถว", "แถวหายจากรายงานใหม่", false));
        }
        foreach (var (key, count) in right)
        {
            var have = left.TryGetValue(key, out var l) ? l.Count : 0;
            if (count <= have) continue;
            if (baselineDates is not null && baselineDates.TryGetValue(key.Item2, out var dates) && key.Item1 is { } date && dates.Any(d => d != date))
                differences.Add(new("RPT-09", range, $"{key.Item2} {key.Item3}", "วันที่อื่นในรายงานเดิม", $"วันที่ {CashLineRules.Iso(date)}", "ใบที่หัวต่างกัน ใช้วันที่ของบรรทัดแรก (DQ-Q16)", true));
            else differences.Add(new("RPT-09", range, $"{key.Item2} {key.Item3}", "ไม่มี", $"{(count - have).ToString(CultureInfo.InvariantCulture)} แถว", "แถวเกินในรายงานใหม่", false));
        }
        var order = loadable.Where(l => receiptDates.GetValueOrDefault(l.No) is var d && (d == default || d == l.Date)).Select(l => (l.Date!.Value, l.No)).ToList();
        var nowOrder = now.Select(r => (r.Date, r.No)).ToList();
        var sameOrder = order.Count != nowOrder.Count || order.SequenceEqual(nowOrder);
        return new CashRangeComparison("RPT-09", range, baseline.Count, now.Count, matched, sameOrder, differences);
    }

    private static (DateOnly?, string, string, decimal?, decimal) SaleKey(CashBaselineLine l) => (l.Date, l.No.Trim(), CashMigrationRules.Name(l.Item) ?? "", l.Qty is { } q ? Round(q, 3) : null, Round(l.Amount, 2));

    // ---------- RPT-08 ยอดรอนำส่ง, AC-23 ----------

    /// <summary>
    /// RPT-08: the numbers waiting, with their totals (above 0 as the report shows them), against the unremitted receipts of the new system — and AC-23: the same set of numbers, waiting
    /// for the same receipts as the old "ค้างส่ง" table, all of them.
    /// </summary>
    public static (CashRangeComparison Report, CashGateLine Gate) CompareWaiting(IReadOnlyList<(string No, decimal Total, int Lines)> baseline, IReadOnlyList<ScrapReceiptDto> now)
    {
        var differences = new List<CashDifference>();
        var byNo = now.ToDictionary(r => r.No, StringComparer.Ordinal);
        var matched = 0;
        foreach (var (no, total, lines) in baseline.OrderBy(b => b.No, Comparer<string>.Create(ScrapReceiptMath.Compare)))
        {
            if (!byNo.TryGetValue(no, out var receipt)) { differences.Add(new("RPT-08", "-", no, Money(total), "ไม่รอนำส่ง", "ใบที่ค้างส่งเดิมไม่รอนำส่งในระบบใหม่", false)); continue; }
            if (receipt.Lines.Count != lines) differences.Add(new("RPT-08", "-", no, $"{lines.ToString(CultureInfo.InvariantCulture)} บรรทัด", $"{receipt.Lines.Count.ToString(CultureInfo.InvariantCulture)} บรรทัด", "จำนวนบรรทัดของใบที่รอนำส่งไม่ตรง", false));
            else if (Math.Abs(receipt.Total - total) >= Tolerance) differences.Add(new("RPT-08", "-", no, Money(total), Money(receipt.Total), "ยอดใบที่รอนำส่งไม่ตรง", false));
            else matched++;
        }
        var known = baseline.Select(b => b.No).ToHashSet(StringComparer.Ordinal);
        foreach (var receipt in now.Where(r => !known.Contains(r.No))) differences.Add(new("RPT-08", "-", receipt.No, "ไม่ค้างส่ง", Money(receipt.Total), "ใบรอนำส่งเกินจากตารางค้างส่งเดิม", false));
        var same = baseline.Count == now.Count && !differences.Any(d => d.Cause.Contains("ไม่รอนำส่ง", StringComparison.Ordinal) || d.Cause.Contains("เกินจาก", StringComparison.Ordinal));
        var report = new CashRangeComparison("RPT-08", "-", baseline.Count, now.Count, matched, true, differences);
        return (report, new CashGateLine("AC-23", "ใบรอนำส่งตรงตารางค้างส่ง (ขายของเก่า1) ทุกใบ ทุกบรรทัด", $"{baseline.Count.ToString(CultureInfo.InvariantCulture)} ใบ {baseline.Sum(b => b.Lines).ToString(CultureInfo.InvariantCulture)} บรรทัด",
            $"{now.Count.ToString(CultureInfo.InvariantCulture)} ใบ {now.Sum(r => r.Lines.Count).ToString(CultureInfo.InvariantCulture)} บรรทัด ตรงทั้งใบ {matched.ToString(CultureInfo.InvariantCulture)}", same && report.Unaccepted.Count == 0, "#240 #241"));
    }

    // ---------- AC-24 ยอดรวมใบเสร็จ ----------

    /// <summary>
    /// AC-24: the total of every receipt against what Access's query gave (the Single amounts added as a Double, then rounded to two decimals half to even). A receipt whose lines add up beyond
    /// two decimals is rounded line by line here before it is added, and may differ — accepted, listed with both values; a difference on any other receipt is not.
    /// </summary>
    public static CashRangeComparison CompareReceiptTotals(IReadOnlyList<(string No, int Lines, double Sum, decimal AccessTotal)> baseline, IReadOnlyList<ScrapReceiptDto> now)
    {
        var differences = new List<CashDifference>();
        var byNo = now.ToDictionary(r => r.No, StringComparer.Ordinal);
        var matched = 0;
        foreach (var (no, _, sum, access) in baseline)
        {
            if (!byNo.TryGetValue(no, out var receipt)) { differences.Add(new("AC-24", "-", no, Money(access), "ไม่โหลด", "ใบเสร็จไม่อยู่ในฐานข้อมูล", false)); continue; }
            if (receipt.Total == access) { matched++; continue; }
            var beyond = Math.Abs(sum * 100 - Math.Round(sum * 100)) > 1e-6;
            differences.Add(new("AC-24", "-", no, Money(access), Money(receipt.Total), beyond ? "ผลรวมเกิน 2 ตำแหน่ง ปัดทีละบรรทัดก่อนรวม (RND-A) ต่างจาก Access" : "ยอดรวมใบเสร็จไม่ตรง", beyond));
        }
        return new CashRangeComparison("AC-24", "-", baseline.Count, now.Count, matched, true, differences);
    }

    // ---------- AC-01 ----------

    public static IReadOnlyList<CashGateLine> CompareSizes(IReadOnlyDictionary<string, string> baseline, int lines, decimal lineAmount, int receipts, int receiptLines)
    {
        string B(string key) => baseline.GetValueOrDefault(key) ?? "0";
        var expectedLines = int.Parse(B("expense_lines_loadable"), CultureInfo.InvariantCulture);
        var expectedAmount = decimal.Parse(B("expense_amount_loadable"), CultureInfo.InvariantCulture);
        var expectedReceipts = int.Parse(B("receipt_numbers"), CultureInfo.InvariantCulture);
        var expectedReceiptLines = int.Parse(B("receipt_lines_loadable"), CultureInfo.InvariantCulture);
        var left = int.Parse(B("expense_lines_all"), CultureInfo.InvariantCulture) - expectedLines;
        return
        [
            new("AC-01", "บรรทัดค่าใช้จ่ายที่ย้ายได้", $"{expectedLines.ToString("N0", CultureInfo.InvariantCulture)} (ต้นทางทั้งหมด {B("expense_lines_all")}, ไม่ย้าย {left.ToString(CultureInfo.InvariantCulture)} ตามรายงาน DQ)", lines.ToString("N0", CultureInfo.InvariantCulture), lines == expectedLines, "#236"),
            new("AC-01", "ยอดรวมจำนวนเงินของบรรทัดค่าใช้จ่าย", Money(expectedAmount), Money(lineAmount), Math.Abs(lineAmount - expectedAmount) < 0.05m, "#236"),
            new("AC-01", "ใบเสร็จขายของเก่า (จำนวนเลขที่)", expectedReceipts.ToString("N0", CultureInfo.InvariantCulture), receipts.ToString("N0", CultureInfo.InvariantCulture), receipts == expectedReceipts, "#241"),
            new("AC-01", "บรรทัดของใบเสร็จที่ย้ายได้", expectedReceiptLines.ToString("N0", CultureInfo.InvariantCulture), receiptLines.ToString("N0", CultureInfo.InvariantCulture), receiptLines == expectedReceiptLines, "#241"),
        ];
    }

    // ---------- the run ----------

    public const string RangesFile = "cash-ranges.csv";

    public static async Task<Result> RunAsync(IServiceProvider services, string baselineFolder, CodeMap map)
    {
        await using var scope = services.CreateAsyncScope();
        var lines = scope.ServiceProvider.GetRequiredService<ExpenseLineDirectory>();
        var receipts = scope.ServiceProvider.GetRequiredService<ScrapReceiptDirectory>();
        var remittances = scope.ServiceProvider.GetRequiredService<ScrapRemittanceDirectory>();

        var ranges = Comparison.Read(baselineFolder, RangesFile).Select(r => (Id: r["id"], From: Date(r["from"])!.Value, To: Date(r["to"])!.Value)).ToList();
        var rpt02 = Comparison.Read(baselineFolder, "cash-rpt02.csv").ToList();
        var rpt04 = Comparison.Read(baselineFolder, "cash-rpt04.csv").ToList();
        var rpt09 = Comparison.Read(baselineFolder, "cash-rpt09.csv").ToList();

        var allLines = await lines.ListAsync(DateOnly.MinValue, DateOnly.MaxValue);
        var allReceipts = await receipts.ListAsync(DateOnly.MinValue, DateOnly.MaxValue);
        var receiptDates = allReceipts.ToDictionary(r => r.No, r => r.Date, StringComparer.Ordinal);
        var baselineDates = rpt09.Where(r => Date(r["date"]) is not null && r["no"].Length > 0).GroupBy(r => r["no"], StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => (IReadOnlySet<DateOnly>)g.Select(r => Date(r["date"])!.Value).ToHashSet(), StringComparer.Ordinal);
        var results = new List<CashRangeComparison>();
        foreach (var (id, from, to) in ranges)
        {
            var summary = await lines.SummaryByTypeAsync(from, to);
            results.Add(CompareExpenseSummary(id, rpt02.Where(r => r["range"] == id).Select(r => (r["type"], Dec(r["total"]))).ToList(), summary, map));
            results.Add(CompareLines(id, rpt04.Where(r => r["range"] == id).Select(r => new CashBaselineLine(Date(r["date"]), r["suf"], "", r["name"], DecOrNull(r["qty"]), Dec(r["amount"]), r["type"])).ToList(),
                CashReportRows.Purchases(from, to, allLines)));
            results.Add(CompareSales(id, rpt09.Where(r => r["range"] == id).Select(r => new CashBaselineLine(Date(r["date"]), r["no"], r["suf"], r["names"], DecOrNull(r["qty"]), Dec(r["amount"]), r["named"])).ToList(),
                CashReportRows.Sales(from, to, allReceipts), receiptDates, baselineDates));
        }

        var waitingNow = await remittances.UnremittedAsync();
        var waitingBase = Comparison.Read(baselineFolder, "cash-rpt08.csv").Select(r => (No: r["no"], Total: Dec(r["total"]), Lines: int.Parse(r["lines"], CultureInfo.InvariantCulture))).ToList();
        var (waitingReport, waitingGate) = CompareWaiting(waitingBase, waitingNow);
        results.Add(waitingReport);
        var totals = CompareReceiptTotals(Comparison.Read(baselineFolder, "cash-receipts.csv").Select(r => (r["no"], int.Parse(r["lines"], CultureInfo.InvariantCulture),
            double.Parse(r["sum"], CultureInfo.InvariantCulture), Dec(r["access_total"]))).ToList(), allReceipts);
        results.Add(totals);

        var gate = new List<CashGateLine>();
        var sizes = Comparison.Read(baselineFolder, "cash-summary.csv").ToDictionary(r => r["metric"], r => r["value"]);
        gate.AddRange(CompareSizes(sizes, allLines.Count, allLines.Sum(l => l.Amount), allReceipts.Count, allReceipts.Sum(r => r.Lines.Count)));
        foreach (var report in new[] { "RPT-02", "RPT-04", "RPT-09" })
        {
            var of = results.Where(r => r.Report == report).ToList();
            gate.Add(new CashGateLine("AC-16", $"{report} ตรงรายงานเดิมทุกช่วง ({of.Count.ToString(CultureInfo.InvariantCulture)} ช่วง) ทุกแถว", $"ตรง 100% ของข้อมูลที่ย้ายได้",
                $"ตรงทั้งช่วง {of.Count(r => r.Passed).ToString(CultureInfo.InvariantCulture)}/{of.Count.ToString(CultureInfo.InvariantCulture)} · ไม่ยอมรับ {of.Sum(r => r.Unaccepted.Count).ToString(CultureInfo.InvariantCulture)} · ยอมรับพร้อมเหตุผล {of.Sum(r => r.Differences.Count(d => d.Accepted)).ToString(CultureInfo.InvariantCulture)}",
                of.All(r => r.Passed), "#242 #243 #244"));
        }
        gate.Add(new CashGateLine("AC-16", "RPT-08 ยอดรอนำส่งตรงรายงานเดิมทุกใบ", $"{waitingBase.Count.ToString(CultureInfo.InvariantCulture)} ใบ",
            $"ตรง {waitingReport.Matched.ToString(CultureInfo.InvariantCulture)} ไม่ยอมรับ {waitingReport.Unaccepted.Count.ToString(CultureInfo.InvariantCulture)}", waitingReport.Passed, "#240"));
        gate.Add(waitingGate);
        gate.Add(new CashGateLine("AC-24", "ยอดรวมใบเสร็จทุกใบ (RND-A) ตรงที่ Access คำนวณ — ใบที่ผลรวมเกิน 2 ตำแหน่งแจกแจงเหตุ", $"{totals.BaselineRows.ToString(CultureInfo.InvariantCulture)} ใบ",
            $"ตรง {totals.Matched.ToString(CultureInfo.InvariantCulture)} · ต่างพร้อมเหตุผล {totals.Differences.Count(d => d.Accepted).ToString(CultureInfo.InvariantCulture)} · ต่างไม่ยอมรับ {totals.Unaccepted.Count.ToString(CultureInfo.InvariantCulture)}", totals.Passed, "#237 #241"));
        gate.Add(new CashGateLine("RPT-01, RPT-06", "ใบสำคัญจ่ายและใบเสร็จ (ระดับ A) วางซ้อนตัวอย่าง + ผู้ใช้เซ็นรับ", "ตรวจด้วยตา", "ดู tools/form-overlay (ผู้ใช้เซ็นรับเอกสารระดับ A แล้ว 2 พ.ย. 2569)", null, "#234 #238"));
        return new Result(results, gate);
    }

    public static void WriteTo(string folder, Result result)
    {
        Directory.CreateDirectory(folder);
        Csv.Write(Path.Combine(folder, "cash-compare.csv"), ["รายงาน", "ช่วง", "แถวต้นทาง", "แถวในฐาน", "ตรง", "ลำดับ", "ยอมรับ", "ไม่ยอมรับ", "ผล"],
            result.Ranges.Select(r => new object?[] { r.Report, r.Range, r.BaselineRows, r.NewRows, r.Matched, r.SameOrder ? "ตรง" : "ไม่ตรง", r.Differences.Count(d => d.Accepted), r.Unaccepted.Count, r.Passed ? "ผ่าน" : "ไม่ผ่าน" }));
        // real values: kept outside git. One list: what differs, why, and whether the fault is in the data or in the program
        Csv.Write(Path.Combine(folder, "cash-compare-details.csv"), ["รายงาน", "ช่วง", "รายการ", "ต้นทาง", "ฐานข้อมูล", "สาเหตุ", "ที่มาของความต่าง", "ยอมรับ"],
            result.Ranges.SelectMany(r => r.Differences).Select(d => new object?[] { d.Report, d.Range, d.Key, d.Baseline, d.New, d.Cause, d.Origin, d.Accepted ? "ยอมรับ" : "ไม่ยอมรับ" }));
        Csv.Write(Path.Combine(folder, "cash-gate.csv"), ["AC", "การตรวจ", "ควรได้", "ได้", "ผล", "ตั๋ว"],
            result.Gate.Select(g => new object?[] { g.Id, g.Check, g.Expected, g.Actual, g.Passed is null ? "ตรวจด้วยตา/เทสต์" : g.Passed.Value ? "ผ่าน" : "ไม่ผ่าน", g.Covers }));
        var summary = new StringBuilder("# เทียบผลเงินสดย่อยกับ Access\n\n").AppendLine(result.Passed ? "**ผ่านทุกข้อที่ตรวจอัตโนมัติ**" : "**มีข้อที่ไม่ผ่าน**").AppendLine();
        summary.AppendLine("| AC | การตรวจ | ควรได้ | ได้ | ผล |").AppendLine("|---|---|---|---|---|");
        foreach (var g in result.Gate) summary.AppendLine($"| {g.Id} | {g.Check} | {g.Expected} | {g.Actual} | {(g.Passed is null ? "ตรวจด้วยตา/เทสต์" : g.Passed.Value ? "ผ่าน" : "ไม่ผ่าน")} |");
        File.WriteAllText(Path.Combine(folder, "cash-summary.md"), summary.ToString(), new UTF8Encoding(true));
    }

    private static decimal Round(decimal value, int places) => Math.Round(value, places, MidpointRounding.AwayFromZero);

    private static string Money(decimal value) => value.ToString("N2", CultureInfo.InvariantCulture);

    private static DateOnly? Date(string text) => DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

    private static decimal Dec(string text) => text.Length == 0 ? 0m : decimal.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);

    private static decimal? DecOrNull(string text) => text.Length == 0 ? null : decimal.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
}
