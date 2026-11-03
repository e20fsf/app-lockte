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

        var (all, allError) = MigrationOptions.Parse(["--program", "all", "--employee", "e.accdb", "--order", "o.accdb", "--cash", "c.accdb", "--connection", "Server=x;Database=D1", "--confirm", "D1", "--out", "out"]);
        Assert.Null(allError);
        Assert.Equal("c.accdb", all!.CashFile); // the third program of the one command
    }
}

/// <summary>The rules of the migration of the expense lines (#236) that need neither a file nor a database.</summary>
public sealed class CashLineRulesTests
{
    [Theory]
    [InlineData("09/35", 9)]
    [InlineData("12/100", 12)]
    [InlineData("9/99", null)] // the legacy has these two wrong shapes
    [InlineData("99//99", null)]
    [InlineData("13/01", null)]
    [InlineData("ก/01", null)]
    public void The_month_of_a_voucher_number_is_read_from_the_two_digits_before_the_slash(string number, int? month) => Assert.Equal(month, CashLineRules.VoucherMonth(number));

    [Fact]
    public void A_double_of_access_is_read_to_the_digits_it_shows()
    {
        Assert.Equal(25.01m, CashLineRules.Decimal(25.01));
        Assert.Equal(0.33m, CashLineRules.Decimal(0.33));
        Assert.Equal((1000m, false), CashLineRules.Fit(1000m, 2));
        Assert.Equal((12.35m, true), CashLineRules.Fit(12.345m, 2)); // half away from zero
        Assert.Equal((2.5m, true), CashLineRules.Fit(2.4999m, 1));
    }

    [Theory] // the amount is kept as it was; this only tells how many differ
    [InlineData("2", "40", "80", false)]
    [InlineData("25.01", "0", "1000", true)] // fuel typed by amount
    [InlineData("3", "40", "80", true)] // the quantity was edited after the amount (BR-002 #3)
    [InlineData("1", "0.333", "0.33", false)]
    public void An_amount_that_is_not_quantity_times_price_is_found(string qty, string price, string amount, bool differs)
    {
        decimal D(string s) => decimal.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(differs, CashLineRules.AmountDiffers(D(qty), D(price), D(amount)));
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
        // the receipt lines still waiting to be sent, and the lines of the last closing: the same columns, no CODE
        ["ขายของเก่า1"] =
        [
            ("DATE", "DATETIME"), ("NO", "TEXT(50)"), ("NAMED", "TEXT(100)"), ("ADDRESS", "TEXT(255)"), ("TEL", "TEXT(50)"), ("SUF", "SINGLE"), ("NAMES", "TEXT(255)"), ("UNIT", "TEXT(50)"),
            ("QTY", "SINGLE"), ("U_PRICE", "SINGLE"), ("AMOUNT", "SINGLE"), ("MARK", "TEXT(50)"),
        ],
        ["ขายของเก่า2"] =
        [
            ("DATE", "DATETIME"), ("NO", "TEXT(50)"), ("NAMED", "TEXT(100)"), ("ADDRESS", "TEXT(255)"), ("TEL", "TEXT(50)"), ("SUF", "SINGLE"), ("NAMES", "TEXT(255)"), ("UNIT", "TEXT(50)"),
            ("QTY", "SINGLE"), ("U_PRICE", "SINGLE"), ("AMOUNT", "SINGLE"), ("MARK", "TEXT(50)"), ("from_no", "TEXT(50)"), ("to_no", "TEXT(50)"),
        ],
    };

    public static FakeLegacyFile Empty() => new(Schema);

    public static FakeLegacyFile WithLine(this FakeLegacyFile file, string? type)
        => file.Row("ซื้อของเงินสด", ("DATE", new DateTime(2026, 9, 1)), ("SUF", "09/01"), ("NAME", "ของตัวอย่าง"), ("QTY", 1.0), ("AMOUNT", 100.0), ("TYPE", type));

