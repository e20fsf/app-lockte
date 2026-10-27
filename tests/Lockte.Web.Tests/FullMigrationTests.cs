using System.Runtime.Versioning;
using Lockte.Migration;
using Lockte.Org;
using Lockte.Prod;
using Lockte.Web.Accounts;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Web.Tests;

/// <summary>
/// เครื่องมือย้ายข้อมูล: รันทั้งชุดคำสั่งเดียว (#134) — both programs and the QA specs from made-up files, one report, the same result when run again, and the go-live flag. Every name and number is fictional.
/// </summary>
[Collection(MigrationDbCollection.Name)]
[SupportedOSPlatform("windows")]
public sealed class FullMigrationTests(MigrationDbFixture db) : IDisposable
{
    private readonly DirectoryInfo work = Directory.CreateTempSubdirectory("lockte-full-migration-test-");

    public void Dispose() => work.Delete(recursive: true);

    private string Output(string name = "out") => Path.Combine(work.FullName, name);

    private async Task<string> ConnectionAsync()
    {
        await using var scope = db.Scope("hr1");
        return scope.Get<AccountsDbContext>().Database.GetConnectionString()!;
    }

    private (string Employee, string Order, string Definitions) Files()
    {
        var employee = FakeLegacyFile.Standard().Save(Path.Combine(work.FullName, "พนักงาน.accdb"));
        var order = FakeOrderFile.Empty()
            .Row("P_NAME", ("PD_C", "G1001"), ("PD_N", "สินค้าหนึ่ง"), ("U_NAME", "ชิ้น"))
            .Row("รหัสพิมพ์", ("ID", "BF-001-1/1"), ("NAME", "แม่พิมพ์สมมุติ"), ("QTYS", 2))
            .Save(Path.Combine(work.FullName, "ผลผลิตและORDER.accdb"));
        var definitions = Directory.CreateDirectory(Path.Combine(work.FullName, "reports")).FullName;
        return (employee, order, definitions);
    }

    private async Task<MigrationOptions> OptionsAsync(string employee, string order, string? definitions, string output)
        => new("", await ConnectionAsync(), null, output, MigrationOptions.AllProgram, false, employee, order, definitions);

    [Fact] // one command: the employee program, then the order program; one report; run again, the same counts
    public async Task Runs_both_programs_with_one_report_and_runs_again_with_the_same_result()
    {
        var (employee, order, definitions) = Files();

        var first = await FullMigrationTool.RunAsync(db.Services, await OptionsAsync(employee, order, null, Output("first")));
        var second = await FullMigrationTool.RunAsync(db.Services, await OptionsAsync(employee, order, null, Output("second")));

        Assert.Equal(["พนักงาน.accdb", "ผลผลิตและORDER.accdb"], first.Select(s => s.Name)); // no QA step: the folder of definitions is not given
        Assert.All(first.Concat(second), s => Assert.True(s.Report.Balanced, s.Name));
        Assert.Equal(first.Select(s => s.Report.Tables.Sum(t => t.InDatabase)), second.Select(s => s.Report.Tables.Sum(t => t.InDatabase)));
        foreach (var file in new[] { "summary.md", "run-info.csv", "row-counts-all.csv", "monthly-totals-all.csv", "dq-exceptions-all.csv", "dq-summary.csv" })
            Assert.True(File.Exists(Path.Combine(Output("first"), file)), file);
        var summary = await File.ReadAllTextAsync(Path.Combine(Output("first"), "summary.md"));
        Assert.Contains("รายงานการย้ายข้อมูลทั้งชุด", summary);
        Assert.Contains("ตรงทั้ง", summary);
        Assert.Contains("พนักงาน.accdb", summary);
        Assert.True(Directory.Exists(Path.Combine(Output("first"), "01-employee")));
        Assert.True(Directory.Exists(Path.Combine(Output("first"), "02-order")));
    }

    [Fact] // the QA specs are the third step when the folder of definitions is given
    public async Task The_qa_specs_are_the_third_step_when_the_definitions_are_given()
    {
        var (employee, order, definitions) = Files();

        var steps = await FullMigrationTool.RunAsync(db.Services, await OptionsAsync(employee, order, definitions, Output()));

        Assert.Equal(3, steps.Count);
        Assert.Equal("สเปคตรวจ QA", steps[2].Name);
        Assert.True(File.Exists(Path.Combine(Output(), "03-qa-spec", "row-counts.csv")));
    }

