using Lockte.Hr;
using Lockte.Prod;
using Lockte.Prod.Data;

namespace Lockte.Migration;

/// <summary>
/// ย้ายสมุดสต็อกและการใช้งานแม่พิมพ์ (#126, ข้อตัดสิน #17 #27 #28) from <c>ผลผลิตและORDER.accdb</c>. Run it <b>after</b> <see cref="OrderMasterDataMigration"/> (products, molds, employees).
/// One ledger for every year: <c>P_ST</c> (the period now open) is loaded as open entries; <c>P_ADDST</c> from 1 Jan 2025 is loaded closed, marked with the cut that closed it (the first cut on or
/// after its date); the opening balance of each cut is kept as the history that <c>ต้นงวดP_ST</c> was — the rows of <c>ต้นงวดP_ST</c> are the history, so the opening rows that <c>P_ADDST</c>
/// carries are not loaded twice, and the opening of the latest cut is the one in <c>P_ST</c> (a copy in <c>ต้นงวดP_ST</c> is only checked against it). The kind of an entry is a field of its own
/// (ข้อสมมติ ช): <c>pd</c>, <c>tr</c>, the opening (<c>stock</c>), a hand entry; a kind that is none of them is a hand entry whose remark keeps what was typed. Cancelled rows (<c>de</c>),
/// a product not in the register and a row with no quantity are not moved and are listed. Q-47: the production of December counted twice in <c>P_ST</c> is found and reported every time,
/// and left out only when asked (<c>--remove-stock-duplicates yes</c>). The mold usage of the open period and the cumulative total at each cut date are loaded as they are (the latest total
/// is the start of the next cut). Deletes and loads again the stock entries and the mold usage, nothing else, no row-by-row history.
/// </summary>
public sealed class StockMigration(StockImport import, ProductDirectory productDirectory, MoldDirectory moldDirectory, EmployeeDirectory employeeDirectory)
{
    private const string Other = "NF-06";
    private const string Duplicate = "DQ-2";
    private const string BadDate = "DQ-4";
    private const string BadKind = "DQ-3";
    private const string Q47 = "Q-47";
    private const string Balance = "AC-09";

    public const string CurrentTable = "P_ST";
    public const string ClosedTable = "P_ADDST";
    public const string OpeningTable = "ต้นงวดP_ST";
    public const string MoldUsageTable = "การใช้งานของพิมพ์0";
    public const string MoldCutoffTable = "ตันงวดการใช้งานของพิมพ์0";

    private static readonly DateOnly From = LegacyDatabase.OrderHistoryFrom;

    private LegacyDatabase legacy = null!;
    private CodeMap map = null!;
    private MigrationReport report = null!;

    private sealed class Tally(string legacyTable, string newTable)
    {
        public string LegacyTable { get; } = legacyTable;
        public string NewTable { get; } = newTable;
        public int Source { get; set; }
        public int Excluded { get; set; }
        public int Rejected { get; set; }
        public int Loaded { get; set; }
    }

    /// <summary>A row of the three stock tables that can be moved.</summary>
    private sealed record Move(string Table, LegacyRow Source, DateOnly? Date, string? DocumentNo, int? LineNo, int ProductId, string ProductCode, decimal Quantity, int? EmployeeId, StockEntryKind Kind,
        string? Remark, string Key);

