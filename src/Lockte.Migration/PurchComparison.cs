using System.Globalization;
using System.Text;
using Lockte.Org;
using Lockte.Purch;
using Microsoft.Extensions.DependencyInjection;

namespace Lockte.Migration;

/// <summary>
/// A cell or a row that differs between what Access gave (the baseline) and what the new system gives from the migrated data. <see cref="Origin"/> says where the difference comes from:
/// <see cref="Data"/> (the legacy data: left out and listed in the DQ report, edited after printing, changed after the evaluation — accepted with the reason), <see cref="Agreed"/> (the new system
/// does it differently on purpose: an AC, a DQ decision or a ข้อสมมติ — accepted, with the reference), <see cref="Unknown"/> (no reason found: a person looks) or <see cref="Program"/> (a fault of
/// the program or of the load: fails the gate).
/// </summary>
public sealed record PurchDifference(string Report, string Range, string Key, string Baseline, string New, string Cause, string Origin)
{
    public const string Data = "ข้อมูล";
    public const string Agreed = "ต่างจากเดิมตามที่ตกลง";
    public const string Unknown = "ยังไม่ทราบสาเหตุ";
    public const string Program = "โปรแกรม";

    public bool Accepted => Origin is Data or Agreed;
}

/// <summary>The comparison of one report over one range: rows each side had, rows that matched, the differences (accepted or not) and whether the common rows come in the same order.</summary>
public sealed record PurchRangeComparison(string Report, string Range, int BaselineRows, int NewRows, int Matched, bool SameOrder, IReadOnlyList<PurchDifference> Differences)
{
    public IReadOnlyList<PurchDifference> Unaccepted => Differences.Where(d => !d.Accepted).ToList();

    public bool Passed => Unaccepted.Count == 0 && SameOrder;
}

/// <summary>One line of the gate: what was expected, what the migrated data gave, and whether it passed (null = left to a person or to the tests).</summary>
public sealed record PurchGateLine(string Id, string Check, string Expected, string Actual, bool? Passed, string Covers);

/// <summary>A row of RPT-04 as Access has it; <see cref="Listed"/> is false for a waiting row with a วันที่รับ typed in but never confirmed, which the old report leaves out (BR-030).</summary>
public sealed record PurchBacklogRow(DateOnly? OrderDate, string OrderNo, int LineNo, string Name, string Unit, decimal Quantity, DateOnly? DesiredDate, bool Listed);

/// <summary>A row of an order in T-PO_RECEI4 / T-PO_RECEI5 (AC-07): the legacy keeps one row per delivery, with the head and the TOTAL of the print repeated.</summary>
public sealed record PurchOrderRow(bool Waiting, string OrderNo, int LineNo, DateOnly? ReceiveDate, decimal UnitPrice, decimal OrderedOrOutstanding, decimal Received, decimal NetAmount, decimal Tax, decimal Total, bool ChargesVat, bool Failed);

/// <summary>
/// A row of RPT-11 as the legacy query (meant to) list it; <see cref="Literal"/> is true only for a row the query as written lists (PUR_ID Null), <see cref="Leftover"/> for a leftover row of
/// the history (no วันที่รับ or name) that the migration does not move (Q-12).
/// </summary>
public sealed record PurchSmallOrderRow(string OrderNo, int LineNo, decimal Tax, decimal Total, bool Literal, bool Leftover = false);

/// <summary>The six counts of an evaluation (BR-050), keyed by the supplier's name as the legacy keys them.</summary>
public sealed record PurchEvaluationCounts(string Supplier, int Passed, int Failed, int WithCertificate, int WithoutCertificate, int OnTime, int Late)
{
    public int[] Values => [Passed, Failed, WithCertificate, WithoutCertificate, OnTime, Late];

    public static readonly string[] Names = ["ผ่าน", "ไม่ผ่าน", "มีใบรับรอง", "ไม่มีใบรับรอง", "ทัน", "ไม่ทัน"];
}

/// <summary>
/// ด่านเทียบผลวัตถุดิบกับ Access (#271): the baseline is what the queries of the old reports give on a copy of the file (<c>tools/baseline/export_purch_baseline.py</c>); the new system
/// computes the same reports from the migrated data and the two are put side by side, row by row — RPT-04 ค้างรับ, RPT-20 (AC-26), RPT-22 (AC-25), RPT-11 (AC-28) over many ranges, RPT-26 of
/// every loaded cut (AC-21, and AC-20 after a rehearsal cut), the balance of every code (AC-18), the counts of the evaluations of a quarter (AC-24), and the totals of the orders (AC-07, the
/// formula of BR-022 on the legacy rows). What matches is counted; what differs is classified, never adjusted to match. Real data stays in the details file outside git.
/// </summary>
public static class PurchComparison
{
    private const decimal Tolerance = 0.005m;
    private const decimal QuantityTolerance = 0.001m;

    public sealed record Result(IReadOnlyList<PurchRangeComparison> Ranges, IReadOnlyList<PurchGateLine> Gate)
    {
        public bool Passed => Gate.All(g => g.Passed != false);
    }

    /// <summary>The number of an order as the legacy wrote it: an order the migration split by supplier or VAT carries <c>-2</c>, <c>-3</c> (DQ-19).</summary>
    public static string BaseNo(string orderNo) => orderNo.IndexOf('-', StringComparison.Ordinal) is var i and > 0 ? orderNo[..i] : orderNo;

    // ---------- RPT-04 รายงานวัตถุดิบที่ยังไม่ได้รับ ----------

    /// <summary>
    /// RPT-04: every line waited for, row by row (date, name, unit, ยอดค้างรับ, วันที่ต้องการ) and in the order of the old report (PO_NO, PO_SUF). A waiting row with a วันที่รับ typed in but
    /// never confirmed is not in the old report but is still waited for here — the line is not received until it is (AC-30: ค้างรับ 37).
    /// </summary>
    public static PurchRangeComparison CompareBacklog(IReadOnlyList<PurchBacklogRow> baseline, IReadOnlyList<OutstandingReceiptLine> now)
    {
        var differences = new List<PurchDifference>();
        var byKey = now.GroupBy(l => (BaseNo(l.OrderNo), l.LineNo)).ToDictionary(g => g.Key, g => g.First());
        var matched = 0;
        foreach (var row in baseline)
        {
            var key = (row.OrderNo, row.LineNo);
            var found = byKey.GetValueOrDefault(key);
            var id = $"{row.OrderNo} ที่ {Int(row.LineNo)}";
            if (!row.Listed)
            {
                if (found is not null) differences.Add(new("RPT-04", "-", id, "ไม่แสดง (กรอกวันที่รับค้างไว้ ไม่ได้ยืนยัน)", $"ค้างรับ {Qty(found.Outstanding)}",
                    "แถวที่กรอกวันที่รับไว้แต่ไม่ยืนยันรับ (BR-030) ยังค้างรับในระบบใหม่ (AC-30 ค้างรับ 37)", PurchDifference.Agreed));
                continue;
            }
            if (found is null) { differences.Add(new("RPT-04", "-", id, $"ค้างรับ {Qty(row.Quantity)}", "ไม่มี", "แถวหายจากรายงานใหม่", PurchDifference.Program)); continue; }
            var problems = new List<string>();
            if (Math.Abs(found.Outstanding - row.Quantity) >= QuantityTolerance) problems.Add($"ค้างรับ {Qty(row.Quantity)} ≠ {Qty(found.Outstanding)}");
            if (!SameText(found.MaterialName, row.Name)) problems.Add("ชื่อวัตถุดิบ");
            if (!SameText(found.Unit, row.Unit)) problems.Add("หน่วย");
            if (found.DesiredDate != row.DesiredDate) problems.Add($"วันที่ต้องการ {Iso(row.DesiredDate)} ≠ {Iso(found.DesiredDate)}");
            if (found.OrderDate != row.OrderDate) problems.Add($"วันที่สั่งซื้อ {Iso(row.OrderDate)} ≠ {Iso(found.OrderDate)}");
            if (problems.Count == 0) matched++;
            else differences.Add(new("RPT-04", "-", id, "", "", string.Join(" · ", problems), PurchDifference.Program));
        }
        var known = baseline.Select(b => (b.OrderNo, b.LineNo)).ToHashSet();
        foreach (var line in now.Where(l => !known.Contains((BaseNo(l.OrderNo), l.LineNo))))
            differences.Add(new("RPT-04", "-", $"{line.OrderNo} ที่ {Int(line.LineNo)}", "ไม่มี", $"ค้างรับ {Qty(line.Outstanding)}", "แถวเกินในรายงานใหม่", PurchDifference.Program));

        var listed = baseline.Where(b => b.Listed).Select(b => (b.OrderNo, b.LineNo)).ToList();
        var order = now.Select(l => (BaseNo(l.OrderNo), l.LineNo)).Where(listed.Contains).ToList();
        var sameOrder = order.Count != listed.Count || order.SequenceEqual(listed);
        if (!sameOrder) differences.Add(new("RPT-04", "-", "(ลำดับ)", "เลขที่ใบสั่งซื้อ แล้วที่", "ต่าง", "ลำดับแถวไม่ตรงรายงานเดิม", PurchDifference.Program));
        return new PurchRangeComparison("RPT-04", "-", baseline.Count(b => b.Listed), now.Count, matched, sameOrder, differences);
    }

