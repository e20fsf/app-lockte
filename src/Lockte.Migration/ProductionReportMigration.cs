using Lockte.Hr;
using Lockte.Org;
using Lockte.Prod;

namespace Lockte.Migration;

/// <summary>
/// ย้ายใบรายงานผลผลิต 7 แผนก (<c>P_REP&lt;แผนก&gt;</c>, ตั้งแต่ 1 ม.ค. 2025) เข้าตารางเดียว (#122, ข้อตัดสิน #17 #27). Run it <b>after</b> <see cref="OrderMasterDataMigration"/> — the products, molds, machines and employees
/// are those it loaded. A report is the rows of one <c>REF_NO</c> (its head: the earliest valid date and the machine most of the rows name); a line is a row, kept in the order of <c>SUF_NO</c> —
/// the key (เลขที่ใบ, ลำดับ) is <b>not unique</b> in the real file, so a repeated number gets the next free one and the row is kept. "เข้าสต็อกแล้ว" is set when a line of the report is found in
/// the transferred copy <c>P_REP</c> (matched on number, line, product, quantities, Lot and date — a copy that differs from its source, edited after the transfer, and a source row with no copy are
/// counted). Fixed on the way: จำนวนรวม = ดี + เสีย, a malformed shift → A–G by the code map. A row with no จำนวนดี was a deleted line (BR-019) and is not moved.
/// Deletes and loads the reports again, nothing else, no row-by-row history.
/// </summary>
public sealed class ProductionReportMigration(
    ProductionReportImport import,
    ProductDirectory productDirectory,
    MoldDirectory moldDirectory,
    OrgDirectory orgDirectory,
    EmployeeDirectory employeeDirectory)
{
    private const string Other = "NF-06";
    private const string Duplicate = "DQ-2";
    private const string ZeroDate = "DQ-4";
    private const string CopyTable = "P_REP";

    /// <summary>The seven department tables and the letter of each (BR-015).</summary>
    public static IReadOnlyDictionary<string, string> DepartmentTables { get; } = new Dictionary<string, string>
    {
        ["P_REPไดคาสท์"] = "D",
        ["P_REPตกแต่ง"] = "T",
        ["P_REPอัดผ้าเบรก"] = "B",
        ["P_REPฝนผ้าเบรก"] = "G",
        ["P_REPผ้าคลัทช์และดิสเบรค"] = "K",
        ["P_REPประกอบ"] = "A",
        ["P_REPบรรจุ"] = "P",
    };

    private LegacyDatabase legacy = null!;
    private CodeMap map = null!;
    private MigrationReport report = null!;
    private readonly Dictionary<string, Tally> tallies = [];

    private sealed class Tally(string legacyTable, string newTable)
    {
        public string LegacyTable { get; } = legacyTable;
        public string NewTable { get; } = newTable;
        public int Source { get; init; }
        public int Excluded { get; set; }
        public int Rejected { get; set; }
        public int Loaded { get; set; }
    }

    private sealed record Row(string Table, string Department, LegacyRow Source, string No, int Line, decimal Good);

    public async Task RunAsync(LegacyDatabase legacyDatabase, CodeMap codeMap, MigrationReport migrationReport)
    {
        (legacy, map, report) = (legacyDatabase, codeMap, migrationReport);
        await import.DeleteAllAsync();

        var products = (await productDirectory.SearchAsync(null, null, int.MaxValue)).ToDictionary(p => p.Code, p => p, StringComparer.OrdinalIgnoreCase);
        var molds = (await moldDirectory.GetAllAsync()).ToDictionary(m => m.Code, m => m.Id, StringComparer.OrdinalIgnoreCase);
        var machines = (await orgDirectory.GetMachinesAsync()).ToDictionary(m => m.Code, m => m.Id, StringComparer.OrdinalIgnoreCase);
        var shifts = (await orgDirectory.GetShiftsAsync()).ToDictionary(s => s.Code, s => s.Id, StringComparer.OrdinalIgnoreCase);
        var employees = new Dictionary<string, int?>(StringComparer.OrdinalIgnoreCase);

        foreach (var (table, _) in DepartmentTables)
            tallies[table] = new Tally(table, "prod.ProductionReports (บรรทัด)") { Source = legacy.SourceRows(table), Excluded = legacy.SourceRows(table) - legacy[table].Count };

        // 1. the rows of each department
        var rows = new List<Row>();
        var withoutQuantity = 0;
        foreach (var (table, department) in DepartmentTables)
        {
            var tally = tallies[table];
            var position = 0;
            foreach (var source in legacy[table])
            {
                position++;
                var where = $"แถวที่ {position}";
                if (source.Text("REF_NO") is not { } no) { Reject(tally, table, $"(REF_NO ว่าง) {where}", Other, "ไม่มีเลขที่ใบรายงาน"); continue; }
                if (source.Number("QTY") is not { } quantity) { tally.Excluded++; withoutQuantity++; continue; } // a blank จำนวนดี is a deleted line (BR-019)
                rows.Add(new Row(table, department, source, no.Trim().ToUpperInvariant(), Int(source, "SUF_NO") ?? 0, Math.Round((decimal)quantity, 4)));
            }
        }
        if (withoutQuantity > 0) report.Notes.Add($"ใบรายงานผลผลิต: {withoutQuantity} แถวไม่มีจำนวนดี (บรรทัดที่ลบแล้วตาม BR-019) — ไม่ย้าย");

        // 2. the transferred copy, to tell which reports are in the stock: a multiset of the keys of its rows
        var copy = new Dictionary<string, int>();
        foreach (var source in legacy[CopyTable])
        {
            var key = Key(source);
            copy[key] = copy.GetValueOrDefault(key) + 1;
        }
        var copyRows = legacy[CopyTable].Count;

        // 3. reports: the rows of one number
        var imports = new List<ProductionReportImportRow>();
        var usedNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var severalMachines = 0;
        var duplicateKeys = 0;
        var matchedWithCopy = 0;
        foreach (var group in rows.GroupBy(r => (r.Department, r.No)))
        {
            var ordered = group.OrderBy(r => r.Line == 0 ? int.MaxValue : r.Line).ToList();
            var first = ordered[0];
            var dates = ordered.Select(r => CleanDate(r.Source, "DATE", r.Table, r.No)).OfType<DateOnly>().ToList();
            var machineCodes = ordered.Select(r => r.Source.Text("MC_NO")).Where(m => m is not null).GroupBy(m => m!, StringComparer.OrdinalIgnoreCase).OrderByDescending(g => g.Count()).ToList();
            if (machineCodes.Count > 1)
            {
                severalMachines++;
                // The machine of a line is not kept (the report has one machine, at its head — decided by the users): the machine named most often is the report's. The report lists them.
                report.Add(first.Table, group.Key.No, Duplicate, $"หลายเครื่องในใบเดียว ({string.Join(", ", machineCodes.Select(g => $"{g.Key} ×{g.Count()}"))}) — ใช้เครื่อง {machineCodes[0].Key} เป็นเครื่องของใบ ข้อมูลเครื่องระดับบรรทัดไม่ถูกเก็บ", "โหลดโดยใช้เครื่องเดียวของใบ");
            }
            int? machineId = null;
            if (machineCodes.Count > 0)
            {
                if (machines.TryGetValue(machineCodes[0].Key, out var id)) machineId = id;
                else report.Add(first.Table, first.No, Other, $"เครื่องจักร '{machineCodes[0].Key}' ไม่มีในทะเบียนเครื่องจักร", MigrationReport.LoadedBlank);
            }

            var number = group.Key.No;
            if (!usedNumbers.Add(number))
            {
                var suffix = 2;
                string renamed;
                while (!usedNumbers.Add(renamed = $"{number}-{suffix}")) suffix++;
                report.Add(first.Table, number, Duplicate, $"เลขที่ใบรายงานซ้ำกับแผนกอื่น — เปลี่ยนเป็น {renamed}", "โหลดโดยเปลี่ยนเลขที่");
                number = renamed;
            }

            var lines = new List<ProductionReportLineImport>();
            var taken = new HashSet<int>();
            var stocked = false;
            foreach (var row in ordered)
            {
                var s = row.Source;
                var line = row.Line;
                if (line <= 0 || !taken.Add(line))
                {
                    if (line > 0) duplicateKeys++;
                    line = taken.Count == 0 ? 1 : taken.Max() + 1;
                    taken.Add(line);
                }
                var key = Key(s);
                if (copy.TryGetValue(key, out var left) && left > 0) { copy[key] = left - 1; stocked = true; matchedWithCopy++; }

                int? productId = null;
                var name = s.Text("PD_N") ?? "";
                if (s.Text("PD_C") is { } productCode)
                {
                    if (products.TryGetValue(productCode, out var product)) { productId = product.Id; if (name.Length == 0) name = product.Name; }
                    else report.Add(row.Table, $"{row.No} #{line}", Other, $"รหัสสินค้า '{productCode}' ไม่มีในทะเบียนสินค้า", MigrationReport.LoadedBlank);
                }
                int? shiftId = null;
                if (s.Text("SHIFT") is { } shiftText)
                {
                    var shiftCode = map.Find(CodeKinds.Shift, shiftText)?.Code ?? shiftText.Trim();
                    if (shifts.TryGetValue(shiftCode, out var id)) shiftId = id;
                    else report.Add(row.Table, $"{row.No} #{line}", Other, $"กะ '{shiftText}' ไม่ใช่กะ A–G — ใส่ {CodeKinds.Shift},{shiftText},<รหัสกะ> ในตารางจับคู่", MigrationReport.LoadedBlank);
                }
                int? moldId = null;
                if (s.Text("MOULD") is { } moldCode)
                {
                    if (molds.TryGetValue(moldCode, out var id)) moldId = id;
                    else report.Add(row.Table, $"{row.No} #{line}", Other, $"แม่พิมพ์ '{moldCode}' ไม่มีในทะเบียนแม่พิมพ์", MigrationReport.LoadedBlank);
                }
                int? employeeId = null;
                if (s.Text("EMP_ID") is { } employeeCode)
                {
                    if (!employees.TryGetValue(employeeCode, out var cached))
                    {
                        var entry = map.Find(CodeKinds.ProductionEmployee, employeeCode);
                        cached = entry?.Code == CodeKinds.Nobody ? null : (await employeeDirectory.FindByCodeAsync(entry?.Code ?? employeeCode))?.Id;
                        employees[employeeCode] = cached;
                    }
                    employeeId = cached;
                }

                var reject = Decimal(s, "NG");
                var qaSelected = string.Equals(s.Text("MARK1"), "y", StringComparison.OrdinalIgnoreCase); // the QA mark (SCR-12): kept as the user left it
                if (qaSelected) qaMarked++;
                var good = row.Good;
                var total = good + (reject ?? 0);
                if (Decimal(s, "TOTAL") is null || Decimal(s, "TOTAL") != total) totalsFixed++;
                lines.Add(new ProductionReportLineImport(line, shiftId, Decimal(s, "TT_T"), s.Text("LOT_MTR1"), s.Text("LOT_MTR2"), productId, name, s.Text("U_NAME"), good, reject, total,
                    employeeId, moldId, s.Text("LOT"), qaSelected));
                tallies[row.Table].Loaded++;
            }
            imports.Add(new ProductionReportImportRow(first.Department, dates.Count == 0 ? null : dates.Min(), number, machineId, stocked, lines));
        }
        await import.AddAsync(imports);

        Notes(severalMachines, duplicateKeys, totalsFixed, copy, copyRows, rows.Count, matchedWithCopy);
        await CountBackAsync(rows, imports);
    }

    private int totalsFixed;
    private int qaMarked;

    private static string Key(LegacyRow row)
        => string.Join("|", row.Text("REF_NO")?.Trim().ToUpperInvariant(), Int(row, "SUF_NO"), row.Text("PD_C")?.Trim().ToUpperInvariant(), Decimal(row, "QTY"), Decimal(row, "NG"), row.Text("LOT"),
            row.Date("DATE")?.ToString("yyyy-MM-dd"));

    private void Notes(int severalMachines, int duplicateKeys, int fixedTotals, Dictionary<string, int> copyLeft, int copyRows, int sourceRows, int matched)
    {
        if (severalMachines > 0) report.Notes.Add($"ใบรายงานผลผลิต {severalMachines} ใบมีหลายเครื่องในใบเดียวกัน — ใช้เครื่องที่พบมากสุดเป็นเครื่องของใบ (ผู้ใช้รับทราบ: ไม่เก็บเครื่องระดับบรรทัด กระทบเป้าตามเครื่องของ BR-020 และตัดยอดแม่พิมพ์) — รายการอยู่ใน dq-exceptions.csv");
        if (duplicateKeys > 0) report.Notes.Add($"ใบรายงานผลผลิต {duplicateKeys} แถวมีเลขที่+ลำดับซ้ำกับแถวอื่นของใบเดียวกัน — เก็บไว้ทุกแถว ให้ลำดับถัดไปที่ว่าง (คีย์เดิมไม่ unique)");
        if (qaMarked > 0) report.Notes.Add($"ใบรายงานผลผลิต {qaMarked} แถวยังติดเครื่องหมายสุ่มตรวจ (MARK1 = Y) ค้างอยู่ — ย้ายตามเดิม ผู้ใช้ QA ลบเครื่องหมายเอง (SCR-12)");
        if (fixedTotals > 0) report.Notes.Add($"ใบรายงานผลผลิต {fixedTotals} แถวที่จำนวนรวมว่างหรือไม่เท่า ดี + เสีย — คำนวณใหม่เป็น ดี + เสีย (AC-21)");
        var unmatchedCopies = copyLeft.Values.Sum();
        report.Notes.Add($"สำเนา P_REP (ถึงสต็อก) {copyRows} แถว: ตรงกับต้นทาง {matched} แถว · ไม่ตรงกับต้นทาง (แก้หลังโอนหรือไม่มีต้นทางตั้งแต่ปี 2568) {unmatchedCopies} แถว · แถวต้นทาง {sourceRows - matched} แถวไม่อยู่ในสำเนา (ยังไม่โอน/แก้หลังโอน)");
        report.Tables.Add(new TableCount(CopyTable, "prod.ProductionReports (สถานะเข้าสต็อกแล้ว)", legacy.SourceRows(CopyTable), legacy.SourceRows(CopyTable), 0, 0, 0));
    }

    private async Task CountBackAsync(List<Row> rows, List<ProductionReportImportRow> imports)
    {
        var inDatabase = await import.GetMonthTotalsAsync();
        var perDepartment = inDatabase.GroupBy(m => m.DepartmentCode).ToDictionary(g => g.Key, g => g.Sum(m => m.Lines));
        foreach (var (table, department) in DepartmentTables)
        {
            var t = tallies[table];
            report.Tables.Add(new TableCount(t.LegacyTable, t.NewTable, t.Source, t.Excluded, t.Rejected, t.Loaded, perDepartment.GetValueOrDefault(department)));
        }

        // AC-20: lines and Σ ดี / เสีย by department and month — as read, as it should load, as it is
        static string Month(DateTime? d) => d is { } date ? $"{date.Year:0000}-{date.Month:00}" : "";
        foreach (var (table, department) in DepartmentTables)
        {
            var source = legacy[table].Where(r => r.Number("QTY") is not null).GroupBy(r => Month(r.Date("DATE"))).ToDictionary(g => g.Key);
            var expected = imports.Where(i => i.DepartmentCode == department).SelectMany(i => i.Lines.Select(l => (Month: i.ReportDate is { } d ? $"{d.Year:0000}-{d.Month:00}" : "", Line: l)))
                .GroupBy(x => x.Month).ToDictionary(g => g.Key);
            var db = inDatabase.Where(m => m.DepartmentCode == department).ToDictionary(m => m.Month);
            foreach (var month in source.Keys.Concat(expected.Keys).Concat(db.Keys).Distinct().OrderBy(m => m, StringComparer.Ordinal))
            {
                var s = source.GetValueOrDefault(month);
                var e = expected.GetValueOrDefault(month);
                var d = db.GetValueOrDefault(month);
                report.MonthlyTotals.Add(new MonthlyTotal(table, month, "จำนวนบรรทัด", s?.Count() ?? 0, e?.Count() ?? 0, d?.Lines ?? 0));
                report.MonthlyTotals.Add(new MonthlyTotal(table, month, "Σ จำนวนดี", s?.Sum(r => r.Number("QTY") ?? 0) ?? 0, e?.Sum(x => (double)x.Line.Good) ?? 0, (double)(d?.Good ?? 0)));
                report.MonthlyTotals.Add(new MonthlyTotal(table, month, "Σ เสีย", s?.Sum(r => r.Number("NG") ?? 0) ?? 0, e?.Sum(x => (double)(x.Line.Reject ?? 0)) ?? 0, (double)(d?.Reject ?? 0)));
            }
        }
    }

    // ---------- helpers ----------

    private void Reject(Tally tally, string table, string key, string rule, string problem)
    {
        tally.Rejected++;
        report.Add(table, key, rule, problem, MigrationReport.NotLoaded);
    }

    private DateOnly? CleanDate(LegacyRow row, string column, string table, string key)
    {
        if (row.Date(column) is not { } date) return null;
        if (date.Year < 1900) return null;
        if (date.Year is < 1990 or > 2100)
        {
            report.Add(table, key, ZeroDate, $"{column} = {date:yyyy-MM-dd} เป็นปีที่ไม่น่าเป็นไปได้", MigrationReport.LoadedBlank);
            return null;
        }
        return DateOnly.FromDateTime(date);
    }

    private static int? Int(LegacyRow row, string column) => row.Number(column) is { } n ? (int)Math.Round(n) : null;

    private static decimal? Decimal(LegacyRow row, string column) => row.Number(column) is { } n ? Math.Round((decimal)n, 4) : null;
}
