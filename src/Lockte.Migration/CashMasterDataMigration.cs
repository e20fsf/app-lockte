using System.Globalization;
using Lockte.Cash;
using Lockte.SharedKernel.Persistence;

namespace Lockte.Migration;

/// <summary>What the migration of <c>เงินสดย่อย.accdb</c> decides on its own, kept apart from the database so it can be tested without one.</summary>
public static class CashMigrationRules
{
    /// <summary>The legacy tables that are not moved, and why (data-model "ตารางที่ไม่ย้าย"). The lines and the receipts are moved by their own tickets (#236, #241).</summary>
    public static IReadOnlyList<(string Table, string Reason)> NotMoved { get; } =
    [
        ("Sheet1", "นำเข้าจาก Excel รายชื่อประเภทเก่า ไม่มีวัตถุใดอ้าง (F)"),
        ("ชื่อของเก่า", "ไม่มีวัตถุใดอ้าง — ใช้ รายชื่อของเก่า แทน (F)"),
        ("เดือน", "จำนวนวันทำงานรายเดือนปี 2549 ไม่มีวัตถุใดอ้าง (F)"),
        ("สำเนาของ ขายของเก่า / สำเนาของ ซื้อของเงินสด / สำเนาของ ขายของเก่า1–4", "snapshot ของ 'ลบข้อมูลปีที่แล้ว' (BR-020) — ระบบใหม่เก็บข้อมูลทุกปีในฐานข้อมูลเดียว (F)"),
        ("วันที่ / วันที่1 / FROM_NO / Table11 / ตาราง1", "ตารางพารามิเตอร์ 1 แถวของหน้าจอ — ระบบใหม่ไม่ใช้ตารางพัก (D)"),
        ("ขายของเก่า2 / REFNO / NO / ขายของเก่า3 / ขายของเก่า4", "ตารางพักผล — ระบบใหม่คำนวณใหม่จากใบเสร็จ; เลขที่ตัดยอดล่าสุดย้ายเป็นค่าตั้งในตั๋ว 13 (E)"),
        ("Switchboard Items / Switchboard Items1", "ถอดเป็นแผนผังเมนูแล้ว"),
    ];

    /// <summary>A name as the register keeps it (blanks at the ends gone, any run of blanks or line breaks one space); null when nothing is left.</summary>
    public static string? Name(string? raw) => ExpenseTypeRegistry.CleanName(raw);

    /// <summary>Whether the text was typed over more than one line.</summary>
    public static bool HasLineBreak(string? raw) => raw is not null && (raw.Contains('\n') || raw.Contains('\r'));

    /// <summary>Whether the cleaning changed more than the blanks at the two ends (a repeated blank, a line break).</summary>
    public static bool HadInnerBlanks(string? raw) => raw is not null && Name(raw) is { } clean && clean != raw.Trim();
}

