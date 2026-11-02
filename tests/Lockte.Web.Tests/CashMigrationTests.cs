using System.Runtime.Versioning;
using System.Security.Cryptography;
using Lockte.Cash;
using Lockte.Migration;

namespace Lockte.Web.Tests;

/// <summary>The rules of the migration of <c>เงินสดย่อย.accdb</c> (#231) that need neither a file nor a database.</summary>
public sealed class CashMigrationRulesTests
{
    [Theory]
    [InlineData("  ค่าขนส่ง ", "ค่าขนส่ง")]
    [InlineData("ค่าใช้จ่ายอบรม\r\nค่าใช้จ่ายการเดินทางในประเทศ", "ค่าใช้จ่ายอบรม ค่าใช้จ่ายการเดินทางในประเทศ")]
    [InlineData("คุณตัวอย่าง   ก", "คุณตัวอย่าง ก")]
    public void A_name_is_cleaned_like_the_screens_clean_it(string raw, string expected) => Assert.Equal(expected, CashMigrationRules.Name(raw));

    [Fact]
    public void A_blank_name_is_nothing()
    {
        Assert.Null(CashMigrationRules.Name(null));
        Assert.Null(CashMigrationRules.Name(" \r\n "));
    }

    [Theory]
    [InlineData("ค่าขนส่ง", false, false)]
    [InlineData(" ค่าขนส่ง ", false, false)] // blanks at the ends are not a finding
    [InlineData("ค่าขนส่ง  สินค้า", false, true)]
    [InlineData("ค่าอบรม\r\nค่าเดินทาง", true, true)]
    public void A_line_break_or_repeated_blank_is_found(string raw, bool lineBreak, bool inner)
    {
        Assert.Equal(lineBreak, CashMigrationRules.HasLineBreak(raw));
        Assert.Equal(inner, CashMigrationRules.HadInnerBlanks(raw));
    }

    [Fact]
    public void The_tables_left_behind_are_listed_with_a_reason()
    {
        Assert.All(CashMigrationRules.NotMoved, n => Assert.False(string.IsNullOrWhiteSpace(n.Reason)));
        var tables = string.Join(" | ", CashMigrationRules.NotMoved.Select(n => n.Table));
        foreach (var name in new[] { "Sheet1", "ชื่อของเก่า", "เดือน", "สำเนาของ", "FROM_NO", "Table11", "ขายของเก่า2", "Switchboard" })
            Assert.Contains(name, tables, StringComparison.Ordinal);
    }

    [Fact]
    public void The_code_map_knows_the_expense_type_kind_and_the_command_line_the_cash_program()
    {
        var map = new CodeMap([new CodeMapEntry(CodeKinds.CashExpenseType, "ค่าซ่อม (1กก 123)", "ค่าซ่อม (1 กก 123)", null, null)]);
        Assert.Equal("ค่าซ่อม (1 กก 123)", map.Find(CodeKinds.CashExpenseType, " ค่าซ่อม (1กก 123) ")!.Code);

        var (options, error) = MigrationOptions.Parse(["--program", "cash", "--source", "เงินสดย่อย.accdb", "--connection", "Server=x;Database=D1", "--confirm", "D1", "--out", "out"]);
        Assert.Null(error);
        Assert.Equal(MigrationOptions.CashProgram, options!.Program);
        Assert.Contains("--program cash", MigrationOptions.Usage);
    }
}

