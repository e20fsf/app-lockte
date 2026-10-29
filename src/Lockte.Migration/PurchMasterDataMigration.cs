using System.Globalization;
using System.Text.RegularExpressions;
using Lockte.Org;
using Lockte.Prod;
using Lockte.Purch;
using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;

namespace Lockte.Migration;

/// <summary>
/// ย้ายข้อมูลหลักของ <c>วัตถุดิบ.accdb</c> (#217): the lists (เครดิต, ประเภทธุรกิจ, ผู้จัดซื้อ, เหตุผล, หมายเหตุ, กลุ่มรหัส), the prefix of the request numbers of each แผนก (matched to the register of
/// <c>org</c> — never created again), ผู้ขาย and วัตถุดิบ. Everything goes through the module's public registries, so a loaded row passes the same checks as one keyed on screen, and nothing it does
/// is written to the change history (#39). A row or value it cannot take goes to the DQ report instead of stopping the run, to be decided in the code map and run again; a supplier code that
/// repeats is **not** merged by the tool (Q-10): the first row is loaded and the others are reported. The tables are read from a copy of the file, never from the file in use.
/// </summary>
public sealed partial class PurchMasterDataMigration(
    AuditScope audit,
    PurchMasterImport import,
    SupplierRegistry suppliers,
    SupplierDirectory supplierDirectory,
    MaterialRegistry materials,
    MaterialDirectory materialDirectory,
    RequestSetupRegistry requestSetup,
    RequestSetupDirectory requestSetupDirectory,
    PurchSettingsDirectory settings,
    OrgDirectory org,
    ProductDirectory products)
{
    private const string Other = "NF-06";
    private const string DuplicateSupplier = "DQ-01";
    private const string DuplicateName = "DQ-02";
    private const string DepartmentByName = "DQ-5";
    private const string BadCode = "DQ-15";

    private LegacyDatabase legacy = null!;
    private CodeMap map = null!;
    private MigrationReport report = null!;
    private readonly List<Tally> tallies = [];
    private readonly Dictionary<string, int> supplierIds = new(StringComparer.OrdinalIgnoreCase);
    private int departmentsMatched;

    /// <summary>Once per instance (it is scoped): the run keeps what it loaded in fields.</summary>
    public async Task<MigrationReport> RunAsync(LegacyDatabase legacyDatabase, CodeMap codeMap)
    {
        if (report is not null) throw new InvalidOperationException("PurchMasterDataMigration runs once per scope.");
        (legacy, map, report) = (legacyDatabase, codeMap, new MigrationReport { Source = legacyDatabase.Source });
        using (audit.SuppressRowChanges())
        {
            await import.DeleteMasterAsync();
            await LoadListsAsync();
            await LoadDepartmentsAsync();
            await LoadSuppliersAsync();
            await LoadMaterialsAsync();
        }
        await CountBackAsync();
        return report;
    }

    // ---------- the lists ----------

    private async Task LoadListsAsync()
    {
        // เครดิต: a number of days or "เงินสด" (0); the days the suppliers use that the list lacks are added, as the form lists the values in use
        var days = new List<int>();
        var creditTally = Count("CREDIT", "purch.SupplierCreditOptions", legacy["CREDIT"].Count);
        foreach (var row in legacy["CREDIT"])
        {
            if (Credit(row.Text("CREDIT")) is { } d) { days.Add(d); creditTally.Loaded++; }
            else Reject(creditTally, "CREDIT", row.Text("CREDIT") ?? "(ว่าง)", Other, "เครดิตแปลงเป็นจำนวนวันไม่ได้");
        }
        foreach (var d in legacy["SP"].Select(r => Credit(r.Text("CREDIT"))).OfType<int>().Distinct().Where(d => !days.Contains(d)))
        {
            days.Add(d);
            report.ListAdditions.Add(("เครดิต", d == 0 ? "เงินสด" : d.ToString(CultureInfo.InvariantCulture)));
        }
        var businessTally = Count("ประเภทธุรกิจ", "purch.SupplierBusinessTypes", legacy["ประเภทธุรกิจ"].Count);
        var types = new List<string>();
        foreach (var row in legacy["ประเภทธุรกิจ"])
        {
            if (row.Text("BUSINESS") is { } type && !types.Contains(type)) { types.Add(type); businessTally.Loaded++; }
            else Reject(businessTally, "ประเภทธุรกิจ", row.Text("BUSINESS") ?? "(ว่าง)", Other, "ประเภทธุรกิจว่างหรือซ้ำ");
        }
        foreach (var type in legacy["SP"].Select(r => r.Text("BUSINESS")).OfType<string>().Distinct().Where(t => !types.Contains(t)))
        {
            types.Add(type);
            report.ListAdditions.Add(("ประเภทธุรกิจ", type));
        }
        Check(await suppliers.SaveOptionsAsync(days, types), "ตัวเลือกเครดิตและประเภทธุรกิจ");

        var locationTally = Count("PURC", "purch.PurchasingLocations", legacy["PURC"].Count);
        var locations = new List<PurchasingLocationDto>();
        foreach (var row in legacy["PURC"])
        {
            if (row.Text("ID") is { } code && row.Text("NAME") is { } name && locations.All(l => l.Code != code)) { locations.Add(new PurchasingLocationDto(code, name)); locationTally.Loaded++; }
            else Reject(locationTally, "PURC", row.Text("ID") ?? "(ว่าง)", Other, "ผู้จัดซื้อไม่มีรหัสหรือชื่อ หรือรหัสซ้ำ");
        }
        Check(await requestSetup.SaveLocationsAsync(locations), "ผู้จัดซื้อ");

        var reasonTexts = legacy["เหตุผลในการขอซื้อ"].Select(r => r.Text("REMARK")).OfType<string>().ToList();
        var noteTexts = legacy["หมายเหตุ"].Select(r => r.Text("MEM")).OfType<string>().ToList();
        await import.AddReasonsAndNotesAsync(reasonTexts, noteTexts);
        ListTally("เหตุผลในการขอซื้อ", "purch.RequestReasons", legacy["เหตุผลในการขอซื้อ"].Count, reasonTexts.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        ListTally("หมายเหตุ", "purch.RequestNotes", legacy["หมายเหตุ"].Count, noteTexts.Distinct(StringComparer.OrdinalIgnoreCase).Count());

        // กลุ่มรหัส: the LIKE pattern B* is the prefix B
        var prefixes = legacy["รหัสตัวแรกของวัตถุดิบ"].Select(r => r.Text("ID")?.TrimEnd('*').Trim()).OfType<string>().Where(p => p.Length > 0).ToList();
        Check(await materials.SaveGroupsAsync(prefixes), "กลุ่มรหัสวัตถุดิบ");
        ListTally("รหัสตัวแรกของวัตถุดิบ", "purch.MaterialGroups", legacy["รหัสตัวแรกของวัตถุดิบ"].Count, prefixes.Select(p => p.ToUpperInvariant()).Distinct().Count());
    }

    /// <summary>A list table whose rows are all loaded but for the blank ones and the repeats, which are counted as excluded by design.</summary>
    private void ListTally(string table, string newTable, int source, int loaded)
    {
        var tally = Count(table, newTable, source);
        tally.Loaded = loaded;
        tally.Excluded = source - loaded;
    }

    // ---------- แผนก: matched to the register, only the prefix of its request numbers is kept ----------

    private async Task LoadDepartmentsAsync()
    {
        const string table = "รายชื่อแผนก";
        var tally = Count(table, "org.Departments (จับคู่)", legacy[table].Count);
        var departments = await org.GetDepartmentsAsync();
        foreach (var row in legacy[table])
        {
            if (row.Text("department") is not { } name) { Reject(tally, table, "(ว่าง)", Other, "ไม่มีชื่อแผนก"); continue; }
            var entry = map.Find(CodeKinds.Department, name);
            var found = entry?.Code is { } code
                ? departments.FirstOrDefault(d => string.Equals(d.Code, code, StringComparison.OrdinalIgnoreCase))
                : departments.FirstOrDefault(d => string.Equals(d.Name, name, StringComparison.Ordinal));
            if (found is null)
            {
                Reject(tally, table, name, DepartmentByName, entry?.Code is { } c
                    ? $"ตารางจับคู่ให้รหัส '{c}' แต่ไม่มีแผนกนี้ในทะเบียนแผนก"
                    : $"ไม่พบแผนกชื่อนี้ในทะเบียนแผนก — ใส่ {CodeKinds.Department},{name},<รหัสแผนก> ในตารางจับคู่");
                continue;
            }
            var prefix = entry?.Prefix ?? Prefix(row.Text("CODE"));
            if (prefix is null) { Reject(tally, table, name, Other, "ไม่มี pattern เลขที่ใบขอซื้อ"); continue; }
            var saved = await requestSetup.SetPrefixAsync(found.Id, prefix, null);
            if (!saved.Succeeded) { Reject(tally, table, name, Other, Errors(saved)); continue; }
            tally.Excluded++; // by design: the แผนก register is the organisation module's, only the prefix is ours
            departmentsMatched++;
            UseCode(CodeKinds.Department, name, found.Code, found.Name, prefix, entry);
        }
    }

    /// <summary>The prefix of a pattern such as <c>D6*</c>: the stars and the digits tied to the year go (D6* → D, MT* → MT).</summary>
    public static string? Prefix(string? pattern)
    {
        var text = pattern?.Trim().TrimEnd('*').TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
        return string.IsNullOrEmpty(text) ? null : text.ToUpperInvariant();
    }

    // ---------- ผู้ขาย (SP) ----------

    private async Task LoadSuppliersAsync()
    {
        const string table = "SP";
        var tally = Count(table, "purch.Suppliers", legacy[table].Count);
        var groups = new List<(string Code, List<LegacyRow> Rows)>();
        foreach (var row in legacy[table])
        {
            if (row.Text("CODE") is not { } code) { Reject(tally, table, "(CODE ว่าง)", Other, "ไม่มีรหัสผู้ขาย"); continue; }
            if (row.Text("NAME") is null) { Reject(tally, table, code, Other, "ไม่มีชื่อผู้ขาย"); continue; }
            var key = code.ToUpperInvariant();
            if (groups.FirstOrDefault(g => g.Code == key) is { Rows: { } rows }) rows.Add(row); else groups.Add((key, [row]));
        }
        foreach (var (code, rows) in groups)
        {
            // Q-10: the tool never merges a repeated code on its own — the first row is loaded, the others wait for a decision
            foreach (var _ in rows.Skip(1)) Reject(tally, table, code, DuplicateSupplier, $"รหัสผู้ขายซ้ำ {rows.Count} แถว — โหลดแถวแรก รอคำตัดสิน Q-10 สำหรับแถวที่เหลือ");
            var row = rows[0];
            var credit = Credit(row.Text("CREDIT"));
            if (row.Text("CREDIT") is { } creditText && credit is null) report.Add(table, code, Other, $"เครดิต '{creditText}' แปลงเป็นจำนวนวันไม่ได้", MigrationReport.LoadedBlank);
            var vat = row.Text("VAT")?.ToUpperInvariant();
            if (vat is null) report.Add(table, code, Other, "ไม่ระบุ VAT — โหลดเป็นคิด VAT เหมือนที่ระบบเดิมคิดให้", "โหลดเป็น VAT");
            var saved = await suppliers.SaveAsync(new SupplierInput(null, code, row.Text("NAME")!, null, row.Text("ADDRESS1"), row.Text("ADDRESS2"), row.Text("TEL_1"), row.Text("TEL_2"),
                row.Text("FAX_NO"), row.Text("MG"), credit, vat != "NO", row.Text("PRODUCT"), row.Text("BUSINESS"), row.Text("CERTIFICATE") == "มี", row.Text("USE IN PRODUCT") == "ใช่"));
            if (!saved.Succeeded) { Reject(tally, table, code, Other, Errors(saved)); continue; }
            tally.Loaded++;
            supplierIds[code] = saved.Id!.Value;
        }
    }

    // ---------- วัตถุดิบ (MTR_NAME) ----------

    private async Task LoadMaterialsAsync()
    {
        const string table = "MTR_NAME";
        var tally = Count(table, "purch.Materials", legacy[table].Count);
        var resale = (await settings.GetAsync()).ResaleGroups;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var names = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var row in legacy[table])
        {
            if (row.Text("CODE") is not { } code) { Reject(tally, table, "(CODE ว่าง)", Other, "ไม่มีรหัสวัตถุดิบ"); continue; }
            if (row.Text("NAME") is not { } name) { Reject(tally, table, code, Other, "ไม่มีชื่อวัตถุดิบ (ระบบเดิมลบแถวที่ไม่มีชื่อเมื่อออกจากฟอร์ม)"); continue; }
            if (!WellFormedCode().IsMatch(code)) { Reject(tally, table, code, BadCode, "รหัสผิดรูปแบบ (ต้องเป็นตัวอักษร 1–2 ตัวตามด้วยตัวเลข)"); continue; }
            if (!seen.Add(code)) { Reject(tally, table, code, Other, "รหัสวัตถุดิบซ้ำ"); continue; }

            int? latest = null;
            if (row.Text("SUP_CODE") is { } supplierCode)
            {
                if (supplierIds.TryGetValue(supplierCode, out var id)) latest = id;
                else report.Add(table, code, Other, $"ผู้ขายล่าสุด '{supplierCode}' ไม่อยู่ในทะเบียนผู้ขายที่โหลด", MigrationReport.LoadedBlank);
            }
            int? productId = null;
            if (resale.Any(g => code.StartsWith(g.Prefix, StringComparison.OrdinalIgnoreCase)))
            {
                if (await products.FindByCodeAsync(code) is { } product) productId = product.Id;
                else report.Add(table, code, Other, "สินค้าซื้อมาขายนี้ไม่มีสินค้าของฝ่ายผลิตรหัสเดียวกัน (ข้อสมมติ ญ)", MigrationReport.LoadedBlank);
            }
            var saved = await materials.SaveAsync(new MaterialInput(null, code, name, null, row.Text("U_NAME"), Decimal(row, "MINIMUM") is { } min and > 0 ? min : null, Decimal(row, "COST") ?? 0, row.Text("INCOMING") == "ใช่",
                Decimal(row, "BUY") ?? 0, productId, latest));
            if (!saved.Succeeded) { Reject(tally, table, code, Other, Errors(saved)); continue; }
            tally.Loaded++;
            if (!names.TryAdd(name, [code])) names[name].Add(code);
        }
        // DQ-02: the name is no key any more (the document keeps its own text), so a repeated name is only told
        foreach (var (_, codes) in names.Where(n => n.Value.Count > 1))
            report.Add(table, string.Join(" ", codes), DuplicateName, $"ชื่อวัตถุดิบซ้ำ {codes.Count} รหัส — โหลดทุกรหัส (ระบบใหม่อ้างด้วยรหัส ไม่ใช่ชื่อ)", "โหลดทุกแถว");
    }

    // ---------- รายงานตรวจยอด ----------

    private async Task CountBackAsync()
    {
        var supplierRows = await supplierDirectory.GetAllAsync();
        var inDatabase = new Dictionary<string, int>
        {
            ["purch.SupplierCreditOptions"] = (await supplierDirectory.GetCreditOptionsAsync()).Count - report.ListAdditions.Count(a => a.List == "เครดิต"),
            ["purch.SupplierBusinessTypes"] = (await supplierDirectory.GetBusinessTypesAsync()).Count - report.ListAdditions.Count(a => a.List == "ประเภทธุรกิจ"),
            ["purch.PurchasingLocations"] = (await requestSetupDirectory.GetLocationsAsync()).Count,
            ["purch.RequestReasons"] = await import.CountReasonsAsync(),
            ["purch.RequestNotes"] = await import.CountNotesAsync(),
            ["purch.MaterialGroups"] = (await materialDirectory.GetGroupsAsync()).Count,
            ["org.Departments (จับคู่)"] = 0,
            ["purch.Suppliers"] = supplierRows.Count,
            ["purch.Materials"] = (await materialDirectory.GetAllAsync()).Count,
        };
        report.Notes.Add($"แผนก: จับคู่ {departmentsMatched} แผนก ตั้ง prefix เลขที่ใบขอซื้อ (ไม่สร้างแผนกใหม่)");
        report.Tables.AddRange(tallies.Select(t => new TableCount(t.LegacyTable, t.NewTable, t.Source, t.Excluded, t.Rejected, t.Loaded, inDatabase[t.NewTable])));
    }

    // ---------- helpers ----------

    /// <summary>"เงินสด" = 0; a number of days; anything else cannot be turned into days.</summary>
    private static int? Credit(string? text)
        => text is null ? null : text == "เงินสด" ? 0 : int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var days) ? days : null;

    private static decimal? Decimal(LegacyRow row, string column) => row.Number(column) is { } n ? Math.Round((decimal)n, 4) : null;

    private static string Errors(ServiceResult result) => string.Join(" · ", result.Errors);

    private static void Check(ServiceResult result, string what)
    {
        if (!result.Succeeded) throw new InvalidOperationException($"{what}: {string.Join(" · ", result.Errors)}");
    }

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

    private void UseCode(string kind, string legacyValue, string code, string? name, string? prefix, CodeMapEntry? entry)
    {
        if (report.CodeMapUsed.Any(u => u.Kind == kind && string.Equals(u.Legacy.Trim(), legacyValue.Trim(), StringComparison.OrdinalIgnoreCase))) return;
        report.CodeMapUsed.Add(new CodeMapUse(kind, legacyValue, code, name, prefix, entry is not null));
    }

    [GeneratedRegex(@"^[A-Za-z]{1,2}\d+$")]
    private static partial Regex WellFormedCode();

    private sealed class Tally(string legacyTable, string newTable)
    {
        public string LegacyTable { get; } = legacyTable;
        public string NewTable { get; } = newTable;
        public int Source { get; init; }
        public int Excluded { get; set; }
        public int Rejected { get; set; }
        public int Loaded { get; set; }
    }
}