    public async Task RunAsync(LegacyDatabase legacyDatabase, CodeMap codeMap, MigrationReport migrationReport, bool removeProductionDuplicates = false)
    {
        (legacy, map, report) = (legacyDatabase, codeMap, migrationReport);
        await import.DeleteAllAsync();

        var products = (await productDirectory.SearchAsync(null, null, int.MaxValue)).ToDictionary(p => p.Code, p => p.Id, StringComparer.OrdinalIgnoreCase);
        var tallies = new Dictionary<string, Tally>
        {
            [CurrentTable] = new(CurrentTable, "prod.StockEntries (งวดที่เปิดอยู่)"),
            [ClosedTable] = new(ClosedTable, "prod.StockEntries (ปิดงวดแล้ว)"),
            [OpeningTable] = new(OpeningTable, "prod.StockEntries (ยอดยกมาแต่ละวันตัด)"),
            [MoldUsageTable] = new(MoldUsageTable, "prod.MoldUsages"),
            [MoldCutoffTable] = new(MoldCutoffTable, "prod.MoldCutoffs"),
        };
        foreach (var (table, tally) in tallies)
        {
            tally.Source = legacy.SourceRows(table);
            tally.Excluded = tally.Source - legacy[table].Count;
        }

        // 1. classify the rows of the three stock tables
        var moves = new Dictionary<string, List<Move>> { [CurrentTable] = [], [ClosedTable] = [], [OpeningTable] = [] };
        var employees = new Dictionary<string, int?>(StringComparer.OrdinalIgnoreCase);
        foreach (var table in moves.Keys)
        {
            var position = 0;
            foreach (var source in legacy[table])
            {
                position++;
                if (await ClassifyAsync(table, source, position, products, employees, tallies[table]) is { } move) moves[table].Add(move);
            }
        }

        // 2. the cuts: the dates of ต้นงวดP_ST and of the opening in P_ST
        var currentOpenings = moves[CurrentTable].Where(m => m.Kind == StockEntryKind.OpeningBalance).ToList();
        var cutDates = moves[OpeningTable].Where(m => m.Date is not null).Select(m => m.Date!.Value)
            .Concat(currentOpenings.Where(m => m.Date is not null).Select(m => m.Date!.Value)).Distinct().Order().ToList();
        var lastCut = cutDates.Count == 0 ? (DateOnly?)null : cutDates[^1];

        // 3. Q-47: rows of P_ST dated up to the last cut that are also in P_ADDST
        var removed = new HashSet<Move>();
        if (lastCut is { } last) FindProductionTwice(moves[CurrentTable], moves[ClosedTable], last, removeProductionDuplicates, tallies[CurrentTable], removed);

        // 4. the entries to load
        var entries = new List<(string Table, StockEntryImportRow Row)>();
        foreach (var move in moves[CurrentTable])
        {
            if (removed.Contains(move)) continue;
            entries.Add((CurrentTable, ToRow(move, null)));
            tallies[CurrentTable].Loaded++;
        }
        var afterLastCut = 0;
        var openingsInClosed = 0;
        foreach (var move in moves[ClosedTable])
        {
            if (move.Kind == StockEntryKind.OpeningBalance) { tallies[ClosedTable].Excluded++; openingsInClosed++; continue; } // the history is ต้นงวดP_ST
            DateOnly? closedOn = null;
            if (move.Date is { } date)
            {
                closedOn = cutDates.Cast<DateOnly?>().FirstOrDefault(c => c >= date);
                if (closedOn is null && lastCut is { } fallback) { closedOn = fallback; afterLastCut++; }
            }
            entries.Add((ClosedTable, ToRow(move, closedOn)));
            tallies[ClosedTable].Loaded++;
        }
        if (openingsInClosed > 0) report.Notes.Add($"P_ADDST: {openingsInClosed} แถวเป็นยอดยกมา (REF_NO = stock) — ไม่โหลดซ้ำ ใช้ยอดยกมาแต่ละวันตัดจาก ต้นงวดP_ST เป็นประวัติ");
        if (afterLastCut > 0) report.Notes.Add($"P_ADDST: {afterLastCut} แถวลงวันที่หลังวันตัดสุดท้าย — ใส่เครื่องหมายปิดงวดเป็นวันตัดสุดท้าย");

        var openingKeys = currentOpenings.Select(m => m.Date).ToHashSet();
        var tailOpen = 0;
        foreach (var move in moves[OpeningTable])
        {
            if (move.Date is null) { Reject(tallies[OpeningTable], OpeningTable, $"{move.ProductCode}", BadDate, "ยอดยกมาไม่มีวันที่ (หรือวันที่ผิดปี)"); continue; }
            var closedOn = cutDates.Where(c => c > move.Date!.Value).Cast<DateOnly?>().FirstOrDefault();
            if (closedOn is null && openingKeys.Contains(move.Date)) { tallies[OpeningTable].Excluded++; continue; } // the live opening is in P_ST; compared below
            if (closedOn is null) tailOpen++; // the latest cut with no opening in P_ST: the balance starts from it
            entries.Add((OpeningTable, ToRow(move, closedOn)));
            tallies[OpeningTable].Loaded++;
        }

        await import.AddEntriesAsync(entries.Select(e => e.Row));
        CompareOpenings(moves[OpeningTable], currentOpenings, lastCut);

        // 5. mold usage and the totals at the cut dates
        var molds = (await moldDirectory.GetAllAsync()).ToDictionary(m => m.Code, m => m.Id, StringComparer.OrdinalIgnoreCase);
        var usages = LoadUsages(molds, tallies[MoldUsageTable]);
        var cutoffs = LoadCutoffs(molds, tallies[MoldCutoffTable]);
        await import.AddMoldUsagesAsync(usages);
        await import.AddMoldCutoffsAsync(cutoffs);

        await CountBackAsync(tallies, entries, tailOpen, usages, cutoffs, removed);
    }