    [Fact] // a file that is not the one asked for stops the run before anything is deleted, with the reason in words
    public async Task A_file_that_is_not_an_access_copy_stops_the_run_with_a_clear_message()
    {
        var (employee, _, definitions) = Files();
        var notAccess = Path.Combine(work.FullName, "ผลผลิตและORDER.accdb.txt");
        await File.WriteAllBytesAsync(notAccess, new byte[100_000]);
        var tiny = Path.Combine(work.FullName, "tiny.accdb");
        await File.WriteAllBytesAsync(tiny, new byte[100]);
        var fake = Path.Combine(work.FullName, "fake.accdb");
        await File.WriteAllBytesAsync(fake, new byte[100_000]);

        var wrongKind = await Assert.ThrowsAsync<InvalidDataException>(async () => await FullMigrationTool.RunAsync(db.Services, await OptionsAsync(employee, notAccess, definitions, Output())));
        var small = await Assert.ThrowsAsync<InvalidDataException>(async () => await FullMigrationTool.RunAsync(db.Services, await OptionsAsync(employee, tiny, definitions, Output())));
        var notDatabase = await Assert.ThrowsAsync<InvalidDataException>(async () => await FullMigrationTool.RunAsync(db.Services, await OptionsAsync(employee, fake, definitions, Output())));
        var missing = await Assert.ThrowsAsync<FileNotFoundException>(async () => await FullMigrationTool.RunAsync(db.Services, await OptionsAsync(employee, Path.Combine(work.FullName, "nope.accdb"), definitions, Output())));

        Assert.Contains("ไม่ใช่ไฟล์ Access", wrongKind.Message);
        Assert.Contains("เล็กเกินไป", small.Message);
        Assert.Contains("ไม่ใช่ไฟล์ฐานข้อมูล Access", notDatabase.Message);
        Assert.Contains("ไม่พบไฟล์", missing.Message);
    }

    [Fact] // the go-live flag: the tool does not run on a database marked as in use; the mark is by the program and is dropped by hand
    public async Task A_database_marked_as_live_is_not_migrated_again()
    {
        var (employee, order, definitions) = Files();
        var connection = await ConnectionAsync();
        try
        {
            Assert.False(await GoLiveGuard.IsLiveAsync(connection));
            await GoLiveGuard.MarkAsync(connection);
            await GoLiveGuard.MarkAsync(connection); // twice is fine

            var error = await Assert.ThrowsAsync<InvalidOperationException>(async () => await FullMigrationTool.RunAsync(db.Services, await OptionsAsync(employee, order, definitions, Output())));

            Assert.True(await GoLiveGuard.IsLiveAsync(connection));
            Assert.Contains("เปิดใช้งานจริงแล้ว", error.Message);
            Assert.Contains("sp_dropextendedproperty", error.Message);
        }
        finally
        {
            await using var c = new SqlConnection(connection);
            await c.OpenAsync();
            await using var drop = new SqlCommand($"IF EXISTS (SELECT 1 FROM sys.extended_properties WHERE class = 0 AND name = N'{GoLiveGuard.Property}') EXEC sp_dropextendedproperty N'{GoLiveGuard.Property}'", c);
            await drop.ExecuteNonQueryAsync();
        }
        Assert.False(await GoLiveGuard.IsLiveAsync(connection));
    }

    [Fact] // the command line of the whole run
    public void The_command_line_of_the_whole_run_needs_both_files_and_the_confirmation()
    {
        string[] args(params string[] extra) => ["--program", "all", "--connection", "Server=x;Database=LockteTemp;", "--confirm", "LockteTemp", "--out", "out", .. extra];

        var ok = MigrationOptions.Parse(args("--employee", "e.accdb", "--order", "o.accdb", "--qa", "folder", "--remove-stock-duplicates", "yes"));
        var withoutOrder = MigrationOptions.Parse(args("--employee", "e.accdb"));
        var wrongDatabase = MigrationOptions.Parse(["--program", "all", "--connection", "Server=x;Database=LockteTemp;", "--confirm", "Other", "--out", "out", "--employee", "e", "--order", "o"]);
        var mark = MigrationOptions.Parse(["--program", "mark-go-live", "--connection", "Server=x;Database=LockteTemp;", "--confirm", "LockteTemp"]);

        Assert.Equal(("e.accdb", "o.accdb", "folder", true), (ok.Options!.EmployeeFile, ok.Options.OrderFile, ok.Options.QaDefinitionsFolder, ok.Options.RemoveStockDuplicates));
        Assert.Contains("--employee และ --order", withoutOrder.Error);
        Assert.Contains("--confirm LockteTemp", wrongDatabase.Error);
        Assert.Equal(MigrationOptions.MarkGoLiveProgram, mark.Options!.Program);
    }

    [Theory] // rows with the same problem are one line of the summary
    [InlineData("รหัสสินค้า 'ZZ999' ไม่มีในทะเบียน 12 แถว", "รหัสสินค้า '…' ไม่มีในทะเบียน N แถว")]
    [InlineData("วันที่ 2025-12-10 จำนวน 1,461.5", "วันที่ N-N-N จำนวน N")]
    [InlineData("หลายเครื่องในใบเดียว (PN1 ×3 PN2 ×2) — ใช้เครื่อง PN1", "หลายเครื่องในใบเดียว (…) — ใช้เครื่อง PNN")]
    public void The_kind_of_a_problem_leaves_out_what_is_special_to_a_row(string problem, string kind)
        => Assert.Equal(kind, FullMigrationTool.Kind(problem));
}