    // ---------- RPT-20 สรุปการรับวัตถุดิบเดือน (AC-26) ----------

    /// <summary>
    /// RPT-20 of one range: the head (deliveries, ใบขอซื้อ, ใบสั่งซื้อ counted once each) and the rows. The old report groups by the material's **name** and unit; the new one by the material
    /// (BR-061, #259) — the lines are grouped here by name and unit as the old one did so the rows can be compared one by one, and a different number of rows is shown, not counted against.
    /// </summary>
    public static PurchRangeComparison CompareMonthlySummary(string range, IReadOnlyList<(string Name, string Unit, decimal Quantity, decimal Amount)> baseline, (int Lines, int Requests, int Orders) head,
        IReadOnlyList<ReceivedLine> purchases)
    {
        var differences = new List<PurchDifference>();
        var byName = purchases.GroupBy(l => (Clean(l.MaterialName), Clean(l.Unit)))
            .ToDictionary(g => g.Key, g => (Quantity: g.Sum(l => l.Quantity), Amount: g.Sum(l => l.Amount)));
        var matched = 0;
        foreach (var (name, unit, quantity, amount) in baseline)
        {
            var key = (Clean(name), Clean(unit));
            if (!byName.TryGetValue(key, out var current)) { differences.Add(new("RPT-20", range, $"{name} ({unit})", Money(amount), "ไม่มี", "แถวหายจากรายงานใหม่", PurchDifference.Program)); continue; }
            if (Math.Abs(current.Amount - amount) >= Tolerance || Math.Abs(current.Quantity - quantity) >= QuantityTolerance)
                differences.Add(new("RPT-20", range, $"{name} ({unit})", $"{Qty(quantity)} / {Money(amount)}", $"{Qty(current.Quantity)} / {Money(current.Amount)}", "จำนวน/จำนวนเงินไม่ตรง", PurchDifference.Program));
            else matched++;
        }
        var known = baseline.Select(b => (Clean(b.Name), Clean(b.Unit))).ToHashSet();
        foreach (var (key, value) in byName.Where(n => !known.Contains(n.Key)))
            differences.Add(new("RPT-20", range, $"{key.Item1} ({key.Item2})", "ไม่มี", Money(value.Amount), "แถวเกินในรายงานใหม่", PurchDifference.Program));

        var requests = purchases.Select(l => l.RequestNo).Distinct(StringComparer.Ordinal).Count();
        var orders = purchases.Select(l => BaseNo(l.OrderNo)).Distinct(StringComparer.Ordinal).Count();
        if (purchases.Count != head.Lines) differences.Add(new("RPT-20", range, "(จำนวนรายการรับ)", Int(head.Lines), Int(purchases.Count), "จำนวนรายการรับไม่ตรง", PurchDifference.Program));
        if (requests != head.Requests) differences.Add(new("RPT-20", range, "(ใบขอซื้อ)", Int(head.Requests), Int(requests), "จำนวนใบขอซื้อไม่ตรง", PurchDifference.Program));
        if (orders != head.Orders) differences.Add(new("RPT-20", range, "(ใบสั่งซื้อ)", Int(head.Orders), Int(orders), "จำนวนใบสั่งซื้อไม่ตรง", PurchDifference.Program));
        var byMaterial = purchases.Select(l => l.MaterialId).Distinct().Count();
        if (byMaterial != byName.Count)
            differences.Add(new("RPT-20", range, "(แถวของรายงาน)", $"{Int(baseline.Count)} แถวตามชื่อ+หน่วย", $"{Int(byMaterial)} แถวตามรหัสวัตถุดิบ",
                "รายงานใหม่จัดกลุ่มตามรหัสวัตถุดิบ ไม่ใช่ชื่อ (BR-061 ก, #259) — ยอดรวมเท่ากัน", PurchDifference.Agreed));
        return new PurchRangeComparison("RPT-20", range, baseline.Count, byName.Count, matched, true, differences);
    }

    // ---------- RPT-22 การซื้อวัตถุดิบแยกแผนก (AC-25) ----------

    /// <summary>RPT-22 of one range: the amount of every แผนก. The old report groups by the name typed on the line (EMP_ID); it is matched to the แผนก the way the migration matched it.</summary>
    public static PurchRangeComparison CompareDepartments(string range, IReadOnlyList<(string Department, decimal Amount)> baseline, IReadOnlyList<(string Department, decimal Amount)> now, Func<string, string> departmentOf)
    {
        var differences = new List<PurchDifference>();
        var merged = baseline.GroupBy(b => departmentOf(b.Department), StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Sum(b => b.Amount), StringComparer.Ordinal);
        var current = now.ToDictionary(n => n.Department, n => n.Amount, StringComparer.Ordinal);
        var matched = 0;
        foreach (var (department, amount) in merged)
        {
            if (!current.TryGetValue(department, out var value)) differences.Add(new("RPT-22", range, department, Money(amount), "ไม่มี", "แผนกหายจากรายงานใหม่", PurchDifference.Program));
            else if (Math.Abs(value - amount) >= Tolerance) differences.Add(new("RPT-22", range, department, Money(amount), Money(value), "ยอดของแผนกไม่ตรง", PurchDifference.Program));
            else matched++;
        }
        foreach (var (department, value) in current.Where(c => !merged.ContainsKey(c.Key)))
            differences.Add(new("RPT-22", range, department, "ไม่มี", Money(value), "แผนกเกินในรายงานใหม่", PurchDifference.Program));
        var total = baseline.Sum(b => b.Amount);
        var totalNow = now.Sum(n => n.Amount);
        if (Math.Abs(total - totalNow) >= Tolerance) differences.Add(new("RPT-22", range, "(รวมทั้งหมด)", Money(total), Money(totalNow), "ยอดรวมไม่ตรง", PurchDifference.Program));
        return new PurchRangeComparison("RPT-22", range, merged.Count, current.Count, matched, true, differences);
    }

