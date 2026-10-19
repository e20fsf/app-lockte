using System.Runtime.Versioning;
using System.Security.Cryptography;
using Lockte.Hr;
using Lockte.Migration;
using Lockte.Org;
using Lockte.Sales;
using Lockte.Web.Audit;

namespace Lockte.Web.Tests;

/// <summary>
/// เครื่องมือย้ายข้อมูล: ข้อมูลหลัก (#47) — a fake <c>พนักงาน.accdb</c> in, the registers of modules องค์กร / พนักงานและค่าแรง /
/// ขายและส่งของ out, read back through their public services, plus the DQ report and the row counts.
/// </summary>
[Collection(MigrationDbCollection.Name)]
[SupportedOSPlatform("windows")]
public sealed class MigrationTests(MigrationDbFixture db) : IDisposable
{
    private readonly DirectoryInfo work = Directory.CreateTempSubdirectory("lockte-migration-test-");

    public void Dispose() => work.Delete(recursive: true);

    private string Save(FakeLegacyFile file, string name = "พนักงาน.accdb") => file.Save(Path.Combine(work.FullName, name));

    private Task<MigrationReport> RunAsync(string source, string? codeMap = null, string output = "out")
        => MigrationTool.RunAsync(db.Services, source, codeMap, Path.Combine(work.FullName, output));

    private async Task<T> ReadAsync<T>(Func<TestScope, Task<T>> read)
    {
        await using var scope = db.Scope("hr1");
        return await read(scope);
    }