/// <summary>
/// ย้ายข้อมูลหลักของ <c>เงินสดย่อย.accdb</c> (#231): ประเภทค่าใช้จ่าย (<c>ค่าใช้จ่ายต่างๆ</c> + the names <c>ซื้อของเงินสด.TYPE</c> uses that the register lacks, loaded as เลิกใช้ — assumption ซ), ผู้ซื้อของเก่า and
/// ชนิดของเก่า. Everything goes through the public registries, so a loaded row passes the same checks as one keyed on screen, and nothing it does is written to the change history (#39).
/// Names are cleaned the way the screens clean them (blanks and line breaks), so two spellings that differ only there are one type; a spelling that differs otherwise is not guessed — the
/// code map (kind <c>cash-expense-type</c>, legacy = the cleaned name from TYPE, code = the type it belongs to) is where a person merges them, and the run is repeated. The report names
/// types, buyers and kinds: real data, kept outside git. The tables are read from a copy of the file.
/// </summary>
public sealed class CashMasterDataMigration(
    AuditScope audit,
    CashMasterImport import,
    ExpenseTypeRegistry types,
    ExpenseTypeDirectory typeDirectory,
    ScrapBuyerRegistry buyers,
    ScrapBuyerDirectory buyerDirectory,
    ScrapItemRegistry items,
    ScrapItemDirectory itemDirectory)
{
    public const string TypeTable = "ค่าใช้จ่ายต่างๆ";
    public const string UsedTypeTable = "ซื้อของเงินสด.TYPE";
    public const string BuyerTable = "ผู้ซื้อของเก่า";
    public const string ItemTable = "รายชื่อของเก่า";
    private const string LinesTable = "ซื้อของเงินสด";
    private const string ReceiptsTable = "ขายของเก่า";
    private const string TypeRule = "DQ-Q05";
    private const string LineBreakRule = "DQ-Q27";
    private const string BuyerRule = "DQ-Q30";
    private const string Other = "NF-06";

    private LegacyDatabase legacy = null!;
    private CodeMap map = null!;
    private MigrationReport report = null!;
    private readonly List<Tally> tallies = [];

    /// <summary>Once per instance (it is scoped): the run keeps what it loaded in fields.</summary>
    public async Task<MigrationReport> RunAsync(LegacyDatabase legacyDatabase, CodeMap codeMap)
    {
        if (report is not null) throw new InvalidOperationException("CashMasterDataMigration runs once per scope.");
        (legacy, map, report) = (legacyDatabase, codeMap, new MigrationReport { Source = legacyDatabase.Source });
        using (audit.SuppressRowChanges())
        {
            await import.DeleteMasterAsync();
            var used = await LoadTypesAsync();
            await LoadBuyersAsync();
            await LoadItemsAsync();
            ReportUnused(used);
        }
        await CountBackAsync();
        foreach (var (table, reason) in CashMigrationRules.NotMoved) report.Notes.Add($"ไม่ย้าย {table}: {reason}");
        return report;
    }

    // ---------- ประเภทค่าใช้จ่าย ----------

    /// <summary>Loads the register, then the names the lines use; returns the cleaned names of the types the lines use (for the "never used" check).</summary>
    private async Task<HashSet<string>> LoadTypesAsync()
    {
        var registry = Count(TypeTable, "cash.ExpenseTypes (ใช้งาน)", legacy[TypeTable].Count);
        var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var number = 0;
        foreach (var row in legacy[TypeTable])
        {
            number++;
            var raw = row["CASH_NAME"] as string;
            if (CashMigrationRules.Name(raw) is not { } name) { Reject(registry, TypeTable, $"แถวที่ {number}", Other, "ไม่มีชื่อค่าใช้จ่าย — ไม่โหลด"); continue; }
            if (CashMigrationRules.HasLineBreak(raw)) report.Add(TypeTable, name, LineBreakRule, "ชื่อในทะเบียนมีขึ้นบรรทัดใหม่ → ตัดเป็นช่องว่าง", "แก้อัตโนมัติ");
            if (!known.Add(name)) { Reject(registry, TypeTable, name, TypeRule, "ชื่อซ้ำหลังตัดช่องว่าง — รวมกับแถวแรก ไม่โหลดแถวนี้"); continue; }
            var saved = await types.SaveAsync(new ExpenseTypeInput(null, name, null));
            if (!saved.Succeeded) { Reject(registry, TypeTable, name, Other, string.Join(" · ", saved.Errors)); continue; }
            registry.Loaded++;
        }

        // the names the lines use, cleaned, with how many lines use each
        var lines = new Dictionary<string, (int Rows, int WithLineBreak)>(StringComparer.OrdinalIgnoreCase);
        var blankRows = 0;
        foreach (var row in legacy[LinesTable])
        {
            var raw = row["TYPE"] as string;
            if (CashMigrationRules.Name(raw) is not { } name) { blankRows++; continue; }
            var (rows, withBreak) = lines.GetValueOrDefault(name);
            lines[name] = (rows + 1, withBreak + (CashMigrationRules.HasLineBreak(raw) ? 1 : 0));
        }
        if (blankRows > 0) report.Notes.Add($"บรรทัดค่าใช้จ่าย {blankRows} แถวไม่มีประเภท (ตั๋ว 08 ตัดสินว่าจะโหลดอย่างไร)");

        var inRegistry = new HashSet<string>(known, StringComparer.OrdinalIgnoreCase);
        var retired = Count(UsedTypeTable, "cash.ExpenseTypes (เลิกใช้)", lines.Keys.Count(name => !known.Contains(name)));
        var usedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, (rows, withBreak)) in lines.OrderBy(l => l.Key, StringComparer.Ordinal))
        {
            if (withBreak > 0) report.Add(UsedTypeTable, name, LineBreakRule, $"{withBreak} แถวของบรรทัดค่าใช้จ่ายมีขึ้นบรรทัดใหม่ในประเภท → ตัดเป็นช่องว่าง", "แก้อัตโนมัติ");
            if (known.Contains(name))
            {
                // in the register, or a type this run made for another spelling that was mapped to this one
                if (!inRegistry.Contains(name)) { retired.Excluded++; report.Add(UsedTypeTable, name, TypeRule, $"{rows} แถวใช้ชื่อที่ตรงกับประเภทที่สร้างจากการจับคู่", "ใช้ประเภทเดียวกัน"); }
                usedTypes.Add(name);
                Use(name, name);
                continue;
            }

            var target = map.Find(CodeKinds.CashExpenseType, name)?.Code is { } mapped ? CashMigrationRules.Name(mapped) ?? name : name;
            var fromMap = !string.Equals(target, name, StringComparison.Ordinal);
            if (known.Contains(target))
            {
                retired.Excluded++;
                usedTypes.Add(target);
                Use(name, target, fromMap: true);
                report.Add(UsedTypeTable, name, TypeRule, $"{rows} แถวใช้ชื่อที่ไม่อยู่ในทะเบียน → จับคู่เข้าประเภท '{target}' ตามตารางจับคู่", "จับคู่ตามคำตัดสิน");
                continue;
            }
            // a name the register lacks: a type เลิกใช้ with the text as it was (assumption ซ), so the summaries of the old data stay as they were
            var saved = await types.SaveAsync(new ExpenseTypeInput(null, target, null));
            if (!saved.Succeeded) { Reject(retired, UsedTypeTable, target, Other, string.Join(" · ", saved.Errors)); continue; }
            var created = (await typeDirectory.GetAsync(saved.Id!.Value))!;
            var turnedOff = await types.SetActiveAsync(created.Id, false, created.RowVersion);
            if (!turnedOff.Succeeded) { Reject(retired, UsedTypeTable, target, Other, string.Join(" · ", turnedOff.Errors)); continue; }
            known.Add(target);
            usedTypes.Add(target);
            retired.Loaded++;
            Use(name, target, fromMap);
            report.Add(UsedTypeTable, name, TypeRule, $"{rows} แถวใช้ชื่อที่ไม่อยู่ในทะเบียน" + (fromMap ? $" → ประเภท '{target}' ตามตารางจับคู่" : ""),
                "สร้างเป็นประเภทเลิกใช้ (ใส่ cash-expense-type,<ชื่อ>,<ประเภทที่ถูก> ในตารางจับคู่เพื่อรวม)");
        }
        return usedTypes;
    }

    // ---------- ผู้ซื้อของเก่า ----------

    private async Task LoadBuyersAsync()
    {
        var tally = Count(BuyerTable, "cash.ScrapBuyers", legacy[BuyerTable].Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var usedNames = legacy[ReceiptsTable].Select(r => CashMigrationRules.Name(r["NAMED"] as string)).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        var number = 0;
        foreach (var row in legacy[BuyerTable])
        {
            number++;
            var raw = row["NAMED"] as string;
            if (CashMigrationRules.Name(raw) is not { } name) { Reject(tally, BuyerTable, $"แถวที่ {number}", Other, "ไม่มีชื่อผู้ซื้อ — ไม่โหลด (ระบบเดิมลบแถวนี้)"); continue; }
            if (CashMigrationRules.HadInnerBlanks(raw)) report.Add(BuyerTable, name, BuyerRule, "ชื่อมีช่องว่างเกินหรือขึ้นบรรทัดใหม่ → ตัดเหลือช่องว่างเดียว", "แก้อัตโนมัติ");
            if (!seen.Add(name)) { Reject(tally, BuyerTable, name, BuyerRule, "ชื่อซ้ำหลังตัดช่องว่าง — โหลดแถวแรก ไม่โหลดแถวนี้"); continue; }
            var saved = await buyers.SaveAsync(new ScrapBuyerInput(null, name, row.Text("ADDRESS"), row.Text("TEL"), null));
            if (!saved.Succeeded) { Reject(tally, BuyerTable, name, Other, string.Join(" · ", saved.Errors)); continue; }
            tally.Loaded++;
            if (!usedNames.Contains(name)) report.Add(BuyerTable, name, Other, "ผู้ซื้อในทะเบียนแต่ไม่มีใบเสร็จใดใช้ชื่อนี้", "โหลดตามเดิม");
        }
    }

    // ---------- ชนิดของเก่า ----------

    private async Task LoadItemsAsync()
    {
        var tally = Count(ItemTable, "cash.ScrapItems", legacy[ItemTable].Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var usedNames = legacy[ReceiptsTable].Select(r => CashMigrationRules.Name(r["NAMES"] as string)).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        var number = 0;
        foreach (var row in legacy[ItemTable])
        {
            number++;
            var raw = row["NAMES"] as string;
            if (CashMigrationRules.Name(raw) is not { } name) { Reject(tally, ItemTable, $"แถวที่ {number}", Other, "ไม่มีชื่อของเก่า — ไม่โหลด (ระบบเดิมลบแถวนี้)"); continue; }
            if (CashMigrationRules.HadInnerBlanks(raw)) report.Add(ItemTable, name, BuyerRule, "ชื่อมีช่องว่างเกินหรือขึ้นบรรทัดใหม่ → ตัดเหลือช่องว่างเดียว", "แก้อัตโนมัติ");
            if (!seen.Add(name)) { Reject(tally, ItemTable, name, BuyerRule, "ชื่อซ้ำหลังตัดช่องว่าง — โหลดแถวแรก ไม่โหลดแถวนี้"); continue; }
            var saved = await items.SaveAsync(new ScrapItemInput(null, name, row.Text("UNIT"), null));
            if (!saved.Succeeded) { Reject(tally, ItemTable, name, Other, string.Join(" · ", saved.Errors)); continue; }
            tally.Loaded++;
            if (!usedNames.Contains(name)) report.Add(ItemTable, name, Other, "ชนิดของเก่าในทะเบียนแต่ไม่มีใบเสร็จใดใช้ชื่อนี้", "โหลดตามเดิม");
        }
    }

    /// <summary>The types in the register that no line uses (the ones the lines use that the register lacked are, by definition, used).</summary>
    private void ReportUnused(HashSet<string> used)
    {
        foreach (var row in legacy[TypeTable])
            if (CashMigrationRules.Name(row["CASH_NAME"] as string) is { } name && !used.Contains(name))
                report.Add(TypeTable, name, Other, "ประเภทในทะเบียนแต่ไม่มีบรรทัดค่าใช้จ่ายใดใช้", "โหลดตามเดิม (ใช้งาน)");
    }

    // ---------- รายงานตรวจยอด ----------

    private async Task CountBackAsync()
    {
        var allTypes = await typeDirectory.ListAllAsync();
        var inDatabase = new Dictionary<string, int>
        {
            ["cash.ExpenseTypes (ใช้งาน)"] = allTypes.Count(t => t.IsActive),
            ["cash.ExpenseTypes (เลิกใช้)"] = allTypes.Count(t => !t.IsActive),
            ["cash.ScrapBuyers"] = (await buyerDirectory.ListAsync()).Count,
            ["cash.ScrapItems"] = (await itemDirectory.ListAsync()).Count,
        };
        report.Tables.AddRange(tallies.Select(t => new TableCount(t.LegacyTable, t.NewTable, t.Source, t.Excluded, t.Rejected, t.Loaded, inDatabase[t.NewTable])));
        var grouped = report.Exceptions.GroupBy(e => (e.Table, e.Rule, e.Action)).OrderBy(g => g.Key.Table, StringComparer.Ordinal).ThenBy(g => g.Key.Rule, StringComparer.Ordinal);
        foreach (var g in grouped)
            report.Notes.Add($"DQ {g.Key.Rule} {g.Key.Table}: {g.Count().ToString(CultureInfo.InvariantCulture)} รายการ — {g.Key.Action}");
    }

    // ---------- helpers ----------

    private Tally Count(string legacyTable, string newTable, int source)
    {
        var tally = new Tally(legacyTable, newTable) { Source = source };
        tallies.Add(tally);
        return tally;
    }

    private void Reject(Tally tally, string table, string key, string rule, string problem)
    {
        tally.Rejected++;
        report.Add(table, key, rule, problem, MigrationReport.NotLoaded);
    }

    /// <summary>One line of code-map-used.csv: what a name in TYPE became (the file a person edits and gives back as the next run's map).</summary>
    private void Use(string legacyName, string target, bool fromMap = false)
    {
        if (report.CodeMapUsed.Any(u => u.Kind == CodeKinds.CashExpenseType && string.Equals(u.Legacy, legacyName, StringComparison.OrdinalIgnoreCase))) return;
        report.CodeMapUsed.Add(new CodeMapUse(CodeKinds.CashExpenseType, legacyName, target, null, null, fromMap || map.Find(CodeKinds.CashExpenseType, legacyName) is not null));
    }

    private sealed class Tally(string legacyTable, string newTable)
    {
        public string LegacyTable { get; } = legacyTable;
        public string NewTable { get; } = newTable;
        public int Source { get; init; }
        public int Rejected { get; set; }
        public int Loaded { get; set; }
        public int Excluded { get; set; }
    }
}
