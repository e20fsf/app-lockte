using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Lockte.Calib;
using Microsoft.Extensions.DependencyInjection;

namespace Lockte.Migration;

/// <summary>One instrument as RPT-02 prints it — what Access printed (the baseline) or what the new system hands the report. Text only, the department and the calibrator as the printed words.</summary>
public sealed record CalibPrintedRow(string? Name, string Code, DateOnly? Registered, string? Maker, string? Department, string? Range, string? Resolution, string? Limit, string? Unit, string? Calibrator, string? Note);

/// <summary>A cell that differs: <see cref="Category"/> says whether it is accepted (and why) or not.</summary>
public sealed record CalibDifference(string Kind, string Code, string Column, string Baseline, string New, string Category, bool Accepted);

/// <summary>The comparison of one kind: how many rows each side had, how many matched exactly, the accepted exceptions and the differences nobody accepted.</summary>
public sealed record CalibKindComparison(InstrumentKind Kind, int BaselineRows, int NewRows, int Matched, bool SameOrder, IReadOnlyList<CalibDifference> Differences)
{
    public IReadOnlyList<CalibDifference> Unaccepted => Differences.Where(d => !d.Accepted).ToList();

    /// <summary>AC-12: every row and every column equal but for the exceptions the tickets accept, and in the same order.</summary>
    public bool Passed => Unaccepted.Count == 0 && SameOrder;
}

/// <summary>One line of the gate AC-01…AC-14: what was expected, what the migrated data gave, and whether it passed (or was left to a person).</summary>
public sealed record CalibGateLine(string Id, string Check, string Expected, string Actual, bool? Passed, string Covers);

/// <summary>
/// ด่านเทียบผลสอบเทียบกับ Access (#213): the program calculates nothing, so what is compared is what is printed. The baseline is the RPT-02 query of each of the six tables, exported by
/// <c>tools/baseline/export_calib_baseline.py</c> from a copy (<c>calib-&lt;kind&gt;.csv</c>, in order of หมายเลขเครื่อง); it is compared row by row with the rows the new system gives
/// the report. What the tickets accept is listed with its reason and counted, never hidden: the dates fixed by #209 (AC-02), a calibrator or department not yet linked to hr/org (the DQ report
/// has them), the unit of the temperature controller that had none. Everything else that differs fails the gate. Real data stays in the details file outside git.
/// </summary>
public static partial class CalibComparison
{
    public const string BaselinePrefix = "calib-";

    public static string BaselineFile(InstrumentKind kind) => BaselinePrefix + kind.ToString().ToLowerInvariant() + ".csv";