    [Fact]
    public async Task Loads_the_master_data_from_a_copy_without_touching_the_source_file()
    {
        var source = Save(FakeLegacyFile.Standard());
        var before = (Hash: SHA256.HashData(File.ReadAllBytes(source)), Written: File.GetLastWriteTimeUtc(source));

        var report = await RunAsync(source);

        Assert.Equal(before.Hash, SHA256.HashData(File.ReadAllBytes(source)));
        Assert.Equal(before.Written, File.GetLastWriteTimeUtc(source));
        Assert.False(File.Exists(Path.ChangeExtension(source, ".laccdb")));

        // ฝ่าย: T-ฝ่าย in its order, then the ฝ่าย only T-แผนก uses (data-model: จัดซื้อและการเงิน is not in T-ฝ่าย)
        var sections = await ReadAsync(s => s.Get<OrgDirectory>().GetSectionsAsync());
        Assert.Equal(["ผลิต", "บุคคลและธุรการ", "ทั่วไป", "จัดซื้อและการเงิน"], sections.Select(x => x.Name));

        // กะ: rows of T-SHIFT that have a code; B ends the next day
        var shifts = await ReadAsync(s => s.Get<OrgDirectory>().GetShiftsAsync());
        Assert.Equal(["A", "B", "D"], shifts.Select(x => x.Code));
        var b = shifts.Single(x => x.Code == "B");
        Assert.Equal((new TimeOnly(20, 10), new TimeOnly(5, 10), new TimeOnly(0, 10), new TimeOnly(1, 10), 60),
            (b.WorkIn, b.WorkOut, b.BreakStart!.Value, b.BreakEnd!.Value, b.RestMinutes));
        Assert.True(b.CrossesMidnight);

        // แผนก: one per name (DQ-5), the T-แผนก rows become its shifts with the names printed per shift
        var departments = await ReadAsync(s => s.Get<OrgDirectory>().GetDepartmentsAsync());
        Assert.Equal([("O", "บุคคลและธุรการ", "O"), ("D", "ไดคาสท์", "D"), ("F", "จัดซื้อและการเงิน", "F")],
            departments.Select(d => (d.Code, d.Name, d.EmployeeIdPrefix)));
        var dieCast = departments.Single(d => d.Name == "ไดคาสท์");
        Assert.Equal(("ผลิต", 2, 14), (dieCast.SectionName, dieCast.SortOrder, dieCast.StaffingTarget!.Value));
        Assert.Equal(["A", "B"], dieCast.Shifts.Select(h => h.ShiftCode));
        var office = departments.Single(d => d.Name == "บุคคลและธุรการ");
        Assert.Equal([("D", "นาง  สมมุติ  ไม่มีตัวตน", "ผู้จัดการสมมุติ", "ผจก.โรงงานสมมุติ")],
            office.Shifts.Select(h => (h.ShiftCode, h.SupervisorName, h.ManagerName, h.FactoryManagerName)));
        Assert.Null(departments.Single(d => d.Name == "จัดซื้อและการเงิน").StaffingTarget);

        // พนักงาน: every row, resigned too, with แผนก / ฝ่าย / กะ linked by the legacy names and codes
        var employees = await ReadAsync(s => s.Get<EmployeeDirectory>().SearchAsync());
        Assert.Equal(["D001", "F001", "O001", "D002"], employees.Select(e => e.Code));
        var o001 = (await ReadAsync(s => s.Get<EmployeeDirectory>().FindByCodeAsync("O001")))!;
        Assert.Equal(("นาง  ทดสอบ  ธุรการ", "บุคคลและธุรการ", "บุคคลและธุรการ", "D"), (o001.Name, o001.DepartmentName, o001.SectionName, o001.ShiftCode));
        Assert.Equal((new DateOnly(1990, 2, 3), new DateOnly(2020, 6, 1), EmployeeTypes.Monthly, 15600.0, 520.0),
            (o001.BirthDate!.Value, o001.StartDate!.Value, o001.EmployeeType!.Value, o001.Salary, o001.DailyWage));
        Assert.Equal(("0000000001", "0000000002", "0000000000001", "ปวส.", "งานเอกสาร", "1", "ไทย", false),
            (o001.BankAccountTtb, o001.BankAccountKbank, o001.NationalId, o001.Education, o001.Duty, o001.ListNo, o001.Nationality, o001.HotAllowance));
        Assert.Equal(("Mrs.", "Test Thurakan"), (o001.EnglishTitle, o001.EnglishName)); // DQ-3: Mrs → Mrs.
        var d001 = (await ReadAsync(s => s.Get<EmployeeDirectory>().FindByCodeAsync("D001")))!;
        Assert.Equal((true, 80, "NO", EmployeeTypes.Daily, 0.0, "A"), (d001.HotAllowance, d001.Evaluation1Score!.Value, d001.Evaluation2Grade, d001.EmployeeType!.Value, d001.Salary, d001.ShiftCode));
        var d002 = (await ReadAsync(s => s.Get<EmployeeDirectory>().FindByCodeAsync("D002")))!;
        Assert.Equal((new DateOnly(2019, 12, 31), "ย้ายภูมิลำเนา"), (d002.ResignDate!.Value, d002.Memo));

        // หัวหน้า: T-แผนก.SUPER matched to one employee by name (spaces ignored) → หัวหน้ากะ of each shift, and หัวหน้าแผนก
        // when every shift has the same one; a name that matches nobody stays a printed name only
        Assert.All(dieCast.Shifts, h => Assert.Equal(d001.Id, h.SupervisorEmployeeId));
        Assert.Equal(d001.Id, dieCast.HeadEmployeeId);
        Assert.Null(office.HeadEmployeeId);
        Assert.Null(office.Shifts.Single().SupervisorEmployeeId);
        Assert.Contains(report.Exceptions, e => e.Table == "แผนก" && e.Key == "บุคคลและธุรการ กะ D" && e.Action == MigrationReport.LoadedBlank);

        // วันหยุด and ลูกค้างานรับจ้าง
        var calendar = await ReadAsync(s => s.Get<CalendarDirectory>().GetAsync(new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31)));
        Assert.Equal([(new DateOnly(2025, 1, 5), "วันอาทิตย์"), (new DateOnly(2025, 1, 12), "วันอาทิตย์")],
            calendar.WeeklyHolidays.Select(h => (h.Date, h.Label)));
        Assert.Equal([(new DateOnly(2025, 1, 1), "วันขึ้นปีใหม่")], calendar.PublicHolidays.Select(h => (h.Date, h.Name)));
        var customers = await ReadAsync(s => s.Get<CustomerDirectory>().GetAllAsync());
        Assert.Equal([("S", "ลูกค้าสมมุติสอง"), ("Y", "ลูกค้าสมมุติหนึ่ง")], customers.Select(c => (c.Code, c.Name)));