    // ---------- the rows ----------

    private async Task<Move?> ClassifyAsync(string table, LegacyRow source, int position, Dictionary<string, int> products, Dictionary<string, int?> employees, Tally tally)
    {
        var mark = source.Text("MARK");
        var documentNo = source.Text("REF_NO");
        var where = $"แถวที่ {position}" + (documentNo is null ? "" : $" เลขที่ {documentNo}");
        if (string.Equals(mark, "de", StringComparison.OrdinalIgnoreCase) || string.Equals(documentNo, "DE", StringComparison.OrdinalIgnoreCase))
        {
            Reject(tally, table, where, Other, "ชนิดรายการ de — ถูกยกเลิก (ตัดจากการส่งของวิธีเดิม)");
            return null;
        }
        var productCode = source.Text("PD_C");
        if (productCode is null) { Reject(tally, table, where, Other, "ไม่มีรหัสสินค้า"); return null; }
        if (!products.TryGetValue(productCode, out var productId)) { Reject(tally, table, $"{where} {productCode}", Other, $"รหัสสินค้า '{productCode}' ไม่มีในทะเบียนสินค้า"); return null; }
        if (source.Number("QTY") is not { } quantity) { Reject(tally, table, $"{where} {productCode}", Other, "ไม่มีจำนวน"); return null; }

        var kind = StockEntryKind.Manual;
        var remark = Truncate(source.Text("REM"), StockRegistry.RemarkLength);
        if (string.Equals(mark, "pd", StringComparison.OrdinalIgnoreCase)) kind = StockEntryKind.Production;
        else if (string.Equals(mark, "tr", StringComparison.OrdinalIgnoreCase)) kind = StockEntryKind.Withdrawal;
        else if (mark is null && string.Equals(documentNo, "stock", StringComparison.OrdinalIgnoreCase)) kind = StockEntryKind.OpeningBalance;
        else if (mark is not null)
        {
            // the legacy kept the kind in the same column as free text: what is not a kind stays as the remark of a hand entry
            report.Add(table, $"{where} {productCode}", BadKind, $"ชนิดรายการ '{mark}' ไม่ใช่ pd/tr — ถือเป็นรายการมือ เก็บค่าเดิมไว้ในหมายเหตุ", "โหลดเป็นรายการมือ");
            remark = Truncate(string.Join(" ", new[] { remark, $"[ชนิดเดิม {mark}]" }.Where(t => t is not null)), StockRegistry.RemarkLength);
        }

        DateOnly? date = null;
        if (source.Date("DATE") is { } raw)
        {
            if (raw.Year is < 1990 or > 2100) report.Add(table, $"{where} {productCode}", BadDate, $"DATE = {raw:yyyy-MM-dd} เป็นปีที่ไม่น่าเป็นไปได้", MigrationReport.LoadedBlank);
            else
            {
                date = DateOnly.FromDateTime(raw);
                if (table == CurrentTable && date < From) report.Add(table, $"{where} {productCode}", BadDate, $"DATE = {date:yyyy-MM-dd} ก่อน 1 ม.ค. 2568 ในสมุดงวดปัจจุบัน (วันที่ผิดปี?)", "โหลดตามค่าเดิม");
            }
        }
        else if (kind != StockEntryKind.OpeningBalance) report.Add(table, $"{where} {productCode}", BadDate, "ไม่มีวันที่", MigrationReport.LoadedBlank);

        int? employeeId = null;
        if (source.Text("ID") is { } employeeCode)
        {
            if (!employees.TryGetValue(employeeCode, out var cached))
            {
                var entry = map.Find(CodeKinds.ProductionEmployee, employeeCode);
                cached = entry?.Code == CodeKinds.Nobody ? null : (await employeeDirectory.FindByCodeAsync(entry?.Code ?? employeeCode))?.Id;
                employees[employeeCode] = cached;
            }
            employeeId = cached;
        }
        int? line = source.Text("SUF_NO") is { } suffix && int.TryParse(suffix, out var n) ? n : null;
        var rounded = Math.Round((decimal)quantity, 4);
        var key = string.Join("|", date?.ToString("yyyy-MM-dd"), documentNo?.ToUpperInvariant(), line, productCode.ToUpperInvariant(), rounded, mark?.ToLowerInvariant());
        return new Move(table, source, date, Truncate(documentNo, StockRegistry.DocumentNoLength), line, productId, productCode, rounded, employeeId, kind, remark, key);
    }