    /// <summary>A line of a receipt in <paramref name="table"/> (<c>ขายของเก่า</c>, <c>ขายของเก่า1</c> or <c>ขายของเก่า2</c>); the numbers are Single, as in the legacy.</summary>
    public static FakeLegacyFile WithReceiptLine(this FakeLegacyFile file, string table, string no, DateTime? date, string? buyer, float? seq, string? item, float? qty, float? price, float? amount,
        string? address = "ที่อยู่ตัวอย่าง", string? tel = "000-000-0000", string? unit = "กก.", string? mark = null)
        => file.Row(table, ("DATE", date), ("NO", no), ("NAMED", buyer), ("ADDRESS", address), ("TEL", tel), ("SUF", seq), ("NAMES", item), ("UNIT", unit), ("QTY", qty), ("U_PRICE", price), ("AMOUNT", amount), ("MARK", mark));

    public static FakeLegacyFile WithExpense(this FakeLegacyFile file, DateTime? date, string? voucher, string? name, double qty, double price, double amount, string? type, string? unit = "ชิ้น")
        => file.Row("ซื้อของเงินสด", ("DATE", date), ("SUF", voucher), ("NAME", name), ("QTY", qty), ("UNIT", unit), ("U_PRICE", price), ("AMOUNT", amount), ("TYPE", type));
}

/// <summary>เครื่องมือย้ายข้อมูล: ข้อมูลหลักของเงินสดย่อย (#231) — a made-up file in, the three registers out, the DQ report beside them.</summary>
[Collection(MigrationDbCollection.Name)]
[SupportedOSPlatform("windows")]
public sealed class CashMigrationTests(MigrationDbFixture db) : IDisposable
{
    private readonly DirectoryInfo work = Directory.CreateTempSubdirectory("lockte-cash-migration-test-");

    public void Dispose() => TestFiles.DeleteScratch(work);

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
    public async Task Loads_every_line_as_it_was_keeps_the_amounts_and_reports_the_odd_vouchers()
    {
        var file = FakeCashFile.Empty()
            .Row("ค่าใช้จ่ายต่างๆ", ("CASH_NAME", "ค่าขนส่ง"))
            .Row("ค่าใช้จ่ายต่างๆ", ("CASH_NAME", "ค่าน้ำมัน"));
        file.WithExpense(new DateTime(2026, 9, 11), "09/36", "หัวสปริงเกอร์", 2, 40, 80, "ค่าขนส่ง") // quantity × price
            .WithExpense(new DateTime(2026, 9, 11), "09/37", "น้ำมันดีเซล", 25.01, 0, 1000, "ค่าน้ำมัน", "ลิตร") // typed amount, price 0
            .WithExpense(new DateTime(2026, 9, 12), "09/100", "ของเลขสามหลัก", 1, 10, 10, "ค่าขนส่ง") // three digits after the slash
            .WithExpense(new DateTime(2026, 9, 12), "08/05", "เดือนในเลขที่ไม่ตรง", 1, 10, 10, "ค่าขนส่ง")
            .WithExpense(new DateTime(2026, 9, 13), "9/99", "รูปเลขที่ผิด", 1, 10, 10, "ค่าขนส่ง")
            .WithExpense(new DateTime(2026, 9, 14), "09/40", "เลขที่เดียวกันคนละวัน", 1, 5, 5, "ค่าขนส่ง")
            .WithExpense(new DateTime(2026, 9, 15), "09/40", "เลขที่เดียวกันคนละวัน", 1, 5, 5, "ค่าน้ำมัน")
            .WithExpense(new DateTime(2011, 9, 3), "09/01", "พิมพ์ปีผิด", 1, 7, 7, "ค่าขนส่ง")
            .WithExpense(new DateTime(2026, 9, 16), "09/41", "ทศนิยมเกิน", 1, 0, 12.345, "ค่าขนส่ง")
            .WithExpense(null, "09/42", "ไม่มีวันที่", 1, 1, 1, "ค่าขนส่ง") // the legacy deleted these on leaving the screen
            .WithExpense(new DateTime(2026, 9, 16), "09/43", "ไม่มีประเภท", 1, 1, 1, null);
        for (var i = 1; i <= 8; i++) file.WithExpense(new DateTime(2026, 10, 1), "10/01", $"รายการยาว{i}", 1, 1, 1, "ค่าขนส่ง"); // a voucher of 8 lines
        var source = file.Save(Path.Combine(work.FullName, "เงินสดย่อย.accdb"));

        var report = await RunAsync(source);

        Assert.True(report.Balanced, string.Join("; ", report.Tables.Where(t => !t.Balanced).Select(t => $"{t.LegacyTable} {t.SourceRows}/{t.Excluded}/{t.Rejected}/{t.Loaded}/{t.InDatabase}"))
            + " " + string.Join("; ", report.MonthlyTotals.Where(m => !m.Balanced).Select(m => $"{m.Month} {m.Expected}/{m.InDatabase}")));
        var total = report.Tables.Single(t => t.LegacyTable == "ซื้อของเงินสด");
        Assert.Equal((19, 2, 17, 17), (total.SourceRows, total.Rejected, total.Loaded, total.InDatabase));
        Assert.Equal(1, report.Tables.Single(t => t.LegacyTable == "ซื้อของเงินสด ปี 2011").Loaded);

        await using var scope = db.Scope();
        var lines = await scope.Get<ExpenseLineDirectory>().ListAsync(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30));
        Assert.Equal(80m, lines.Single(l => l.VoucherNo == "09/36").Amount);
        var fuel = lines.Single(l => l.VoucherNo == "09/37");
        Assert.Equal((25.01m, 0m, 1000m, "ลิตร", "ค่าน้ำมัน"), (fuel.Qty, fuel.UnitPrice, fuel.Amount, fuel.Unit, fuel.ExpenseTypeName)); // not worked out again
        Assert.Contains(lines, l => l.VoucherNo == "09/100");
        Assert.Equal(12.35m, lines.Single(l => l.VoucherNo == "09/41").Amount); // the one value with more than two decimals
        Assert.Equal(new DateOnly(2011, 9, 3), (await scope.Get<ExpenseLineDirectory>().ListAsync(new DateOnly(2011, 1, 1), new DateOnly(2011, 12, 31))).Single().Date);
        Assert.DoesNotContain(lines, l => l.Name is "ไม่มีวันที่" or "ไม่มีประเภท");

