using System.Globalization;
using Lockte.Cash;
using Lockte.SharedKernel.Persistence;

namespace Lockte.Migration;

/// <summary>
/// ย้ายใบเสร็จขายของเก่า (#241): the lines of <c>ขายของเก่า</c> (one row per line, the head repeated on each) become receipts — one per เลขที่ — with their lines, and the state of
/// each receipt, sent or waiting, is worked out from <c>ขายของเก่า1</c> (ค้างส่ง) and <c>ขายของเก่า2</c> (the last closing). Run after the registers and the expense lines, in the same
/// run (<see cref="CashMasterDataMigration"/>, <see cref="CashExpenseLineMigration"/>). The head is the first line's (lowest ลำดับที่) and what the other lines said differently is listed;
/// buyer and item are kept as written on the receipt and tied to the register only when the name is the same after the blanks are cleaned — the register is never added to (Q-30, the
/// numbers of names left out are in the report). An amount is kept as it was, never worked out again. The receipts already sent go to one การนำส่ง made by the migration
/// ("นำส่งก่อนย้ายข้อมูล"); the ones waiting stay waiting (AC-23). The report names buyers and numbers of real receipts: kept outside git.
/// </summary>
public sealed class CashReceiptMigration(AuditScope audit, CashReceiptImport import, ScrapBuyerDirectory buyerDirectory, ScrapItemDirectory itemDirectory, ScrapReceiptDirectory receiptDirectory)
{
    public const string ReceiptsTable = "ขายของเก่า";
    public const string WaitingTable = "ขายของเก่า1";
    public const string LastClosingTable = "ขายของเก่า2";
    public const string RemittanceName = "migration";
    private const string BuyerRule = "DQ-Q30";
    private const string HeadRule = "DQ-Q16";
    private const string StateRule = "AC-23";
    private const string TotalRule = "AC-24";
    private const string Other = "NF-06";

    private sealed record Built(string No, ImportedReceipt Receipt, decimal LegacySum, IReadOnlyList<LegacyRow> Rows);

    public async Task RunAsync(LegacyDatabase legacy, MigrationReport report)
    {
        var buyers = (await buyerDirectory.ListAsync()).GroupBy(b => b.Name, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);
        var items = (await itemDirectory.ListAsync()).GroupBy(i => i.Name, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);
        var waitingNumbers = legacy[WaitingTable].Select(r => CashMigrationRules.Name(r["NO"] as string)).OfType<string>().ToHashSet(StringComparer.Ordinal);
        var lastClosing = legacy[LastClosingTable].Select(r => CashMigrationRules.Name(r["NO"] as string)).OfType<string>().OrderBy(n => n, Comparer<string>.Create(ScrapReceiptMath.Compare)).LastOrDefault();

        var groups = new Dictionary<string, List<LegacyRow>>(StringComparer.Ordinal);
        var order = new List<string>();
        var rejectedRows = 0;
        var rowNumber = 0;
        foreach (var row in legacy[ReceiptsTable])
        {
            rowNumber++;
            if (CashMigrationRules.Name(row["NO"] as string) is not { } no)
            {
                rejectedRows++;
                report.Add(ReceiptsTable, $"แถวที่ {rowNumber.ToString(CultureInfo.InvariantCulture)}", Other, "ไม่มีเลขที่ใบเสร็จ — ไม่โหลด", MigrationReport.NotLoaded);
                continue;
            }
            if (!groups.TryGetValue(no, out var rows)) { groups[no] = rows = []; order.Add(no); }
            rows.Add(row);
        }

        var built = new List<Built>();
        var receiptsRejected = 0;
        var unknownBuyers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var unknownItems = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var no in order)
        {
            var rows = groups[no];
            var made = Build(no, rows, buyers, items, unknownBuyers, unknownItems, report, ref rejectedRows);
            if (made is null) receiptsRejected++;
            else built.Add(made);
        }

