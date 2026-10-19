using System.Globalization;
using Lockte.Hr;
using Lockte.SharedKernel.Persistence;

namespace Lockte.Migration;

/// <summary>
/// ย้ายข้อมูลประวัติ (#51, ข้อตัดสิน #17), after <see cref="MasterDataMigration"/> has deleted everything and loaded the
/// employees: เวลาทำงานตั้งแต่ 1 ม.ค. 2568 (E_WORK + ADD_EW into one table) and การลาปี 2569 (LEAVE + LEAVE_ADD into one
/// table). The rows are taken as the legacy kept them — no quota check, a blank TYPE allowed — and the repeats BR-007
/// removes before every calculation are loaded once (DQ-2). Nothing is written to the change history. Adds to the
/// report: the row counts, ยอดสรุปรายเดือนปี 2026 (AC-10) and the WORK_T check of every migrated row (AC-01). Then
/// งวดที่เปิดอยู่: the one row of ตั้งวันที่ตัดWEEK becomes a งวดค่าแรง at its first step, and ไม่มาทำงาน (NO_WORK) is
/// loaded from that period's first day on, so สร้างเวลาปกติ (BR-010) and RPT-17 work again on those days.
/// </summary>
public sealed class HistoryMigration(HrHistoryImport import, EmployeeDirectory employeeDirectory, PayPeriodRegistry payPeriods,
    PayPeriodDirectory payPeriodDirectory, AbsenceRegistry absences, AbsenceDirectory absenceDirectory, AuditScope audit)
{
    private const string Other = "NF-06";
    private const string BlankDate = "DQ-1";
    private const string Duplicate = "DQ-2";
    private const string Orphan = "DQ-6";
    private const string LateCut = "DQ-7";
    private const string BlankType = "DQ-12";
    private const string AbsenceDate = "DQ-10";
    private const string WorkTimeTable = "hr.WorkTimes";
    private const string LeaveTable = "hr.Leaves";
    private const string PayPeriodTable = "hr.PayPeriods";
    private const string AbsenceTable = "hr.Absences";
    /// <summary>The legacy's oldest data (ADD_EW goes back to 2016): a NO_WORK date before it is a typing slip (DQ-10).</summary>
    private static readonly DateOnly OldestLegacyDate = new(2016, 1, 1);
    private static readonly DateOnly MonthlyFrom = new(2026, 1, 1);
    private static readonly DateOnly MonthlyTo = new(2026, 12, 31);

    private LegacyDatabase legacy = null!;
    private MigrationReport report = null!;
    private Dictionary<string, int> employeeIds = null!;

    public async Task RunAsync(LegacyDatabase legacyDatabase, MigrationReport migrationReport)
    {
        if (report is not null) throw new InvalidOperationException("HistoryMigration runs once per scope.");
        (legacy, report) = (legacyDatabase, migrationReport);
        employeeIds = (await employeeDirectory.SearchAsync()).ToDictionary(e => e.Code, e => e.Id, StringComparer.OrdinalIgnoreCase);
        await LoadWorkTimesAsync();
        await LoadLeavesAsync();
        using (audit.SuppressRowChanges())
        {
            var openPeriod = await LoadOpenPeriodAsync();
            await LoadAbsencesAsync(openPeriod);
        }
    }

    // ---------- เวลาทำงาน (E_WORK + ADD_EW) ----------

    private async Task LoadWorkTimesAsync()
    {
        string[] tables = ["E_WORK", "ADD_EW"]; // the open period first: it wins over a copy moved back into the history
        var customers = legacy["ชื่อลูกค้า"].Select(r => r.Text("CODE")?.ToUpperInvariant()).OfType<string>().ToHashSet();
        var tally = new Tally(string.Join(" + ", tables), WorkTimeTable);
        var candidates = new List<WorkCandidate>();
        var read = new List<(DateOnly Date, double? Hours)>(); // every legacy row with a date, for ยอดต้นทาง
        var lateCut = 0;
        var otherMarks = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var table in tables)
        {
            var rows = legacy[table];
            tally.Source += legacy.SourceRows(table);
            tally.Excluded += legacy.SourceRows(table) - rows.Count; // before 1 ม.ค. 2568: stays in Access (#17)
            foreach (var row in rows)
            {
                if (row.Date("DATE") is not { } date)
                {
                    Reject(tally, table, $"(DATE ว่าง) {row.Text("ID")}", BlankDate, "ไม่มีวันที่");
                    continue;
                }
                var day = DateOnly.FromDateTime(date);
                var (workIn, workOut) = (row.Date("W_IN"), row.Date("W_OUT"));
                var rest = (int)(row.Number("REST_T") ?? 0);
                double? legacyHours = workIn is { } i && workOut is { } o ? (o.ToOADate() - i.ToOADate()) * 24 - rest / 60.0 : null;
                read.Add((day, legacyHours));
                if (EmployeeId(tally, table, row, Iso(day)) is not { } employee) continue;
                var type = row.Text("TYPE")?.ToUpperInvariant();
                if (type is not null && !WorkTypes.Codes.Contains(type))
                {
                    Reject(tally, table, $"{Iso(day)} {employee.Code}", Other, $"ประเภท '{type}' ไม่อยู่ใน T-ประเภทการทำงาน");
                    continue;
                }
                TimeOnly? late = null;
                if (table == "ADD_EW") { if (row.Text("LATE") is not null) lateCut++; }
                else late = Clock(row.Date("LATE"));
                var mark = row.Text("MARK")?.ToUpperInvariant();
                if (mark is not null && !customers.Contains(mark))
                {
                    otherMarks[$"{table} {mark}"] = otherMarks.GetValueOrDefault($"{table} {mark}") + 1;
                    mark = null;
                }
                var import = new WorkTimeImport(day, employee.Id, type, Clock(workIn), Clock(workOut), late, rest, mark);
                candidates.Add(new WorkCandidate(table, employee.Code, import, row.Number("WORK_T"), legacyHours));
            }
        }
        if (lateCut > 0)
            report.Add("ADD_EW", "LATE", LateCut, $"LATE ของประวัติเป็นข้อความ 2 ตัวอักษร ใช้เป็นเวลาไม่ได้ ({lateCut} แถว)", MigrationReport.LoadedBlank);
        foreach (var (key, count) in otherMarks)
            report.Add(key.Split(' ')[0], $"MARK {key.Split(' ')[1]}", Other,
                $"ไม่ใช่รหัสลูกค้างานรับจ้างใน T-ชื่อลูกค้า (flag ชั่วคราว BR-015/BR-042) — {count} แถว", MigrationReport.LoadedBlank);

        var loaded = new List<WorkCandidate>();
        // BR-007: one row per DATE & W_IN & TYPE & ID, the first as read (the legacy keeps one, which one is not fixed).
        foreach (var group in candidates.GroupBy(c => (c.Import.Date, c.Code, c.Import.Type, c.Import.WorkIn)))
        {
            var first = group.First();
            loaded.Add(first);
            foreach (var copy in group.Skip(1))
                Reject(tally, copy.Table, $"{Iso(copy.Import.Date)} {copy.Code} {copy.Import.Type} {copy.Import.WorkIn:HH\\:mm}", Duplicate,
                    copy.Import == first.Import && copy.LegacyWorkT == first.LegacyWorkT
                        ? "ซ้ำตามคีย์ BR-007 ค่าเหมือนกัน — โหลดแถวเดียว"
                        : $"ซ้ำตามคีย์ BR-007 ค่าไม่ตรงกัน — โหลดแถวแรก (จาก {first.Table})");
        }
        foreach (var row in loaded.Where(c => c.Import.Type is null))
            report.Add(row.Table, $"{Iso(row.Import.Date)} {row.Code}", BlankType, "TYPE ว่าง — ไม่ถูกนับในสูตรใด (BR-010 ใส่ W ให้ตอนสร้างเวลาปกติของวันนั้น)",
                MigrationReport.LoadedBlank);

        await import.AddWorkTimesAsync(loaded.Select(c => c.Import));
        tally.Loaded = loaded.Count;
        report.Tables.Add(tally.Count(await import.CountWorkTimesAsync()));
        CheckWorkT(loaded);

        var inDatabase = (await import.GetWorkTimeMonthsAsync(MonthlyFrom, MonthlyTo)).ToDictionary(m => (m.Year, m.Month));
        foreach (var month in Months(read.Select(r => r.Date).Concat(inDatabase.Keys.Select(k => new DateOnly(k.Year, k.Month, 1)))))
        {
            var source = read.Where(r => (r.Date.Year, r.Date.Month) == month).ToArray();
            var expected = loaded.Where(r => (r.Import.Date.Year, r.Import.Date.Month) == month).ToArray();
            var saved = inDatabase.GetValueOrDefault(month) ?? new WorkTimeMonth(month.Year, month.Month, 0, 0);
            Monthly(WorkTimeTable, month, "แถว", source.Length, expected.Length, saved.Rows);
            Monthly(WorkTimeTable, month, "ชั่วโมงทำงาน", source.Sum(r => r.Hours ?? 0), expected.Sum(r => r.LegacyHours ?? 0), saved.Hours);
        }
    }

    /// <summary>AC-01: WORK_T of BR-001 on every migrated row the legacy has calculated, against its WORK_T at 6 decimals.</summary>
    private void CheckWorkT(IEnumerable<WorkCandidate> loaded)
    {
        foreach (var row in loaded)
        {
            if (row.LegacyWorkT is not { } legacyWorkT || row.Import is not { Type: { } type, WorkIn: { } workIn, WorkOut: { } workOut }) continue;
            report.WorkTChecked++;
            var workT = WorkDuration.WorkT(type, workIn, workOut, row.Import.RestMinutes);
            if (!MigrationReport.Same(workT, legacyWorkT))
                report.WorkTMismatches.Add(new WorkTMismatch(row.Table, row.Code, row.Import.Date, type, workIn, workOut,
                    row.Import.RestMinutes, legacyWorkT, workT));
        }
    }

    // ---------- การลา (LEAVE + LEAVE_ADD) ----------

    private async Task LoadLeavesAsync()
    {
        string[] tables = ["LEAVE", "LEAVE_ADD"];
        var tally = new Tally(string.Join(" + ", tables), LeaveTable);
        var candidates = new List<LeaveCandidate>();
        var read = new List<(DateOnly From, double Days, bool Paid)>(); // every legacy row with FROM_D, for ยอดต้นทาง
        var defaultTimes = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var table in tables)
        {
            var rows = legacy[table];
            tally.Source += legacy.SourceRows(table);
            tally.Excluded += legacy.SourceRows(table) - rows.Count; // FROM_D before 2569: stays in Access (#17)
            foreach (var row in rows)
            {
                if (row.Date("FROM_D") is not { } fromDate)
                {
                    Reject(tally, table, $"(FROM_D ว่าง) {row.Text("ID")}", Other, "ไม่มีวันเริ่มลา");
                    continue;
                }
                var from = DateOnly.FromDateTime(fromDate);
                read.Add((from, row.Number("SUM_D") ?? 0, string.Equals(row.Text("SAL"), "Y", StringComparison.OrdinalIgnoreCase)));
                if (EmployeeId(tally, table, row, Iso(from)) is not { } employee) continue;
                var key = $"{Iso(from)} {employee.Code}";
                if (row.Date("TO_D") is not { } toDate)
                {
                    Reject(tally, table, key, Other, "ไม่มีวันสิ้นสุดลา (TO_D)");
                    continue;
                }
                var type = row.Text("SR");
                if (type is null)
                {
                    Reject(tally, table, key, BlankType, "ประเภทการลา (SR) ว่าง — ไม่ถูกนับในสูตรใด");
                    continue;
                }
                if (!LeaveTypes.Names.ContainsKey(type))
                {
                    Reject(tally, table, key, Other, $"ประเภทการลา '{type}' ไม่อยู่ใน T-ประเภทการลางาน");
                    continue;
                }
                if (row.Number("SUM_D") is not { } days)
                {
                    Reject(tally, table, key, Other, "ไม่มีจำนวนวันลา (SUM_D) — ระบบเดิมลบแถวนี้ตอนปิดฟอร์ม (BR-051)");
                    continue;
                }
                bool? paid = row.Text("SAL")?.ToUpperInvariant() switch { null => null, "Y" => true, "N" => false, _ => null };
                if (paid is null && row.Text("SAL") is { } sal)
                    report.Add(table, key, Other, $"SAL '{sal}' ไม่ใช่ Y หรือ N", MigrationReport.LoadedBlank);
                var recorded = row.Date("DATE") is { } date ? DateOnly.FromDateTime(date) : from;
                if (row.Date("DATE") is null) report.Add(table, key, Other, "ไม่มีวันที่บันทึก (DATE)", "โหลดโดยใช้วันเริ่มลาเป็นวันที่บันทึก");
                var (fromTime, toTime) = (Clock(row.Date("FROM_T")), Clock(row.Date("TO_T")));
                if (fromTime is null || toTime is null) defaultTimes[table] = defaultTimes.GetValueOrDefault(table) + 1;
                candidates.Add(new LeaveCandidate(table, employee.Code, new LeaveImport(employee.Id, recorded, from, DateOnly.FromDateTime(toDate),
                    fromTime ?? LeaveRegistry.DefaultFromTime, toTime ?? LeaveRegistry.DefaultToTime, days, type, paid, row.Text("REMARK"),
                    row.Number("SALA_DAY") ?? 0, row.Number("PAY") ?? 0)));
            }
        }
        foreach (var (table, count) in defaultTimes)
            report.Add(table, "FROM_T / TO_T", Other, $"เวลาเริ่ม/สิ้นสุดลาว่าง {count} แถว",
                $"โหลดโดยใช้ค่าเริ่มต้นของฟอร์ม {LeaveRegistry.DefaultFromTime:HH\\:mm}–{LeaveRegistry.DefaultToTime:HH\\:mm}");

        var loaded = new List<LeaveCandidate>();
        // BR-007: one row per FROM_D & TO_D & FROM_T & ID, the first as read.
        foreach (var group in candidates.GroupBy(c => (c.Code, c.Import.FromDate, c.Import.ToDate, c.Import.FromTime)))
        {
            var first = group.First();
            loaded.Add(first);
            foreach (var copy in group.Skip(1))
                Reject(tally, copy.Table, $"{Iso(copy.Import.FromDate)} {copy.Code}", Duplicate,
                    copy.Import == first.Import ? "ซ้ำตามคีย์ BR-007 ค่าเหมือนกัน — โหลดแถวเดียว"
                        : $"ซ้ำตามคีย์ BR-007 ค่าไม่ตรงกัน — โหลดแถวแรก (จาก {first.Table})");
        }

        await import.AddLeavesAsync(loaded.Select(c => c.Import));
        tally.Loaded = loaded.Count;
        report.Tables.Add(tally.Count(await import.CountLeavesAsync()));

        var inDatabase = (await import.GetLeaveMonthsAsync(MonthlyFrom, MonthlyTo)).ToDictionary(m => (m.Year, m.Month));
        foreach (var month in Months(read.Select(r => r.From).Concat(inDatabase.Keys.Select(k => new DateOnly(k.Year, k.Month, 1)))))
        {
            var source = read.Where(r => (r.From.Year, r.From.Month) == month).ToArray();
            var expected = loaded.Select(r => r.Import).Where(r => (r.FromDate.Year, r.FromDate.Month) == month).ToArray();
            var saved = inDatabase.GetValueOrDefault(month) ?? new LeaveMonth(month.Year, month.Month, 0, 0, 0);
            Monthly(LeaveTable, month, "แถว", source.Length, expected.Length, saved.Rows);
            Monthly(LeaveTable, month, "วันลา", source.Sum(r => r.Days), expected.Sum(r => r.Days), saved.Days);
            Monthly(LeaveTable, month, "วันลาได้ค่าแรง", source.Where(r => r.Paid).Sum(r => r.Days),
                expected.Where(r => r.Paid == true).Sum(r => r.Days), saved.PaidDays);
        }
    }

    // ---------- งวดที่เปิดอยู่ (ตั้งวันที่ตัดWEEK, NO_WORK) ----------

    /// <summary>
    /// The legacy keeps one row: the period last set on SCR-40. It is loaded at <see cref="PayPeriodStep.Set"/> (the legacy
    /// keeps no step), so the new system calculates it again. Returns the newest period loaded, or null when none could be.
    /// </summary>
    private async Task<PayPeriodDates?> LoadOpenPeriodAsync()
    {
        const string table = "ตั้งวันที่ตัดWEEK";
        var rows = legacy[table];
        var tally = new Tally(table, PayPeriodTable) { Source = rows.Count };
        PayPeriodDates? open = null;
        foreach (var row in rows)
        {
            var (from, to, payDate) = (Day(row.Date("FROM_DA")), Day(row.Date("TO_DA")), Day(row.Date("PAY_D")));
            if (row.Number("DAY") is { } days and not 0)
                report.Add(table, "DAY", Other, $"จำนวนวันจ่ายของงวด 2 ช่วง (BR-008) = {days} — งวดค่าแรงของระบบใหม่ไม่มีช่องนี้",
                    MigrationReport.LoadedBlank);
            var saved = await payPeriods.SaveAsync(new PayPeriodInput(null, from, to, payDate, null));
            if (!saved.Succeeded)
            {
                Reject(tally, table, from is { } f ? Iso(f) : "(FROM_DA ว่าง)", Other, string.Join(" · ", saved.Errors));
                continue;
            }
            tally.Loaded++;
            if (open is null || from > open.From) open = new PayPeriodDates(from!.Value, to!.Value, payDate!.Value);
        }
        report.Tables.Add(tally.Count((await payPeriodDirectory.ListAsync()).Count));
        return open;
    }

    /// <summary>
    /// ไม่มาทำงาน from the open period's first day on, with no end: days keyed ahead for the next period come too. Earlier
    /// days stay in Access (ไม่ย้ายตามกติกา). A blank or impossible date (DQ-10) is not loaded, nor a second row of the
    /// same employee on the same day (the new system records an absence once a day).
    /// </summary>
    private async Task LoadAbsencesAsync(PayPeriodDates? open)
    {
        const string table = "NO_WORK";
        var rows = legacy[table];
        var tally = new Tally(table, AbsenceTable) { Source = rows.Count };
        if (open is null)
        {
            tally.Excluded = rows.Count;
            if (rows.Count > 0)
                report.Add(table, $"{rows.Count} แถว", Other, "ไม่มีงวดที่เปิดอยู่ที่ย้ายได้ จึงไม่รู้ว่าจะย้ายไม่มาทำงานตั้งแต่วันไหน",
                    "ไม่ย้าย — บันทึกเองที่ SCR-10 หลังตั้งงวด");
            report.Tables.Add(tally.Count(0));
            return;
        }
        var latest = new DateOnly(open.To.Year + 1, 12, 31);
        var loaded = new HashSet<(DateOnly Day, int EmployeeId)>();
        foreach (var row in rows)
        {
            if (Day(row.Date("DATE1")) is not { } day)
            {
                Reject(tally, table, $"(DATE1 ว่าง) {row.Text("ID1")}", AbsenceDate, "ไม่มีวันที่");
                continue;
            }
            if (day < OldestLegacyDate || day > latest)
            {
                Reject(tally, table, $"{Iso(day)} {row.Text("ID1")}", AbsenceDate,
                    $"ปีผิด (คีย์ พ.ศ. ลงช่อง ค.ศ. หรือพิมพ์ผิด) — ไม่อยู่ระหว่างปี {OldestLegacyDate.Year} ถึง {latest.Year}");
                continue;
            }
            if (day < open.From)
            {
                tally.Excluded++; // before the open period: stays in Access
                continue;
            }
            if (EmployeeId(tally, table, row, Iso(day), "ID1") is not { } employee) continue;
            var key = $"{Iso(day)} {employee.Code}";
            if (!loaded.Add((day, employee.Id)))
            {
                Reject(tally, table, key, Duplicate, "บันทึกไม่มาทำงานซ้ำในวันเดียวกัน — โหลดแถวแรก");
                continue;
            }
            var saved = await absences.SaveAsync(new AbsenceInput(null, day, employee.Id, Clock(row.Date("W_IN")), row.Text("REMARK"), null));
            if (saved.Succeeded) tally.Loaded++;
            else
            {
                loaded.Remove((day, employee.Id));
                Reject(tally, table, key, Other, string.Join(" · ", saved.Errors));
            }
        }
        report.Tables.Add(tally.Count(await CountAbsencesAsync(open.From, loaded.Select(l => l.Day).DefaultIfEmpty(open.From).Max())));
    }

    /// <summary>
    /// ไม่มาทำงาน in the new table through its public read side, day by day from <paramref name="from"/> to
    /// <paramref name="to"/>: the run deleted the table first, so the days loaded hold all its rows.
    /// </summary>
    private async Task<int> CountAbsencesAsync(DateOnly from, DateOnly to)
    {
        var count = 0;
        for (var day = from; day <= to; day = day.AddDays(1))
            count += (await absenceDirectory.ListAsync(day)).Count;
        return count;
    }

    // ---------- helpers ----------

    /// <summary>The employee of the row's ID; a blank ID (BR-051) or one not in EMPLO (DQ-6, Q-15) is not loaded.</summary>
    private (string Code, int Id)? EmployeeId(Tally tally, string table, LegacyRow row, string day, string column = "ID")
    {
        if (EmployeeFormRules.NormalizeCode(row.Text(column)) is not { } code)
        {
            Reject(tally, table, $"{day} (ID ว่าง)", Other, "ไม่มีรหัสพนักงาน (ระบบเดิมลบแถวนี้ BR-051)");
            return null;
        }
        if (employeeIds.TryGetValue(code, out var id)) return (code, id);
        Reject(tally, table, $"{day} {code}", Orphan, "ไม่พบรหัสพนักงานนี้ในทะเบียนพนักงานที่ย้ายแล้ว (แถวกำพร้า)");
        return null;
    }

    /// <summary>The months of 2026 that either side has, in order.</summary>
    private static IEnumerable<(int Year, int Month)> Months(IEnumerable<DateOnly> dates)
        => dates.Where(d => d >= MonthlyFrom && d <= MonthlyTo).Select(d => (d.Year, d.Month)).Distinct().Order();

    private void Monthly(string table, (int Year, int Month) month, string measure, double source, double expected, double inDatabase)
        => report.MonthlyTotals.Add(new MonthlyTotal(table, $"{month.Year:0000}-{month.Month:00}", measure, source, expected, inDatabase));

    private void Reject(Tally tally, string table, string key, string rule, string problem)
    {
        tally.Rejected++;
        report.Add(table, key, rule, problem, MigrationReport.NotLoaded);
    }

    /// <summary>
    /// A legacy time (1899-12-30 + time; 1899-12-31 = the next day) to the nearest second: Access keeps it as a fraction
    /// of a day, so 08:00 may come back as 07:59:59.9999.
    /// </summary>
    private static TimeOnly? Clock(DateTime? legacyTime)
        => legacyTime is { } t ? new TimeOnly(TimeSpan.FromSeconds(Math.Round(t.TimeOfDay.TotalSeconds) % 86400).Ticks) : null;

    private static DateOnly? Day(DateTime? legacyDate) => legacyDate is { } d ? DateOnly.FromDateTime(d) : null;

    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private sealed class Tally(string legacyTable, string newTable)
    {
        public int Source { get; set; }
        public int Excluded { get; set; }
        public int Rejected { get; set; }
        public int Loaded { get; set; }

        public TableCount Count(int inDatabase) => new(legacyTable, newTable, Source, Excluded, Rejected, Loaded, inDatabase);
    }

    /// <summary>
    /// <see cref="LegacyHours"/> = ชั่วโมงทำงาน computed on the legacy values themselves (days since 1899-12-30 × 24 −
    /// REST_T / 60), so the monthly total checks the times as converted.
    /// </summary>
    private sealed record WorkCandidate(string Table, string Code, WorkTimeImport Import, double? LegacyWorkT, double? LegacyHours);

    private sealed record LeaveCandidate(string Table, string Code, LeaveImport Import);
}