    // ---------- RPT-11 ใบสั่งซื้อต่ำกว่า 5,000 (AC-28) ----------

    /// <summary>
    /// RPT-11 of one range, line by line (ใบสั่งซื้อ + ที่, ยอดรายการรวม VAT). The old query keeps the rows whose PUR_ID is Null, meaning "not received, or the last delivery" (BR-063); in the
    /// delivered rows PUR_ID is an empty text, so as written it lists none of them — the rows it means to list are compared, and the rows it misses are told (AC-28 wants 690977, received, shown).
    /// A line delivered more than once is one line here. An order whose rows carry different TOTALs (printed more than once) is in or out by the total of the order.
    /// </summary>
    public static PurchRangeComparison CompareSmallOrders(string range, IReadOnlyList<PurchSmallOrderRow> baseline, IReadOnlyList<SmallOrderLine> now, IReadOnlySet<string> ordersWithManyTotals)
    {
        var differences = new List<PurchDifference>();
        var lines = baseline.GroupBy(b => (b.OrderNo, b.LineNo)).ToList();
        var byKey = now.GroupBy(l => (BaseNo(l.OrderNo), l.LineNo)).ToDictionary(g => g.Key, g => g.First());
        var matched = 0;
        var unlisted = lines.Count(g => !g.Any(r => r.Literal));
        if (unlisted > 0)
            differences.Add(new("RPT-11", range, $"({Int(unlisted)} รายการ)", "query เดิมตามตัวอักษรไม่แสดง", "แสดง",
                "PUR_ID ของแถวที่รับแล้วเป็นข้อความว่าง ไม่ใช่ Null — รายงานเดิมไม่แสดงใบที่รับแล้ว ระบบใหม่แสดงตาม BR-063 (AC-28: 690977 รับครบต้องแสดง)", PurchDifference.Agreed));
        var repeated = lines.Count(g => g.Count() > 1);
        if (repeated > 0)
            differences.Add(new("RPT-11", range, $"({Int(repeated)} รายการ)", "แสดงซ้ำตามจำนวนครั้งที่รับ", "แสดงครั้งเดียว", "รายการที่รับหลายครั้งแสดงครั้งเดียว (BR-063: แต่ละรายการปรากฏครั้งเดียว)", PurchDifference.Agreed));
        foreach (var line in lines)
        {
            var (no, suffix) = line.Key;
            var id = $"{no} ที่ {Int(suffix)}";
            var tax = line.First().Tax;
            if (!byKey.ContainsKey(line.Key) && line.All(r => r.Leftover))
                differences.Add(new("RPT-11", range, id, Money(tax), "ไม่ย้าย", "แถวเศษของประวัติ ไม่มีวันที่รับหรือชื่อวัตถุดิบ — ไม่ย้าย (Q-12, DQ-05)", PurchDifference.Data));
            else if (!byKey.TryGetValue(line.Key, out var found))
                differences.Add(new("RPT-11", range, id, Money(tax), "ไม่มี", ordersWithManyTotals.Contains(no) ? "ใบที่พิมพ์หลายครั้ง TOTAL ต่างกันต่อแถว — ระบบใหม่ใช้ยอดของใบ" : "รายการหายจากรายงานใหม่",
                    ordersWithManyTotals.Contains(no) ? PurchDifference.Data : PurchDifference.Program));
            else if (Math.Abs(found.AmountWithTax - tax) >= Tolerance) differences.Add(new("RPT-11", range, id, Money(tax), Money(found.AmountWithTax), "ยอดรายการรวม VAT ไม่ตรง", PurchDifference.Program));
            else matched++;
        }
        var known = lines.Select(l => l.Key).ToHashSet();
        foreach (var line in now.Where(l => !known.Contains((BaseNo(l.OrderNo), l.LineNo))))
        {
            var many = ordersWithManyTotals.Contains(BaseNo(line.OrderNo));
            differences.Add(new("RPT-11", range, $"{line.OrderNo} ที่ {Int(line.LineNo)}", "ไม่มี", Money(line.AmountWithTax), many ? "ใบที่พิมพ์หลายครั้ง TOTAL ต่างกันต่อแถว — ระบบใหม่ใช้ยอดของใบ" : "รายการเกินในรายงานใหม่",
                many ? PurchDifference.Data : PurchDifference.Program));
        }
        return new PurchRangeComparison("RPT-11", range, lines.Count, now.Count, matched, true, differences);
    }

    // ---------- RPT-26 รายงานการตัด STOCK (AC-20, AC-21) ----------

