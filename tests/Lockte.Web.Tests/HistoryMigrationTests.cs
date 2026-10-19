using System.Runtime.Versioning;
using Lockte.Hr;
using Lockte.Migration;
using Lockte.Web.Audit;

namespace Lockte.Web.Tests;

/// <summary>
/// เครื่องมือย้ายข้อมูล: ประวัติเวลา / การลา (#51) — E_WORK + ADD_EW from 1 ม.ค. 2568 into เวลาทำงาน and LEAVE + LEAVE_ADD of
/// 2569 into การลา, from a fake <c>พนักงาน.accdb</c>, read back through the module's public services; plus the row counts,
/// ยอดสรุปรายเดือนปี 2026 (AC-10) and the WORK_T check (AC-01).
/// </summary>
[Collection(MigrationDbCollection.Name)]
[SupportedOSPlatform("windows")]
public sealed class HistoryMigrationTests(MigrationDbFixture db) : IDisposable
{
    private static readonly DateTime Mar3 = new(2025, 3, 3);
    private static readonly DateTime Oct12 = new(2026, 10, 12);
    private static readonly DateTime Oct13 = new(2026, 10, 13);

    private readonly DirectoryInfo work = Directory.CreateTempSubdirectory("lockte-history-test-");

    public void Dispose() => work.Delete(recursive: true);

    private static DateTime T(int hour, int minute, bool nextDay = false) => FakeLegacyFile.Time(hour, minute, nextDay);

    /// <summary>
    /// The standard company with: ADD_EW one row of 2567 (stays in Access), a กะ A day with LATE cut to text (DQ-7), a กะ B night
    /// and an OT of the last period; E_WORK one day of the open period (no WORK_T yet) late at 08:15 for customer Y; leave of
    /// 2569 in LEAVE and LEAVE_ADD and one of 2568 that stays.
    /// </summary>
    private static FakeLegacyFile WithHistory() => FakeLegacyFile.Standard()
        .Row("ADD_EW", ("DATE", new DateTime(2024, 12, 31)), ("ID", "D001"), ("W_IN", T(8, 0)), ("W_OUT", T(17, 0)), ("TYPE", "W"),
            ("REST_T", 60), ("WORK_T", 1.0))
        .Row("ADD_EW", ("DATE", Mar3), ("ID", "D001"), ("W_IN", T(8, 0)), ("W_OUT", T(17, 0)), ("TYPE", "W"), ("REST_T", 60),
            ("WORK_T", 1.0), ("LATE", "08"))
        .Row("ADD_EW", ("DATE", new DateTime(2025, 6, 2)), ("ID", "d002"), ("W_IN", T(20, 10)), ("W_OUT", T(5, 10, nextDay: true)),
            ("TYPE", "W"), ("REST_T", 60), ("WORK_T", 1.0))
        .Row("ADD_EW", ("DATE", Oct12), ("ID", "D001"), ("W_IN", T(17, 20)), ("W_OUT", T(20, 10)), ("TYPE", "O1"), ("REST_T", 0),
            ("WORK_T", 170 / 60.0))
        .Row("E_WORK", ("DATE", Oct13), ("ID", "D001"), ("W_IN", T(8, 0)), ("W_OUT", T(17, 0)), ("TYPE", "W"), ("REST_T", 60),
            ("LATE", T(8, 15)), ("MARK", "y"))
        .Row("LEAVE", ("DATE", new DateTime(2026, 3, 1, 8, 30, 0)), ("ID", "O001"), ("FROM_D", new DateTime(2026, 3, 2)),
            ("TO_D", new DateTime(2026, 3, 2)), ("FROM_T", T(8, 0)), ("TO_T", T(17, 0)), ("SUM_D", 1f), ("SR", "1"), ("SAL", "Y"),
            ("REMARK", "ธุระสมมุติ"), ("SALA_DAY", 520f), ("PAY", 520f), ("CODE_A", "x"))
        .Row("LEAVE_ADD", ("DATE", new DateTime(2026, 1, 5)), ("ID", "O001"), ("FROM_D", new DateTime(2026, 1, 5)),
            ("TO_D", new DateTime(2026, 1, 5)), ("SUM_D", 0.375f), ("SR", "3"), ("SAL", "N"), ("SALA_DAY", 10.4f))
        .Row("LEAVE_ADD", ("DATE", new DateTime(2025, 12, 29)), ("ID", "O001"), ("FROM_D", new DateTime(2025, 12, 29)),
            ("TO_D", new DateTime(2025, 12, 29)), ("SUM_D", 1f), ("SR", "1"), ("SAL", "Y"));

    private Task<MigrationReport> RunAsync(FakeLegacyFile file, string output = "out")
    {
        var source = Path.Combine(work.FullName, output + ".accdb");
        if (!File.Exists(source)) file.Save(source);
        return MigrationTool.RunAsync(db.Services, source, null, Path.Combine(work.FullName, output));
    }

