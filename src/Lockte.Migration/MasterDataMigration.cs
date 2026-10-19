using System.Globalization;
using System.Text.RegularExpressions;
using Lockte.Hr;
using Lockte.Org;
using Lockte.Org.Data;
using Lockte.Sales;
using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;

namespace Lockte.Migration;

/// <summary>
/// ย้ายข้อมูลหลัก (#47): deletes the master data of modules พนักงานและค่าแรง, องค์กร and ขายและส่งของ, then loads
/// ฝ่าย, กะ, แผนก (with its shifts and หัวหน้า), พนักงาน, วันหยุด and ลูกค้างานรับจ้าง from the legacy tables — through each
/// module's public registry, so a loaded row passes the same checks as one keyed on screen. Nothing it does is written
/// to the change history (#41). A legacy row or value the registers cannot take as it is goes to the DQ report
/// (data-model DQ-x, NF-06) instead of stopping the run; every table is counted back afterwards.
/// </summary>
public sealed partial class MasterDataMigration(
    AuditScope audit,
    HrDataReset hrReset,
    OrgDataReset orgReset,
    SalesDataReset salesReset,
    CustomerRegistry customers,
    CustomerDirectory customerDirectory,
    OrgRegistry org,
    OrgDirectory orgDirectory,
    EmployeeRegistry employees,
    EmployeeDirectory employeeDirectory,
    CalendarRegistry calendar,
    CalendarDirectory calendarDirectory)
{
    private const string Other = "NF-06";
    private const string Duplicate = "DQ-2";
    private const string DepartmentByName = "DQ-5";
    private const string DelFlag = "DEL";

    private LegacyDatabase legacy = null!;
    private CodeMap map = null!;
    private MigrationReport report = null!;
    private readonly List<Tally> tallies = [];
    private readonly Dictionary<string, int> shiftIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> sectionIds = new(StringComparer.Ordinal);
    /// <summary>By the legacy name (T-แผนก.DEPART, EMPLO.DEPARTMENT) — DQ-5.</summary>
    private readonly Dictionary<string, int> departmentIds = new(StringComparer.Ordinal);
    private readonly List<LoadedDepartmentShift> departmentShifts = [];
    private readonly List<(int Id, string Name, bool Working)> loadedEmployees = [];

    public async Task<MigrationReport> RunAsync(LegacyDatabase legacyDatabase, CodeMap codeMap)
    {
        (legacy, map, report) = (legacyDatabase, codeMap, new MigrationReport());
        using (audit.SuppressRowChanges())
        {
            await hrReset.DeleteAllAsync(); // hr first: its rows refer to org
            await orgReset.DeleteAllAsync();
            await salesReset.DeleteAllAsync();
            await LoadCustomersAsync();
            await LoadShiftsAsync();
            await LoadSectionsAsync();
            await LoadDepartmentsAsync();
            await LoadEmployeesAsync();
            await LinkSupervisorsAsync();
            await LoadWeeklyHolidaysAsync();
            await LoadPublicHolidaysAsync();
        }
        await CountBackAsync();
        return report;
    }

    // ---------- ลูกค้างานรับจ้าง (T-ชื่อลูกค้า → ทะเบียนลูกค้าของ module ขายและส่งของ) ----------

    private async Task LoadCustomersAsync()
    {
        const string table = "ชื่อลูกค้า";
        var tally = Count(table, "sales.Customers", legacy[table].Count);
        var candidates = new List<(string Legacy, string Code, string? Name)>();
        foreach (var row in legacy[table])
        {
            var legacyCode = row.Text("CODE");
            if (legacyCode is null)
            {
                Reject(tally, table, $"(CODE ว่าง) {row.Text("NAME")}", Other, "ไม่มีรหัสลูกค้า");
                continue;
            }
            var entry = map.Find(CodeKinds.Customer, legacyCode);
            var code = (entry?.Code ?? legacyCode).ToUpperInvariant();
            var name = entry?.Name ?? row.Text("NAME");
            candidates.Add((legacyCode, code, name));
            UseCode(CodeKinds.Customer, legacyCode, code, name, null, entry);
        }
        foreach (var group in candidates.GroupBy(c => c.Code))
        {
            if (!OneValue(tally, table, group.Key, "รหัสลูกค้า", group.ToList(), c => c.Name)) continue;
            var (legacyCode, code, name) = group.First();
            var saved = await customers.SaveAsync(new CustomerInput(null, code, name ?? "", null));
            if (saved.Succeeded) tally.Loaded++;
            else Reject(tally, table, legacyCode, Other, string.Join(" · ", saved.Errors));
        }
    }

    // ---------- กะ (T-SHIFT) ----------

    private async Task LoadShiftsAsync()
    {
        const string table = "SHIFT";
        var tally = Count(table, "org.Shifts", legacy[table].Count);
        var candidates = new List<(string Legacy, ShiftInput Input)>();
        foreach (var row in legacy[table])
        {
            if (row.Text("SHIFT") is not { } legacyCode)
            {
                tally.Excluded++; // a preset time range of the time combos, not a shift (data-model T-SHIFT)
                continue;
            }
            var code = ShiftCode(legacyCode)!;
            if (row.Date("W_IN") is not { } workIn || row.Date("W_OUT") is not { } workOut)
            {
                Reject(tally, table, legacyCode, Other, "ไม่มีเวลาเข้า (W_IN) หรือเวลาออก (W_OUT)");
                continue;
            }
            candidates.Add((legacyCode, new ShiftInput(null, code, Time(workIn), Time(workOut), Time(row.Date("STOP")),
                Time(row.Date("START")), Time(row.Date("O_IN")), Time(row.Date("O_OUT")), (int)(row.Number("REST_T") ?? 0), null)));
        }
        foreach (var group in candidates.GroupBy(c => c.Input.Code, StringComparer.OrdinalIgnoreCase))
        {
            if (!OneValue(tally, table, group.Key, "รหัสกะ", group.ToList(), c => c.Input)) continue;
            var (legacyCode, input) = group.First();
            var saved = await org.SaveShiftAsync(input);
            if (saved.Succeeded)
            {
                tally.Loaded++;
                shiftIds[input.Code] = saved.Id!.Value;
            }
            else Reject(tally, table, legacyCode, Other, string.Join(" · ", saved.Errors));
        }
    }

    /// <summary>A legacy shift value (T-SHIFT, EMPLO, T-แผนก) → รหัสกะ, through the code map.</summary>
    private string? ShiftCode(string? legacyCode)
    {
        if (legacyCode is null) return null;
        var entry = map.Find(CodeKinds.Shift, legacyCode);
        var code = (entry?.Code ?? legacyCode).ToUpperInvariant();
        UseCode(CodeKinds.Shift, legacyCode, code, null, null, entry);
        return code;
    }

    private int? ShiftId(string? legacyCode) => ShiftCode(legacyCode) is { } code && shiftIds.TryGetValue(code, out var id) ? id : null;

    // ---------- ฝ่าย (T-ฝ่าย + the ฝ่าย that T-แผนก and EMPLO use) ----------

    private async Task LoadSectionsAsync()
    {
        const string table = "ฝ่าย";
        var listed = new List<string>();
        var excluded = 0;
        foreach (var row in legacy[table])
        {
            if (row.Text("TYPE") is { } name && !listed.Contains(name)) listed.Add(name);
            else excluded++; // blank or listed twice
        }
        var extra = legacy["แผนก"].Concat(legacy["EMPLO"]).Select(r => r.Text("SECTION")).OfType<string>()
            .Where(n => !listed.Contains(n)).Distinct().Order(StringComparer.Ordinal).ToList();
        var tally = Count($"{table} + แผนก/EMPLO.SECTION", "org.Sections", legacy[table].Count + extra.Count);
        tally.Excluded = excluded;
        foreach (var name in extra)
            report.Add(table, name, Other, "ฝ่ายนี้มีใน T-แผนก หรือ EMPLO แต่ไม่อยู่ใน T-ฝ่าย", "โหลดเพิ่มเป็นฝ่าย");
        var order = 0;
        foreach (var name in listed.Concat(extra))
        {
            order++;
            if (name.Length > OrgDbContext.NameLength)
            {
                Reject(tally, table, name, Other, $"ชื่อฝ่ายยาวเกิน {OrgDbContext.NameLength} ตัว");
                continue;
            }
            var saved = await org.SaveSectionAsync(new SectionInput(null, name, order, null));
            if (saved.Succeeded)
            {
                tally.Loaded++;
                sectionIds[name] = saved.Id!.Value;
            }
            else Reject(tally, table, name, Other, string.Join(" · ", saved.Errors));
        }
    }

    // ---------- แผนก (T-แผนก: one row per department × shift → one department with its shifts; T-แผนก2 อัตรากำลัง) ----------

    private async Task LoadDepartmentsAsync()
    {
        const string table = "แผนก";
        var rows = legacy[table];
        var departmentsTally = Count(table, "org.Departments", rows.Count);
        var shiftsTally = Count(table, "org.DepartmentShifts", rows.Count);
        var candidates = new List<DepartmentCandidate>();
        foreach (var group in rows.Select(r => (Row: r, Suf: (int)(r.Number("SUF") ?? int.MaxValue)))
                     .GroupBy(r => r.Row.Text("DEPART")).OrderBy(g => g.Min(r => r.Suf)))
        {
            var ordered = group.OrderBy(r => r.Suf).Select(r => r.Row).ToList();
            if (group.Key is not { } name)
            {
                foreach (var row in ordered)
                {
                    Reject(departmentsTally, table, $"(DEPART ว่าง) SUF {row.Text("SUF")}", Other, "ไม่มีชื่อแผนก");
                    shiftsTally.Rejected++;
                }
                continue;
            }
            candidates.Add(Department(name, group.Min(r => r.Suf), ordered));
        }

        var staffing = StaffingTargets(candidates);
        foreach (var group in candidates.GroupBy(c => c.Code, StringComparer.OrdinalIgnoreCase))
        {
            var members = group.ToList();
            if (members.Count > 1 && members.Any(m => !m.FromMap))
            {
                // Two departments end up with one code only if the code map says so (a merge).
                foreach (var member in members)
                {
                    var others = string.Join(", ", members.Where(m => m != member).Select(m => m.LegacyName));
                    Reject(departmentsTally, table, member.LegacyName, "DQ-8",
                        $"รหัสแผนก '{group.Key}' ซ้ำกับ {others} — ใส่รหัสของแผนกนี้ในตารางจับคู่", rows: member.Rows.Count);
                    shiftsTally.Rejected += member.Rows.Count;
                }
                continue;
            }
            var first = members[0];
            var shifts = new List<(DepartmentShiftInput Input, LegacyRow Row, string LegacyName)>();
            foreach (var member in members)
                foreach (var row in member.Rows)
                {
                    var key = $"{member.LegacyName} กะ {row.Text("SHIFT")}";
                    if (ShiftId(row.Text("SHIFT")) is not { } shiftId)
                        Reject(shiftsTally, table, key, Other, $"ไม่พบกะ '{row.Text("SHIFT")}' ในทะเบียนกะ");
                    else if (shifts.Any(s => s.Input.ShiftId == shiftId))
                        Reject(shiftsTally, table, key, Duplicate, "แผนกนี้มีกะนี้มากกว่าหนึ่งแถว");
                    else
                        shifts.Add((new DepartmentShiftInput(shiftId, PrintedName(row, "SUPER", key), PrintedName(row, "MANAGER", key),
                            PrintedName(row, "FACTORY MANAGER", key)), row, member.LegacyName));
                }
            var target = members.Select(m => staffing.GetValueOrDefault(m.LegacyName)).FirstOrDefault(t => t is not null);
            var saved = await org.SaveDepartmentAsync(new DepartmentInput(null, first.Code, first.Name, first.Prefix,
                members.Min(m => m.SortOrder), first.SectionName is { } s && sectionIds.TryGetValue(s, out var sectionId) ? sectionId : null, null,
                shifts.Select(s => s.Input).ToList(), null, target));
            var rowCount = members.Sum(m => m.Rows.Count);
            if (!saved.Succeeded)
            {
                Reject(departmentsTally, table, first.LegacyName, Other, string.Join(" · ", saved.Errors), rows: rowCount);
                shiftsTally.Rejected += shifts.Count;
                continue;
            }
            departmentsTally.Loaded++;
            departmentsTally.Excluded += rowCount - 1; // the other shifts of the same department
            shiftsTally.Loaded += shifts.Count;
            foreach (var member in members) departmentIds[member.LegacyName] = saved.Id!.Value;
            departmentShifts.AddRange(shifts.Select(s =>
                new LoadedDepartmentShift(saved.Id!.Value, s.Input.ShiftId, $"{s.LegacyName} กะ {s.Row.Text("SHIFT")}", s.Input.SupervisorName)));
        }
    }

    private DepartmentCandidate Department(string legacyName, int sortOrder, List<LegacyRow> rows)
    {
        const string table = "แผนก";
        var entry = map.Find(CodeKinds.Department, legacyName);
        var prefixes = rows.Select(r => r.Text("CODE")?.TrimEnd('*').Trim().ToUpperInvariant()).OfType<string>()
            .Where(p => p.Length > 0).ToList();
        var prefix = entry?.Prefix?.ToUpperInvariant()
            ?? prefixes.GroupBy(p => p).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault() ?? "";
        if (entry?.Prefix is null && prefixes.Distinct().Count() > 1)
            report.Add(table, legacyName, "DQ-8", $"ตัวอักษรนำหน้ารหัสพนักงานไม่ตรงกันระหว่างกะ: {string.Join(", ", prefixes.Select(p => p + "*"))}",
                $"โหลดโดยใช้ตัวอักษร {prefix} (ตัวที่ใช้มากที่สุด)");
        var sections = rows.Select(r => r.Text("SECTION")).OfType<string>().Distinct().ToList();
        if (sections.Count > 1)
            report.Add(table, legacyName, Other, $"ฝ่ายไม่ตรงกันระหว่างกะ: {string.Join(", ", sections)}", $"โหลดโดยใช้ฝ่าย {sections[0]}");
        var code = (entry?.Code ?? prefix).ToUpperInvariant();
        var name = entry?.Name ?? legacyName;
        UseCode(CodeKinds.Department, legacyName, code, name, prefix, entry);
        return new DepartmentCandidate(legacyName, code, name, prefix, sortOrder, sections.FirstOrDefault(), rows, entry is not null);
    }

    /// <summary>T-แผนก2.QTY by the legacy department name.</summary>
    private Dictionary<string, int?> StaffingTargets(List<DepartmentCandidate> departments)
    {
        const string table = "แผนก2";
        var tally = Count(table, "org.Departments (อัตรากำลัง)", legacy[table].Count);
        var targets = new Dictionary<string, int?>(StringComparer.Ordinal);
        foreach (var row in legacy[table])
        {
            var name = row.Text("DEPART");
            if (row.Number("QTY") is not { } qty) tally.Excluded++; // no target (ทั่วไป, ตกแต่ง2 in the real file)
            else if (name is null || departments.All(d => d.LegacyName != name))
                Reject(tally, table, name ?? "(DEPART ว่าง)", DepartmentByName, "ไม่พบแผนกนี้ใน T-แผนก");
            else if (!targets.TryAdd(name, (int)qty))
                Reject(tally, table, name, Duplicate, "แผนกนี้มีอัตรากำลังมากกว่าหนึ่งแถว");
            else tally.Loaded++;
        }
        return targets;
    }

    private string? PrintedName(LegacyRow row, string column, string key)
    {
        var name = row.Text(column);
        if (name is null || name.Length <= OrgDbContext.NameLength) return name;
        report.Add("แผนก", key, Other, $"{column} ยาวเกิน {OrgDbContext.NameLength} ตัว", MigrationReport.LoadedBlank);
        return null;
    }

    // ---------- พนักงาน (EMPLO) ----------

    private async Task LoadEmployeesAsync()
    {
        const string table = "EMPLO";
        var rows = legacy[table];
        var tally = Count(table, "hr.Employees", rows.Count);
        var working = Count($"{table} (RESIGN ว่าง)", "hr.Employees (ทำงานอยู่)", rows.Count(r => r.Date("RESIGN") is null));
        var codes = rows.Select(r => EmployeeFormRules.NormalizeCode(r.Text("ID"))).ToList();
        var copies = codes.OfType<string>().GroupBy(c => c).ToDictionary(g => g.Key, g => g.Count());
        for (var i = 0; i < rows.Count; i++)
        {
            var (row, code) = (rows[i], codes[i]);
            var counts = row.Date("RESIGN") is null ? [tally, working] : new[] { tally };
            if (code is null)
            {
                RejectAll(counts, table, $"(ID ว่าง) {row.Text("NAME")}", Other, "ไม่มีรหัสพนักงาน");
                continue;
            }
            if (copies[code] > 1)
            {
                RejectAll(counts, table, code, Duplicate, $"รหัสพนักงานซ้ำ {copies[code]} แถว: {row.Text("NAME")}");
                continue;
            }
            if (row.Text("NAME") is null)
            {
                RejectAll(counts, table, code, Other, "ไม่มีชื่อ (ระบบเดิมลบแถวนี้ตอนปิดฟอร์ม BR-051)");
                continue;
            }
            var saved = await employees.SaveAsync(Employee(row, code));
            if (!saved.Succeeded)
            {
                RejectAll(counts, table, code, Other, string.Join(" · ", saved.Errors));
                continue;
            }
            foreach (var t in counts) t.Loaded++;
            loadedEmployees.Add((saved.Id!.Value, row.Text("NAME")!, row.Date("RESIGN") is null));
        }
    }

    private EmployeeInput Employee(LegacyRow row, string code)
    {
        void Blank(string column, string problem, string rule = Other)
            => report.Add("EMPLO", code, rule, $"{column}: {problem}", MigrationReport.LoadedBlank);
        DateOnly? Day(string column)
        {
            if (row.Date(column) is not { } date) return null;
            if (date.Year is >= 1900 and <= 2100) return DateOnly.FromDateTime(date);
            Blank(column, $"ปีผิด {date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)} (คีย์ พ.ศ. ลงช่อง ค.ศ.?)");
            return null;
        }

        var sex = row.Text("SEX")?.ToUpperInvariant();
        if (sex is not null && !EmployeeLookups.Sexes.ContainsKey(sex)) { Blank("SEX", $"'{sex}' ไม่ใช่ M หรือ F"); sex = null; }
        int? type = row.Text("CLAS") is { } clas ? int.TryParse(clas, out var t) && EmployeeTypes.Names.ContainsKey(t) ? t : -1 : null;
        if (type == -1) { Blank("CLAS", $"'{row.Text("CLAS")}' ไม่ใช่ 1 (รายเดือน) หรือ 2 (รายวัน)"); type = null; }
        int? departmentId = null;
        if (row.Text("DEPARTMENT") is { } department)
        {
            if (departmentIds.TryGetValue(department, out var id)) departmentId = id;
            else Blank("DEPARTMENT", $"ไม่พบแผนก '{department}' ในทะเบียนแผนก", DepartmentByName);
        }
        int? sectionId = null;
        if (row.Text("SECTION") is { } section)
        {
            if (sectionIds.TryGetValue(section, out var id)) sectionId = id;
            else Blank("SECTION", $"ไม่พบฝ่าย '{section}'");
        }
        var shiftId = ShiftId(row.Text("SHIFT"));
        if (row.Text("SHIFT") is { } shift && shiftId is null) Blank("SHIFT", $"ไม่พบกะ '{shift}' ในทะเบียนกะ");

        return new EmployeeInput
        {
            Code = code,
            Name = row.Text("NAME")!,
            EnglishTitle = EnglishTitle(row.Text("NAME TITLE"), code),
            EnglishName = row.Text("NAME ENGLISH"),
            Sex = sex,
            BirthDate = Day("BORN"),
            NationalId = row.Text("ID_CODE"),
            SocialSecurityNo = row.Text("ID_S"),
            IdCardPlace = row.Text("ID_PLACE"),
            IdCardIssued = Day("ID_DATE"),
            RegisteredAddress = row.Text("PER_ADD"),
            CurrentAddress = row.Text("ADDRESS"),
            Telephone = row.Text("TELEPHONE"),
            Nationality = row.Text("NATION") ?? EmployeeInput.DefaultNationality,
            StartDate = Day("START"),
            ListNo = row.Text("NO"),
            Education = row.Text("POINT"),
            DepartmentId = departmentId,
            SectionId = sectionId,
            Position = row.Text("POSITION"),
            Duty = row.Text("WORK"),
            EmployeeType = type,
            Salary = row.Number("SALARY") ?? 0,
            DailyWage = row.Number("SA_DAY") ?? 0,
            ShiftId = shiftId,
            HotAllowance = string.Equals(row.Text("HOT1"), "Y", StringComparison.OrdinalIgnoreCase),
            BankAccountTtb = row.Text("ACC"),
            BankAccountKbank = row.Text("ACC2"),
            Hospital = row.Text("HOSPITAL"),
            ResignDate = Day("RESIGN"),
            Memo = row.Text("MEM"),
            Evaluation1Date = Day("DATE_1"),
            Evaluation1Score = (int?)row.Number("POINT_1"),
            Evaluation1Grade = row.Text("GRED_1"),
            Evaluation2Date = Day("DATE_2"),
            Evaluation2Score = (int?)row.Number("POINT_2"),
            Evaluation2Grade = row.Text("GRED_2"),
            ProbationPassedDate = Day("DATE_3"),
        };
    }

    /// <summary>DQ-3: <c>Mrs</c> and <c>Mrs.</c> both occur — normalized to the T-TITLE spelling.</summary>
    private string? EnglishTitle(string? title, string code)
    {
        if (title is null || EmployeeLookups.EnglishTitles.Contains(title)) return title;
        var listed = EmployeeLookups.EnglishTitles.FirstOrDefault(t =>
            string.Equals(t.TrimEnd('.'), title.TrimEnd('.'), StringComparison.OrdinalIgnoreCase));
        if (listed is null) return title;
        report.Add("EMPLO", code, "DQ-3", $"NAME TITLE '{title}'", $"โหลดโดยแก้เป็น {listed}");
        return listed;
    }

    // ---------- หัวหน้ากะ / หัวหน้าแผนก: T-แผนก.SUPER is a printed name, linked to the employee it names ----------

    private async Task LinkSupervisorsAsync()
    {
        var byName = loadedEmployees.ToLookup(e => PersonName(e.Name));
        var linked = new Dictionary<LoadedDepartmentShift, int>();
        foreach (var shift in departmentShifts.Where(s => s.SupervisorName is not null))
        {
            var matches = byName[PersonName(shift.SupervisorName!)].ToList();
            if (matches.Count > 1 && matches.Count(m => m.Working) == 1) matches = matches.Where(m => m.Working).ToList();
            if (matches.Count != 1)
            {
                report.Add("แผนก", shift.Key, Other,
                    $"ชื่อหัวหน้า (SUPER) '{shift.SupervisorName}' ตรงกับพนักงาน {matches.Count} คน — หัวหน้ากะยังไม่ผูกกับพนักงาน (ชื่อยังพิมพ์ในเอกสารได้)",
                    MigrationReport.LoadedBlank);
                continue;
            }
            var department = (await orgDirectory.GetDepartmentAsync(shift.DepartmentId))!;
            var rowVersion = department.Shifts.Single(h => h.ShiftId == shift.ShiftId).RowVersion;
            if ((await org.SetShiftSupervisorAsync(shift.DepartmentId, shift.ShiftId, matches[0].Id, rowVersion)).Succeeded)
                linked[shift] = matches[0].Id;
        }

        // หัวหน้าแผนก = the one หัวหน้ากะ of every shift of the department (the legacy file has no separate head).
        foreach (var group in departmentShifts.GroupBy(s => s.DepartmentId))
        {
            var heads = group.Select(s => linked.TryGetValue(s, out var id) ? id : (int?)null).Distinct().ToList();
            if (heads is not [{ } head]) continue;
            var d = (await orgDirectory.GetDepartmentAsync(group.Key))!;
            await org.SaveDepartmentAsync(new DepartmentInput(d.Id, d.Code, d.Name, d.EmployeeIdPrefix, d.SortOrder, d.SectionId, head,
                d.Shifts.Select(h => new DepartmentShiftInput(h.ShiftId, h.SupervisorName, h.ManagerName, h.FactoryManagerName)).ToList(),
                d.RowVersion, d.StaffingTarget));
        }
    }

    /// <summary>A Thai name without its title, spaces collapsed: EMPLO.NAME separates the parts with two spaces, SUPER often one.</summary>
    private static string PersonName(string name)
    {
        var collapsed = Spaces().Replace(name.Trim(), " ");
        foreach (var title in (string[])["นางสาว", "น.ส.", "นาง", "นาย", "Miss", "Mrs.", "Mr."])
            if (collapsed.StartsWith(title, StringComparison.Ordinal)) return collapsed[title.Length..].TrimStart();
        return collapsed;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    // ---------- วันหยุด (T-SUNDAY, T-HOLIDAY) ----------

    private Task LoadWeeklyHolidaysAsync() => LoadDaysAsync("SUNDAY", "hr.WeeklyHolidays", CalendarRegistry.DefaultWeeklyLabel,
        (date, item) => calendar.SaveWeeklyHolidayAsync(new WeeklyHolidayInput(null, date, item, null)));

    private Task LoadPublicHolidaysAsync() => LoadDaysAsync("HOLIDAY", "hr.PublicHolidays", null,
        (date, item) => calendar.SavePublicHolidayAsync(new PublicHolidayInput(null, date, item, null)));

    /// <summary>
    /// Rules checked on the real file 2026-10-18 (#47): DATE ว่าง, ITEM = DEL (marked for the yearly delete, BR-042) and a
    /// date entered twice with different ITEM are not loaded; a blank SUNDAY.ITEM is the legacy default "วันอาทิตย์".
    /// </summary>
    private async Task LoadDaysAsync(string table, string newTable, string? defaultItem, Func<DateOnly, string?, Task<ServiceResult>> save)
    {
        var tally = Count(table, newTable, legacy[table].Count);
        var candidates = new List<(DateOnly Date, string? Item)>();
        foreach (var row in legacy[table])
        {
            var item = row.Text("ITEM");
            if (row.Date("DATE") is not { } date) Reject(tally, table, "(DATE ว่าง)", Other, $"ไม่มีวันที่ (ITEM {item})");
            else if (item == DelFlag) Reject(tally, table, Iso(DateOnly.FromDateTime(date)), Other, "ITEM = DEL (ทำเครื่องหมายลบรายปี BR-042)");
            else candidates.Add((DateOnly.FromDateTime(date), item ?? defaultItem));
        }
        foreach (var group in candidates.GroupBy(c => c.Date))
        {
            if (!OneValue(tally, table, Iso(group.Key), "วันที่", group.ToList(), c => c.Item)) continue;
            var saved = await save(group.Key, group.First().Item);
            if (saved.Succeeded) tally.Loaded++;
            else Reject(tally, table, Iso(group.Key), Other, string.Join(" · ", saved.Errors));
        }
    }

    // ---------- รายงานตรวจยอด ----------

    private async Task CountBackAsync()
    {
        var departments = await orgDirectory.GetDepartmentsAsync();
        var employeeList = await employeeDirectory.SearchAsync();
        var days = await calendarDirectory.GetAsync(DateOnly.MinValue, DateOnly.MaxValue);
        var inDatabase = new Dictionary<string, int>
        {
            ["sales.Customers"] = (await customerDirectory.GetAllAsync()).Count,
            ["org.Shifts"] = (await orgDirectory.GetShiftsAsync()).Count,
            ["org.Sections"] = (await orgDirectory.GetSectionsAsync()).Count,
            ["org.Departments"] = departments.Count,
            ["org.DepartmentShifts"] = departments.Sum(d => d.Shifts.Count),
            ["org.Departments (อัตรากำลัง)"] = departments.Count(d => d.StaffingTarget is not null),
            ["hr.Employees"] = employeeList.Count,
            ["hr.Employees (ทำงานอยู่)"] = employeeList.Count(e => e.IsWorking),
            ["hr.WeeklyHolidays"] = days.WeeklyHolidays.Count,
            ["hr.PublicHolidays"] = days.PublicHolidays.Count,
        };
        report.Tables.AddRange(tallies.Select(t =>
            new TableCount(t.LegacyTable, t.NewTable, t.Source, t.Excluded, t.Rejected, t.Loaded, inDatabase[t.NewTable])));
    }

    // ---------- helpers ----------

    /// <summary>
    /// Rows that end up with one key: identical copies load once, the other copies go to the report; copies that differ
    /// are all left out (DQ-2: no primary key in Access), to be fixed in the file or the code map.
    /// </summary>
    private bool OneValue<T, TValue>(Tally tally, string table, string key, string what, List<T> rows, Func<T, TValue> value)
    {
        if (rows.Count == 1) return true;
        var same = rows.Select(value).Distinct().Count() == 1;
        foreach (var _ in rows.Skip(same ? 1 : 0))
            Reject(tally, table, key, Duplicate, same ? $"{what}ซ้ำ {rows.Count} แถว ค่าเหมือนกัน — โหลดแถวเดียว"
                : $"{what}ซ้ำ {rows.Count} แถว ค่าไม่ตรงกัน");
        return same;
    }

    private Tally Count(string legacyTable, string newTable, int source)
    {
        var tally = new Tally(legacyTable, newTable) { Source = source };
        tallies.Add(tally);
        return tally;
    }

    private void Reject(Tally tally, string table, string key, string rule, string problem, int rows = 1)
    {
        tally.Rejected += rows;
        report.Add(table, key, rule, problem, MigrationReport.NotLoaded);
    }

    private void RejectAll(Tally[] counts, string table, string key, string rule, string problem)
    {
        Reject(counts[0], table, key, rule, problem);
        foreach (var other in counts.Skip(1)) other.Rejected++;
    }

    private void UseCode(string kind, string legacyValue, string code, string? name, string? prefix, CodeMapEntry? entry)
    {
        if (report.CodeMapUsed.Any(u => u.Kind == kind && string.Equals(u.Legacy.Trim(), legacyValue.Trim(), StringComparison.OrdinalIgnoreCase))) return;
        report.CodeMapUsed.Add(new CodeMapUse(kind, legacyValue, code, name, prefix, entry is not null));
    }

    private static TimeOnly Time(DateTime legacy) => TimeOnly.FromDateTime(legacy);

    private static TimeOnly? Time(DateTime? legacy) => legacy is { } t ? TimeOnly.FromDateTime(t) : null;

    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private sealed class Tally(string legacyTable, string newTable)
    {
        public string LegacyTable { get; } = legacyTable;
        public string NewTable { get; } = newTable;
        public int Source { get; init; }
        public int Excluded { get; set; }
        public int Rejected { get; set; }
        public int Loaded { get; set; }
    }

    private sealed record DepartmentCandidate(
        string LegacyName, string Code, string Name, string Prefix, int SortOrder, string? SectionName, List<LegacyRow> Rows, bool FromMap);

    private sealed record LoadedDepartmentShift(int DepartmentId, int ShiftId, string Key, string? SupervisorName);
}
