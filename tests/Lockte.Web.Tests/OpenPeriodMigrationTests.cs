using System.Runtime.Versioning;
using Lockte.Hr;
using Lockte.Migration;
using Lockte.Web.Audit;

namespace Lockte.Web.Tests;

/// <summary>
/// เครื่องมือย้ายข้อมูล: งวดที่เปิดอยู่ (#51) — the one row of ตั้งวันที่ตัดWEEK into งวดค่าแรง (#52) and NO_WORK from the
/// period's first day on into ไม่มาทำงาน, from a fake <c>พนักงาน.accdb</c>, read back through the module's public services.
/// </summary>
[Collection(MigrationDbCollection.Name)]
[SupportedOSPlatform("windows")]
public sealed class OpenPeriodMigrationTests(MigrationDbFixture db) : IDisposable
{
    private static readonly DateOnly Oct12 = new(2026, 10, 12);
    private static readonly DateOnly Oct27 = new(2026, 10, 27);

    private readonly DirectoryInfo work = Directory.CreateTempSubdirectory("lockte-open-period-test-");

    public void Dispose() => work.Delete(recursive: true);

    private static DateTime Day(DateOnly date) => date.ToDateTime(TimeOnly.MinValue);

    /// <summary>
    /// The standard company (open period 12–26 ต.ค. 2569) with NO_WORK: a day of the period before (stays in Access), the first
    /// day of the open period with a remark, and a day after it already keyed for the next period.
    /// </summary>
    private static FakeLegacyFile WithAbsences() => FakeLegacyFile.Standard()
        .Row("NO_WORK", ("DATE1", new DateTime(2026, 10, 9)), ("ID1", "D001"), ("W_IN", FakeLegacyFile.Time(8, 0)), ("TYPE", "A"))
        .Row("NO_WORK", ("DATE1", Day(Oct12)), ("ID1", "d001"), ("W_IN", FakeLegacyFile.Time(8, 0)), ("W_OUT", FakeLegacyFile.Time(17, 0)),
            ("WORK_T", 1.0), ("TYPE", "A"), ("REMARK", "ป่วยสมมุติ"))
        .Row("NO_WORK", ("DATE1", Day(Oct27)), ("ID1", "F001"), ("W_IN", FakeLegacyFile.Time(8, 0)), ("TYPE", "A"));

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

    private Task<IReadOnlyList<AbsenceDto>> AbsencesAsync(DateOnly date) => ReadAsync(s => s.Get<AbsenceDirectory>().ListAsync(date));

    private Task<int> HistoryCountAsync()
        => ReadAsync(async s => (await s.Get<HistoryService>().SearchAsync(new HistoryFilter(Max: 100_000))).Count);

    [Fact]
    public async Task Loads_the_open_period_and_the_absences_from_its_first_day_on()
    {
        var historyBefore = await HistoryCountAsync();

        var report = await RunAsync(WithAbsences());

        var period = Assert.Single(await ReadAsync(s => s.Get<PayPeriodDirectory>().ListAsync()));
        Assert.Equal((Oct12, new DateOnly(2026, 10, 26), new DateOnly(2026, 11, 1), PayPeriodStep.Set),
            (period.From, period.To, period.PayDate, period.CompletedStep)); // calculated again in the new system

        var first = Assert.Single(await AbsencesAsync(Oct12));
        Assert.Equal(("D001", new TimeOnly(8, 0), "ป่วยสมมุติ"), (first.EmployeeCode, first.WorkIn, first.Remark));
        Assert.Equal("F001", Assert.Single(await AbsencesAsync(Oct27)).EmployeeCode); // keyed ahead for the next period
        Assert.Empty(await AbsencesAsync(new DateOnly(2026, 10, 9)));

        Assert.True(report.Balanced, string.Join("\n", report.Tables.Where(t => !t.Balanced)));
        Assert.Contains(report.Tables, t => t is { LegacyTable: "ตั้งวันที่ตัดWEEK", NewTable: "hr.PayPeriods", SourceRows: 1, Excluded: 0, Rejected: 0, Loaded: 1, InDatabase: 1 });
        Assert.Contains(report.Tables, t => t is { LegacyTable: "NO_WORK", NewTable: "hr.Absences", SourceRows: 3, Excluded: 1, Rejected: 0, Loaded: 2, InDatabase: 2 });
        var counts = File.ReadAllText(Path.Combine(work.FullName, "out", MigrationReport.CountsFile));
        Assert.Contains("hr.PayPeriods", counts);
        Assert.Contains("hr.Absences", counts);

        Assert.Equal(historyBefore, await HistoryCountAsync());
    }