    private static StockEntryImportRow ToRow(Move m, DateOnly? closedOn)
        => new(m.Date, m.DocumentNo, m.LineNo, m.ProductId, m.Quantity, m.EmployeeId, m.Kind, m.Remark, closedOn);

    private static string? Truncate(string? text, int length) => text is not null && text.Length > length ? text[..length] : text;

    // ---------- Q-47 ----------

    private void FindProductionTwice(List<Move> current, List<Move> closed, DateOnly lastCut, bool remove, Tally tally, HashSet<Move> removed)
    {
        var left = closed.Where(m => m.Kind == StockEntryKind.Production).GroupBy(m => m.Key).ToDictionary(g => g.Key, g => g.Count());
        var count = 0;
        var units = 0m;
        var perMonth = new SortedDictionary<string, (int Rows, decimal Units)>(StringComparer.Ordinal);
        foreach (var move in current.Where(m => m.Kind == StockEntryKind.Production && m.Date is { } d && d <= lastCut))
        {
            if (!left.TryGetValue(move.Key, out var n) || n == 0) continue;
            left[move.Key] = n - 1;
            count++;
            units += move.Quantity;
            var month = $"{move.Date!.Value.Year:0000}-{move.Date.Value.Month:00}";
            var (rows, sum) = perMonth.GetValueOrDefault(month);
            perMonth[month] = (rows + 1, sum + move.Quantity);
            report.Add(CurrentTable, $"{move.DocumentNo} #{move.LineNo} {move.ProductCode}", Q47,
                $"ผลผลิตวันที่ {move.Date:yyyy-MM-dd} จำนวน {move.Quantity:#,##0.####} อยู่ใน P_ADDST (ปิดงวดแล้ว) ด้วย — นับซ้ำในยอดคงเหลือปัจจุบัน", remove ? "ไม่โหลด (ตัดแถวซ้ำ)" : "โหลดตามเดิม (ไม่ตัด)");
            if (remove) removed.Add(move);
        }
        if (count == 0) { report.Notes.Add("Q-47: ไม่พบผลผลิตที่นับซ้ำระหว่าง P_ST กับ P_ADDST"); return; }
        report.Notes.Add($"Q-47: พบผลผลิต (pd) ลงวันที่ถึง {lastCut:yyyy-MM-dd} ใน P_ST ที่อยู่ใน P_ADDST ด้วย {count} แถว รวม {units:#,##0.####} หน่วย ({string.Join(", ", perMonth.Select(p => $"{p.Key}: {p.Value.Rows} แถว {p.Value.Units:#,##0.####}"))}) — "
            + (remove ? "ตัดแถวซ้ำออก (--remove-stock-duplicates yes) ยอดคงเหลือไม่นับซ้ำ" : "ไม่ตัด (ค่าเริ่มต้น) ยอดคงเหลือปัจจุบันนับซ้ำเท่านี้ — ใช้ --remove-stock-duplicates yes เพื่อซ้อม/UAT ก่อนปิดปี"));
        if (remove) tally.Excluded += count;
    }

    // ---------- opening balances ----------

    private void CompareOpenings(List<Move> history, List<Move> live, DateOnly? lastCut)
    {
        if (lastCut is not { } last) { report.Notes.Add("ยอดยกมา: ไม่พบวันตัดสต็อกตั้งแต่ปี 2568"); return; }
        var fromHistory = history.Where(m => m.Date == last).GroupBy(m => m.ProductCode, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.Sum(m => m.Quantity), StringComparer.OrdinalIgnoreCase);
        var fromCurrent = live.Where(m => m.Date == last).GroupBy(m => m.ProductCode, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.Sum(m => m.Quantity), StringComparer.OrdinalIgnoreCase);
        if (fromHistory.Count == 0 || fromCurrent.Count == 0) return;
        var different = fromHistory.Keys.Concat(fromCurrent.Keys).Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(code => fromHistory.GetValueOrDefault(code) != fromCurrent.GetValueOrDefault(code)).Order(StringComparer.Ordinal).ToList();
        foreach (var code in different)
            report.Add(OpeningTable, $"{last:yyyy-MM-dd} {code}", Balance, $"ยอดยกมา ณ {last:yyyy-MM-dd}: ต้นงวดP_ST = {fromHistory.GetValueOrDefault(code):#,##0.####} แต่ใน P_ST = {fromCurrent.GetValueOrDefault(code):#,##0.####}", "โหลดตาม P_ST (ยอดคงเหลือใช้ค่านี้)");
        report.Notes.Add($"ยอดยกมา ณ {last:yyyy-MM-dd}: ต้นงวดP_ST {fromHistory.Count} สินค้า เทียบกับยอดยกมาใน P_ST {fromCurrent.Count} สินค้า — ต่างกัน {different.Count} สินค้า");
    }

    // ---------- mold usage ----------

    private List<MoldUsageImportRow> LoadUsages(Dictionary<string, int> molds, Tally tally)
    {
        var rows = new List<MoldUsageImportRow>();
        var position = 0;
        foreach (var source in legacy[MoldUsageTable])
        {
            position++;
            var mold = source.Text("MOULD_ID");
            var where = $"แถวที่ {position} {mold}";
            var mark = source.Text("MARK");
            if (string.Equals(mark, "de", StringComparison.OrdinalIgnoreCase)) { Reject(tally, MoldUsageTable, where, Other, "เครื่องหมาย de — แถวชั่วคราวระหว่างตัดยอด"); continue; }
            if (mold is null || !molds.TryGetValue(mold, out var moldId)) { Reject(tally, MoldUsageTable, where, Other, $"แม่พิมพ์ '{mold}' ไม่มีในทะเบียนแม่พิมพ์"); continue; }
            if (source.Date("DAY") is not { } day || day.Year is < 1990 or > 2100) { Reject(tally, MoldUsageTable, where, BadDate, "ไม่มีวันที่หรือวันที่ผิดปี"); continue; }
            var shots = (int)Math.Round(source.Number("PD_QTY") ?? 0);
            rows.Add(new MoldUsageImportRow(moldId, DateOnly.FromDateTime(day), shots, string.Equals(mark, "ST", StringComparison.OrdinalIgnoreCase)));
            tally.Loaded++;
        }
        return rows;
    }

    private List<MoldCutoffImportRow> LoadCutoffs(Dictionary<string, int> molds, Tally tally)
    {
        var rows = new Dictionary<(DateOnly CutDate, int MoldId), MoldCutoffImportRow>();
        var position = 0;
        foreach (var source in legacy[MoldCutoffTable])
        {
            position++;
            var mold = source.Text("MOULD_ID");
            var where = $"แถวที่ {position} {mold}";
            if (string.Equals(source.Text("MARK"), "de", StringComparison.OrdinalIgnoreCase)) { Reject(tally, MoldCutoffTable, where, Other, "เครื่องหมาย de — ชั่วคราว"); continue; }
            if (mold is null || !molds.TryGetValue(mold, out var moldId)) { Reject(tally, MoldCutoffTable, where, Other, $"แม่พิมพ์ '{mold}' ไม่มีในทะเบียนแม่พิมพ์"); continue; }
            if (source.Date("DAY") is not { } day || day.Year is < 1990 or > 2100) { Reject(tally, MoldCutoffTable, where, BadDate, "ไม่มีวันที่หรือวันที่ผิดปี"); continue; }
            var key = (DateOnly.FromDateTime(day), moldId);
            var cumulative = (int)Math.Round(source.Number("PD_QTY") ?? 0);
            if (rows.ContainsKey(key))
            {
                report.Add(MoldCutoffTable, $"{key.Item1:yyyy-MM-dd} {mold}", Duplicate, "แม่พิมพ์เดียวกันมีหลายแถวในวันตัดเดียวกัน — ใช้แถวหลังสุด", "โหลดแถวหลังสุด");
                tally.Rejected++;
            }
            else tally.Loaded++;
            rows[key] = new MoldCutoffImportRow(moldId, key.Item1, cumulative);
        }
        return [.. rows.Values];
    }

    // ---------- counting back ----------

    private async Task CountBackAsync(Dictionary<string, Tally> tallies, List<(string Table, StockEntryImportRow Row)> entries, int tailOpen, List<MoldUsageImportRow> usages,
        List<MoldCutoffImportRow> cutoffs, HashSet<Move> removed)
    {
        var months = await import.GetMonthTotalsAsync();
        var (usageRows, usageShots, dbCutoffs) = await import.GetMoldTotalsAsync();
        var dbOpen = months.Where(m => !m.Closed).Sum(m => m.Entries);
        var dbClosedOpenings = months.Where(m => m.Closed && m.Kind == StockEntryKind.OpeningBalance).Sum(m => m.Entries);
        var dbClosedOther = months.Where(m => m.Closed && m.Kind != StockEntryKind.OpeningBalance).Sum(m => m.Entries);
        var inDatabase = new Dictionary<string, int>
        {
            [CurrentTable] = dbOpen - tailOpen,
            [ClosedTable] = dbClosedOther,
            [OpeningTable] = dbClosedOpenings + tailOpen,
            [MoldUsageTable] = usageRows,
            [MoldCutoffTable] = dbCutoffs.Count,
        };
        foreach (var (table, tally) in tallies)
            report.Tables.Add(new TableCount(tally.LegacyTable, tally.NewTable, tally.Source, tally.Excluded, tally.Rejected, tally.Loaded, inDatabase[table]));

        // AC-20: entries and Σ จำนวน by month — as read (every row with a quantity), as it should load, as it is
        static string Month(DateTime? d) => d is { } date ? $"{date.Year:0000}-{date.Month:00}" : "";
        var source = new[] { CurrentTable, ClosedTable }.SelectMany(t => legacy[t]).Where(r => r.Number("QTY") is not null).GroupBy(r => Month(r.Date("DATE"))).ToDictionary(g => g.Key);
        var expected = entries.Where(e => e.Row.Kind != StockEntryKind.OpeningBalance).GroupBy(e => e.Row.Date is { } d ? $"{d.Year:0000}-{d.Month:00}" : "").ToDictionary(g => g.Key);
        var db = months.Where(m => m.Kind != StockEntryKind.OpeningBalance).GroupBy(m => m.Month).ToDictionary(g => g.Key);
        foreach (var month in source.Keys.Concat(expected.Keys).Concat(db.Keys).Distinct().OrderBy(m => m, StringComparer.Ordinal))
        {
            var s = source.GetValueOrDefault(month);
            var e = expected.GetValueOrDefault(month);
            var d = db.GetValueOrDefault(month);
            report.MonthlyTotals.Add(new MonthlyTotal("P_ST + P_ADDST", month, "จำนวนรายการ", s?.Count() ?? 0, e?.Count() ?? 0, d?.Sum(x => x.Entries) ?? 0));
            report.MonthlyTotals.Add(new MonthlyTotal("P_ST + P_ADDST", month, "Σ จำนวน", s?.Sum(r => r.Number("QTY") ?? 0) ?? 0, e?.Sum(x => (double)x.Row.Quantity) ?? 0, (double)(d?.Sum(x => x.Quantity) ?? 0)));
        }

        // AC-11: the opening balance of every cut date — Σ over the products, and the number of products
        var openings = await import.GetOpeningsAsync();
        var sourceOpenings = legacy[OpeningTable].Where(r => r.Date("DATE") is not null && r.Number("QTY") is not null).GroupBy(r => r.Date("DATE")!.Value.ToString("yyyy-MM-dd")).ToDictionary(g => g.Key);
        var expectedOpenings = entries.Where(e => e.Row.Kind == StockEntryKind.OpeningBalance && e.Row.Date is not null).GroupBy(e => e.Row.Date!.Value.ToString("yyyy-MM-dd")).ToDictionary(g => g.Key);
        var dbOpenings = openings.GroupBy(o => o.CutDate.ToString("yyyy-MM-dd")).ToDictionary(g => g.Key);
        foreach (var cut in sourceOpenings.Keys.Concat(expectedOpenings.Keys).Concat(dbOpenings.Keys).Distinct().Order(StringComparer.Ordinal))
        {
            var s = sourceOpenings.GetValueOrDefault(cut);
            var e = expectedOpenings.GetValueOrDefault(cut);
            var d = dbOpenings.GetValueOrDefault(cut);
            report.MonthlyTotals.Add(new MonthlyTotal(OpeningTable, cut, "จำนวนสินค้าที่มียอดยกมา", s?.Count() ?? 0, e?.Count() ?? 0, d?.Count() ?? 0));
            report.MonthlyTotals.Add(new MonthlyTotal(OpeningTable, cut, "Σ ยอดยกมา", s?.Sum(r => r.Number("QTY") ?? 0) ?? 0, e?.Sum(x => (double)x.Row.Quantity) ?? 0, (double)(d?.Sum(x => x.Quantity) ?? 0)));
        }

        // AC-09: the balance of every product — Σ of what is open, against Σ QTY of P_ST as read
        var balances = await import.GetBalancesAsync();
        var asRead = legacy[CurrentTable].Where(r => r.Text("PD_C") is not null && r.Number("QTY") is not null && !string.Equals(r.Text("MARK"), "de", StringComparison.OrdinalIgnoreCase))
            .GroupBy(r => r.Text("PD_C")!, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => Math.Round((decimal)g.Sum(r => r.Number("QTY")!.Value), 4), StringComparer.OrdinalIgnoreCase);
        var differ = 0;
        foreach (var code in balances.Keys.Concat(asRead.Keys).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal))
        {
            var loaded = balances.GetValueOrDefault(code);
            var original = asRead.GetValueOrDefault(code);
            if (Math.Abs(loaded - original) < 0.0001m) continue;
            differ++;
            report.Add(CurrentTable, code, Balance, $"ยอดคงเหลือ {loaded:#,##0.####} ต่างจาก Σ จำนวน ของ P_ST เดิม {original:#,##0.####} (ต่าง {loaded - original:#,##0.####})",
                removed.Count > 0 ? "อธิบายด้วยแถวที่ตัดตาม Q-47 หรือแถวที่ไม่โหลด (DQ)" : "อธิบายด้วยแถวที่ไม่โหลด (DQ)");
        }
        report.Notes.Add($"ยอดคงเหลือ (AC-09): {balances.Count} สินค้ามียอดในฐานข้อมูล Σ {balances.Values.Sum():#,##0.####} · Σ P_ST เดิม {asRead.Values.Sum():#,##0.####} · {differ} สินค้าต่างกัน (รายการอยู่ใน dq-exceptions.csv)");

        // AC-13: the cumulative total of each mold at each cut date — Σ and the number of molds
        var sourceCutoffs = legacy[MoldCutoffTable].Where(r => r.Date("DAY") is not null).GroupBy(r => r.Date("DAY")!.Value.ToString("yyyy-MM-dd")).ToDictionary(g => g.Key);
        var expectedCutoffs = cutoffs.GroupBy(c => c.CutDate.ToString("yyyy-MM-dd")).ToDictionary(g => g.Key);
        var dbByDate = dbCutoffs.GroupBy(c => c.CutDate.ToString("yyyy-MM-dd")).ToDictionary(g => g.Key);
        foreach (var cut in sourceCutoffs.Keys.Concat(expectedCutoffs.Keys).Concat(dbByDate.Keys).Distinct().Order(StringComparer.Ordinal))
        {
            var s = sourceCutoffs.GetValueOrDefault(cut);
            var e = expectedCutoffs.GetValueOrDefault(cut);
            var d = dbByDate.GetValueOrDefault(cut);
            report.MonthlyTotals.Add(new MonthlyTotal(MoldCutoffTable, cut, "จำนวนแม่พิมพ์", s?.Count() ?? 0, e?.Count() ?? 0, d?.Count() ?? 0));
            report.MonthlyTotals.Add(new MonthlyTotal(MoldCutoffTable, cut, "Σ ยอดสะสม", s?.Sum(r => r.Number("PD_QTY") ?? 0) ?? 0, e?.Sum(x => (double)x.Cumulative) ?? 0, d?.Sum(x => (double)x.Cumulative) ?? 0));
        }
        report.MonthlyTotals.Add(new MonthlyTotal(MoldUsageTable, "งวดที่เปิดอยู่", "Σ จำนวนครั้ง", legacy[MoldUsageTable].Sum(r => r.Number("PD_QTY") ?? 0), usages.Sum(u => (double)u.Shots), usageShots));
    }

    private void Reject(Tally tally, string table, string key, string rule, string problem)
    {
        tally.Rejected++;
        report.Add(table, key, rule, problem, MigrationReport.NotLoaded);
    }
}
