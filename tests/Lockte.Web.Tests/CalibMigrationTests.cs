using System.Runtime.Versioning;
using System.Security.Cryptography;
using Lockte.Calib;
using Lockte.Hr;
using Lockte.Migration;
using Lockte.Org;

namespace Lockte.Web.Tests;

/// <summary>The rules of the migration of <c>สอบเทียบ.accdb</c> (#209) without a file or a database.</summary>
public sealed class CalibMigrationRulesTests
{
    [Theory] // AC-02 / BR-006 #2: the year of a date typed with the wrong calendar
    [InlineData("1962-08-17", "2019-08-17")] // PG-BL032/2562: 19xx → +57
    [InlineData("1968-05-03", "2025-05-03")]
    [InlineData("2568-08-04", "2025-08-04")] // TC-DC028/2568: 25xx → −543
    [InlineData("2567-05-03", "2024-05-03")]
    [InlineData("2026-08-29", "2026-08-29")] // a right date stays
    [InlineData("1964-02-29", "2021-02-28")] // 1964 was a leap year, 2021 is not
    public void A_date_with_the_wrong_calendar_is_fixed_the_one_possible_way(string legacy, string expected)
    {
        var fix = CalibMigrationRules.FixDate(DateTime.Parse(legacy, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(DateOnly.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), fix.Date);
        Assert.Equal(legacy != expected, fix.Changed);
        Assert.False(fix.IsUndecided);
    }

    [Fact] // PG-DC107/2565 has the year 4722 (Q-08): no answer is guessed
    public void A_year_the_rules_do_not_cover_is_left_for_a_person()
    {
        foreach (var year in new[] { 4722, 1850, 3109 })
        {
            var fix = CalibMigrationRules.FixDate(new DateTime(year, 8, 17));
            Assert.True(fix.IsUndecided);
            Assert.Null(fix.Date);
        }
    }

    [Theory]
    [InlineData("DC (D02)", "DC", "D02")]
    [InlineData("DC(44)", "DC", "44")]
    [InlineData("BL(32,33)", "BL", "32,33")]
    [InlineData("BL(B32)", "BL", "B32")]
    [InlineData("QA", "QA", "")]
    [InlineData(" GR ", "GR", "")]
    public void A_department_text_splits_into_the_abbreviation_and_the_machine_codes(string text, string abbreviation, string machines)
    {
        var (a, m) = CalibMigrationRules.SplitDepartment(text);
        Assert.Equal((abbreviation, machines), (a, string.Join(",", m)));
    }

    [Theory] // BR-021 / Q-07: what the text of the standard instrument gets wrong — never changed
    [InlineData("GB-QA001/2551 - GB-QA005/2551", null)]
    [InlineData("SW-QA001/2555-SW-QA005/2555", null)]
    [InlineData("PG-QA001/2550", null)]
    [InlineData("TC-QA001/255", "ปีท้ายหมายเลขไม่ครบ 4 หลัก")]
    [InlineData("GB-QA001/2551GB-QA005/2551", "หมายเลขสองตัวติดกันไม่มีขีดคั่น")]
    [InlineData("TC-QA001/2547", "หมายเลข TC-QA001/2547 ไม่อยู่ในทะเบียนเครื่องมือภายนอก")]
    [InlineData("ไม่ทราบ", "ไม่พบหมายเลขเครื่องมือมาตรฐานในข้อความ")]
    public void The_standard_instrument_text_is_checked_against_the_register(string text, string? problem)
    {
        ISet<string> external = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "GB-QA001/2551", "GB-QA005/2551", "SW-QA001/2555", "SW-QA005/2555", "PG-QA001/2550" };
        Assert.Equal(problem, CalibMigrationRules.StandardProblem(text, external));
    }
}