/// <summary>A made-up <c>เงินสดย่อย.accdb</c> with the legacy column types (data-model.md). Every name, address and number in it is fictional — the repo is public.</summary>
[SupportedOSPlatform("windows")]
public static class FakeCashFile
{
    public static Dictionary<string, (string Column, string Type)[]> Schema { get; } = new()
    {
        ["ค่าใช้จ่ายต่างๆ"] = [("CASH_NAME", "TEXT(255)"), ("MARK", "TEXT(50)")],
        ["ผู้ซื้อของเก่า"] = [("NAMED", "TEXT(255)"), ("ADDRESS", "TEXT(255)"), ("TEL", "TEXT(50)")],
        ["รายชื่อของเก่า"] = [("NAMES", "TEXT(255)"), ("UNIT", "TEXT(50)")],
        ["ซื้อของเงินสด"] =
        [
            ("DATE", "DATETIME"), ("SUF", "TEXT(50)"), ("NAME", "TEXT(255)"), ("QTY", "DOUBLE"), ("UNIT", "TEXT(50)"), ("U_PRICE", "DOUBLE"), ("AMOUNT", "DOUBLE"), ("TYPE", "TEXT(255)"), ("MARK", "TEXT(50)"),
        ],
        ["ขายของเก่า"] =
        [
            ("DATE", "DATETIME"), ("NO", "TEXT(50)"), ("NAMED", "TEXT(100)"), ("ADDRESS", "TEXT(255)"), ("TEL", "TEXT(50)"), ("SUF", "SINGLE"), ("NAMES", "TEXT(255)"), ("UNIT", "TEXT(50)"),
            ("QTY", "SINGLE"), ("U_PRICE", "SINGLE"), ("AMOUNT", "SINGLE"), ("MARK", "TEXT(50)"), ("CODE", "TEXT(50)"),
        ],
    };

    public static FakeLegacyFile Empty() => new(Schema);

    public static FakeLegacyFile WithLine(this FakeLegacyFile file, string? type)
        => file.Row("ซื้อของเงินสด", ("DATE", new DateTime(2026, 9, 1)), ("SUF", "09/01"), ("NAME", "ของตัวอย่าง"), ("QTY", 1.0), ("AMOUNT", 100.0), ("TYPE", type));
}

/// <summary>เครื่องมือย้ายข้อมูล: ข้อมูลหลักของเงินสดย่อย (#231) — a made-up file in, the three registers out, the DQ report beside them.</summary>
[Collection(MigrationDbCollection.Name)]
[SupportedOSPlatform("windows")]
public sealed class CashMigrationTests(MigrationDbFixture db) : IDisposable
{
    private readonly DirectoryInfo work = Directory.CreateTempSubdirectory("lockte-cash-migration-test-");

    public void Dispose() => work.Delete(recursive: true);

    private Task<MigrationReport> RunAsync(string source, string? codeMap = null, string output = "out")
        => CashMigrationTool.RunAsync(db.Services, source, codeMap, Path.Combine(work.FullName, output));

    private static FakeLegacyFile Legacy()
    {
        var file = FakeCashFile.Empty()
            .Row("ค่าใช้จ่ายต่างๆ", ("CASH_NAME", "ค่าขนส่ง"))
            .Row("ค่าใช้จ่ายต่างๆ", ("CASH_NAME", " ค่าขนส่ง ")) // the same name after trimming
            .Row("ค่าใช้จ่ายต่างๆ", ("CASH_NAME", "ค่ารับรอง\r\nลูกค้า")) // typed over two lines
            .Row("ค่าใช้จ่ายต่างๆ", ("CASH_NAME", "ค่าไม่ได้ใช้")) // no line uses it
            .Row("ค่าใช้จ่ายต่างๆ", ("CASH_NAME", ""))
            .Row("ค่าใช้จ่ายต่างๆ", ("CASH_NAME", "ค่าซ่อม (1 กก 123)"))
            .Row("ผู้ซื้อของเก่า", ("NAMED", "คุณตัวอย่าง ก"), ("ADDRESS", "ที่อยู่ตัวอย่าง 1"), ("TEL", "000-000-0001, 000-000-0002"))
            .Row("ผู้ซื้อของเก่า", ("NAMED", "คุณตัวอย่าง  ข")) // a repeated blank, no address; no receipt uses it
            .Row("ผู้ซื้อของเก่า", ("NAMED", "คุณตัวอย่าง ก ")) // the same name after trimming
            .Row("ผู้ซื้อของเก่า", ("NAMED", null))
            .Row("รายชื่อของเก่า", ("NAMES", "เศษเหล็ก"), ("UNIT", "กก."))
            .Row("รายชื่อของเก่า", ("NAMES", "น้ำมันเก่า"), ("UNIT", "ถัง")) // no receipt uses it
            .Row("ขายของเก่า", ("DATE", new DateTime(2026, 9, 1)), ("NO", "69001"), ("NAMED", "คุณตัวอย่าง ก"), ("NAMES", "เศษเหล็ก"), ("QTY", 1.0f), ("AMOUNT", 100.0f));
        foreach (var type in new[] { "ค่าขนส่ง", "ค่าขนส่ง", "ค่ารับรอง\r\nลูกค้า", "ค่าซ่อม (1กก 123)", "ค่าซ่อม (1 กก 123 )", "ค่าที่ไม่มีในทะเบียน", "ค่าที่ไม่มีในทะเบียน", "ค่าที่ไม่มีในทะเบียน", "ค่าอบรม\r\nค่าเดินทาง" })
            file.WithLine(type);
        return file;
    }

