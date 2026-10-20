using System.Runtime.Versioning;
using Lockte.Hr;
using Lockte.Migration;
using Lockte.Org;
using Lockte.Web.Audit;

namespace Lockte.Web.Tests;

/// <summary>
/// เครื่องมือย้ายข้อมูล: ใบเตือน (#51, #59) — ใบเตือน + ใบเตือนADD of every year into one table, from a fake
/// <c>พนักงาน.accdb</c>, read back through the module's public services; plus the row count.
/// </summary>
[Collection(MigrationDbCollection.Name)]
[SupportedOSPlatform("windows")]
public sealed class WarningMigrationTests(MigrationDbFixture db) : IDisposable
{
    private readonly DirectoryInfo work = Directory.CreateTempSubdirectory("lockte-warning-test-");

    public void Dispose() => work.Delete(recursive: true);

    /// <summary>
    /// A warning of this round and two of rounds before (one with a แผนก that is not in T-แผนก, one without ระดับ), a blank
    /// row the form left and one of an employee who is not in EMPLO.
    /// </summary>
    private static FakeLegacyFile WithWarnings() => FakeLegacyFile.Standard()
        .Row("ใบเตือน", ("DATE", new DateTime(2026, 9, 1)), ("ID", "d001"), ("NAME", "นาย  ทดสอบ  หัวหน้ากะ"), ("DEPART", "ไดคาสท์"),
            ("CODE", "1"), ("CODE_NAME", "ใบเตือนด้วยวาจา"), ("REMARK", "มาสาย 3 ครั้ง"))
        .Row("ใบเตือน", ("NAME", null))
        .Row("ใบเตือน", ("DATE", new DateTime(2026, 9, 2)), ("ID", "X999"), ("CODE", "1"))
        .Row("ใบเตือนADD", ("DATE", new DateTime(2019, 5, 7)), ("ID", "D002"), ("DEPART", "แผนกที่เลิกแล้ว"), ("CODE", "2"),
            ("CODE_NAME", "ใบเตือนเป็นหนังสือ"), ("REMARK", "ทะเลาะวิวาท"))
        .Row("ใบเตือนADD", ("DATE", new DateTime(2025, 11, 18)), ("ID", "O001"), ("DEPART", "บุคคลและธุรการ"));

    private async Task<MigrationReport> RunAsync()
    {
        var source = Path.Combine(work.FullName, "warnings.accdb");
        if (!File.Exists(source)) WithWarnings().Save(source);
        return await MigrationTool.RunAsync(db.Services, source, null, Path.Combine(work.FullName, "out"));
    }

    private async Task<T> ReadAsync<T>(Func<TestScope, Task<T>> read)
    {
        await using var scope = db.Scope("hr1");
        return await read(scope);
    }

    private Task<IReadOnlyList<WarningDto>> WarningsOfAsync(string code)
        => ReadAsync(async s => await s.Get<WarningDirectory>().ListOfEmployeeAsync((await s.Get<EmployeeDirectory>().FindByCodeAsync(code))!.Id));

    [Fact]
    public async Task Warnings_of_every_round_are_loaded_with_their_department_and_level_and_counted()
    {
        var report = await RunAsync();

        var current = Assert.Single(await WarningsOfAsync("D001"));
        var diecast = await ReadAsync(s => s.Get<OrgDirectory>().FindDepartmentByNameAsync("ไดคาสท์"));
        Assert.Equal((new DateOnly(2026, 9, 1), diecast!.Id, "1", "มาสาย 3 ครั้ง"), (current.Date, current.DepartmentId!.Value, current.Level, current.Remark));
        var old = Assert.Single(await WarningsOfAsync("D002"));
        Assert.Equal((new DateOnly(2019, 5, 7), (int?)null, "2"), (old.Date, old.DepartmentId, old.Level));
        Assert.Null(Assert.Single(await WarningsOfAsync("O001")).Level); // DQ-9

        var count = Assert.Single(report.Tables, t => t.NewTable == "hr.Warnings");
        Assert.Equal((5, 2, 3, 3), (count.SourceRows, count.Rejected, count.Loaded, count.InDatabase));
        Assert.True(count.Balanced);
        Assert.Contains(report.Exceptions, e => e.Table == "ใบเตือน" && e.Rule == "DQ-6" && e.Key.EndsWith("X999"));
        Assert.Contains(report.Exceptions, e => e.Table == "ใบเตือนADD" && e.Problem.Contains("แผนกที่เลิกแล้ว"));
    }

    [Fact] // ลบแล้วโหลดใหม่ รันซ้ำได้ · ไม่เข้าประวัติการแก้ไข (#41)
    public async Task A_second_run_loads_the_same_rows_again_without_history()
    {
        await RunAsync();
        await RunAsync();

        Assert.Single(await WarningsOfAsync("D001"));
        await using var scope = db.Scope();
        Assert.Empty(await scope.Get<HistoryService>().SearchAsync(new(Table: WarningRegistry.WarningTable)));
    }
}