/// <summary>A made-up <c>สอบเทียบ.accdb</c> with the legacy column types (data-model.md). Every name, code and number in it is fictional — the repo is public.</summary>
[SupportedOSPlatform("windows")]
public static class FakeCalibFile
{
    private static (string Column, string Type)[] Common(string nameColumn, string departmentColumn = "แผนก") =>
    [
        (nameColumn, "TEXT(255)"), ("หมายเลขเครื่อง", "TEXT(255)"), ("วันที่ขึ้นบัญชี", "DATETIME"), ("ยี่ห้อ/ผู้ผลิต", "TEXT(255)"), (departmentColumn, "TEXT(255)"),
        ("พิกัดใช้งาน", "TEXT(255)"), ("ค่าความละเอียด", "TEXT(50)"), ("ขีดจำกัดที่ยอมรับได้", "TEXT(50)"), ("หน่วย", "TEXT(50)"),
        ("หมายเลขเครื่องมือที่ใช้(ผ่านการสอบเทียบแล้ว)", "TEXT(255)"), ("ผู้สอบเทียบ", "TEXT(255)"), ("หมายเหตุ", "TEXT(255)"),
    ];

    public static Dictionary<string, (string Column, string Type)[]> Schema { get; } = new()
    {
        ["ภายนอก"] = Common("ชื่อเครื่องมือวัด", "แผนกที่ใช้งาน").Where(c => c.Column != "หมายเลขเครื่องมือที่ใช้(ผ่านการสอบเทียบแล้ว)").ToArray(),
        ["CALIPER"] = Common("ชื่อเครื่องมือวัด"),
        ["PRESSURE GAUGE"] = Common("ชื่อเครื่องมือวัด"),
        ["เครื่องชั่ง"] = Common("ชื่อเครื่องมือวัด"),
        ["ตัวควบคุมอุณหภูมิ"] = Common("ชื่อเครื่องมือวัด"),
        ["jig"] =
        [
            ("หมายเลขเครื่อง", "TEXT(255)"), ("ชื่อ JIG", "TEXT(255)"), ("แกน (mm)", "TEXT(255)"), ("วันที่ขึ้นบัญชี", "DATETIME"), ("คู่มือวิธีการที่ใช้เลขที่", "TEXT(50)"),
            ("ผู้สอบเทียบ", "TEXT(50)"), ("ขีดจำกัดที่ยอมรับได้", "TEXT(50)"), ("หมายเลขเวอร์เนียที่ใช้(ผ่านการสอบเทียบแล้ว)", "TEXT(50)"), ("หมายเหตุ", "TEXT(255)"), ("แผนก", "TEXT(255)"),
        ],
        ["บริษัทที่สอบเทียบ"] = [("NAME", "TEXT(255)")],
    };

    public static FakeLegacyFile Empty() => new(Schema);
}

/// <summary>เครื่องมือย้ายข้อมูล: ข้อมูลหลักของสอบเทียบ (#209) — a made-up file in, the instruments and companies out, the DQ report beside them.</summary>
[Collection(MigrationDbCollection.Name)]
[SupportedOSPlatform("windows")]
public sealed class CalibMigrationTests(MigrationDbFixture db) : IDisposable
{
    private readonly DirectoryInfo work = Directory.CreateTempSubdirectory("lockte-calib-migration-test-");

    public void Dispose() => TestFiles.DeleteScratch(work);

    private async Task<T> AsAsync<T>(Func<TestScope, Task<T>> action)
    {
        await using var scope = db.Scope("hr1");
        return await action(scope);
    }

    private static string Unique() => Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();

    private Task<MigrationReport> RunAsync(string source, string? codeMap = null, string output = "out", string? images = null)
        => CalibMigrationTool.RunAsync(db.Services, source, codeMap, Path.Combine(work.FullName, output), images);

    private string Save(FakeLegacyFile file) => file.Save(Path.Combine(work.FullName, "สอบเทียบ.accdb"));

