using System.Globalization;
using Lockte.Calib;
using Lockte.Org;
using Lockte.SharedKernel.Persistence;

namespace Lockte.Migration;

/// <summary>
/// ย้ายข้อมูลหลักของ <c>สอบเทียบ.accdb</c> (#209): the six instrument tables into the one register of <c>calib</c> and the calibration companies as marked suppliers of <c>purch</c>. Everything goes through the
/// public registries, so a loaded row passes the same checks as one keyed on screen, and nothing it does is written to the change history (#39). What has one possible answer is fixed and listed
/// (a registration date typed with the wrong calendar, a missing unit); what needs a person (a date the rules do not cover, a department or machine the register of <c>org</c> does not know, a calibrator
/// who is no employee, a standard instrument the register lacks) goes to the DQ report and the row is loaded without it, to be decided in the code map and run again. A repeated or blank หมายเลขเครื่อง is
/// not loaded. The people are reported by the code of the instrument, never by name — the report may be shared, the names of people are kept outside git. The tables are read from a copy of the file.
/// </summary>
public sealed class CalibMasterDataMigration(
    AuditScope audit,
    CalibMasterImport import,
    InstrumentRegistry instruments,
    InstrumentDirectory instrumentDirectory,
    CalibrationProviderRegistry providers,
    CalibrationProviderDirectory providerDirectory,
    OrgDirectory org,
    ICalibEmployeeLookup employees)
{
    private const string Other = "NF-06";
    private const string DateRule = "DQ-Q08";
    private const string DepartmentRule = "DQ-Q05";
    private const string StandardRule = "DQ-Q07";
    private const string UnitRule = "DQ-Q09";
    private const string CodeRule = "DQ-Q22";
    private const string CalibratorRule = "DQ-Q16";
    private const string CompanyRule = "DQ-Q24";

    /// <summary>The legacy tables, the external one first: the standard instruments of the others are looked up in it.</summary>
    private static readonly (string Table, InstrumentKind Kind)[] Tables =
    [
        ("ภายนอก", InstrumentKind.External), ("CALIPER", InstrumentKind.Caliper), ("PRESSURE GAUGE", InstrumentKind.PressureGauge), ("เครื่องชั่ง", InstrumentKind.Scale),
        ("ตัวควบคุมอุณหภูมิ", InstrumentKind.TempControl), ("jig", InstrumentKind.Jig),
    ];

    public const string CompanyTable = "บริษัทที่สอบเทียบ";

    private LegacyDatabase legacy = null!;
    private CodeMap map = null!;
    private MigrationReport report = null!;
    private readonly List<Tally> tallies = [];
    private IReadOnlyList<DepartmentDto> departments = [];
    private IReadOnlyList<MachineDto> machines = [];
    private HashSet<string> externalCodes = new(StringComparer.OrdinalIgnoreCase);
    private int trimmed;
    private int companiesAdded;

    /// <summary>Once per instance (it is scoped): the run keeps what it loaded in fields.</summary>
    public async Task<MigrationReport> RunAsync(LegacyDatabase legacyDatabase, CodeMap codeMap)
    {
        if (report is not null) throw new InvalidOperationException("CalibMasterDataMigration runs once per scope.");
        (legacy, map, report) = (legacyDatabase, codeMap, new MigrationReport { Source = legacyDatabase.Source });
        departments = await org.GetDepartmentsAsync();
        machines = await org.GetMachinesAsync();
        externalCodes = legacy["ภายนอก"].Select(r => Clean(r.Text("หมายเลขเครื่อง"))).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        using (audit.SuppressRowChanges())
        {
            await import.DeleteMasterAsync();
            await LoadCompaniesAsync();
            foreach (var (table, kind) in Tables) await LoadInstrumentsAsync(table, kind);
        }
        await CountBackAsync();
        return report;
    }

    // ---------- บริษัทที่สอบเทียบ ----------

    private async Task LoadCompaniesAsync()
    {
        var tally = Count(CompanyTable, "calib.CalibrationProviders", legacy[CompanyTable].Count);
        foreach (var row in legacy[CompanyTable])
        {
            if (Clean(row.Text("NAME")) is not { } name) { Reject(tally, CompanyTable, "(ชื่อว่าง)", Other, "ไม่มีชื่อบริษัท"); continue; }
            var saved = await providers.AddAsync(name);
            if (!saved.Succeeded) { Reject(tally, CompanyTable, name, CompanyRule, string.Join(" · ", saved.Errors)); continue; }
            tally.Loaded++;
        }
    }

    /// <summary>The company an external instrument names; one that the lookup lacks (บ.ไทยเครื่องชั่ง จก.) is added to the calibration companies (one possible answer).</summary>
    private async Task<int?> CompanyAsync(string table, string code, string? name)
    {
        if (name is null) return null;
        var found = (await providerDirectory.ListAsync()).FirstOrDefault(p => string.Equals(p.Name.Trim(), name, StringComparison.OrdinalIgnoreCase));
        if (found is null)
        {
            var added = await providers.AddAsync(name);
            if (!added.Succeeded) { report.Add(table, code, CompanyRule, "เพิ่มบริษัทที่สอบเทียบไม่ได้: " + string.Join(" · ", added.Errors), MigrationReport.LoadedBlank); return null; }
            companiesAdded++;
            report.Add(table, code, CompanyRule, $"บริษัท '{name}' ใช้ในทะเบียนแต่ไม่อยู่ในรายชื่อบริษัทที่สอบเทียบเดิม", "เพิ่มเป็นบริษัทที่สอบเทียบ");
            found = (await providerDirectory.ListAsync()).First(p => string.Equals(p.Name.Trim(), name, StringComparison.OrdinalIgnoreCase));
        }
        return found.SupplierId;
    }

    // ---------- เครื่องมือวัด ----------

    private async Task LoadInstrumentsAsync(string table, InstrumentKind kind)
    {
        var tally = Count(table, $"calib.Instruments ({InstrumentDirectory.KindName(kind)})", legacy[table].Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var number = 0;
        foreach (var row in legacy[table])
        {
            number++;
            if (Clean(row.Text("หมายเลขเครื่อง")) is not { } code) { Reject(tally, table, $"แถวที่ {number}", CodeRule, "ไม่มีหมายเลขเครื่อง — ไม่โหลด (ระบบเดิมลบแถวนี้)"); continue; }
            if (!seen.Add(code)) { Reject(tally, table, code, CodeRule, "หมายเลขเครื่องซ้ำ — โหลดแถวแรก ไม่โหลดแถวนี้"); continue; }

            var name = Text(row, kind == InstrumentKind.Jig ? "ชื่อ JIG" : "ชื่อเครื่องมือวัด");
            var registered = Registered(table, code, row.Date("วันที่ขึ้นบัญชี"));
            var unit = kind == InstrumentKind.Jig ? null : Text(row, "หน่วย");
            if (kind == InstrumentKind.TempControl && unit is null)
            {
                unit = CalibMigrationRules.TempControlUnit;
                report.Add(table, code, UnitRule, $"หน่วยว่าง → {unit} (เครื่องอื่นของชนิดนี้เป็น {unit} ทั้งหมด)", "แก้อัตโนมัติ");
            }
            var standard = kind == InstrumentKind.External ? null : Text(row, kind == InstrumentKind.Jig ? "หมายเลขเวอร์เนียที่ใช้(ผ่านการสอบเทียบแล้ว)" : "หมายเลขเครื่องมือที่ใช้(ผ่านการสอบเทียบแล้ว)");
            if (standard is not null && CalibMigrationRules.StandardProblem(standard, kind == InstrumentKind.Jig ? CaliperCodes() : externalCodes) is { } problem)
                report.Add(table, code, StandardRule, $"เครื่องมือมาตรฐาน '{standard}': {problem}", "โหลดข้อความตามต้นฉบับ รอคำตัดสิน Q-07");

            var (departmentId, machineIds, note) = await DepartmentAsync(table, code, Text(row, kind == InstrumentKind.External ? "แผนกที่ใช้งาน" : "แผนก"), Text(row, "หมายเหตุ"));
            int? employeeId = null, providerId = null;
            if (kind == InstrumentKind.External) providerId = await CompanyAsync(table, code, Text(row, "ผู้สอบเทียบ"));
            else employeeId = await CalibratorAsync(table, code, Text(row, "ผู้สอบเทียบ"));

            var saved = await instruments.SaveAsync(new InstrumentInput(null, kind, code, null, name, registered, kind == InstrumentKind.Jig ? null : Text(row, "ยี่ห้อ/ผู้ผลิต"), departmentId, machineIds,
                kind == InstrumentKind.Jig ? null : Text(row, "พิกัดใช้งาน"), kind == InstrumentKind.Jig ? null : Text(row, "ค่าความละเอียด"), Text(row, "ขีดจำกัดที่ยอมรับได้"), unit, standard, employeeId, providerId,
                kind == InstrumentKind.Jig ? Text(row, "แกน (mm)") : null, kind == InstrumentKind.Jig ? Text(row, "คู่มือวิธีการที่ใช้เลขที่") : null, note));
            if (!saved.Succeeded) { Reject(tally, table, code, Other, string.Join(" · ", saved.Errors)); continue; }
            tally.Loaded++;
        }
    }

    private HashSet<string> CaliperCodes() => legacy["CALIPER"].Select(r => Clean(r.Text("หมายเลขเครื่อง"))).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>The registration date: fixed the one possible way and listed with its before and after, or — when the rules do not cover it — blank until the code map decides.</summary>
    private DateOnly? Registered(string table, string code, DateTime? date)
    {
        if (date is not { } value) return null;
        var fix = CalibMigrationRules.FixDate(value);
        if (fix.Changed)
        {
            report.Add(table, code, DateRule, $"วันที่ขึ้นบัญชี {value:yyyy-MM-dd} → {fix.Date:yyyy-MM-dd} ({fix.Rule})", "แก้อัตโนมัติ");
            return fix.Date;
        }
        if (!fix.IsUndecided) return fix.Date;
        var decided = map.Find(CodeKinds.CalibDate, code)?.Code;
        if (decided is not null && DateOnly.TryParseExact(decided, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var chosen))
        {
            report.Add(table, code, DateRule, $"วันที่ขึ้นบัญชี {value:yyyy-MM-dd} → {chosen:yyyy-MM-dd} (ตามตารางจับคู่)", "ตามคำตัดสิน");
            Use(CodeKinds.CalibDate, code, decided, null);
            return chosen;
        }
        report.Add(table, code, DateRule, $"วันที่ขึ้นบัญชี {value:yyyy-MM-dd} ปีนี้ไม่เข้ากฎแปลง — เว้นว่างรอคำตัดสิน (ใส่ {CodeKinds.CalibDate},{code},<yyyy-MM-dd> ในตารางจับคู่)", MigrationReport.LoadedBlank);
        Use(CodeKinds.CalibDate, code, "", null);
        return null;
    }

    /// <summary>
    /// <c>DC (D02)</c> → the department and its machines of the register of org. An abbreviation, or a machine, that matches nothing is not guessed: the instrument is loaded without it, the legacy text kept in
    /// the note, and the problem reported with how to decide it.
    /// </summary>
    private async Task<(int? DepartmentId, IReadOnlyList<int> MachineIds, string? Note)> DepartmentAsync(string table, string code, string? text, string? note)
    {
        if (text is null) return (null, [], note);
        var (abbreviation, machineCodes) = CalibMigrationRules.SplitDepartment(text);
        var entry = map.Find(CodeKinds.CalibDepartment, abbreviation);
        var department = entry?.Code is { } mapped
            ? departments.FirstOrDefault(d => string.Equals(d.Code, mapped, StringComparison.OrdinalIgnoreCase))
            : departments.FirstOrDefault(d => string.Equals(d.Code, abbreviation, StringComparison.OrdinalIgnoreCase)) ?? departments.FirstOrDefault(d => string.Equals(d.Name, abbreviation, StringComparison.Ordinal));
        if (department is null)
        {
            report.Add(table, code, DepartmentRule, $"แผนก '{text}' จับคู่แผนกในทะเบียน org ไม่ได้ (ใส่ {CodeKinds.CalibDepartment},{abbreviation},<รหัสแผนก> ในตารางจับคู่)", MigrationReport.LoadedBlank);
            Use(CodeKinds.CalibDepartment, abbreviation, "", null);
            return (null, [], AppendNote(note, $"แผนกเดิม: {text}"));
        }
        Use(CodeKinds.CalibDepartment, abbreviation, department.Code, department.Name);
        var ids = new List<int>();
        var lost = new List<string>();
        foreach (var token in machineCodes)
        {
            var inDepartment = machines.Where(m => m.DepartmentId == department.Id).ToList();
            var machine = inDepartment.FirstOrDefault(m => string.Equals(m.Code, token, StringComparison.OrdinalIgnoreCase))
                ?? inDepartment.FirstOrDefault(m => abbreviation.Length > 0 && string.Equals(m.Code, abbreviation[0] + token, StringComparison.OrdinalIgnoreCase));
            if (machine is null) lost.Add(token);
            else if (!ids.Contains(machine.Id)) ids.Add(machine.Id);
        }
        if (lost.Count > 0)
        {
            report.Add(table, code, DepartmentRule, $"เครื่องจักร {string.Join(",", lost)} ของแผนก {department.Code} ไม่พบในทะเบียนเครื่องจักร (ข้อความเดิม '{text}')", MigrationReport.LoadedBlank);
            note = AppendNote(note, $"เครื่องจักรเดิม: {text}");
        }
        return (department.Id, ids, note);
    }

    private async Task<int?> CalibratorAsync(string table, string code, string? name)
    {
        if (name is null) return null;
        var entry = map.Find(CodeKinds.Calibrator, name);
        var found = entry?.Code is { } employeeCode
            ? (await employees.SearchAsync(employeeCode, 20)).FirstOrDefault(e => string.Equals(e.Code, employeeCode, StringComparison.OrdinalIgnoreCase))
            : (await employees.SearchAsync(name, 20)).FirstOrDefault(e => string.Equals(e.Name.Trim(), name, StringComparison.Ordinal));
        if (found is null)
        {
            report.Add(table, code, CalibratorRule, "ผู้สอบเทียบจับคู่ทะเบียนพนักงานไม่ได้ (ชื่ออยู่ใน code-map-used.csv ที่เก็บนอก git ใส่ calibrator,<ชื่อ>,<รหัสพนักงาน> แล้วรันซ้ำ)", MigrationReport.LoadedBlank);
            Use(CodeKinds.Calibrator, name, "", null);
            return null;
        }
        Use(CodeKinds.Calibrator, name, found.Code, null);
        return found.Id;
    }

    // ---------- รายงานตรวจยอด ----------

    private async Task CountBackAsync()
    {
        var inDatabase = new Dictionary<string, int> { ["calib.CalibrationProviders"] = (await providerDirectory.ListAsync()).Count - companiesAdded };
        foreach (var (_, kind) in Tables) inDatabase[$"calib.Instruments ({InstrumentDirectory.KindName(kind)})"] = (await instrumentDirectory.ListAsync(kind)).Count;
        report.Notes.Add($"ตัดช่องว่างหัวท้ายค่าข้อความของเครื่องมือวัดทั้งหมด (นับ {trimmed} ค่า)");
        if (companiesAdded > 0) report.Notes.Add($"เพิ่มบริษัทที่สอบเทียบจากทะเบียนภายนอก {companiesAdded} ราย (ไม่อยู่ในรายชื่อเดิม)");
        report.Tables.AddRange(tallies.Select(t => new TableCount(t.LegacyTable, t.NewTable, t.Source, t.Excluded, t.Rejected, t.Loaded, inDatabase[t.NewTable])));
    }

    // ---------- helpers ----------

    /// <summary>A text cell trimmed; blank is null. Counts what the trimming changed.</summary>
    private string? Text(LegacyRow row, string column)
    {
        var raw = row.Text(column);
        var clean = Clean(raw);
        if (raw is not null && clean is not null && clean != raw) trimmed++;
        return clean;
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static string AppendNote(string? note, string text) => note is null ? $"[{text}]" : $"{note} [{text}]";

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

    private void Use(string kind, string legacyValue, string code, string? name)
    {
        if (report.CodeMapUsed.Any(u => u.Kind == kind && string.Equals(u.Legacy.Trim(), legacyValue.Trim(), StringComparison.OrdinalIgnoreCase))) return;
        report.CodeMapUsed.Add(new CodeMapUse(kind, legacyValue, code, name, null, map.Find(kind, legacyValue) is not null));
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