    [Fact]
    public async Task Each_run_loads_the_legacy_period_again()
    {
        var legacy = WithAbsences();
        var first = await RunAsync(legacy, "first");
        await using (var scope = db.Scope("hr1")) // HR sets the next period between the rehearsals: gone after the next run
        {
            Assert.True((await scope.Get<PayPeriodRegistry>().SaveAsync(
                new PayPeriodInput(null, new DateOnly(2026, 10, 27), new DateOnly(2026, 11, 11), new DateOnly(2026, 11, 16), null))).Succeeded);
        }

        var second = await RunAsync(legacy, "first");

        Assert.Equal(Oct12, Assert.Single(await ReadAsync(s => s.Get<PayPeriodDirectory>().ListAsync())).From);
        Assert.Single(await AbsencesAsync(Oct12));
        Assert.Equal(first.Tables, second.Tables);
    }

    [Fact]
    public async Task Absences_that_cannot_be_loaded_as_they_are_go_to_the_DQ_report()
    {
        var legacy = FakeLegacyFile.Standard()
            .Row("NO_WORK", ("ID1", "D001"), ("TYPE", "A"))                                          // DQ-10 no date
            .Row("NO_WORK", ("DATE1", new DateTime(2569, 10, 13)), ("ID1", "D001"))                  // DQ-10 พ.ศ. typed as ค.ศ.
            .Row("NO_WORK", ("DATE1", new DateTime(1982, 10, 13)), ("ID1", "D001"))                  // DQ-10 impossible year
            .Row("NO_WORK", ("DATE1", new DateTime(2026, 10, 13)))                                   // no ID (BR-011)
            .Row("NO_WORK", ("DATE1", new DateTime(2026, 10, 13)), ("ID1", "X999"))                  // DQ-6 orphan
            .Row("NO_WORK", ("DATE1", new DateTime(2026, 10, 14)), ("ID1", "D001"), ("REMARK", "แรก"))
            .Row("NO_WORK", ("DATE1", new DateTime(2026, 10, 14)), ("ID1", "D001"), ("REMARK", "ซ้ำ")); // DQ-2: once a day
        legacy.Tables["ตั้งวันที่ตัดWEEK"][0]["DAY"] = 5; // BR-008 งวด 2 ช่วง: no such field in the new system

        var report = await RunAsync(legacy);

        void Has(string table, string key, string rule, string action)
            => Assert.Contains(report.Exceptions, e => e.Table == table && e.Key == key && e.Rule == rule && e.Action.StartsWith(action));
        Has("NO_WORK", "(DATE1 ว่าง) D001", "DQ-10", MigrationReport.NotLoaded);
        Has("NO_WORK", "2569-10-13 D001", "DQ-10", MigrationReport.NotLoaded);
        Has("NO_WORK", "1982-10-13 D001", "DQ-10", MigrationReport.NotLoaded);
        Has("NO_WORK", "2026-10-13 (ID ว่าง)", "NF-06", MigrationReport.NotLoaded);
        Has("NO_WORK", "2026-10-13 X999", "DQ-6", MigrationReport.NotLoaded);
        Has("NO_WORK", "2026-10-14 D001", "DQ-2", MigrationReport.NotLoaded);
        Has("ตั้งวันที่ตัดWEEK", "DAY", "NF-06", MigrationReport.LoadedBlank);

        Assert.Equal("แรก", Assert.Single(await AbsencesAsync(new DateOnly(2026, 10, 14))).Remark);
        Assert.True(report.Balanced, string.Join("\n", report.Tables.Where(t => !t.Balanced)));
        Assert.Contains(report.Tables, t => t is { NewTable: "hr.Absences", SourceRows: 7, Excluded: 0, Rejected: 6, Loaded: 1, InDatabase: 1 });
    }

    [Fact]
    public async Task Without_a_usable_period_nothing_of_the_open_period_is_loaded()
    {
        var legacy = WithAbsences();
        legacy.Tables["ตั้งวันที่ตัดWEEK"][0]["PAY_D"] = null;

        var report = await RunAsync(legacy);

        Assert.Empty(await ReadAsync(s => s.Get<PayPeriodDirectory>().ListAsync()));
        Assert.Empty(await AbsencesAsync(Oct12));
        Assert.Contains(report.Exceptions, e => e is { Table: "ตั้งวันที่ตัดWEEK", Rule: "NF-06", Action: MigrationReport.NotLoaded });
        Assert.Contains(report.Exceptions, e => e is { Table: "NO_WORK", Rule: "NF-06" });
        Assert.True(report.Balanced, string.Join("\n", report.Tables.Where(t => !t.Balanced)));
        Assert.Contains(report.Tables, t => t is { NewTable: "hr.PayPeriods", SourceRows: 1, Rejected: 1, Loaded: 0, InDatabase: 0 });
        Assert.Contains(report.Tables, t => t is { NewTable: "hr.Absences", SourceRows: 3, Excluded: 3, Loaded: 0, InDatabase: 0 });
    }
}