    [Fact]
    public async Task Loads_the_instruments_and_companies_fixes_what_has_one_answer_and_reports_what_needs_a_person()
    {
        var tag = Unique();
        var dept = "T" + tag[..3];
        (int Department, int[] Machines) org;
        string employeeCode, employeeName = "ช่างสมมุติ " + tag;
        await using (var scope = db.Scope("hr1"))
        {
            var registry = scope.Get<OrgRegistry>();
            var section = (await registry.SaveSectionAsync(new SectionInput(null, "ฝ่าย" + tag, 1, null))).Id!.Value;
            var department = (await registry.SaveDepartmentAsync(new DepartmentInput(null, dept, "แผนก" + tag, "t", 3, section, null, [], null))).Id!.Value;
            var m1 = (await registry.SaveMachineAsync(new MachineInput(null, "D02" + tag, "เครื่อง 1", department, null))).Id!.Value;
            var m2 = (await registry.SaveMachineAsync(new MachineInput(null, "D" + tag + "9", "เครื่อง 2", department, null))).Id!.Value;
            org = (department, [m1, m2]);
            employeeCode = "Z" + Random.Shared.Next(100, 1000);
            var saved = await scope.Get<EmployeeRegistry>().SaveAsync(new EmployeeInput { Code = employeeCode, Name = employeeName });
            Assert.True(saved.Succeeded, string.Join(", ", saved.Errors));
        }

        var file = FakeCalibFile.Empty()
            .Row("บริษัทที่สอบเทียบ", ("NAME", "Quality Calibration " + tag + " Co.,Ltd."))
            .Row("ภายนอก", ("ชื่อเครื่องมือวัด", "GAUGE BLOCK (5 mm)"), ("หมายเลขเครื่อง", $"GB-{tag}1/2551"), ("วันที่ขึ้นบัญชี", new DateTime(2012, 10, 1)), ("แผนกที่ใช้งาน", "QA"),
                ("พิกัดใช้งาน", " 5"), ("หน่วย", "MM"), ("ผู้สอบเทียบ", "Quality Calibration " + tag + " Co.,Ltd."))
            .Row("ภายนอก", ("ชื่อเครื่องมือวัด", "BRASS WEIGHT"), ("หมายเลขเครื่อง", $"Q01-{tag}"), ("ผู้สอบเทียบ", "บ.ทดสอบเครื่องชั่ง " + tag + " จก.")) // a company the lookup lacks
            .Row("CALIPER", ("ชื่อเครื่องมือวัด", "Vernier Digital"), ("หมายเลขเครื่อง", $"VN-{tag}01/2567"), ("วันที่ขึ้นบัญชี", new DateTime(2567, 5, 3)), ("แผนก", $"{dept} (D02{tag})"),
                ("หมายเลขเครื่องมือที่ใช้(ผ่านการสอบเทียบแล้ว)", $"GB-{tag}1/2551"), ("ผู้สอบเทียบ", employeeName))
            .Row("PRESSURE GAUGE", ("ชื่อเครื่องมือวัด", "Pressure Gauge"), ("หมายเลขเครื่อง", $"PG-{tag}01/2562"), ("วันที่ขึ้นบัญชี", new DateTime(1962, 8, 17)), ("แผนก", dept), ("หน่วย", "Kg/cm²"), ("ผู้สอบเทียบ", "ผู้สอบเทียบที่ไม่มีในทะเบียน"))
            .Row("PRESSURE GAUGE", ("ชื่อเครื่องมือวัด", "Pressure Gauge"), ("หมายเลขเครื่อง", $"PG-{tag}02/2565"), ("วันที่ขึ้นบัญชี", new DateTime(4722, 3, 1)), ("แผนก", "ZZ"), ("หน่วย", "Kg/cm²")) // year 4722; department unknown
            .Row("PRESSURE GAUGE", ("ชื่อเครื่องมือวัด", "Pressure Gauge"), ("หมายเลขเครื่อง", $"PG-{tag}01/2562"), ("หน่วย", "Kg/cm²"))            // the same code again
            .Row("PRESSURE GAUGE", ("ชื่อเครื่องมือวัด", "Pressure Gauge"), ("หมายเลขเครื่อง", null))                                                  // no code
            .Row("เครื่องชั่ง", ("ชื่อเครื่องมือวัด", "เครื่องชั่ง 3 กก."), ("หมายเลขเครื่อง", $"WG-{tag}01/2568"), ("แผนก", $"{dept}(D02{tag},D{tag}9,99)"), ("หน่วย", "กรัม"))        // machine 99 is not in the register
            .Row("ตัวควบคุมอุณหภูมิ", ("ชื่อเครื่องมือวัด", "Temp Control"), ("หมายเลขเครื่อง", $"TC-{tag}01/2562"), ("หน่วย", null), ("หมายเลขเครื่องมือที่ใช้(ผ่านการสอบเทียบแล้ว)", $"TC-QA001/2547"))   // blank unit; standard not in the register
            .Row("ตัวควบคุมอุณหภูมิ", ("ชื่อเครื่องมือวัด", "Temp Control"), ("หมายเลขเครื่อง", $"TC-{tag}02/2568"), ("วันที่ขึ้นบัญชี", new DateTime(2568, 8, 4)), ("หน่วย", "°C"))
            .Row("jig", ("ชื่อ JIG", "RX100,MATE'U,BELL,087,200"), ("หมายเลขเครื่อง", $"JG-{tag}1/2550"), ("แกน (mm)", "7, 6"), ("คู่มือวิธีการที่ใช้เลขที่", "WI-QA049"), ("ขีดจำกัดที่ยอมรับได้", "0.20 mm."),
                ("หมายเลขเวอร์เนียที่ใช้(ผ่านการสอบเทียบแล้ว)", $"VN-{tag}01/2567"), ("แผนก", "QA"));
        var source = Save(file);
        var before = (Hash: SHA256.HashData(File.ReadAllBytes(source)), Written: File.GetLastWriteTimeUtc(source));
        var map = Path.Combine(work.FullName, "map.csv");
        File.WriteAllLines(map, ["kind,legacy,code,name,prefix", $"calib-department,{dept},{dept},,", $"calibrator,{employeeName},{employeeCode},,"]);

        var report = await RunAsync(source, map);

        Assert.Equal(before.Hash, SHA256.HashData(File.ReadAllBytes(source))); // the source is read through a copy
        Assert.Equal(before.Written, File.GetLastWriteTimeUtc(source));
        Assert.True(report.Balanced, string.Join("; ", report.Tables.Where(t => !t.Balanced).Select(t => $"{t.LegacyTable} {t.SourceRows}/{t.Excluded}/{t.Rejected}/{t.Loaded}/{t.InDatabase}")));

        string Rules(string key) => string.Join(",", report.Exceptions.Where(e => e.Key == key).Select(e => e.Rule).Order());
        var all = await AsAsync(async s => (await s.Get<InstrumentDirectory>().ListAsync(InstrumentKind.PressureGauge)).Concat(await s.Get<InstrumentDirectory>().ListAsync(InstrumentKind.TempControl))
            .Concat(await s.Get<InstrumentDirectory>().ListAsync(InstrumentKind.Caliper)).Concat(await s.Get<InstrumentDirectory>().ListAsync(InstrumentKind.Scale)).ToList());
        InstrumentDto Find(string code) => all.Single(i => i.Code == code);

        // dates: 1962 → 2019, 2567 → 2024, 2568 → 2025, year 4722 blank; each fix is a row of the report with before and after
        Assert.Equal(new DateOnly(2019, 8, 17), Find($"PG-{tag}01/2562").RegisteredOn);
        Assert.Equal(new DateOnly(2024, 5, 3), Find($"VN-{tag}01/2567").RegisteredOn);
        Assert.Equal(new DateOnly(2025, 8, 4), Find($"TC-{tag}02/2568").RegisteredOn);
        Assert.Null(Find($"PG-{tag}02/2565").RegisteredOn);
        Assert.Contains(report.Exceptions, e => e.Key == $"PG-{tag}01/2562" && e.Rule == "DQ-Q08" && e.Problem.Contains("1962-08-17 → 2019-08-17") && e.Action == "แก้อัตโนมัติ");
        Assert.Contains(report.Exceptions, e => e.Key == $"PG-{tag}02/2565" && e.Rule == "DQ-Q08" && e.Action == MigrationReport.LoadedBlank);

        // text is trimmed; a blank unit of a temperature controller becomes °C; the standard instrument text is kept and reported
        Assert.Equal("°C", Find($"TC-{tag}01/2562").Unit);
        Assert.Contains("DQ-Q09", Rules($"TC-{tag}01/2562"));
        Assert.Contains("DQ-Q07", Rules($"TC-{tag}01/2562"));
        Assert.Equal("TC-QA001/2547", Find($"TC-{tag}01/2562").StandardUsed);
        var external = await AsAsync(s => s.Get<InstrumentDirectory>().ListAsync(InstrumentKind.External));
        Assert.Equal("5", external.Single(i => i.Code == $"GB-{tag}1/2551").Range);

        // department and machines from org; a machine or an abbreviation that does not match is not guessed
        Assert.Equal((org.Department, 1), (Find($"VN-{tag}01/2567").DepartmentId, Find($"VN-{tag}01/2567").MachineIds.Count));
        Assert.Equal(2, Find($"WG-{tag}01/2568").MachineIds.Count);
        Assert.Contains("DQ-Q05", Rules($"WG-{tag}01/2568"));
        Assert.Contains("[เครื่องจักรเดิม:", Find($"WG-{tag}01/2568").Note);
        Assert.Null(Find($"PG-{tag}02/2565").DepartmentId);
        Assert.Contains("[แผนกเดิม: ZZ]", Find($"PG-{tag}02/2565").Note);

        // the calibrator by the map; the unmatched one is reported by the code of the instrument, never by name
        Assert.Equal(employeeName, Find($"VN-{tag}01/2567").CalibratorName);
        Assert.Contains("DQ-Q16", Rules($"PG-{tag}01/2562"));
        Assert.DoesNotContain(report.Exceptions, e => e.Problem.Contains("ผู้สอบเทียบที่ไม่มีในทะเบียน") || e.Key.Contains("ผู้สอบเทียบที่ไม่มีในทะเบียน"));

        // companies: the lookup's and the one only the external register uses
        var companies = await AsAsync(s => s.Get<CalibrationProviderDirectory>().ListAsync());
        Assert.Contains(companies, c => c.Name == "Quality Calibration " + tag + " Co.,Ltd.");
        Assert.Contains(companies, c => c.Name == "บ.ทดสอบเครื่องชั่ง " + tag + " จก.");
        Assert.Contains("DQ-Q24", Rules($"Q01-{tag}"));
        Assert.Equal(companies.Single(c => c.Name.StartsWith("Quality", StringComparison.Ordinal) && c.Name.Contains(tag, StringComparison.Ordinal)).SupplierId, external.Single(i => i.Code == $"GB-{tag}1/2551").ProviderSupplierId);

        // a repeated code and a blank one are not loaded; the JIG keeps its own fields
        Assert.Single(all, i => i.Code == $"PG-{tag}01/2562");
        Assert.Contains(report.Exceptions, e => e.Rule == "DQ-Q22" && e.Key == $"PG-{tag}01/2562" && e.Action == MigrationReport.NotLoaded);
        Assert.Contains(report.Exceptions, e => e.Rule == "DQ-Q22" && e.Key.StartsWith("แถวที่", StringComparison.Ordinal));
        var jig = (await AsAsync(s => s.Get<InstrumentDirectory>().ListAsync(InstrumentKind.Jig))).Single(i => i.Code == $"JG-{tag}1/2550");
        Assert.Equal(("RX100,MATE'U,BELL,087,200", "7, 6", "WI-QA049", "0.20 mm."), (jig.Name, jig.Axis, jig.ManualNo, jig.Limit));
        Assert.Null(jig.Maker);

        // the code map used lists the names that still need a decision (kept outside git with the rest of the report)
        Assert.Contains(report.CodeMapUsed, u => u.Kind == CodeKinds.Calibrator && u.Legacy == "ผู้สอบเทียบที่ไม่มีในทะเบียน" && u.Code == "");
        Assert.Contains(report.CodeMapUsed, u => u.Kind == CodeKinds.CalibDate && u.Legacy == $"PG-{tag}02/2565");
        Assert.True(File.Exists(Path.Combine(work.FullName, "out", MigrationReport.DqFile)));
    }