    [Fact]
    public async Task Loads_the_three_registers_retires_the_names_only_the_lines_use_and_reports_what_needs_a_person()
    {
        var source = Legacy().Save(Path.Combine(work.FullName, "เงินสดย่อย.accdb"));
        var before = (Hash: SHA256.HashData(File.ReadAllBytes(source)), Written: File.GetLastWriteTimeUtc(source));
        var map = Path.Combine(work.FullName, "map.csv");
        File.WriteAllLines(map, ["kind,legacy,code,name,prefix", "cash-expense-type,ค่าซ่อม (1กก 123),ค่าซ่อม (1 กก 123),,"]);

        var report = await RunAsync(source, map);

        Assert.Equal(before.Hash, SHA256.HashData(File.ReadAllBytes(source))); // the source is read through a copy
        Assert.Equal(before.Written, File.GetLastWriteTimeUtc(source));
        Assert.True(report.Balanced, string.Join("; ", report.Tables.Where(t => !t.Balanced).Select(t => $"{t.LegacyTable} {t.SourceRows}/{t.Excluded}/{t.Rejected}/{t.Loaded}/{t.InDatabase}")));

        TableCount Table(string name) => report.Tables.Single(t => t.LegacyTable == name);
        Assert.Equal((6, 0, 2, 4, 4), (Table("ค่าใช้จ่ายต่างๆ").SourceRows, Table("ค่าใช้จ่ายต่างๆ").Excluded, Table("ค่าใช้จ่ายต่างๆ").Rejected, Table("ค่าใช้จ่ายต่างๆ").Loaded, Table("ค่าใช้จ่ายต่างๆ").InDatabase));
        // used by the lines and not in the register: 4 spellings; one is merged by the map, three become types เลิกใช้
        Assert.Equal((4, 1, 0, 3, 3), (Table("ซื้อของเงินสด.TYPE").SourceRows, Table("ซื้อของเงินสด.TYPE").Excluded, Table("ซื้อของเงินสด.TYPE").Rejected, Table("ซื้อของเงินสด.TYPE").Loaded, Table("ซื้อของเงินสด.TYPE").InDatabase));
        Assert.Equal((4, 2, 2), (Table("ผู้ซื้อของเก่า").SourceRows, Table("ผู้ซื้อของเก่า").Rejected, Table("ผู้ซื้อของเก่า").Loaded));
        Assert.Equal((2, 0, 2), (Table("รายชื่อของเก่า").SourceRows, Table("รายชื่อของเก่า").Rejected, Table("รายชื่อของเก่า").Loaded));

        await using var scope = db.Scope();
        var types = await scope.Get<ExpenseTypeDirectory>().ListAllAsync();
        Assert.Equal(new[] { "ค่าขนส่ง", "ค่าไม่ได้ใช้", "ค่ารับรอง ลูกค้า", "ค่าซ่อม (1 กก 123)" }.Order(StringComparer.Ordinal), types.Where(t => t.IsActive).Select(t => t.Name).Order(StringComparer.Ordinal));
        Assert.Equal(new[] { "ค่าซ่อม (1 กก 123 )", "ค่าที่ไม่มีในทะเบียน", "ค่าอบรม ค่าเดินทาง" }.Order(StringComparer.Ordinal), types.Where(t => !t.IsActive).Select(t => t.Name).Order(StringComparer.Ordinal));
        var buyers = await scope.Get<ScrapBuyerDirectory>().ListAsync();
        Assert.Equal(["คุณตัวอย่าง ก", "คุณตัวอย่าง ข"], buyers.Select(b => b.Name));
        Assert.Equal("000-000-0001, 000-000-0002", buyers[0].Tel);
        Assert.Null(buyers[1].Address);
        Assert.Equal(["น้ำมันเก่า", "เศษเหล็ก"], (await scope.Get<ScrapItemDirectory>().ListAsync()).Select(i => i.Name));

        // the DQ report: grouped and per row
        Assert.Contains(report.Exceptions, e => e.Table == "ค่าใช้จ่ายต่างๆ" && e.Rule == "DQ-Q05" && e.Key == "ค่าขนส่ง" && e.Action == MigrationReport.NotLoaded);
        Assert.Contains(report.Exceptions, e => e.Table == "ค่าใช้จ่ายต่างๆ" && e.Rule == "DQ-Q27" && e.Key == "ค่ารับรอง ลูกค้า"); // a line break in the register
        Assert.Contains(report.Exceptions, e => e.Table == "ซื้อของเงินสด.TYPE" && e.Rule == "DQ-Q27" && e.Key == "ค่าอบรม ค่าเดินทาง"); // a line break in the lines
        Assert.Contains(report.Exceptions, e => e.Table == "ซื้อของเงินสด.TYPE" && e.Rule == "DQ-Q05" && e.Key == "ค่าที่ไม่มีในทะเบียน" && e.Problem.StartsWith("3 แถว", StringComparison.Ordinal));
        Assert.Contains(report.Exceptions, e => e.Table == "ซื้อของเงินสด.TYPE" && e.Key == "ค่าซ่อม (1กก 123)" && e.Action == "จับคู่ตามคำตัดสิน");
        Assert.Contains(report.Exceptions, e => e.Table == "ค่าใช้จ่ายต่างๆ" && e.Key == "ค่าไม่ได้ใช้" && e.Problem.Contains("ไม่มีบรรทัดค่าใช้จ่ายใดใช้", StringComparison.Ordinal));
        Assert.Contains(report.Exceptions, e => e.Table == "ผู้ซื้อของเก่า" && e.Key == "คุณตัวอย่าง ข" && e.Problem.Contains("ไม่มีใบเสร็จใดใช้", StringComparison.Ordinal));
        Assert.Contains(report.Exceptions, e => e.Table == "รายชื่อของเก่า" && e.Key == "น้ำมันเก่า" && e.Problem.Contains("ไม่มีใบเสร็จใดใช้", StringComparison.Ordinal));
        Assert.Contains(report.Notes, n => n.StartsWith("DQ DQ-Q05 ซื้อของเงินสด.TYPE:", StringComparison.Ordinal));
        Assert.Contains(report.Notes, n => n.StartsWith("ไม่ย้าย Sheet1:", StringComparison.Ordinal));

        // the map the next run reads: every name the lines use and what it became
        Assert.Contains(report.CodeMapUsed, u => u.Kind == CodeKinds.CashExpenseType && u.Legacy == "ค่าซ่อม (1กก 123)" && u.Code == "ค่าซ่อม (1 กก 123)" && u.FromMap);
        Assert.Contains(report.CodeMapUsed, u => u.Kind == CodeKinds.CashExpenseType && u.Legacy == "ค่าขนส่ง" && u.Code == "ค่าขนส่ง" && !u.FromMap);
    }

    [Fact]
    public async Task Running_it_again_gives_the_same_registers()
    {
        var source = Legacy().Save(Path.Combine(work.FullName, "เงินสดย่อย.accdb"));
        var map = Path.Combine(work.FullName, "map.csv");
        File.WriteAllLines(map, ["kind,legacy,code,name,prefix", "cash-expense-type,ค่าซ่อม (1กก 123),ค่าซ่อม (1 กก 123),,"]);

        var first = await RunAsync(source, map, "out1");
        var second = await RunAsync(source, map, "out2");

        Assert.True(second.Balanced);
        Assert.Equal(first.Tables.Select(t => (t.LegacyTable, t.Loaded, t.InDatabase)), second.Tables.Select(t => (t.LegacyTable, t.Loaded, t.InDatabase)));
        await using var scope = db.Scope();
        Assert.Equal(7, (await scope.Get<ExpenseTypeDirectory>().ListAllAsync()).Count);
        Assert.Equal(2, (await scope.Get<ScrapBuyerDirectory>().ListAsync()).Count);
        Assert.Equal(2, (await scope.Get<ScrapItemDirectory>().ListAsync()).Count);
        Assert.True(File.Exists(Path.Combine(work.FullName, "out2", MigrationReport.DqFile)));
    }
}