        // the state: waiting when it has lines in ขายของเก่า1; the rule behind it (a number above the last closing) is checked on every receipt
        var states = built.Select(b => (b, Waiting: waitingNumbers.Contains(b.No))).ToList();
        foreach (var (b, waiting) in states)
        {
            var expected = lastClosing is not null && ScrapReceiptMath.Compare(b.No, lastClosing) > 0;
            if (expected != waiting)
                report.Add(ReceiptsTable, b.No, StateRule, waiting ? $"อยู่ในค้างส่งทั้งที่เลขที่ไม่เกินเลขตัดยอดล่าสุด ({lastClosing ?? "ไม่มี"})" : $"ไม่อยู่ในค้างส่งทั้งที่เลขที่เกินเลขตัดยอดล่าสุด ({lastClosing})", "ตามขายของเก่า1");
        }
        var sent = states.Where(s => !s.Waiting).Select(s => s.b).ToList();
        var final = built.Select(b => b.Receipt with { Remitted = !waitingNumbers.Contains(b.No) }).ToList();
        ImportedRemittance? remittance = null;
        if (sent.Count > 0)
        {
            var ordered = sent.OrderBy(s => s.No, Comparer<string>.Create(ScrapReceiptMath.Compare)).ToList();
            remittance = new ImportedRemittance(ordered[^1].Receipt.Date, ordered[0].No, lastClosing ?? ordered[^1].No, RemittanceName);
            report.Notes.Add($"นำส่งก่อนย้ายข้อมูล: {sent.Count.ToString(CultureInfo.InvariantCulture)} ใบ ตั้งแต่เลขที่ {remittance.FromNo} ถึง {remittance.ToNo} วันที่ {CashLineRules.Iso(remittance.MadeOn)} (เป็นการนำส่งเดียวที่การย้ายข้อมูลสร้าง)");
        }

        using (audit.SuppressRowChanges()) await import.LoadAsync(final, remittance);