        // รายงานตรวจยอด: every legacy row is accounted for and every loaded row is in the database
        Assert.True(report.Balanced, string.Join("\n", report.Tables.Where(t => !t.Balanced)));
        Assert.Contains(report.Tables, t => t is { LegacyTable: "EMPLO", SourceRows: 4, Loaded: 4, InDatabase: 4 });
        Assert.Contains(report.Tables, t => t is { LegacyTable: "SHIFT", SourceRows: 4, Excluded: 1, Loaded: 3 });
        Assert.Contains(report.Tables, t => t is { NewTable: "org.Departments", SourceRows: 4, Excluded: 1, Loaded: 3 });
        Assert.Contains(report.Tables, t => t is { NewTable: "org.DepartmentShifts", SourceRows: 4, Loaded: 4 });
        foreach (var file in new[] { MigrationReport.DqFile, MigrationReport.CountsFile, MigrationReport.CodeMapFile })
            Assert.True(File.Exists(Path.Combine(work.FullName, "out", file)), file);
    }

    [Fact]
    public async Task Each_run_deletes_and_loads_again_with_the_same_result()
    {
        var source = Save(FakeLegacyFile.Standard());
        await RunAsync(source, output: "first");
        var first = await SnapshotAsync();
        await using (var scope = db.Scope("hr1")) // keyed in between the rehearsals: gone after the next run
            Assert.True((await scope.Get<EmployeeRegistry>().SaveAsync(new EmployeeInput { Code = "Z999", Name = "นาย ทดสอบ คีย์เพิ่ม" })).Succeeded);

        await RunAsync(source, output: "second");

        Assert.Equal(first, await SnapshotAsync());
        foreach (var file in new[] { MigrationReport.DqFile, MigrationReport.CountsFile, MigrationReport.CodeMapFile })
            Assert.Equal(File.ReadAllText(Path.Combine(work.FullName, "first", file)), File.ReadAllText(Path.Combine(work.FullName, "second", file)));
    }

    [Fact]
    public async Task The_code_map_gives_codes_and_names_to_legacy_values()
    {
        var legacy = FakeLegacyFile.Standard();
        legacy.Tables["EMPLO"].Single(r => (string)r["ID"]! == "F001")["SHIFT"] = "กD"; // malformed
        legacy.Tables["แผนก"].Single(r => (int)r["SUF"]! == 4)["SHIFT"] = "กD";
        var source = Save(legacy);
        var map = Path.Combine(work.FullName, "map.csv");
        File.WriteAllText(map, """
            kind,legacy,code,name,prefix
            # comments are ignored
            department,ไดคาสท์,DC,ไดคาสท์ (ฉีดอลูมิเนียม),
            department,จัดซื้อและการเงิน,FIN,,
            customer,y,40-01-001,"บริษัท ลูกค้าสมมุติหนึ่ง, จำกัด",
            shift,กD,D,,
            """, new System.Text.UTF8Encoding(true));

        var report = await RunAsync(source, map);

        var departments = await ReadAsync(s => s.Get<OrgDirectory>().GetDepartmentsAsync());
        Assert.Equal([("O", "บุคคลและธุรการ"), ("DC", "ไดคาสท์ (ฉีดอลูมิเนียม)"), ("FIN", "จัดซื้อและการเงิน")],
            departments.Select(d => (d.Code, d.Name)));
        Assert.Equal(["D"], departments.Single(d => d.Code == "FIN").Shifts.Select(h => h.ShiftCode));
        var d001 = (await ReadAsync(s => s.Get<EmployeeDirectory>().FindByCodeAsync("D001")))!;
        Assert.Equal("ไดคาสท์ (ฉีดอลูมิเนียม)", d001.DepartmentName); // EMPLO.DEPARTMENT still says ไดคาสท์
        Assert.Equal("D", (await ReadAsync(s => s.Get<EmployeeDirectory>().FindByCodeAsync("F001")))!.ShiftCode);
        var customers = await ReadAsync(s => s.Get<CustomerDirectory>().GetAllAsync());
        Assert.Equal([("40-01-001", "บริษัท ลูกค้าสมมุติหนึ่ง, จำกัด"), ("S", "ลูกค้าสมมุติสอง")], customers.Select(c => (c.Code, c.Name)));
        Assert.True(report.Balanced);

        // The map the run used, ready to edit and give back for the next run
        var used = CodeMap.Read(Path.Combine(work.FullName, "out", MigrationReport.CodeMapFile));
        Assert.Equal("40-01-001", used.Find(CodeKinds.Customer, "Y")!.Code);
        Assert.Equal("S", used.Find(CodeKinds.Customer, "S")!.Code);
        Assert.Equal(("O", "บุคคลและธุรการ", "O"), used.Find(CodeKinds.Department, "บุคคลและธุรการ") is { } o ? (o.Code, o.Name, o.Prefix) : default);
        Assert.Equal("D", used.Find(CodeKinds.Shift, "กD")!.Code);
        Assert.Contains(report.CodeMapUsed, u => u is { Kind: CodeKinds.Customer, Legacy: "Y", FromMap: true });
        Assert.Contains(report.CodeMapUsed, u => u is { Kind: CodeKinds.Customer, Legacy: "S", FromMap: false });
    }

    [Fact]
    public async Task Rows_that_cannot_be_loaded_as_they_are_go_to_the_DQ_report()
    {
        var legacy = FakeLegacyFile.Standard()
            .Row("EMPLO", ("NAME", "นาย ไม่มี รหัส"))
            .Row("EMPLO", ("ID", "Q001"), ("NAME", "นาย รหัสซ้ำ หนึ่ง"))
            .Row("EMPLO", ("ID", "q001"), ("NAME", "นาย รหัสซ้ำ สอง"))
            .Row("EMPLO", ("ID", "Q002"))
            .Row("EMPLO", ("ID", "Q003"), ("NAME", "นาย ค่า แปลก"), ("DEPARTMENT", "แผนกที่ไม่มี"), ("SEX", "X"), ("CLAS", "3"),
                ("SHIFT", "Z"), ("START", new DateTime(2564, 1, 4)), ("SECTION", "ผลิต"))
            // DQ-8: one department, two prefixes over its shifts
            .Row("แผนก", ("SUF", 5), ("DEPART", "อัดผ้าเบรก"), ("CODE", "B*"), ("SECTION", "ผลิต"), ("SHIFT", "B"))
            .Row("แผนก", ("SUF", 6), ("DEPART", "อัดผ้าเบรก"), ("CODE", "B*"), ("SECTION", "ผลิต"), ("SHIFT", "A"))
            .Row("แผนก", ("SUF", 7), ("DEPART", "อัดผ้าเบรก"), ("CODE", "A*"), ("SECTION", "ผลิต"), ("SHIFT", "D"))
            .Row("SHIFT", ("SHIFT", "H"), ("W_IN", FakeLegacyFile.Time(8, 0)), ("W_OUT", FakeLegacyFile.Time(17, 0)), ("REST_T", 60))
            .Row("ชื่อลูกค้า", ("NAME", "ลูกค้าไม่มีรหัส"))
            .Row("SUNDAY", ("ITEM", "วันอาทิตย์"))
            .Row("SUNDAY", ("DATE", new DateTime(2025, 1, 19)), ("ITEM", "DEL"))
            .Row("SUNDAY", ("DATE", new DateTime(2025, 1, 26)))
            .Row("SUNDAY", ("DATE", new DateTime(2025, 2, 2)), ("ITEM", "วันอาทิตย์"))
            .Row("SUNDAY", ("DATE", new DateTime(2025, 2, 2)), ("ITEM", "เลื่อนหยุด"))
            .Row("HOLIDAY", ("DATE", new DateTime(2025, 4, 13)), ("ITEM", "DEL"));

        var report = await RunAsync(Save(legacy));

        void Has(string table, string key, string rule, string action)
            => Assert.Contains(report.Exceptions, e => e.Table == table && e.Key == key && e.Rule == rule && e.Action.StartsWith(action));
        Has("EMPLO", "(ID ว่าง) นาย ไม่มี รหัส", "NF-06", MigrationReport.NotLoaded);
        Has("EMPLO", "Q001", "DQ-2", MigrationReport.NotLoaded);
        Has("EMPLO", "Q002", "NF-06", MigrationReport.NotLoaded); // no NAME (BR-051)
        Has("EMPLO", "Q003", "DQ-5", MigrationReport.LoadedBlank);
        Has("EMPLO", "Q003", "NF-06", MigrationReport.LoadedBlank); // SEX, CLAS, SHIFT, START
        Assert.Equal(4, report.Exceptions.Count(e => e is { Table: "EMPLO", Key: "Q003", Rule: "NF-06" }));
        Has("EMPLO", "O001", "DQ-3", "โหลดโดยแก้เป็น Mrs.");
        Has("แผนก", "อัดผ้าเบรก", "DQ-8", "โหลดโดยใช้ตัวอักษร B");
        Has("SHIFT", "H", "NF-06", MigrationReport.NotLoaded);
        Has("ชื่อลูกค้า", "(CODE ว่าง) ลูกค้าไม่มีรหัส", "NF-06", MigrationReport.NotLoaded);
        Has("SUNDAY", "(DATE ว่าง)", "NF-06", MigrationReport.NotLoaded);
        Has("SUNDAY", "2025-01-19", "NF-06", MigrationReport.NotLoaded); // ITEM = DEL (BR-042)
        Has("SUNDAY", "2025-02-02", "DQ-2", MigrationReport.NotLoaded);
        Has("HOLIDAY", "2025-04-13", "NF-06", MigrationReport.NotLoaded);

        var q003 = (await ReadAsync(s => s.Get<EmployeeDirectory>().FindByCodeAsync("Q003")))!;
        Assert.Equal((null, "ผลิต", null, null, null, null), (q003.DepartmentName, q003.SectionName, q003.Sex, q003.EmployeeType, q003.ShiftCode, q003.StartDate));
        Assert.Null(await ReadAsync(s => s.Get<EmployeeDirectory>().FindByCodeAsync("Q001")));
        var brake = (await ReadAsync(s => s.Get<OrgDirectory>().FindDepartmentByNameAsync("อัดผ้าเบรก")))!;
        Assert.Equal(("B", "B"), (brake.Code, brake.EmployeeIdPrefix));
        Assert.Equal(["A", "B", "D"], brake.Shifts.Select(h => h.ShiftCode));
        var calendar = await ReadAsync(s => s.Get<CalendarDirectory>().GetAsync(new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31)));
        Assert.Equal([new DateOnly(2025, 1, 5), new DateOnly(2025, 1, 12), new DateOnly(2025, 1, 26)], calendar.WeeklyHolidays.Select(h => h.Date));
        Assert.Equal("วันอาทิตย์", calendar.WeeklyHolidays[^1].Label); // blank ITEM = the legacy default

        Assert.True(report.Balanced, string.Join("\n", report.Tables.Where(t => !t.Balanced)));
        Assert.Contains(report.Tables, t => t is { LegacyTable: "EMPLO", SourceRows: 9, Rejected: 4, Loaded: 5 });
        Assert.Contains(report.Tables, t => t is { LegacyTable: "SUNDAY", SourceRows: 7, Rejected: 4, Loaded: 3 });
    }

    [Fact]
    public async Task The_load_writes_no_change_history()
    {
        var source = Save(FakeLegacyFile.Standard());
        var before = await HistoryCountAsync();

        await RunAsync(source);

        Assert.Equal(before, await HistoryCountAsync());
    }

    [Fact]
    public void The_console_deletes_only_the_database_named_on_the_command_line()
    {
        const string connection = "Server=example;Database=LockteRehearsal;User Id=u;Password=p";
        string[] args = ["--source", "a.accdb", "--out", "out", "--connection", connection];

        Assert.NotNull(MigrationOptions.Parse(args).Error);
        Assert.NotNull(MigrationOptions.Parse([.. args, "--confirm", "Lockte"]).Error);
        var (options, error) = MigrationOptions.Parse([.. args, "--confirm", "LockteRehearsal", "--map", "map.csv"]);
        Assert.Null(error);
        Assert.Equal(("a.accdb", "map.csv", "out", connection), (options!.SourceFile, options.CodeMapFile, options.OutputFolder, options.ConnectionString));
    }

    private async Task<int> HistoryCountAsync()
        => (await ReadAsync(s => s.Get<HistoryService>().SearchAsync(new HistoryFilter(Max: 100_000)))).Count;

    /// <summary>Everything the run loaded, by business key (internal ids differ between runs).</summary>
    private async Task<string> SnapshotAsync()
    {
        await using var scope = db.Scope("hr1");
        var org = scope.Get<OrgDirectory>();
        var shiftCodes = (await org.GetShiftsAsync()).ToDictionary(s => s.Id, s => s.Code);
        var employees = new List<EmployeeDto>();
        foreach (var item in await scope.Get<EmployeeDirectory>().SearchAsync())
            employees.Add((await scope.Get<EmployeeDirectory>().GetAsync(item.Id))!);
        var employeeCodes = employees.ToDictionary(e => e.Id, e => e.Code);
        var lines = new List<string>();
        lines.AddRange((await org.GetSectionsAsync()).Select(s => $"ฝ่าย {s.Name} {s.SortOrder}"));
        lines.AddRange((await org.GetShiftsAsync()).Select(s => $"กะ {s with { Id = 0, RowVersion = [] }}"));
        foreach (var d in await org.GetDepartmentsAsync())
        {
            lines.Add($"แผนก {d.Code} {d.Name} {d.EmployeeIdPrefix} {d.SortOrder} {d.SectionName} {d.StaffingTarget} {Code(employeeCodes, d.HeadEmployeeId)}");
            lines.AddRange(d.Shifts.Select(h => $"  {h.ShiftCode} {Code(employeeCodes, h.SupervisorEmployeeId)} {h.SupervisorName} {h.ManagerName} {h.FactoryManagerName}"));
        }
        lines.AddRange(employees.Select(e => "พนักงาน " + (e with
        {
            Id = 0, RowVersion = [], DepartmentId = null, SectionId = null, ShiftId = null,
        }) + $" {Code(shiftCodes, e.ShiftId)}"));
        var calendar = await scope.Get<CalendarDirectory>().GetAsync(DateOnly.MinValue, DateOnly.MaxValue);
        lines.AddRange(calendar.WeeklyHolidays.Select(h => $"อาทิตย์ {h.Date} {h.Label}"));
        lines.AddRange(calendar.PublicHolidays.Select(h => $"วันหยุด {h.Date} {h.Name}"));
        lines.AddRange((await scope.Get<CustomerDirectory>().GetAllAsync()).Select(c => $"ลูกค้า {c.Code} {c.Name}"));
        return string.Join("\n", lines);
    }

    private static string Code(Dictionary<int, string> codes, int? id) => id is { } i ? codes[i] : "-";
}