    [Fact] // the decision on the date written back in the code map, and the run repeated: the same result, the decision applied
    public async Task A_second_run_gives_the_same_rows_and_applies_the_decisions_of_the_map()
    {
        var tag = Unique();
        var file = FakeCalibFile.Empty()
            .Row("PRESSURE GAUGE", ("ชื่อเครื่องมือวัด", "Pressure Gauge"), ("หมายเลขเครื่อง", $"PG-{tag}07/2565"), ("วันที่ขึ้นบัญชี", new DateTime(4722, 3, 1)), ("หน่วย", "Kg/cm²"))
            .Row("PRESSURE GAUGE", ("ชื่อเครื่องมือวัด", "Pressure Gauge"), ("หมายเลขเครื่อง", $"PG-{tag}08/2565"), ("วันที่ขึ้นบัญชี", new DateTime(2022, 3, 1)), ("หน่วย", "Kg/cm²"));
        var source = Save(file);

        var first = await RunAsync(source, null, "out1");
        Assert.True(first.Balanced);
        var firstRows = await AsAsync(s => s.Get<InstrumentDirectory>().ListAsync(InstrumentKind.PressureGauge));
        Assert.Null(firstRows.Single(i => i.Code == $"PG-{tag}07/2565").RegisteredOn);

        var map = Path.Combine(work.FullName, "map.csv");
        File.WriteAllLines(map, ["kind,legacy,code,name,prefix", $"calib-date,PG-{tag}07/2565,2022-03-01,,"]);
        var second = await RunAsync(source, map, "out2");
        Assert.True(second.Balanced);
        var secondRows = await AsAsync(s => s.Get<InstrumentDirectory>().ListAsync(InstrumentKind.PressureGauge));
        Assert.Equal(firstRows.Count, secondRows.Count); // deleted and loaded again, not added to
        Assert.Equal(new DateOnly(2022, 3, 1), secondRows.Single(i => i.Code == $"PG-{tag}07/2565").RegisteredOn);
        Assert.DoesNotContain(second.Exceptions, e => e.Key == $"PG-{tag}07/2565" && e.Action == MigrationReport.LoadedBlank);
    }