    private async Task<T> ReadAsync<T>(Func<TestScope, Task<T>> read)
    {
        await using var scope = db.Scope("hr1");
        return await read(scope);
    }

    private Task<int> EmployeeIdAsync(string code) => ReadAsync(async s => (await s.Get<EmployeeDirectory>().FindByCodeAsync(code))!.Id);

    private Task<IReadOnlyList<WorkTimeDto>> WorkTimesAsync(int employeeId)
        => ReadAsync(s => s.Get<WorkTimeDirectory>().ListOfEmployeeAsync(employeeId, new DateOnly(2000, 1, 1), new DateOnly(2100, 1, 1)));

    [Fact]
    public async Task Loads_work_time_from_2568_and_leave_of_2569_into_one_table_each()
    {
        var historyBefore = await ReadAsync(async s => (await s.Get<HistoryService>().SearchAsync(new HistoryFilter(Max: 100_000))).Count);

        var report = await RunAsync(WithHistory());

        var d001 = await WorkTimesAsync(await EmployeeIdAsync("D001"));
        Assert.Equal(
            [
                (new DateOnly(2025, 3, 3), "W", new TimeOnly(8, 0), new TimeOnly(17, 0), 60, (TimeOnly?)null, (string?)null),
                (new DateOnly(2026, 10, 12), "O1", new TimeOnly(17, 20), new TimeOnly(20, 10), 0, null, null),
                (new DateOnly(2026, 10, 13), "W", new TimeOnly(8, 0), new TimeOnly(17, 0), 60, new TimeOnly(8, 15), "Y"),
            ],
            d001.Select(w => (w.Date, w.Type!, w.WorkIn!.Value, w.WorkOut!.Value, w.RestMinutes, w.Late, w.Mark)));
        var night = Assert.Single(await WorkTimesAsync(await EmployeeIdAsync("D002"))); // resigned later: history kept
        Assert.True(night.CrossesMidnight);
        Assert.Equal(1.0, night.WorkT);

        var o001 = await EmployeeIdAsync("O001");
        var leaves = await ReadAsync(s => s.Get<LeaveDirectory>().ListOfEmployeeAsync(o001, 2026));
        Assert.Equal(
            [
                (new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 5), new TimeOnly(8, 0), new TimeOnly(17, 0),
                    0.375, "3", (bool?)false, (string?)null, 10.4, 0.0),
                (new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 2), new DateOnly(2026, 3, 2), new TimeOnly(8, 0), new TimeOnly(17, 0),
                    1.0, "1", true, "ธุระสมมุติ", 520.0, 520.0),
            ],
            leaves.Select(l => (l.RecordedOn, l.FromDate, l.ToDate, l.FromTime, l.ToTime, l.Days, l.Type, l.Paid, l.Remark, l.DailyWage, l.Pay)));
        Assert.Empty(await ReadAsync(s => s.Get<LeaveDirectory>().ListOfEmployeeAsync(o001, 2025)));

        // รายงานตรวจยอด: rows before the first date stay in Access (ไม่ย้ายตามกติกา), the rest are in the new table
        Assert.True(report.Balanced, string.Join("\n", report.Tables.Where(t => !t.Balanced).Cast<object>().Concat(report.MonthlyTotals.Where(m => !m.Balanced))));
        Assert.Contains(report.Tables, t => t is { LegacyTable: "E_WORK + ADD_EW", NewTable: "hr.WorkTimes", SourceRows: 5, Excluded: 1, Rejected: 0, Loaded: 4, InDatabase: 4 });
        Assert.Contains(report.Tables, t => t is { LegacyTable: "LEAVE + LEAVE_ADD", NewTable: "hr.Leaves", SourceRows: 3, Excluded: 1, Rejected: 0, Loaded: 2, InDatabase: 2 });
        Assert.Contains(report.Exceptions, e => e is { Table: "ADD_EW", Key: "LATE", Rule: "DQ-7" });
        Assert.Contains(report.Exceptions, e => e is { Table: "LEAVE_ADD", Key: "FROM_T / TO_T" });

        // AC-10: ยอดสรุปรายเดือนปี 2026 (only 2026 is summed)
        Assert.Equal(["2026-01", "2026-03", "2026-10"], report.MonthlyTotals.Select(m => m.Month).Distinct().Order());
        var octoberHours = report.MonthlyTotals.Single(m => m is { Table: "hr.WorkTimes", Month: "2026-10", Measure: "ชั่วโมงทำงาน" });
        Assert.Equal(170 / 60.0 + 8, octoberHours.InDatabase, 9);
        Assert.Contains(report.MonthlyTotals, m => m is { Table: "hr.Leaves", Month: "2026-03", Measure: "วันลาได้ค่าแรง", Source: 1, Expected: 1, InDatabase: 1 });

        // AC-01: every row the legacy has calculated (not the open period yet) has the same WORK_T
        Assert.Equal(3, report.WorkTChecked);
        Assert.True(report.WorkTMatches);

        Assert.Equal(historyBefore, await ReadAsync(async s => (await s.Get<HistoryService>().SearchAsync(new HistoryFilter(Max: 100_000))).Count));
        foreach (var file in new[] { MigrationReport.MonthlyFile, MigrationReport.WorkTFile })
            Assert.True(File.Exists(Path.Combine(work.FullName, "out", file)), file);
    }

    [Fact]
    public async Task Each_run_deletes_the_history_and_loads_it_again()
    {
        var legacy = WithHistory();
        var first = await RunAsync(legacy, "first");
        var d001 = await EmployeeIdAsync("D001");
        await using (var scope = db.Scope("hr1")) // keyed in between the rehearsals: gone after the next run
        {
            Assert.True((await scope.Get<WorkTimeRegistry>().SaveAsync(new WorkTimeInput(null, new DateOnly(2026, 10, 14), d001, "W",
                new TimeOnly(8, 0), new TimeOnly(17, 0), 60, null, null, null))).Succeeded);
            Assert.True((await scope.Get<AbsenceRegistry>().SaveAsync(new AbsenceInput(null, new DateOnly(2026, 10, 15), d001, null, null, null))).Succeeded);
        }

        var second = await RunAsync(legacy, "first"); // the same file again

        Assert.Equal(3, (await WorkTimesAsync(await EmployeeIdAsync("D001"))).Count);
        Assert.Equal(first.Tables, second.Tables);
        Assert.Equal(first.MonthlyTotals, second.MonthlyTotals);
    }

    [Fact]
    public async Task Rows_that_cannot_be_loaded_as_they_are_go_to_the_DQ_report()
    {
        var legacy = FakeLegacyFile.Standard()
            .Row("E_WORK", ("ID", "D001"), ("TYPE", "W"))                                                    // DQ-1 no date
            .Row("E_WORK", ("DATE", Oct13), ("TYPE", "W"))                                                   // no ID (BR-051)
            .Row("E_WORK", ("DATE", Oct13), ("ID", "X999"), ("TYPE", "W"))                                   // DQ-6 orphan
            .Row("E_WORK", ("DATE", Oct13), ("ID", "D001"), ("TYPE", "ZZ"), ("W_IN", T(8, 0)))                // unknown type
            .Row("E_WORK", ("DATE", Oct13), ("ID", "F001"), ("W_IN", T(8, 0)), ("W_OUT", T(17, 0)), ("REST_T", 60)) // DQ-12 loaded
            .Row("E_WORK", ("DATE", Oct13), ("ID", "D001"), ("TYPE", "W"), ("W_IN", T(8, 0)), ("W_OUT", T(17, 0)), ("REST_T", 60), ("MARK", "DE"))
            .Row("ADD_EW", ("DATE", Oct13), ("ID", "D001"), ("TYPE", "W"), ("W_IN", T(8, 0)), ("W_OUT", T(16, 0)), ("REST_T", 60)) // DQ-2 differs
            .Row("ADD_EW", ("DATE", Oct12), ("ID", "D001"), ("TYPE", "W"), ("W_IN", T(8, 0)), ("W_OUT", T(17, 0)), ("REST_T", 60), ("WORK_T", 0.5))
            .Row("ADD_EW", ("DATE", Oct12), ("ID", "D001"), ("TYPE", "W"), ("W_IN", T(8, 0)), ("W_OUT", T(17, 0)), ("REST_T", 60), ("WORK_T", 0.5))
            .Row("LEAVE", ("ID", "O001"), ("FROM_D", new DateTime(2026, 2, 2)), ("TO_D", new DateTime(2026, 2, 2)), ("SUM_D", 1f))       // DQ-12 SR blank
            .Row("LEAVE", ("ID", "O001"), ("FROM_D", new DateTime(2026, 2, 3)), ("TO_D", new DateTime(2026, 2, 3)), ("SUM_D", 1f), ("SR", "99"))
            .Row("LEAVE", ("ID", "O001"), ("FROM_D", new DateTime(2026, 2, 4)), ("TO_D", new DateTime(2026, 2, 4)), ("SR", "1"))        // no SUM_D
            .Row("LEAVE", ("ID", "O001"), ("FROM_D", new DateTime(2026, 2, 5)), ("SUM_D", 1f), ("SR", "1"))                           // no TO_D
            .Row("LEAVE", ("ID", "X999"), ("FROM_D", new DateTime(2026, 2, 6)), ("TO_D", new DateTime(2026, 2, 6)), ("SUM_D", 1f), ("SR", "1"))
            .Row("LEAVE", ("DATE", new DateTime(2026, 2, 9)), ("ID", "O001"), ("FROM_D", new DateTime(2026, 2, 9)), ("TO_D", new DateTime(2026, 2, 9)),
                ("FROM_T", T(8, 0)), ("TO_T", T(17, 0)), ("SUM_D", 1f), ("SR", "1"), ("SAL", "Y"))
            .Row("LEAVE_ADD", ("DATE", new DateTime(2026, 2, 9)), ("ID", "O001"), ("FROM_D", new DateTime(2026, 2, 9)), ("TO_D", new DateTime(2026, 2, 9)),
                ("FROM_T", T(8, 0)), ("TO_T", T(17, 0)), ("SUM_D", 1f), ("SR", "1"), ("SAL", "Y"));

        var report = await RunAsync(legacy);

        void Has(string table, string key, string rule, string action)
            => Assert.Contains(report.Exceptions, e => e.Table == table && e.Key == key && e.Rule == rule && e.Action.StartsWith(action));
        Has("E_WORK", "(DATE ว่าง) D001", "DQ-1", MigrationReport.NotLoaded);
        Has("E_WORK", "2026-10-13 (ID ว่าง)", "NF-06", MigrationReport.NotLoaded);
        Has("E_WORK", "2026-10-13 X999", "DQ-6", MigrationReport.NotLoaded);
        Has("E_WORK", "2026-10-13 D001", "NF-06", MigrationReport.NotLoaded); // TYPE ZZ
        Has("E_WORK", "2026-10-13 F001", "DQ-12", MigrationReport.LoadedBlank);
        Has("E_WORK", "MARK DE", "NF-06", MigrationReport.LoadedBlank);
        Has("ADD_EW", "2026-10-13 D001 W 08:00", "DQ-2", MigrationReport.NotLoaded);
        Has("ADD_EW", "2026-10-12 D001 W 08:00", "DQ-2", MigrationReport.NotLoaded);
        Has("LEAVE", "2026-02-02 O001", "DQ-12", MigrationReport.NotLoaded);
        Has("LEAVE", "2026-02-03 O001", "NF-06", MigrationReport.NotLoaded);
        Has("LEAVE", "2026-02-04 O001", "NF-06", MigrationReport.NotLoaded);
        Has("LEAVE", "2026-02-05 O001", "NF-06", MigrationReport.NotLoaded);
        Has("LEAVE", "2026-02-06 X999", "DQ-6", MigrationReport.NotLoaded);
        Has("LEAVE_ADD", "2026-02-09 O001", "DQ-2", MigrationReport.NotLoaded);

        // E_WORK wins over the copy in ADD_EW; MARK DE is a flag, not a customer
        var d001 = await WorkTimesAsync(await EmployeeIdAsync("D001"));
        Assert.Equal([(new DateOnly(2026, 10, 12), new TimeOnly(17, 0), (string?)null), (new DateOnly(2026, 10, 13), new TimeOnly(17, 0), null)],
            d001.Select(w => (w.Date, w.WorkOut!.Value, w.Mark)));
        Assert.Null(Assert.Single(await WorkTimesAsync(await EmployeeIdAsync("F001"))).Type);
        var o001 = await EmployeeIdAsync("O001");
        Assert.Single(await ReadAsync(s => s.Get<LeaveDirectory>().ListOfEmployeeAsync(o001, 2026)));

        Assert.True(report.Balanced, string.Join("\n", report.Tables.Where(t => !t.Balanced).Cast<object>().Concat(report.MonthlyTotals.Where(m => !m.Balanced))));
        Assert.Contains(report.Tables, t => t is { NewTable: "hr.WorkTimes", SourceRows: 9, Rejected: 6, Loaded: 3 });
        Assert.Contains(report.Tables, t => t is { NewTable: "hr.Leaves", SourceRows: 7, Rejected: 6, Loaded: 1 });
        // Source − Expected is what the DQ report explains
        Assert.Contains(report.MonthlyTotals, m => m is { Table: "hr.Leaves", Month: "2026-02", Measure: "แถว", Source: 7, Expected: 1, InDatabase: 1 });

        // AC-01: a legacy WORK_T that BR-001 does not give is listed
        var mismatch = Assert.Single(report.WorkTMismatches);
        Assert.Equal(("ADD_EW", "D001", new DateOnly(2026, 10, 12), 0.5, 1.0), (mismatch.Table, mismatch.EmployeeCode, mismatch.Date, mismatch.Legacy, mismatch.New));
        Assert.False(report.WorkTMatches);
        Assert.Contains("D001", File.ReadAllText(Path.Combine(work.FullName, "out", MigrationReport.WorkTFile)));
    }
}
