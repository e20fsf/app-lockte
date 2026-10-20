using System.Runtime.Versioning;
using Lockte.Migration;

namespace Lockte.Web.Tests;

/// <summary>Reading the fake <c>พนักงาน.accdb</c> without a database: the history tables are read from their first date on (#51).</summary>
[SupportedOSPlatform("windows")]
public sealed class LegacyFileTests : IDisposable
{
    private readonly DirectoryInfo work = Directory.CreateTempSubdirectory("lockte-legacy-test-");

    public void Dispose() => work.Delete(recursive: true);

    [Fact]
    public void History_tables_are_read_from_their_first_date_and_counted_in_full()
    {
        var source = FakeLegacyFile.Standard()
            .Row("ADD_EW", ("DATE", new DateTime(2024, 12, 31)), ("ID", "D001"))
            .Row("ADD_EW", ("DATE", new DateTime(2025, 1, 1)), ("ID", "D001"))
            .Row("ADD_EW", ("ID", "D001"))
            .Row("ADD_EW", ("DATE", new DateTime(1899, 12, 30)), ("ID", "D001")) // DQ-1: stays in Access with the old years
            .Row("LEAVE_ADD", ("FROM_D", new DateTime(2025, 12, 31)), ("ID", "O001"), ("SUM_D", 0.375f))
            .Row("LEAVE_ADD", ("FROM_D", new DateTime(2026, 1, 1)), ("ID", "O001"), ("SUM_D", 0.375f), ("SALA_DAY", 10.4f))
            .Save(Path.Combine(work.FullName, "พนักงาน.accdb"));

        var legacy = LegacyDatabase.ReadAccessFile(source);

        Assert.Equal([new DateTime(2025, 1, 1), null], legacy["ADD_EW"].Select(r => r.Date("DATE")));
        Assert.Equal(4, legacy.SourceRows("ADD_EW"));
        var leave = Assert.Single(legacy["LEAVE_ADD"]);
        Assert.Equal((0.375, 10.4), (leave.Number("SUM_D")!.Value, leave.Number("SALA_DAY")!.Value)); // Single read as its text
        Assert.Equal(2, legacy.SourceRows("LEAVE_ADD"));
        Assert.Empty(legacy["E_WORK"]);
        Assert.Equal(4, legacy.SourceRows("EMPLO"));
    }

    [Fact]
    public void The_open_period_and_NO_WORK_are_read_in_full()
    {
        var source = FakeLegacyFile.Standard()
            .Row("NO_WORK", ("DATE1", new DateTime(2019, 1, 2)), ("ID1", "D001"))
            .Row("NO_WORK", ("ID1", "D001"))
            .Save(Path.Combine(work.FullName, "พนักงาน.accdb"));

        var legacy = LegacyDatabase.ReadAccessFile(source);

        var period = Assert.Single(legacy["ตั้งวันที่ตัดWEEK"]);
        Assert.Equal((new DateTime(2026, 10, 12), new DateTime(2026, 10, 26), new DateTime(2026, 11, 1)),
            (period.Date("FROM_DA"), period.Date("TO_DA"), period.Date("PAY_D")));
        Assert.Equal(2, legacy["NO_WORK"].Count); // filtered by the period's first day in the migration, not when read
        Assert.Equal(2, legacy.SourceRows("NO_WORK"));
    }
}