    /// <summary>
    /// RPT-26 of one cut: every code with a balance, its quantity, the register's price and the amount, and the total. The old query joins the register by code, which ignores case. After a
    /// rehearsal cut, <paramref name="backdated"/> holds per code the rows of the old Stock dated on or before the cut but keyed after Access had cut it: the cut made again takes them in, and a
    /// difference of exactly that much is the data, not the program.
    /// </summary>
    public static PurchRangeComparison CompareStockCut(string cut, IReadOnlyList<(string Code, decimal Quantity, decimal Cost)> baseline, IReadOnlyList<StockPeriodBalanceDto> now,
        IReadOnlyDictionary<string, decimal>? backdated = null)
    {
        backdated ??= new Dictionary<string, decimal>();
        var differences = new List<PurchDifference>();
        var legacy = baseline.GroupBy(b => b.Code.ToUpperInvariant(), StringComparer.Ordinal).ToDictionary(g => g.Key, g => (Quantity: g.Sum(b => b.Quantity), g.First().Cost), StringComparer.Ordinal);
        var rows = PurchReportRows.StockCut(now);
        var current = rows.ToDictionary(r => r.MaterialCode.ToUpperInvariant(), StringComparer.Ordinal);
        var matched = 0;
        foreach (var (code, (quantity, cost)) in legacy.Where(l => l.Value.Quantity != 0))
        {
            if (!current.TryGetValue(code, out var row)) { differences.Add(new("RPT-26", cut, code, Qty(quantity), "ไม่มี", "รหัสหายจากรายงานใหม่", PurchDifference.Program)); continue; }
            var problems = new List<string>();
            var late = backdated.GetValueOrDefault(code);
            if (late != 0 && Math.Abs(row.Quantity - quantity - late) < QuantityTolerance)
                differences.Add(new("RPT-26", cut, code, Qty(quantity), Qty(row.Quantity), $"แถวลงวันที่ก่อนวันตัดแต่คีย์หลัง Access ตัด ตัดซ้ำจึงรวมเข้า ({Qty(late)})", PurchDifference.Data));
            else if (Math.Abs(row.Quantity - quantity) >= QuantityTolerance) problems.Add($"จำนวน {Qty(quantity)} ≠ {Qty(row.Quantity)}");
            if (Math.Abs(row.Cost - cost) >= 0.0001m) problems.Add($"ราคา {cost.ToString("0.####", CultureInfo.InvariantCulture)} ≠ {row.Cost.ToString("0.####", CultureInfo.InvariantCulture)}");
            if (problems.Count == 0) matched++;
            else differences.Add(new("RPT-26", cut, code, "", "", string.Join(" · ", problems), PurchDifference.Program));
        }
        foreach (var row in rows.Where(r => !legacy.TryGetValue(r.MaterialCode.ToUpperInvariant(), out var l) || l.Quantity == 0))
        {
            var late = backdated.GetValueOrDefault(row.MaterialCode.ToUpperInvariant());
            differences.Add(late != 0 && Math.Abs(row.Quantity - late) < QuantityTolerance
                ? new("RPT-26", cut, row.MaterialCode, "ไม่มี", Qty(row.Quantity), $"แถวลงวันที่ก่อนวันตัดแต่คีย์หลัง Access ตัด ตัดซ้ำจึงรวมเข้า ({Qty(late)})", PurchDifference.Data)
                : new("RPT-26", cut, row.MaterialCode, "ไม่มี", Qty(row.Quantity), "รหัสเกินในรายงานใหม่", PurchDifference.Program));
        }
        var total = legacy.Values.Sum(l => l.Quantity * l.Cost);
        var totalNow = rows.Sum(r => r.Quantity * r.Cost);
        var lateValue = rows.Sum(r => backdated.GetValueOrDefault(r.MaterialCode.ToUpperInvariant()) * r.Cost);
        if (Math.Abs(total - totalNow) >= 0.01m)
            differences.Add(Math.Abs(total + lateValue - totalNow) < 0.01m
                ? new("RPT-26", cut, "(รวมทั้งหมด)", Money(total), Money(totalNow), "มูลค่ารวมต่างเท่ามูลค่าของแถวที่คีย์หลัง Access ตัด", PurchDifference.Data)
                : new("RPT-26", cut, "(รวมทั้งหมด)", Money(total), Money(totalNow), "มูลค่ารวมไม่ตรง", PurchDifference.Program));
        return new PurchRangeComparison("RPT-26", cut, legacy.Count(l => l.Value.Quantity != 0), rows.Count, matched, true, differences);
    }

    // ---------- AC-18 ยอดคงเหลือ ----------

    /// <summary>
    /// AC-18: the balance of every code = Σ RE_QTY of its rows in the old Stock table. A code that is not in the register (DQ-07) and a row with no date are not moved — both are in the DQ
    /// report, and the balance is expected without them.
    /// </summary>
    public static PurchRangeComparison CompareBalances(IReadOnlyList<(string Code, decimal Quantity, int Rows, decimal Undated)> baseline, IReadOnlyList<StockBalanceDto> now, IReadOnlySet<string> registered)
    {
        var differences = new List<PurchDifference>();
        var current = now.ToDictionary(b => b.MaterialCode.ToUpperInvariant(), b => b.Balance, StringComparer.Ordinal);
        var matched = 0;
        foreach (var (code, quantity, rows, undated) in baseline)
        {
            if (!registered.Contains(code))
            {
                differences.Add(new("AC-18", "-", code, $"{Qty(quantity)} ({Int(rows)} แถว)", "ไม่ย้าย", "รหัสไม่อยู่ในทะเบียนวัตถุดิบ (DQ-07) — ลงรายงาน DQ", PurchDifference.Data));
                continue;
            }
            var expected = quantity - undated;
            if (undated != 0) differences.Add(new("AC-18", "-", code, Qty(undated), "ไม่ย้าย", "แถวไม่มีวันที่ไม่ย้าย — ลงรายงาน DQ", PurchDifference.Data));
            var balance = current.GetValueOrDefault(code);
            if (Math.Abs(balance - expected) >= QuantityTolerance) differences.Add(new("AC-18", "-", code, Qty(expected), Qty(balance), "ยอดคงเหลือไม่ตรง", PurchDifference.Program));
            else matched++;
        }
        var known = baseline.Select(b => b.Code).ToHashSet(StringComparer.Ordinal);
        foreach (var (code, balance) in current.Where(c => !known.Contains(c.Key) && c.Value != 0))
            differences.Add(new("AC-18", "-", code, "ไม่มี", Qty(balance), "รหัสมียอดในระบบใหม่แต่ไม่มีใน Stock เดิม", PurchDifference.Program));
        return new PurchRangeComparison("AC-18", "-", baseline.Count, now.Count, matched, true, differences);
    }

    // ---------- AC-24 ผลนับประเมิน ----------

    /// <summary>
    /// AC-24: the counts of the quarter for every supplier against T-ประเมินผู้ขาย. A count kept that the old query no longer gives on the data as it is now is a delivery changed after the
    /// evaluation (data). Where the new count differs from the old query on today's data: ทัน/ไม่ทัน are worked out from the dates, not taken from the typed SEND (DQ-11); anything else is a fault.
    /// </summary>
    public static PurchRangeComparison CompareEvaluations(IReadOnlyList<PurchEvaluationCounts> kept, IReadOnlyList<PurchEvaluationCounts> legacyNow, IReadOnlyList<PurchEvaluationCounts> now)
    {
        var differences = new List<PurchDifference>();
        var today = legacyNow.ToDictionary(c => Clean(c.Supplier), StringComparer.Ordinal);
        var current = now.ToDictionary(c => Clean(c.Supplier), StringComparer.Ordinal);
        var matched = 0;
        foreach (var row in kept)
        {
            var name = Clean(row.Supplier);
            var fresh = today.GetValueOrDefault(name)?.Values ?? new int[6];
            var mine = current.GetValueOrDefault(name)?.Values ?? new int[6];
            var same = true;
            for (var i = 0; i < 6; i++)
            {
                if (mine[i] == row.Values[i]) continue;
                same = false;
                var (cause, origin) = mine[i] == fresh[i] ? ($"ข้อมูลรับของเปลี่ยนหลังวันที่ประเมิน (query เดิมบนข้อมูลปัจจุบันได้ {Int(fresh[i])})", PurchDifference.Data)
                    : i >= 4 ? ($"ระบบใหม่คำนวณทัน/ไม่ทันจากวันที่ต้องการกับวันที่รับ ไม่ใช้ข้อความ SEND (DQ-11; query เดิมบนข้อมูลปัจจุบันได้ {Int(fresh[i])})", PurchDifference.Agreed)
                    : ($"ผลนับไม่ตรง (query เดิมบนข้อมูลปัจจุบันได้ {Int(fresh[i])})", PurchDifference.Program);
                differences.Add(new("AC-24", "-", $"{name} {PurchEvaluationCounts.Names[i]}", Int(row.Values[i]), Int(mine[i]), cause, origin));
            }
            if (same) matched++;
        }
        var keptNames = kept.Select(k => Clean(k.Supplier)).ToHashSet(StringComparer.Ordinal);
        foreach (var (name, counts) in current.Where(c => !keptNames.Contains(c.Key)))
            differences.Add(new("AC-24", "-", name, "ไม่มีผลประเมินที่เก็บไว้", string.Join("/", counts.Values.Select(Int)),
                today.ContainsKey(name) ? "ระบบเดิมไม่ได้ประเมินผู้ขายรายนี้ของช่วงนั้น (มีรายการที่มี LOT)" : "ผู้ขายมีรายการที่มี LOT เฉพาะในระบบใหม่", today.ContainsKey(name) ? PurchDifference.Data : PurchDifference.Program));
        return new PurchRangeComparison("AC-24", "-", kept.Count, now.Count, matched, true, differences);
    }

    // ---------- AC-07 ยอดใบสั่งซื้อ ----------

    /// <summary>
    /// AC-07: the formula of BR-022 (<see cref="PurchaseOrderMath.Calculate"/>) on every order of the year, from ราคา × จำนวนสั่ง, against the TOTAL Access kept. The TOTAL is of a print —
    /// the lines picked together, which may be some lines of an order or lines of several — so the lines are taken by order number and TOTAL. TOTAL is a Single: equal within 0.01 or one step
    /// of a Single. A print that does not match is explained when it can be: NET_AMT not worked out again after the quantity was edited (BR-021 #4), an order with lines of both VAT (#5), several
    /// order numbers printed at once, or a price/quantity edited after printing (the line totals with VAT kept at printing still add up to TOTAL). The rest is left to a person.
    /// </summary>
    public static PurchRangeComparison CompareOrderTotals(IReadOnlyList<PurchOrderRow> rows, decimal vatRate, out int explained)
    {
        var differences = new List<PurchDifference>();
        var mixedVat = rows.GroupBy(r => r.OrderNo).Where(g => g.Select(r => r.ChargesVat).Distinct().Count() > 1).Select(g => g.Key).ToHashSet(StringComparer.Ordinal);
        var lines = rows.GroupBy(r => (r.OrderNo, r.LineNo)).Select(g =>
        {
            var waiting = g.FirstOrDefault(r => r.Waiting);
            var received = g.Where(r => !r.Waiting).OrderBy(r => r.ReceiveDate).ToList();
            // as the migration: what was received (not failed) plus what is still waited for; with nothing waiting, what the first delivery found outstanding
            var ordered = waiting is not null ? received.Where(r => !r.Failed).Sum(r => r.Received) + waiting.OrderedOrOutstanding : received[0].OrderedOrOutstanding;
            var head = waiting ?? received[0];
            return (Key: g.Key, Head: head, Net: PurchaseOrderMath.LineNet(head.UnitPrice, ordered));
        }).ToList();
        var prints = lines.GroupBy(l => (l.Key.OrderNo, l.Head.Total)).Select(g => g.ToList()).ToList();
        var matched = 0;
        explained = 0;
        var left = new List<List<(( string OrderNo, int LineNo) Key, PurchOrderRow Head, decimal Net)>>();
        foreach (var print in prints)
        {
            var total = print[0].Head.Total;
            var vat = print[0].Head.ChargesVat;
            var id = $"{print[0].Key.OrderNo} ({Int(print.Count)} รายการ)";
            var calculated = PurchaseOrderMath.Calculate(print.Select(l => l.Net).ToList(), vat, vatRate).Total;
            if (SameTotal(calculated, total)) { matched++; continue; }
            var fromNet = PurchaseOrderMath.Calculate(print.Select(l => l.Head.NetAmount).ToList(), vat, vatRate).Total;
            string? cause = SameTotal(fromNet, total) ? "NET_AMT ไม่ถูกคำนวณใหม่เมื่อแก้จำนวน (BR-021 #4) — สูตรจาก NET_AMT ได้ TOTAL เดิม"
                : mixedVat.Contains(print[0].Key.OrderNo) ? "ใบมีรายการ VAT ทั้ง YES และ NO คำนวณแยกกลุ่ม (BR-021 #5)"
                : Math.Abs(print.Sum(l => l.Head.Tax) - total) <= 0.01m * print.Count ? "ราคา/จำนวนถูกแก้หลังพิมพ์ (รับของ BR-030 หรือแก้ใบ BR-024) — ยอดรายการรวม VAT ตอนพิมพ์ (TAX) ยังรวมได้ TOTAL"
                : null;
            if (cause is null) { left.Add(print); continue; }
            explained++;
            differences.Add(new("AC-07", "-", id, Money(total), Money(calculated), cause, cause.Contains("#4", StringComparison.Ordinal) || cause.Contains("#5", StringComparison.Ordinal) ? PurchDifference.Agreed : PurchDifference.Data));
        }
        // several order numbers printed at once: the TOTAL is of all of them
        foreach (var together in left.GroupBy(p => (p[0].Head.Total, p[0].Head.ChargesVat)).ToList())
        {
            var all = together.SelectMany(p => p).ToList();
            var combined = together.Count() > 1 && SameTotal(PurchaseOrderMath.Calculate(all.Select(l => l.Net).ToList(), together.Key.ChargesVat, vatRate).Total, together.Key.Total);
            foreach (var print in together)
            {
                var id = $"{print[0].Key.OrderNo} ({Int(print.Count)} รายการ)";
                var calculated = PurchaseOrderMath.Calculate(print.Select(l => l.Net).ToList(), together.Key.ChargesVat, vatRate).Total;
                if (combined)
                {
                    explained++;
                    differences.Add(new("AC-07", "-", id, Money(together.Key.Total), Money(calculated), $"พิมพ์ {Int(together.Count())} เลขที่ในครั้งเดียว — TOTAL เป็นยอดของทุกรายการที่เลือกพิมพ์ (BR-021 ขั้น 3) ตรงสูตร", PurchDifference.Data));
                }
                else differences.Add(new("AC-07", "-", id, Money(together.Key.Total), Money(calculated), "TOTAL ไม่ตรงสูตร และอธิบายด้วย BR-021 #4/#5 ไม่ได้ — ให้คนตรวจ", PurchDifference.Unknown));
            }
        }
        return new PurchRangeComparison("AC-07", "-", prints.Count, prints.Count, matched, true, differences);
    }

    /// <summary>TOTAL is kept as a Single: within 0.01, or within one step of a Single at that size (above 131,072 a step is more than 0.01).</summary>
    public static bool SameTotal(decimal calculated, decimal kept)
    {
        var single = (float)kept;
        var step = (decimal)(MathF.BitIncrement(Math.Abs(single)) - Math.Abs(single));
        return Math.Abs(calculated - kept) <= Math.Max(0.01m, step);
    }

    // ---------- the run ----------

    public const string RangesFile = "purch-ranges.csv";
    private static readonly DateOnly EvaluationFrom = new(2026, 4, 1), EvaluationTo = new(2026, 6, 30);
    private static readonly DateOnly Ac21Cut = new(2025, 10, 31);

    /// <summary>
    /// Reads the baseline, computes the reports from the database and compares. Reads only — unless <paramref name="rehearseCut"/> is given: then the stock of the database is cut at that date
    /// twice (AC-20: the same balances the second time) with the program's own ตัดสต็อก, after the balances are compared (AC-18), and RPT-26 of that date is compared with what Access cut.
    /// </summary>
    public static async Task<Result> RunAsync(IServiceProvider services, string baselineFolder, CodeMap map, DateOnly? rehearseCut = null)
    {
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var receipts = provider.GetRequiredService<GoodsReceiptDirectory>();
        var orders = provider.GetRequiredService<PurchaseOrderDirectory>();
        var settings = await provider.GetRequiredService<PurchSettingsDirectory>().GetAsync();
        var departments = await provider.GetRequiredService<OrgDirectory>().GetDepartmentsAsync();
        var materials = await provider.GetRequiredService<MaterialDirectory>().GetAllAsync();
        var suppliers = await provider.GetRequiredService<SupplierDirectory>().GetAllAsync();

        var ranges = Comparison.Read(baselineFolder, RangesFile).Select(r => (Id: r["id"], From: Date(r["from"])!.Value, To: Date(r["to"])!.Value)).ToList();
        var results = new List<PurchRangeComparison>();
        var gate = new List<PurchGateLine>();

        // RPT-04
        var backlogBase = Comparison.Read(baselineFolder, "purch-rpt04.csv").Select(r => new PurchBacklogRow(Date(r["po_date"]), r["po_no"], Whole(r["po_suf"]), r["name"], r["unit"], Dec(r["qty"]),
            Date(r["deli_d"]), r["listed"] == "1")).ToList();
        var outstanding = await receipts.GetAllOutstandingAsync();
        var backlog = CompareBacklog(backlogBase, outstanding);
        results.Add(backlog);
        gate.Add(new PurchGateLine("RPT-04", "รายงานวัตถุดิบที่ยังไม่ได้รับ ตรงทุกแถวและลำดับ", $"รายงานเดิม {Int(backlog.BaselineRows)} แถว + กรอกวันที่รับค้างไว้ {Int(backlogBase.Count(b => !b.Listed))} = ค้างรับ {Int(backlogBase.Count)} (AC-30)",
            $"ค้างรับ {Int(outstanding.Count)} · ตรง {Int(backlog.Matched)} · ต่างตามที่ตกลง {Int(backlog.Differences.Count(d => d.Accepted))} · ไม่ยอมรับ {Int(backlog.Unaccepted.Count)}", backlog.Passed, "#261 #266"));

        // RPT-20, RPT-22, RPT-11 of every range
        var allReceived = await receipts.GetReceivedLinesAsync(DateOnly.MinValue, DateOnly.MaxValue);
        var rpt20 = Comparison.Read(baselineFolder, "purch-rpt20.csv").ToList();
        var heads = Comparison.Read(baselineFolder, "purch-rpt20-heads.csv").ToDictionary(r => r["range"], r => (Whole(r["lines"]), Whole(r["requests"]), Whole(r["orders"])));
        var rpt22 = Comparison.Read(baselineFolder, "purch-rpt22.csv").ToList();
        var rpt11 = Comparison.Read(baselineFolder, "purch-rpt11.csv").ToList();
        var orderRows = Comparison.Read(baselineFolder, "purch-orders.csv").ToList();
        var manyTotals = orderRows.GroupBy(r => r["po_no"]).Where(g => g.Select(r => Dec(r["total"])).Distinct().Count() > 1).Select(g => g.Key).ToHashSet(StringComparer.Ordinal);
        var nameOf = departments.ToDictionary(d => d.Id, d => d.Name);
        string DepartmentOf(string legacy) => map.Find(CodeKinds.Department, legacy)?.Code is { } code && departments.FirstOrDefault(d => string.Equals(d.Code, code, StringComparison.OrdinalIgnoreCase)) is { } d
            ? d.Name : legacy;
        foreach (var (id, from, to) in ranges)
        {
            var purchases = PurchReportRows.Purchases(allReceived, from, to);
            results.Add(CompareMonthlySummary(id, rpt20.Where(r => r["range"] == id).Select(r => (r["name"], r["unit"], Dec(r["qty"]), Dec(r["amount"]))).ToList(), heads.GetValueOrDefault(id), purchases));
            results.Add(CompareDepartments(id, rpt22.Where(r => r["range"] == id).Select(r => (r["department"], Dec(r["amount"]))).ToList(),
                PurchReportRows.ByDepartment(allReceived, from, to).Select(r => (nameOf.GetValueOrDefault(r.DepartmentId) ?? "", r.Amount)).ToList(), DepartmentOf));
            results.Add(CompareSmallOrders(id, rpt11.Where(r => r["range"] == id).Select(r => new PurchSmallOrderRow(r["po_no"], Whole(r["po_suf"]), Dec(r["tax"]), Dec(r["total"]), r["literal"] == "1", r.GetValueOrDefault("leftover") == "1")).ToList(),
                await orders.GetSmallOrderLinesAsync(from, to, settings.SmallOrderLimit), manyTotals));
        }
        AddRangeGate(gate, results, "RPT-20", "AC-26", "สรุปการรับวัตถุดิบ (RPT-20) ตรงทุกช่วง ทุกแถว จำนวนใบขอซื้อ/ใบสั่งซื้อ", "#259");
        AddRangeGate(gate, results, "RPT-22", "AC-25", "การซื้อวัตถุดิบแยกแผนก (RPT-22) ตรงทุกช่วง ทุกแผนก", "#259");
        AddRangeGate(gate, results, "RPT-11", "AC-28", "ใบสั่งซื้อต่ำกว่า 5,000 (RPT-11) ตรงทุกช่วง ทุกรายการ", "#261");
        var sample = results.FirstOrDefault(r => r.Report == "RPT-22" && r.Range == "AC25-26");
        if (sample is not null)
        {
            var (from, to) = ranges.Where(r => r.Id == "AC25-26").Select(r => (r.From, r.To)).First();
            var purchases = PurchReportRows.Purchases(allReceived, from, to);
            gate.Add(new PurchGateLine("AC-25", "RPT-22 ส.ค. 2026", "11 แผนก รวม 4,058,812.97", $"{Int(sample.NewRows)} แผนก รวม {Money(purchases.Sum(l => l.Amount))}", sample.NewRows == 11 && purchases.Sum(l => l.Amount) == 4_058_812.97m, "#259"));
            gate.Add(new PurchGateLine("AC-26", "RPT-20 ส.ค. 2026", "ใบขอซื้อ 74, ใบสั่งซื้อ 87, รวม 4,058,812.97",
                $"ใบขอซื้อ {Int(purchases.Select(l => l.RequestNo).Distinct().Count())}, ใบสั่งซื้อ {Int(purchases.Select(l => BaseNo(l.OrderNo)).Distinct().Count())}, รวม {Money(purchases.Sum(l => l.Amount))}",
                purchases.Select(l => l.RequestNo).Distinct().Count() == 74 && purchases.Select(l => BaseNo(l.OrderNo)).Distinct().Count() == 87 && purchases.Sum(l => l.Amount) == 4_058_812.97m, "#259"));
        }

        // AC-07
        var vatRows = orderRows.Select(r => new PurchOrderRow(r["table"] == "PO_RECEI4", r["po_no"], Whole(r["po_suf"]), Date(r["re_date"]), Math.Round(Dec(r["u_price"]), 4), Dec(r["po_qty"]), Dec(r["re_qty"]),
            Dec(r["net_amt"]), Math.Round(Dec(r["tax"]), 2, MidpointRounding.ToEven), Math.Round(Dec(r["total"]), 2, MidpointRounding.ToEven), PurchaseOrderMath.ChargesVat(r["vat"]), r["pass"] == "ไม่ผ่าน")).ToList();
        var totals = CompareOrderTotals(vatRows, settings.VatRate, out var explained);
        results.Add(totals);
        var share = totals.BaselineRows == 0 ? 0m : (totals.Matched + explained) * 100m / totals.BaselineRows;
        gate.Add(new PurchGateLine("AC-07", $"ยอดใบสั่งซื้อปีล่าสุด: สูตร BR-022 ให้ TOTAL เดิม (ต่อการพิมพ์ {Int(totals.BaselineRows)} ครั้ง)", "≥ 99% ตรง หรือต่างโดยอธิบายได้ (BR-021 #4/#5)",
            $"ตรงสูตร {Int(totals.Matched)} ({(totals.Matched * 100m / Math.Max(1, totals.BaselineRows)).ToString("0.0", CultureInfo.InvariantCulture)}%) · อธิบายได้ {Int(explained)} · ยังไม่ทราบสาเหตุ {Int(totals.Differences.Count(d => d.Origin == PurchDifference.Unknown))} → {share.ToString("0.0", CultureInfo.InvariantCulture)}%",
            share >= 99m && totals.Differences.All(d => d.Origin != PurchDifference.Program), "#212 #266"));

        // AC-18 before any cut: a cut clears the springs
        var registered = materials.Select(m => m.Code.ToUpperInvariant()).ToHashSet(StringComparer.Ordinal);
        var balances = CompareBalances(Comparison.Read(baselineFolder, "purch-stock.csv").Select(r => (r["code"], Dec(r["qty"]), Whole(r["rows"]), Dec(r["undated_qty"]))).ToList(),
            await provider.GetRequiredService<StockDirectory>().GetBalancesAsync(), registered);
        results.Add(balances);
        gate.Add(new PurchGateLine("AC-18", "ยอดคงเหลือทุกรหัส = Σ Stock.RE_QTY เดิม", $"{Int(balances.BaselineRows)} รหัส ตรง 100% (ไม่นับรหัสนอกทะเบียน/แถวไม่มีวันที่ — DQ)",
            $"ตรง {Int(balances.Matched)} · ไม่ย้ายพร้อมเหตุผล {Int(balances.Differences.Count(d => d.Accepted))} · ไม่ตรง {Int(balances.Unaccepted.Count)}", balances.Passed, "#267"));

        // RPT-26 of every cut in the baseline, as migrated (AC-21: printed again with the register's prices)
        var closing = provider.GetRequiredService<StockClosingDirectory>();
        var rpt26 = Comparison.Read(baselineFolder, "purch-rpt26.csv").ToList();
        IReadOnlyList<(string Code, decimal Quantity, decimal Cost)> Access(string date) => rpt26.Where(r => r["cut"] == date).Select(r => (r["code"], Dec(r["qty"]), Dec(r["cost"]))).ToList();
        foreach (var date in rpt26.Select(r => r["cut"]).Distinct().Order(StringComparer.Ordinal))
        {
            var day = Date(date)!.Value;
            var now = await closing.GetBalancesAsync(day);
            var compared = CompareStockCut(date, Access(date), now);
            results.Add(compared);
            if (day != Ac21Cut) continue;
            var rows = PurchReportRows.StockCut(now);
            var total = rows.Sum(r => r.Quantity * r.Cost);
            gate.Add(new PurchGateLine("AC-21", $"รายงานตัดสต็อก {date} ราคาทะเบียน ณ วันที่ถอด", "346 รายการ รวม 54,038,681.24",
                $"{Int(rows.Count)} รายการ รวม {Money(total)} · เทียบทีละรหัส ตรง {Int(compared.Matched)} ไม่ตรง {Int(compared.Unaccepted.Count)}",
                rows.Count == 346 && Math.Round(total, 2) == 54_038_681.24m && compared.Passed, "#262"));
        }
        var cuts = results.Where(r => r.Report == "RPT-26").ToList();
        gate.Add(new PurchGateLine("RPT-26", $"รายงานตัดสต็อกทุกวันตัดที่ย้าย ({Int(cuts.Count)} วัน)", "ตรงทุกรหัส", $"ตรงทั้งวัน {Int(cuts.Count(c => c.Passed))}/{Int(cuts.Count)}", cuts.All(c => c.Passed), "#262 #267"));

        // AC-20 rehearsal: cut twice with the program's own ตัดสต็อก, then the balances it carried forward against what Access cut that day
        if (rehearseCut is { } cut)
        {
            var registry = provider.GetRequiredService<StockClosingRegistry>();
            var first = await registry.CloseAsync(cut);
            var once = await closing.GetBalancesAsync(cut);
            var second = await registry.CloseAsync(cut);
            var twice = await closing.GetBalancesAsync(cut);
            var same = once.Count == twice.Count && once.Zip(twice).All(p => p.First.MaterialId == p.Second.MaterialId && p.First.Quantity == p.Second.Quantity);
            gate.Add(new PurchGateLine("AC-20", $"ซ้อมตัดสต็อก {Iso(cut)} จากข้อมูลที่ย้าย แล้วตัดซ้ำวันเดียวกัน", "ตัดได้ · ตัดซ้ำได้ยอดเท่าเดิม",
                $"{(first.Succeeded && second.Succeeded ? "ตัดได้" : string.Join(" ", first.Errors.Concat(second.Errors)))} · ปิด {Int(first.ClosedRows)} แถว ยกมา {Int(first.OpeningBalances)} รหัส · ตัดซ้ำ{(same ? "ได้ยอดเท่าเดิม" : "ได้ยอดต่าง")}",
                first.Succeeded && second.Succeeded && same, "#262"));
            var date = Iso(cut);
            var backdated = Comparison.Read(baselineFolder, "purch-stock-backdated.csv").Where(r => r["cut"] == date).ToDictionary(r => r["code"], r => Dec(r["qty"]), StringComparer.Ordinal);
            var rehearsed = CompareStockCut($"{date} (ซ้อมตัด)", Access(date), twice, backdated);
            results.Add(rehearsed);
            gate.Add(new PurchGateLine("AC-20", $"ยอดยกมา {date} ที่ระบบใหม่ตัด = ยอดที่ Access ตัดไว้ (ต้นงวดP_ST) ทุกรหัส", $"{Int(rehearsed.BaselineRows)} รหัส",
                $"ตรง {Int(rehearsed.Matched)} · ต่างเท่าแถวที่คีย์หลัง Access ตัด {Int(rehearsed.Differences.Count(d => d.Accepted && d.Key != "(รวมทั้งหมด)"))} · ไม่ตรง {Int(rehearsed.Unaccepted.Count)}", rehearsed.Passed, "#262 #267"));
        }

        // AC-24
        var production = suppliers.ToDictionary(s => s.Id);
        var counted = (await receipts.GetIncomingAsync(EvaluationFrom, EvaluationTo)).GroupBy(r => r.SupplierId)
            .Select(g => new PurchEvaluationCounts(production.TryGetValue(g.Key, out var s) ? s.Name : g.First().SupplierName, g.Count(r => !r.Failed), g.Count(r => r.Failed),
                g.Count(r => r.HasCertificate == true), g.Count(r => r.HasCertificate == false), g.Count(r => r.OnTime), g.Count(r => !r.OnTime))).ToList();
        var kept = Comparison.Read(baselineFolder, "purch-eval.csv").Where(r => r["sup"] != "*")
            .Select(r => new PurchEvaluationCounts(r["sup"], Whole(r["passed"]), Whole(r["failed"]), Whole(r["with_cert"]), Whole(r["without_cert"]), Whole(r["on_time"]), Whole(r["late"]))).ToList();
        var legacyNow = Comparison.Read(baselineFolder, "purch-eval-now.csv")
            .Select(r => new PurchEvaluationCounts(r["sup"], Whole(r["passed"]), Whole(r["failed"]), Whole(r["with_cert"]), Whole(r["without_cert"]), Whole(r["on_time"]), Whole(r["late"]))).ToList();
        var evaluations = CompareEvaluations(kept, legacyNow, counted);
        results.Add(evaluations);
        gate.Add(new PurchGateLine("AC-24", "ผลนับประเมินไตรมาส 2/2026 (ผ่าน/ไม่ผ่าน/มี/ไม่มี/ทัน/ไม่ทัน) = T-ประเมินผู้ขาย", $"{Int(kept.Count)} ผู้ขาย ตรง 100%",
            $"ตรงทั้งแถว {Int(evaluations.Matched)} · ต่างพร้อมเหตุผล {Int(evaluations.Differences.Count(d => d.Accepted))} ค่า · ไม่ยอมรับ {Int(evaluations.Unaccepted.Count)} ค่า", evaluations.Passed, "#255 #268"));

        // AC-30 sizes
        var sizes = Comparison.Read(baselineFolder, "purch-summary.csv").ToDictionary(r => r["metric"], r => r["value"]);
        string B(string key) => sizes.GetValueOrDefault(key) ?? "0";
        var storedEvaluations = (await provider.GetRequiredService<SupplierEvaluationDirectory>().ListAsync()).Count;
        // a count short of the source is not decided here: the rows left out are in the DQ report of the migration, each with its rule (a person checks them there)
        void Size(string check, string expected, int want, int have, string covers)
            => gate.Add(new PurchGateLine("AC-30", check, expected, have == want ? Int(have) : $"{Int(have)} — ขาด {Int(want - have)} ตรวจกับ dq-exceptions.csv ของการย้าย", have == want ? true : null, covers));
        Size("วัตถุดิบ", B("materials"), Whole(B("materials")), materials.Count, "#217");
        Size("ผู้ขาย (รหัสซ้ำรวมตาม Q-10)", $"{B("suppliers")} แถว · รหัสไม่ซ้ำ {B("supplier_codes")}", Whole(B("supplier_codes")), suppliers.Count, "#217");
        Size("รายการรับของ (ไม่รวมแถวเศษ Q-12)", $"{B("history_loadable")} (ต้นทาง {B("history_rows")})", Whole(B("history_loadable")), allReceived.Count, "#266");
        Size("ใบสั่งซื้อค้างรับ", B("waiting_rows"), Whole(B("waiting_rows")), outstanding.Count, "#266");
        Size("ผลประเมินผู้ขาย (ไม่รวมแถว * ผู้ขายทั้งหมด)", $"{Int(Whole(B("evaluations")) - Whole(B("evaluations_all_suppliers")))} (ต้นทาง {B("evaluations")})",
            Whole(B("evaluations")) - Whole(B("evaluations_all_suppliers")), storedEvaluations, "#268");
        gate.Add(new PurchGateLine("RPT-01, RPT-02", "ใบขอซื้อ/ใบสั่งซื้อ (ระดับ A) วางซ้อนตัวอย่าง + ผู้ใช้เซ็นรับ", "ตรวจด้วยตา", "ดู tools/form-overlay", null, "#213 #214"));
        return new Result(results, gate);
    }

    private static void AddRangeGate(List<PurchGateLine> gate, IReadOnlyList<PurchRangeComparison> results, string report, string id, string check, string covers)
    {
        var of = results.Where(r => r.Report == report).ToList();
        gate.Add(new PurchGateLine(id, $"{check} ({Int(of.Count)} ช่วง)", "ตรง 100% ของข้อมูลที่ย้ายได้",
            $"ตรงทั้งช่วง {Int(of.Count(r => r.Passed))}/{Int(of.Count)} · ไม่ยอมรับ {Int(of.Sum(r => r.Unaccepted.Count))} · ต่างพร้อมเหตุผล {Int(of.Sum(r => r.Differences.Count(d => d.Accepted)))}", of.All(r => r.Passed), covers));
    }

    public static void WriteTo(string folder, Result result)
    {
        Directory.CreateDirectory(folder);
        Csv.Write(Path.Combine(folder, "purch-compare.csv"), ["รายงาน", "ช่วง", "แถวต้นทาง", "แถวในฐาน", "ตรง", "ลำดับ", "ยอมรับ", "ไม่ยอมรับ", "ผล"],
            result.Ranges.Select(r => new object?[] { r.Report, r.Range, r.BaselineRows, r.NewRows, r.Matched, r.SameOrder ? "ตรง" : "ไม่ตรง", r.Differences.Count(d => d.Accepted), r.Unaccepted.Count, r.Passed ? "ผ่าน" : "ไม่ผ่าน" }));
        // real values: kept outside git. One list: what differs, why, and where the difference comes from
        Csv.Write(Path.Combine(folder, "purch-compare-details.csv"), ["รายงาน", "ช่วง", "รายการ", "ต้นทาง", "ฐานข้อมูล", "สาเหตุ", "ที่มาของความต่าง", "ยอมรับ"],
            result.Ranges.SelectMany(r => r.Differences).Select(d => new object?[] { d.Report, d.Range, d.Key, d.Baseline, d.New, d.Cause, d.Origin, d.Accepted ? "ยอมรับ" : "ไม่ยอมรับ" }));
        Csv.Write(Path.Combine(folder, "purch-gate.csv"), ["AC", "การตรวจ", "ควรได้", "ได้", "ผล", "ตั๋ว"],
            result.Gate.Select(g => new object?[] { g.Id, g.Check, g.Expected, g.Actual, Verdict(g.Passed), g.Covers }));
        var summary = new StringBuilder("# เทียบผลวัตถุดิบกับ Access\n\n").AppendLine(result.Passed ? "**ผ่านทุกข้อที่ตรวจอัตโนมัติ**" : "**มีข้อที่ไม่ผ่าน**").AppendLine();
        summary.AppendLine("| AC | การตรวจ | ควรได้ | ได้ | ผล |").AppendLine("|---|---|---|---|---|");
        foreach (var g in result.Gate) summary.AppendLine($"| {g.Id} | {g.Check} | {g.Expected} | {g.Actual} | {Verdict(g.Passed)} |");
        summary.AppendLine().AppendLine("## ความต่างตามที่มา (จำนวน — รายการอยู่ใน purch-compare-details.csv)").AppendLine().AppendLine("| รายงาน | ที่มา | สาเหตุ | จำนวน |").AppendLine("|---|---|---|---|");
        foreach (var g in result.Ranges.SelectMany(r => r.Differences).GroupBy(d => (d.Report, d.Origin, Cause: CauseKind(d.Cause))).OrderBy(g => g.Key.Report, StringComparer.Ordinal).ThenBy(g => g.Key.Origin, StringComparer.Ordinal))
            summary.AppendLine($"| {g.Key.Report} | {g.Key.Origin} | {g.Key.Cause} | {Int(g.Count())} |");
        File.WriteAllText(Path.Combine(folder, "purch-summary.md"), summary.ToString(), new UTF8Encoding(true));
    }

    /// <summary>The cause without the numbers in brackets, so the summary can count by kind.</summary>
    private static string CauseKind(string cause) => cause.IndexOf(" (", StringComparison.Ordinal) is var i and > 0 && cause.EndsWith(')') ? cause[..i] : cause;

    private static string Verdict(bool? passed) => passed is null ? "ตรวจด้วยคน" : passed.Value ? "ผ่าน" : "ไม่ผ่าน";

    private static bool SameText(string? a, string? b) => Clean(a) == Clean(b);

    private static string Clean(string? text) => string.Join(' ', (text ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string Money(decimal value) => value.ToString("N2", CultureInfo.InvariantCulture);

    private static string Qty(decimal value) => value.ToString("#,0.###", CultureInfo.InvariantCulture);

    private static string Int(int value) => value.ToString("N0", CultureInfo.InvariantCulture);

    private static string Iso(DateOnly? date) => date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "(ว่าง)";

    private static DateOnly? Date(string text) => DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

    private static decimal Dec(string text) => text.Length == 0 ? 0m : decimal.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);

    private static int Whole(string text) => text.Length == 0 ? 0 : (int)Math.Round(decimal.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture));
}