    [Fact] // the loading is not in the change history (#39)
    public async Task The_load_leaves_no_history()
    {
        var tag = Unique();
        var file = FakeCalibFile.Empty().Row("CALIPER", ("ชื่อเครื่องมือวัด", "Vernier Digital"), ("หมายเลขเครื่อง", $"VN-{tag}09/2569"));
        await RunAsync(Save(file));
        await using var scope = db.Scope();
        var rows = await scope.Get<Lockte.Web.Audit.HistoryService>().SearchAsync(new(Table: "เครื่องมือวัด", RowKey: $"VN-{tag}09/2569"));
        Assert.Empty(rows);
    }

    [Fact] // --jig-images: the pictures of a folder written by tools/calib-jig-images are loaded after the instruments and the gaps are in the report
    public async Task The_jig_pictures_of_a_folder_are_loaded_and_the_sheets_without_one_are_reported()
    {
        var folder = Path.Combine(work.FullName, "jig-images");
        Directory.CreateDirectory(Path.Combine(folder, "images"));
        await File.WriteAllBytesAsync(Path.Combine(folder, "images", "aaaa0001.png"), [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 9, 9, 9, (byte)Random.Shared.Next(0, 255)]);
        await File.WriteAllLinesAsync(Path.Combine(folder, "mapping.csv"), ["type,model,report,image", "ZeroCheck,CLICK,CLICK,aaaa0001.png", "Grinding,CLICK,JIG กรอ CLICK,aaaa0001.png"]);
        var source = Save(FakeCalibFile.Empty().Row("CALIPER", ("ชื่อเครื่องมือวัด", "Vernier Digital"), ("หมายเลขเครื่อง", $"VN-{Unique()}01/2569")));

        var report = await RunAsync(source, null, "out-images", folder);

        Assert.Contains(report.Notes, n => n.StartsWith("รูป JIG: โหลด 2 แบบ (รูปที่ไม่ซ้ำ 1) จากแมปปิ้ง 2 แถว", StringComparison.Ordinal));
        Assert.Equal(40, report.Exceptions.Count(e => e.Table == "รูป JIG" && e.Problem.Contains("ยังไม่มีรูป", StringComparison.Ordinal)));
        Assert.DoesNotContain(report.Exceptions, e => e.Table == "รูป JIG" && e.Key.Contains("CLICK", StringComparison.Ordinal));
    }
}