        ReportRegisters(report, unknownBuyers, unknownItems);
        ReportGaps(report, built.Select(b => b.No).ToList());
        await CountBackAsync(legacy, report, built, waitingNumbers, groups.Count, receiptsRejected, rejectedRows, legacy[ReceiptsTable].Count);
    }

    /// <summary>One receipt from the lines of its number; null when none of its lines can be loaded.</summary>
    private static Built? Build(string no, List<LegacyRow> rows, Dictionary<string, int> buyers, Dictionary<string, int> items, Dictionary<string, int> unknownBuyers,
        Dictionary<string, int> unknownItems, MigrationReport report, ref int rejectedRows)
    {
        var taken = new List<(LegacyRow Row, int Seq, string Item, decimal Qty, decimal Price, decimal Amount)>();
        var used = new HashSet<int>();
        var legacySum = 0m;
        var position = 0;
        foreach (var row in rows)
        {
            position++;
            var at = $"{no} บรรทัดที่ {position.ToString(CultureInfo.InvariantCulture)}";
            legacySum += CashLineRules.Decimal(row.Number("AMOUNT") ?? 0);
            if (CashMigrationRules.Name(row["NAMES"] as string) is not { } item) { rejectedRows++; report.Add(ReceiptsTable, at, Other, "ไม่มีรายการ — ไม่โหลด (ระบบเดิมลบแถวนี้ BR-014)", MigrationReport.NotLoaded); continue; }
            if (row.Number("QTY") is not { } rawQty) { rejectedRows++; report.Add(ReceiptsTable, at, Other, "ไม่มีจำนวน — ไม่โหลด (ระบบเดิมลบแถวนี้ BR-014)", MigrationReport.NotLoaded); continue; }
            var seqValue = row.Number("SUF") ?? 0;
            var seq = (int)Math.Round(seqValue, MidpointRounding.AwayFromZero);
            if (seq >= 1 && !used.Add(seq)) { rejectedRows++; report.Add(ReceiptsTable, at, HeadRule, $"ลำดับที่ {seq.ToString(CultureInfo.InvariantCulture)} ซ้ำในใบเดียวกัน — ใช้แถวแรก ไม่โหลดแถวนี้", MigrationReport.NotLoaded); continue; }
            if (seq < 1)
            {
                seq = 1;
                while (!used.Add(seq)) seq++;
                report.Add(ReceiptsTable, at, HeadRule, $"ไม่มีลำดับที่ → ใช้ {seq.ToString(CultureInfo.InvariantCulture)}", "แก้อัตโนมัติ");
            }
            var (qty, qtyChanged) = CashLineRules.Fit(CashLineRules.Decimal(rawQty), 3);
            var (price, priceChanged) = CashLineRules.Fit(CashLineRules.Decimal(row.Number("U_PRICE") ?? 0), 3);
            var rawAmount = CashLineRules.Decimal(row.Number("AMOUNT") ?? 0);
            var (amount, _) = CashLineRules.Fit(rawAmount, 2);
            if (qtyChanged && Math.Abs(qty - CashLineRules.Decimal(rawQty)) > 0.005m) report.Add(ReceiptsTable, at, TotalRule, $"จำนวนปัดเป็น {CashLineRules.Money(qty)} (เดิม {CashLineRules.Money(CashLineRules.Decimal(rawQty))})", "แก้อัตโนมัติ");
            if (priceChanged && Math.Abs(price - CashLineRules.Decimal(row.Number("U_PRICE") ?? 0)) > 0.005m) report.Add(ReceiptsTable, at, TotalRule, $"ราคาปัดเป็น {CashLineRules.Money(price)}", "แก้อัตโนมัติ");
            if (Math.Abs(amount - rawAmount) > 0.005m) report.Add(ReceiptsTable, at, TotalRule, $"จำนวนเงินปัดเป็น {CashLineRules.Money(amount)} (เดิม {CashLineRules.Money(rawAmount)})", "แก้อัตโนมัติ");
            taken.Add((row, seq, item, qty, price, amount));
        }
        if (taken.Count == 0) { report.Add(ReceiptsTable, no, Other, "ไม่มีบรรทัดที่โหลดได้ — ไม่โหลดใบนี้", MigrationReport.NotLoaded); return null; }

        var ordered = taken.OrderBy(t => t.Seq).ToList();
        var head = ordered[0].Row;
        var date = head.Date("DATE") ?? ordered.Select(t => t.Row.Date("DATE")).FirstOrDefault(d => d is not null);
        if (date is null)
        {
            rejectedRows += taken.Count;
            report.Add(ReceiptsTable, no, Other, $"ไม่มีวันที่ในบรรทัดใดเลย — ไม่โหลดใบนี้ ({taken.Count.ToString(CultureInfo.InvariantCulture)} บรรทัด)", MigrationReport.NotLoaded);
            return null;
        }
        if (head.Date("DATE") is null) report.Add(ReceiptsTable, no, HeadRule, $"บรรทัดแรกไม่มีวันที่ → ใช้วันที่ {CashLineRules.Iso(DateOnly.FromDateTime(date.Value))} ของบรรทัดถัดไป", "แก้อัตโนมัติ");
        var day = DateOnly.FromDateTime(date.Value);

        var name = Cut(report, no, "ชื่อผู้ซื้อ", CashMigrationRules.Name(head["NAMED"] as string), ScrapReceiptRegistry.BuyerNameLength) ?? "";
        var address = Cut(report, no, "ที่อยู่", CashMigrationRules.Name(head["ADDRESS"] as string), ScrapReceiptRegistry.AddressLength);
        var tel = Cut(report, no, "โทร", CashMigrationRules.Name(head["TEL"] as string), ScrapReceiptRegistry.TelLength);
        foreach (var t in ordered.Skip(1))
        {
            var other = t.Row;
            if (other.Date("DATE") is { } d && DateOnly.FromDateTime(d) != day) report.Add(ReceiptsTable, no, HeadRule, $"วันที่ต่างกันในใบเดียว: ใช้ {CashLineRules.Iso(day)} ทิ้ง {CashLineRules.Iso(DateOnly.FromDateTime(d))}", "ใช้หัวของบรรทัดแรก");
            if (CashMigrationRules.Name(other["NAMED"] as string) is { } n && !string.Equals(n, CashMigrationRules.Name(head["NAMED"] as string), StringComparison.Ordinal))
                report.Add(ReceiptsTable, no, HeadRule, $"ชื่อผู้ซื้อต่างกันในใบเดียว: ใช้ '{name}' ทิ้ง '{n}'", "ใช้หัวของบรรทัดแรก");
        }

        int? buyerId = name.Length > 0 && buyers.TryGetValue(name, out var b) ? b : null;
        if (name.Length > 0 && buyerId is null) unknownBuyers[name] = unknownBuyers.GetValueOrDefault(name) + taken.Count;
        var lines = new List<ImportedReceiptLine>();
        foreach (var t in ordered)
        {
            var unit = Cut(report, no, "หน่วย", CashMigrationRules.Name(t.Row["UNIT"] as string), ScrapReceiptRegistry.UnitLength);
            var itemName = t.Item.Length > ScrapReceiptRegistry.ItemNameLength ? Cut(report, no, "ชื่อรายการ", t.Item, ScrapReceiptRegistry.ItemNameLength)! : t.Item;
            int? itemId = items.TryGetValue(itemName, out var i) ? i : null;
            if (itemId is null) unknownItems[itemName] = unknownItems.GetValueOrDefault(itemName) + 1;
            lines.Add(new ImportedReceiptLine(t.Seq, itemName, itemId, unit, t.Qty, t.Price, t.Amount));
        }
        return new Built(no, new ImportedReceipt(no, day, name, address, tel, buyerId, lines, false), legacySum, rows);
    }

    /// <summary>The text cut to what the receipt keeps; a cut is listed with what was lost.</summary>
    private static string? Cut(MigrationReport report, string no, string field, string? text, int max)
    {
        if (text is null || text.Length <= max) return text;
        report.Add(ReceiptsTable, no, Other, $"{field}ยาว {text.Length.ToString(CultureInfo.InvariantCulture)} ตัว เกิน {max.ToString(CultureInfo.InvariantCulture)} → ตัดเหลือ {max.ToString(CultureInfo.InvariantCulture)}", "ตัดข้อความ");
        return text[..max];
    }

    /// <summary>Q-30: the names of buyers and kinds that the register lacks — reported only, one row for each name with the number of lines.</summary>
    private static void ReportRegisters(MigrationReport report, Dictionary<string, int> buyers, Dictionary<string, int> items)
    {
        foreach (var (name, rows) in buyers.OrderBy(p => p.Key, StringComparer.Ordinal))
            report.Add(ReceiptsTable, name, BuyerRule, $"ผู้ซื้อไม่อยู่ในทะเบียน ({rows.ToString(CultureInfo.InvariantCulture)} บรรทัด)", "รายงานอย่างเดียว (ใบเก็บชื่อตามเดิม)");
        foreach (var (name, rows) in items.OrderBy(p => p.Key, StringComparer.Ordinal))
            report.Add(ReceiptsTable, name, BuyerRule, $"ชนิดของเก่าไม่อยู่ในทะเบียน ({rows.ToString(CultureInfo.InvariantCulture)} บรรทัด)", "รายงานอย่างเดียว (ใบเก็บชื่อตามเดิม)");
        report.Notes.Add($"ผู้ซื้อนอกทะเบียน {buyers.Count.ToString(CultureInfo.InvariantCulture)} ชื่อ {buyers.Values.Sum().ToString(CultureInfo.InvariantCulture)} บรรทัด · ชนิดของเก่านอกทะเบียน {items.Count.ToString(CultureInfo.InvariantCulture)} ชื่อ {items.Values.Sum().ToString(CultureInfo.InvariantCulture)} บรรทัด (ไม่เพิ่มทะเบียน — Q-30)");
    }

    /// <summary>The numbers missing between the first and the last of a year (the legacy lacks one in 2554).</summary>
    private static void ReportGaps(MigrationReport report, List<string> numbers)
    {
        foreach (var year in numbers.Where(n => n.All(char.IsAsciiDigit) && n.Length > 2).GroupBy(n => n[..2], StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var seqs = year.Select(n => long.Parse(n[2..], NumberStyles.None, CultureInfo.InvariantCulture)).ToHashSet();
            var missing = Enumerable.Range((int)seqs.Min(), (int)(seqs.Max() - seqs.Min() + 1)).Where(s => !seqs.Contains(s)).Take(200).ToList();
            foreach (var s in missing) report.Add(ReceiptsTable, year.Key + s.ToString("000", CultureInfo.InvariantCulture), Other, "เลขที่ขาดในลำดับของปี", "รายงานอย่างเดียว");
        }
    }

    /// <summary>The count back: receipts and lines, the sum by month, the receipts waiting against <c>ขายของเก่า1</c>, and the total of every receipt against Access's own rounding (AC-24).</summary>
    private async Task CountBackAsync(LegacyDatabase legacy, MigrationReport report, List<Built> built, HashSet<string> waitingNumbers, int numbersInFile, int receiptsRejected, int rejectedRows, int sourceRows)
    {
        var inDatabase = await receiptDirectory.ListAsync(DateOnly.MinValue, DateOnly.MaxValue);
        var lineCount = built.Sum(b => b.Receipt.Lines.Count);
        report.Tables.Add(new TableCount(ReceiptsTable + " (ใบ)", "cash.ScrapReceipts", numbersInFile, 0, receiptsRejected, built.Count, inDatabase.Count));
        report.Tables.Add(new TableCount(ReceiptsTable + " (บรรทัด)", "cash.ScrapReceiptLines", sourceRows, 0, rejectedRows, lineCount, inDatabase.Sum(r => r.Lines.Count)));
        foreach (var year in built.Select(b => b.Receipt.Date.Year).Union(inDatabase.Select(r => r.Date.Year)).Distinct().Order())
        {
            var (inFile, here) = (built.Count(b => b.Receipt.Date.Year == year), inDatabase.Count(r => r.Date.Year == year));
            report.Tables.Add(new TableCount($"{ReceiptsTable} ปี {year.ToString(CultureInfo.InvariantCulture)}", $"cash.ScrapReceipts ปี {year.ToString(CultureInfo.InvariantCulture)}", inFile, 0, 0, inFile, here));
        }
        var waitingBuilt = built.Where(b => waitingNumbers.Contains(b.No)).ToList();
        var waiting = inDatabase.Count(r => r.RemittanceId is null);
        report.Tables.Add(new TableCount(WaitingTable + " (ค้างส่ง)", "cash.ScrapReceipts (รอนำส่ง)", legacy[WaitingTable].Select(r => CashMigrationRules.Name(r["NO"] as string)).OfType<string>().Distinct().Count(), 0,
            legacy[WaitingTable].Select(r => CashMigrationRules.Name(r["NO"] as string)).OfType<string>().Distinct().Count() - waitingBuilt.Count, waitingBuilt.Count, waiting));

        foreach (var month in built.Select(b => (b.Receipt.Date.Year, b.Receipt.Date.Month)).Union(inDatabase.Select(r => (r.Date.Year, r.Date.Month))).Distinct().Order())
        {
            var label = $"{month.Year.ToString("0000", CultureInfo.InvariantCulture)}-{month.Month.ToString("00", CultureInfo.InvariantCulture)}";
            report.MonthlyTotals.Add(new MonthlyTotal(ReceiptsTable, label, "จำนวนเงิน", (double)built.Where(b => (b.Receipt.Date.Year, b.Receipt.Date.Month) == month).Sum(b => b.LegacySum),
                (double)built.Where(b => (b.Receipt.Date.Year, b.Receipt.Date.Month) == month).Sum(b => b.Receipt.Lines.Sum(l => l.Amount)),
                (double)inDatabase.Where(r => (r.Date.Year, r.Date.Month) == month).Sum(r => r.Lines.Sum(l => l.Amount))));
        }

        // the amount waiting, receipt by receipt, against ขายของเก่า1; and every receipt's total (RND-A) against Access's rounding of the sum it has
        var waitingLegacy = legacy[WaitingTable].Where(r => CashMigrationRules.Name(r["NO"] as string) is not null)
            .GroupBy(r => CashMigrationRules.Name(r["NO"] as string)!, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Sum(r => CashLineRules.Decimal(r.Number("AMOUNT") ?? 0)), StringComparer.Ordinal);
        var byNo = inDatabase.ToDictionary(r => r.No, StringComparer.Ordinal);
        var waitingDiffers = 0;
        foreach (var (no, legacySum) in waitingLegacy)
            if (byNo.TryGetValue(no, out var r) && r.RemittanceId is null && Math.Abs(r.Lines.Sum(l => l.Amount) - legacySum) >= 0.005m)
            {
                waitingDiffers++;
                report.Add(ReceiptsTable, no, StateRule, $"ยอดรอนำส่งต่างจากขายของเก่า1: ใหม่ {CashLineRules.Money(r.Lines.Sum(l => l.Amount))} เดิม {CashLineRules.Money(legacySum)}", "รายงานอย่างเดียว");
            }
        var totalDiffers = 0;
        foreach (var b in built)
        {
            var access = Math.Round(b.LegacySum, 2, MidpointRounding.ToEven);
            if (byNo.TryGetValue(b.No, out var r) && r.Total != access)
            {
                totalDiffers++;
                report.Add(ReceiptsTable, b.No, TotalRule, $"ยอดรวม (RND-A) ใหม่ {CashLineRules.Money(r.Total)} ต่างจากที่ Access คำนวณ {CashLineRules.Money(access)} (ผลรวมของจำนวนเงินเดิม {CashLineRules.Money(b.LegacySum)} ปัดทีละบรรทัดก่อนรวม)", "รายงานอย่างเดียว ไม่ปรับให้ตรง");
            }
        }
        report.Notes.Add($"ยอดรอนำส่ง {waitingBuilt.Count.ToString(CultureInfo.InvariantCulture)} ใบ ต่างจากขายของเก่า1 {waitingDiffers.ToString(CultureInfo.InvariantCulture)} ใบ · ยอดรวมใบต่างจาก Access {totalDiffers.ToString(CultureInfo.InvariantCulture)} ใบ (AC-24)");
        var differs = built.Sum(b => b.Receipt.Lines.Count(l => CashLineRules.AmountDiffers(l.Qty, l.UnitPrice, l.Amount)));
        report.Notes.Add($"จำนวนเงิน ≠ จำนวน × ราคา {differs.ToString(CultureInfo.InvariantCulture)} บรรทัดของใบเสร็จ — เก็บตามต้นทาง ไม่คำนวณใหม่");
    }
}