    /// <summary>Compares the two sides of one kind (pure: no file, no database).</summary>
    public static CalibKindComparison Compare(InstrumentKind kind, IReadOnlyList<CalibPrintedRow> baseline, IReadOnlyList<CalibPrintedRow> now)
    {
        var differences = new List<CalibDifference>();
        var matched = 0;
        var byCode = now.GroupBy(r => r.Code, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var old in baseline)
        {
            if (string.IsNullOrWhiteSpace(old.Code)) { differences.Add(Diff(kind, "(ว่าง)", "หมายเลขเครื่อง", "ว่าง", "ไม่โหลด", "หมายเลขว่าง ไม่ย้าย (DQ-Q22, ระบบเดิมลบแถวนี้)", true)); continue; }
            if (!seen.Add(old.Code.Trim())) { differences.Add(Diff(kind, old.Code, "หมายเลขเครื่อง", old.Code, "ไม่โหลดแถวซ้ำ", "หมายเลขซ้ำ ไม่ย้ายแถวที่สอง (DQ-Q22)", true)); continue; }
            if (!byCode.TryGetValue(old.Code.Trim(), out var current)) { differences.Add(Diff(kind, old.Code, "แถว", "มี", "ไม่มีในฐานข้อมูล", "แถวหาย", false)); continue; }
            var before = differences.Count;
            Row(kind, old, current, differences);
            if (differences.Count == before) matched++;
        }
        foreach (var extra in now.Where(r => !seen.Contains(r.Code))) differences.Add(Diff(kind, extra.Code, "แถว", "ไม่มี", "มีในฐานข้อมูลเท่านั้น", "แถวเกิน", false));

        // the order of the report: the codes of the baseline that were loaded, as Access sorted them, against the order the database gives
        var loaded = baseline.Select(r => r.Code.Trim()).Where(c => c.Length > 0 && byCode.ContainsKey(c)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var sameOrder = loaded.SequenceEqual(now.Select(r => r.Code).Where(loaded.Contains), StringComparer.OrdinalIgnoreCase);
        return new CalibKindComparison(kind, baseline.Count, now.Count, matched, sameOrder, differences);
    }

    private static void Row(InstrumentKind kind, CalibPrintedRow old, CalibPrintedRow now, List<CalibDifference> into)
    {
        var code = old.Code.Trim();
        Text(kind, code, "ชื่อเครื่องมือวัด", old.Name, now.Name, into);

        // วันที่ขึ้นบัญชี: the migration fixes the year the one possible way (AC-02); a year it cannot decide is blank until a person decides
        var expected = old.Registered is { } d ? CalibMigrationRules.FixDate(d.ToDateTime(TimeOnly.MinValue)) : null;
        if (expected is { IsUndecided: true }) { if (now.Registered is not null) into.Add(Diff(kind, code, "วันที่ขึ้นบัญชี", Date(old.Registered), Date(now.Registered), "ตัดสินแล้ว (ตารางจับคู่)", true)); else into.Add(Diff(kind, code, "วันที่ขึ้นบัญชี", Date(old.Registered), "", "ปีนอกกฎ เว้นว่างรอคำตัดสิน (Q-08)", true)); }
        else if (expected?.Date != now.Registered) into.Add(Diff(kind, code, "วันที่ขึ้นบัญชี", Date(old.Registered), Date(now.Registered), "วันที่ไม่ตรง", false));
        else if (expected is { Changed: true }) into.Add(Diff(kind, code, "วันที่ขึ้นบัญชี", Date(old.Registered), Date(now.Registered), "แก้ปีตามกฎ AC-02 (#209)", true));

        Text(kind, code, "ยี่ห้อ/ผู้ผลิต", old.Maker, now.Maker, into);
        Department(kind, code, old.Department, now.Department, into);
        Text(kind, code, "พิกัดใช้งาน", old.Range, now.Range, into);
        Text(kind, code, "ค่าความละเอียด", old.Resolution, now.Resolution, into);
        if (kind == InstrumentKind.TempControl && Norm(old.Unit) == "" && Norm(now.Unit) == CalibMigrationRules.TempControlUnit)
            into.Add(Diff(kind, code, "หน่วย", "", now.Unit ?? "", "หน่วยว่าง → °C ตามกฎ (#209)", true));
        else Text(kind, code, "หน่วย", old.Unit, now.Unit, into);
        Text(kind, code, "ขีดจำกัดที่ยอมรับได้", old.Limit, now.Limit, into);

        // ผู้สอบเทียบ: the printed name of the employee or the company; not yet linked (the DQ report has it) is accepted, a different name is not
        if (Norm(old.Calibrator) != "" && Norm(now.Calibrator) == "") into.Add(Diff(kind, code, "ผู้สอบเทียบ", "(ชื่อ)", "", "ผู้สอบเทียบยังไม่ผูกกับทะเบียน (DQ-Q16)", true));
        else Text(kind, code, "ผู้สอบเทียบ", old.Calibrator, now.Calibrator, into);

        // หมายเหตุ: the migration adds the legacy words it could not link to the note, in brackets
        Text(kind, code, "หมายเหตุ", old.Note, MigrationNote().Replace(now.Note ?? "", "").Trim(), into);
    }

    /// <summary>แผนก as printed: <c>DC (D02)</c> is the same as <c>DC(D02)</c>; one that did not link to org is accepted (the note keeps the legacy text).</summary>
    private static void Department(InstrumentKind kind, string code, string? old, string? now, List<CalibDifference> into)
    {
        static string Compact(string? text) => Regex.Replace(text ?? "", @"\s+", "").ToUpperInvariant();
        if (Compact(old) == Compact(now)) return;
        if (Compact(now) == "") into.Add(Diff(kind, code, "แผนก", old ?? "", "", "แผนก/เครื่องจักรยังไม่ผูกกับ org (DQ-Q05)", true));
        else into.Add(Diff(kind, code, "แผนก", old ?? "", now ?? "", "แผนกไม่ตรง", false));
    }

    private static void Text(InstrumentKind kind, string code, string column, string? old, string? now, List<CalibDifference> into)
    {
        if (Norm(old) != Norm(now)) into.Add(Diff(kind, code, column, old ?? "", now ?? "", "ข้อความไม่ตรง", false));
    }

    private static string Norm(string? text) => Regex.Replace(text ?? "", @"\s+", " ").Trim();

    private static string Date(DateOnly? date) => date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "";

    private static CalibDifference Diff(InstrumentKind kind, string code, string column, string baseline, string now, string category, bool accepted)
        => new(kind.ToString(), code, column, baseline, now, category, accepted);

    [GeneratedRegex(@"\s*\[(?:แผนกเดิม|เครื่องจักรเดิม):[^\]]*\]")]
    private static partial Regex MigrationNote();

    // ---------- reading the baseline, the database and the gate ----------

    /// <summary>Reads <c>calib-&lt;kind&gt;.csv</c>: columns name, code, registered (yyyy-MM-dd), maker, department, range, resolution, limit, unit, calibrator, note.</summary>
    public static IReadOnlyList<CalibPrintedRow> ReadBaseline(string folder, InstrumentKind kind)
        => Comparison.Read(folder, BaselineFile(kind)).Select(r => new CalibPrintedRow(r["name"], r["code"],
            DateOnly.TryParseExact(r["registered"], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null,
            r["maker"], r["department"], r["range"], r["resolution"], r["limit"], r["unit"], r["calibrator"], r["note"])).ToList();

    public static CalibPrintedRow Printed(InstrumentDto i)
        => new(i.Name, i.Code, i.RegisteredOn, i.Maker, i.DepartmentText, i.Range, i.Resolution, i.Limit, i.Unit, i.Kind == InstrumentKind.External ? i.ProviderName : i.CalibratorName, i.Note);

    /// <summary>The expected counts of AC-01 (data-model): ภายนอก 20, CALIPER 13, PRESSURE GAUGE 53, เครื่องชั่ง 10, ตัวควบคุมอุณหภูมิ 67, jig 59.</summary>
    public static IReadOnlyDictionary<InstrumentKind, int> ExpectedCounts { get; } = new Dictionary<InstrumentKind, int>
    {
        [InstrumentKind.External] = 20, [InstrumentKind.Caliper] = 13, [InstrumentKind.PressureGauge] = 53, [InstrumentKind.Scale] = 10, [InstrumentKind.TempControl] = 67, [InstrumentKind.Jig] = 59,
    };

    public sealed record Result(IReadOnlyList<CalibKindComparison> Kinds, IReadOnlyList<CalibGateLine> Gate)
    {
        public bool Passed => Kinds.All(k => k.Passed) && Gate.All(g => g.Passed != false);
    }

    public static async Task<Result> RunAsync(IServiceProvider services, string baselineFolder)
    {
        await using var scope = services.CreateAsyncScope();
        var directory = scope.ServiceProvider.GetRequiredService<InstrumentDirectory>();
        var sheets = scope.ServiceProvider.GetRequiredService<JigSheetDirectory>();
        var kinds = new List<CalibKindComparison>();
        var nowByKind = new Dictionary<InstrumentKind, IReadOnlyList<InstrumentDto>>();
        foreach (var kind in Enum.GetValues<InstrumentKind>())
        {
            var rows = await directory.ListAsync(kind);
            nowByKind[kind] = rows;
            kinds.Add(Compare(kind, ReadBaseline(baselineFolder, kind), rows.Select(Printed).ToList()));
        }
        var gate = new List<CalibGateLine>();
        gate.AddRange(kinds.Select(k => new CalibGateLine("AC-12", $"บัญชีรายชื่อ {InstrumentDirectory.KindName(k.Kind)} ตรงรายงานเดิมทุกแถว/ทุกคอลัมน์/ลำดับ",
            $"ต้นทาง {k.BaselineRows} แถว", $"ฐาน {k.NewRows} แถว ตรงทั้งแถว {k.Matched} ไม่ตรงที่ไม่ยอมรับ {k.Unaccepted.Count} ลำดับ{(k.SameOrder ? "ตรง" : "ไม่ตรง")}", k.Passed, "#203 #204 #213")));
        foreach (var (kind, expected) in ExpectedCounts)
            gate.Add(new CalibGateLine("AC-01", $"จำนวนเครื่อง {InstrumentDirectory.KindName(kind)}", expected.ToString(CultureInfo.InvariantCulture), nowByKind[kind].Count.ToString(CultureInfo.InvariantCulture), nowByKind[kind].Count == expected, "#209"));

        var caliper = nowByKind[InstrumentKind.Caliper].Count;
        var starCaliper = (await directory.FindByPatternAsync(InstrumentKind.Caliper, "*")).Count;
        gate.Add(new CalibGateLine("AC-09", "สติกเกอร์ CALIPER ID `*` = ทุกเครื่อง", "13", starCaliper.ToString(CultureInfo.InvariantCulture), starCaliper == 13 && starCaliper == caliper, "#201"));
        var pgDc = (await directory.FindByPatternAsync(InstrumentKind.PressureGauge, "PG-DC*")).Count;
        gate.Add(new CalibGateLine("AC-10", "สติกเกอร์ PRESSURE GAUGE ID `PG-DC*`", "35", pgDc.ToString(CultureInfo.InvariantCulture), pgDc == 35, "#201"));
        var missing = (await directory.FindByPatternAsync(InstrumentKind.Caliper, "XX-999")).Count;
        gate.Add(new CalibGateLine("AC-11", "สติกเกอร์ `XX-999` ไม่พบ → ไม่มีดวง", "0", missing.ToString(CultureInfo.InvariantCulture), missing == 0, "#201"));
        foreach (var (kind, expected) in new[] { (InstrumentKind.External, 20), (InstrumentKind.Caliper, 13), (InstrumentKind.Scale, 10) })
            gate.Add(new CalibGateLine("AC-13", $"จำนวนหน้า RPT-03 {InstrumentDirectory.KindName(kind)} = จำนวนเครื่อง", expected.ToString(CultureInfo.InvariantCulture), nowByKind[kind].Count.ToString(CultureInfo.InvariantCulture), nowByKind[kind].Count == expected, "#205"));

        var all = await sheets.ListAsync();
        var seedOk = JigInspectionSeed.Sheets.All(s => all.FirstOrDefault(a => a.Type == s.Type && a.ModelKey == s.ModelKey) is { } sheet
            && sheet.DocumentKey == s.DocumentKey && sheet.Points.Select(p => (p.Label, p.Kind, p.Value, p.Tolerance)).SequenceEqual(s.Points.Select(p => (p.Label, p.Kind, p.Value, p.Tolerance))));
        gate.Add(new CalibGateLine("AC-14", "ค่ากำหนด RPT-05 (23 แบบ) และ RPT-06 (19 แบบ) ตรงตารางใน reports.md (ข้อมูลตั้งต้นของ #199)", "42 แบบตรงตาราง",
            $"{all.Count(a => a.Type == JigSheetType.ZeroCheck)} + {all.Count(a => a.Type == JigSheetType.Grinding)} แบบ, ตรง {(seedOk ? "ทุกแบบ" : "ไม่ครบ/ไม่ตรง")}", all.Count == 42 && seedOk, "#199 #207 #208"));
        gate.Add(new CalibGateLine("AC-14", "แบบตรวจที่ยังไม่มีรูป JIG", "0", all.Count(a => !a.HasImage).ToString(CultureInfo.InvariantCulture), all.All(a => a.HasImage), "#200"));

        // the gates of what was changed on purpose and the ones a person has to see
        gate.Add(new CalibGateLine("AC-02", "วันที่ที่แก้ปีตามกฎ ลงรายงาน DQ-Q08 ทุกแถวพร้อมก่อน/หลัง", "ดู dq-exceptions.csv ของการย้าย", $"ในเทียบนี้: {kinds.Sum(k => k.Differences.Count(d => d.Category.StartsWith("แก้ปี", StringComparison.Ordinal)))} แถว",
            null, "#209 (Q-08 ผู้ใช้ยืนยันกฎแปลง)"));
        gate.Add(new CalibGateLine("AC-03..08", "เพิ่ม/ไม่ใส่หมายเลข/ตัวเลือก/ผู้สอบเทียบภายนอก/แก้/ลบ", "เทสต์อัตโนมัติ (InstrumentTests, ExternalInstrumentTests, CalibrationProviderTests)", "ผ่านในชุดเทสต์ (AC-04 และ AC-08 ปรับตาม #191)", null, "#194 #195 #196 #197 #198"));
        return new Result(kinds, gate);
    }

    public static void WriteTo(string folder, Result result)
    {
        Directory.CreateDirectory(folder);
        Csv.Write(Path.Combine(folder, "calib-compare.csv"), ["ชนิด", "แถวต้นทาง", "แถวในฐาน", "ตรงทั้งแถว", "ลำดับ", "ยอมรับ", "ไม่ยอมรับ", "ผล"],
            result.Kinds.Select(k => new object?[] { k.Kind, k.BaselineRows, k.NewRows, k.Matched, k.SameOrder ? "ตรง" : "ไม่ตรง", k.Differences.Count(d => d.Accepted), k.Unaccepted.Count, k.Passed ? "ผ่าน" : "ไม่ผ่าน" }));
        // real values: this file is kept outside git
        Csv.Write(Path.Combine(folder, "calib-compare-details.csv"), ["ชนิด", "หมายเลขเครื่อง", "คอลัมน์", "ต้นทาง", "ฐานข้อมูล", "ประเภท", "ยอมรับ"],
            result.Kinds.SelectMany(k => k.Differences).Select(d => new object?[] { d.Kind, d.Code, d.Column, d.Baseline, d.New, d.Category, d.Accepted ? "ยอมรับ" : "ไม่ยอมรับ" }));
        Csv.Write(Path.Combine(folder, "calib-gate.csv"), ["AC", "การตรวจ", "ควรได้", "ได้", "ผล", "ตั๋ว/เทสต์"],
            result.Gate.Select(g => new object?[] { g.Id, g.Check, g.Expected, g.Actual, g.Passed is null ? "ตรวจด้วยตา/เทสต์" : g.Passed.Value ? "ผ่าน" : "ไม่ผ่าน", g.Covers }));
        var summary = new StringBuilder("# เทียบผลสอบเทียบกับ Access\n\n").AppendLine(result.Passed ? "**ผ่านทุกข้อที่ตรวจอัตโนมัติ**" : "**มีข้อที่ไม่ผ่าน**").AppendLine();
        summary.AppendLine("| AC | การตรวจ | ควรได้ | ได้ | ผล |").AppendLine("|---|---|---|---|---|");
        foreach (var g in result.Gate) summary.AppendLine($"| {g.Id} | {g.Check} | {g.Expected} | {g.Actual} | {(g.Passed is null ? "ตรวจด้วยตา/เทสต์" : g.Passed.Value ? "ผ่าน" : "ไม่ผ่าน")} |");
        File.WriteAllText(Path.Combine(folder, "calib-summary.md"), summary.ToString(), new UTF8Encoding(true));
    }
}