        Assert.Contains(report.Exceptions, e => e.Rule == "DQ-Q02" && e.Key == "2026-09-16 09/41" && e.Problem.Contains("12.35", StringComparison.Ordinal) && e.Problem.Contains("12.345", StringComparison.Ordinal));
        Assert.Contains(report.Exceptions, e => e.Rule == "DQ-Q06" && e.Key == "2011-09-03 09/01");
        Assert.Contains(report.Exceptions, e => e.Rule == "DQ-Q31" && e.Key == "2026-10-01 10/01" && e.Problem.Contains("8 บรรทัด", StringComparison.Ordinal));
        Assert.Contains(report.Exceptions, e => e.Rule == "DQ-Q03" && e.Key == "2026-09-12 08/05" && e.Problem.Contains("ไม่ตรงเดือน", StringComparison.Ordinal));
        Assert.Contains(report.Exceptions, e => e.Rule == "DQ-Q03" && e.Key == "2026-09-13 9/99" && e.Problem.Contains("ไม่ใช่รูป", StringComparison.Ordinal));
        Assert.Contains(report.Exceptions, e => e.Rule == "DQ-Q03" && e.Key == "2026 09/40" && e.Problem.Contains("2 วันที่", StringComparison.Ordinal));
        Assert.Contains(report.Exceptions, e => e.Table == "ซื้อของเงินสด" && e.Problem.Contains("วันที่", StringComparison.Ordinal) && e.Action == MigrationReport.NotLoaded);
        Assert.Contains(report.Exceptions, e => e.Table == "ซื้อของเงินสด" && e.Problem.Contains("ประเภท", StringComparison.Ordinal) && e.Action == MigrationReport.NotLoaded);
        Assert.Contains(report.Notes, n => n.StartsWith("จำนวนเงิน ≠ จำนวน × ราคา", StringComparison.Ordinal));
        Assert.Contains(report.Notes, n => n.StartsWith("ยอดตามชื่อดิบ 'ค่าน้ำมัน'", StringComparison.Ordinal));
    }

    [Fact] // AC-23, AC-24, AC-01: the receipts, their lines and their state
    public async Task Loads_the_receipts_with_their_lines_and_works_the_state_out_from_the_waiting_table()
    {
        var file = FakeCashFile.Empty()
            .Row("ผู้ซื้อของเก่า", ("NAMED", "คุณตัวอย่าง ก"), ("ADDRESS", "ที่อยู่ทะเบียน"), ("TEL", "000-111-1111"))
            .Row("รายชื่อของเก่า", ("NAMES", "เศษเหล็ก"), ("UNIT", "กก."));
        var (d1, d2) = (new DateTime(2026, 9, 5), new DateTime(2026, 9, 8));
        // 69025 and 69026 were sent before the migration (69026 is the last closing); 69027–69029 and 69031 wait
        file.WithReceiptLine("ขายของเก่า", "69025", d1, "คุณตัวอย่าง ก", 1, "เศษเหล็ก", 10, 2, 20)
            .WithReceiptLine("ขายของเก่า", "69026", d1, "คุณตัวอย่าง ก", 1, "เศษเหล็ก", 5, 2, 10)
            .WithReceiptLine("ขายของเก่า", "69027", d2, "คุณตัวอย่าง  ก", 1, "เศษเหล็ก", 100, 10, 1000) // a repeated blank in the name: still the register's buyer
            .WithReceiptLine("ขายของเก่า", "69027", d2, "คุณตัวอย่าง ก", 2, "เศษเหล็ก", 36, 10, 360)
            // 69028: the second line has another date and another buyer; a Single with noise (0.33000001), a line with no item, and a repeated ลำดับที่
            .WithReceiptLine("ขายของเก่า", "69028", d2, "คุณตัวอย่าง ข", 2, "น้ำมันเก่า", 0.33f, 3, 1, unit: "ถัง")
            .WithReceiptLine("ขายของเก่า", "69028", d1, "คุณตัวอย่าง ค", 1, "ถังเหล็ก", 1, 99, 99, unit: "ใบ")
            .WithReceiptLine("ขายของเก่า", "69028", d2, "คุณตัวอย่าง ข", 3, null, 1, 5, 5)
            .WithReceiptLine("ขายของเก่า", "69028", d2, "คุณตัวอย่าง ข", 2, "ซ้ำ", 1, 5, 5)
            // 69029: the sum of its amounts is 100.125, which Access cuts to 100.12 — the lines are 50.13 + 50.00 here
            .WithReceiptLine("ขายของเก่า", "69029", d2, "คุณตัวอย่าง ข", 1, "เศษเหล็ก", 1, 50.125f, 50.125f)
            .WithReceiptLine("ขายของเก่า", "69029", d2, "คุณตัวอย่าง ข", 2, "เศษเหล็ก", 1, 50, 50)
            .WithReceiptLine("ขายของเก่า", "69031", d2, "คุณตัวอย่าง ข", 1, "เศษเหล็ก", 1, 7, 7); // 69030 was never issued
        foreach (var (no, line) in new[] { ("69027", 1), ("69027", 2), ("69028", 1), ("69029", 1), ("69029", 2), ("69031", 1) })
            file.WithReceiptLine("ขายของเก่า1", no, d2, "คุณตัวอย่าง ข", line, "เศษเหล็ก", 1, 1, no == "69027" && line == 1 ? 1000 : no == "69027" ? 360 : no == "69029" ? 50 : 1, mark: "Y");
        file.WithReceiptLine("ขายของเก่า2", "69026", d1, "คุณตัวอย่าง ก", 1, "เศษเหล็ก", 5, 2, 10);
        var source = file.Save(Path.Combine(work.FullName, "เงินสดย่อย.accdb"));

        var report = await RunAsync(source);

        Assert.True(report.Balanced, string.Join("; ", report.Tables.Where(t => !t.Balanced).Select(t => $"{t.LegacyTable} {t.SourceRows}/{t.Excluded}/{t.Rejected}/{t.Loaded}/{t.InDatabase}"))
            + " " + string.Join("; ", report.MonthlyTotals.Where(m => !m.Balanced).Select(m => $"{m.Table} {m.Month} {m.Expected}/{m.InDatabase}")));
        var receipts = report.Tables.Single(t => t.LegacyTable == "ขายของเก่า (ใบ)");
        Assert.Equal((6, 0, 6), (receipts.SourceRows, receipts.Rejected, receipts.Loaded));
        var lines = report.Tables.Single(t => t.LegacyTable == "ขายของเก่า (บรรทัด)");
        Assert.Equal((11, 2, 9), (lines.SourceRows, lines.Rejected, lines.Loaded)); // the line with no item and the repeated ลำดับที่ are left out

        await using var scope = db.Scope();
        var directory = scope.Get<ScrapReceiptDirectory>();
        var r27 = (await directory.GetByNoAsync("69027"))!;
        Assert.Equal((new DateOnly(2026, 9, 8), "คุณตัวอย่าง ก", 1360m, false), (r27.Date, r27.BuyerName, r27.Total, r27.IsRemitted));
        Assert.NotNull(r27.ScrapBuyerId); // the name matches the register once the blanks are cleaned
        Assert.NotNull(r27.Lines[0].ScrapItemId);

        var r28 = (await directory.GetByNoAsync("69028"))!;
        Assert.Equal((new DateOnly(2026, 9, 5), "คุณตัวอย่าง ค"), (r28.Date, r28.BuyerName)); // the head of the line with the lowest ลำดับที่
        Assert.Null(r28.ScrapBuyerId); // not in the register: the name stays, no entry is made
        Assert.Equal(["ถังเหล็ก", "น้ำมันเก่า"], r28.Lines.Select(l => l.ItemName));
        Assert.Equal(0.33m, r28.Lines[1].Qty); // Single without the noise
        Assert.Equal(100m, r28.Total);

        var r29 = (await directory.GetByNoAsync("69029"))!;
        Assert.Equal((100.13m, 100.13m), (r29.Lines.Sum(l => l.Amount), r29.Total));
        Assert.Contains(report.Exceptions, e => e.Key == "69029" && e.Rule == "AC-24" && e.Problem.Contains("100.12", StringComparison.Ordinal));

        // the state: the numbers above the last closing (69026) wait, the others were sent in the one closing the migration made
        var waiting = (await scope.Get<ScrapRemittanceDirectory>().UnremittedAsync()).Select(r => r.No).ToList();
        Assert.Equal(["69027", "69028", "69029", "69031"], waiting);
        var r25 = (await directory.GetByNoAsync("69025"))!;
        Assert.True(r25.IsRemitted);
        var closing = (await scope.Get<ScrapRemittanceDirectory>().GetAsync(r25.RemittanceId!.Value))!;
        Assert.Equal(("69025", "69026", "migration", new DateOnly(2026, 9, 5)), (closing.FromNo, closing.ToNo, closing.MadeBy, closing.MadeOn));
        Assert.Equal(["69025", "69026"], closing.Receipts.Select(r => r.No));

        Assert.Contains(report.Exceptions, e => e.Key == "69028" && e.Rule == "DQ-Q16" && e.Problem.Contains("วันที่ต่างกัน", StringComparison.Ordinal));
        Assert.Contains(report.Exceptions, e => e.Key == "69028" && e.Rule == "DQ-Q16" && e.Problem.Contains("ชื่อผู้ซื้อต่างกัน", StringComparison.Ordinal));
        Assert.Contains(report.Exceptions, e => e.Key == "คุณตัวอย่าง ข" && e.Rule == "DQ-Q30");
        Assert.Contains(report.Exceptions, e => e.Key == "69030" && e.Problem.Contains("ขาดในลำดับ", StringComparison.Ordinal));
        Assert.DoesNotContain(report.Exceptions, e => e.Rule == "AC-23" && e.Problem.Contains("เลขตัดยอดล่าสุด", StringComparison.Ordinal)); // waiting and above the last closing: the two agree
        Assert.Contains(report.Notes, n => n.StartsWith("นำส่งก่อนย้ายข้อมูล: 2 ใบ", StringComparison.Ordinal));
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
