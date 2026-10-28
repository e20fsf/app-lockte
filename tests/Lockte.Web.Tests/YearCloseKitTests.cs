using System.Data.OleDb;
using System.Runtime.Versioning;

namespace Lockte.Web.Tests;

/// <summary>
/// ชุดปิดปีใน Access (#135): the SQL of <c>tools/access-year-close/sql</c> run on a made-up copy — only the rows that are exact twins of <c>P_ADDST</c> go, the rows whose quantity differs are listed and stay,
/// the before/after balances say which products changed, and the run can be repeated. Every code and number is fictional.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class YearCloseKitTests : IDisposable
{
    private readonly DirectoryInfo work = Directory.CreateTempSubdirectory("lockte-year-close-test-");

    public void Dispose()
    {
        OleDbConnection.ReleaseObjectPool();
        work.Delete(recursive: true);
    }

    private static string SqlFolder()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "tools", "access-year-close", "sql");
            if (Directory.Exists(candidate)) return candidate;
        }
        throw new DirectoryNotFoundException("tools/access-year-close/sql");
    }

    private static List<string> Statements(string file)
    {
        var lines = File.ReadAllLines(Path.Combine(SqlFolder(), file)).Where(l => l.Trim().Length > 0 && !l.TrimStart().StartsWith("--")).ToList();
        return file.StartsWith("00") ? lines.Select(l => l.Trim().TrimEnd(';')).ToList() : [string.Join(" ", lines.Select(l => l.Trim()))];
    }

    private static void Execute(OleDbConnection connection, string sql, bool ignoreErrors = false)
    {
        try { new OleDbCommand(sql, connection).ExecuteNonQuery(); }
        catch (OleDbException) when (ignoreErrors) { }
    }

    private static object? Scalar(OleDbConnection connection, string sql) => new OleDbCommand(sql, connection).ExecuteScalar();

    private static (string Ref, string Suf, string Product, double Quantity, string Mark, DateTime Date) Move(string reference, string product, double quantity, string mark, int month, int day, int year = 2025, string suffix = "1")
        => (reference, suffix, product, quantity, mark, new DateTime(year, month, day));

    private string BuildCopy()
    {
        var file = FakeOrderFile.Empty();
        var current = new[]
        {
            Move("D00001", "G1001", 100, "pd", 12, 5), // twin in P_ADDST
            Move("D00002", "G1001", 250, "pd", 12, 6), // twin
            Move("D00003", "G2002", 400, "pd", 12, 30), // twin
            Move("D00004", "G2002", 2024, "pd", 12, 13), // same document and line in P_ADDST, but 224 there: not sure, must stay
            Move("D00005", "G3003", 70, "pd", 12, 15), // only in P_ST: stays
            Move("D00006", "G1001", 90, "pd", 12, 31), // 31 December is outside the range: stays
            Move("D00007", "G1001", -60, "tr", 12, 7), // not a production (a withdrawal is negative): stays
            Move("D00008", "G3003", 15, "pd", 1, 5, 2026), // after the year: stays
            Move("stock", "G1001", 500, "", 12, 31),
        };
        foreach (var m in current)
            file.Row("P_ST", ("DATE", m.Date), ("REF_NO", m.Ref), ("SUF_NO", m.Suf), ("PD_C", m.Product), ("QTY", m.Quantity), ("MARK", m.Mark == "" ? null : m.Mark));
        var closed = new[]
        {
            Move("D00001", "G1001", 100, "pd", 12, 5), Move("D00002", "G1001", 250, "pd", 12, 6), Move("D00003", "G2002", 400, "pd", 12, 30), Move("D00004", "G2002", 224, "pd", 12, 13),
            Move("D00006", "G1001", 90, "pd", 12, 31), Move("D00007", "G1001", -60, "tr", 12, 7), Move("D00008", "G3003", 15, "pd", 1, 5, 2026),
        };
        foreach (var m in closed)
            file.Row("P_ADDST", ("DATE", m.Date), ("REF_NO", m.Ref), ("SUF_NO", m.Suf), ("PD_C", m.Product), ("QTY", m.Quantity), ("MARK", m.Mark));
        return file.Save(Path.Combine(work.FullName, "ผลผลิตและORDER.accdb"));
    }

    private static OleDbConnection Open(string path)
    {
        var connection = new OleDbConnection($"Provider=Microsoft.ACE.OLEDB.16.0;Data Source={path};Mode=ReadWrite;");
        connection.Open();
        return connection;
    }

    private static void RunKit(OleDbConnection connection)
    {
        foreach (var sql in Statements("00-drop-working-tables.sql")) Execute(connection, sql, ignoreErrors: true);
        foreach (var file in new[] { "01-balance-before.sql", "02-find-duplicates.sql", "03-find-ambiguous.sql", "05-delete-duplicates.sql", "05b-backup-ambiguous.sql", "05c-delete-ambiguous.sql", "06-balance-after.sql" })
            Execute(connection, Statements(file)[0]);
    }

    [Fact] // the exact twins and the row whose quantity differs (decided by the user, 28 Oct 2026) go, each with its backup; the others stay
    public void The_exact_twins_and_the_row_with_a_different_quantity_in_the_range_are_deleted_with_a_backup()
    {
        var path = BuildCopy();
        using (var connection = Open(path))
        {
            RunKit(connection);

            Assert.Equal(3, Convert.ToInt32(Scalar(connection, "SELECT COUNT(*) FROM bak_P_ST_dup_2569")));
            Assert.Equal(750d, Convert.ToDouble(Scalar(connection, "SELECT SUM(QTY) FROM bak_P_ST_dup_2569")));
            Assert.Equal(1, Convert.ToInt32(Scalar(connection, "SELECT COUNT(*) FROM chk_P_ST_ambiguous_2569")));
            Assert.Equal(2024d, Convert.ToDouble(Scalar(connection, "SELECT SUM(QTY) FROM chk_P_ST_ambiguous_2569")));
            Assert.Equal(1, Convert.ToInt32(Scalar(connection, "SELECT COUNT(*) FROM bak_P_ST_ambiguous_2569")));
            Assert.Equal(5, Convert.ToInt32(Scalar(connection, "SELECT COUNT(*) FROM P_ST")));
            Assert.Equal(["D00005", "D00006", "D00007", "D00008", "stock"], Rows(connection, "SELECT REF_NO FROM P_ST ORDER BY REF_NO").Select(r => (string)r[0]!).ToArray());
            Assert.Equal(0, Convert.ToInt32(Scalar(connection, Statements("08-verify-after.sql")[0])));
        }
    }

    [Fact] // the report of the products whose balance changed: before, after and the difference
    public void The_products_whose_balance_changed_are_listed()
    {
        using var connection = Open(BuildCopy());
        RunKit(connection);

        var changed = Rows(connection, Statements("07-changed-products.sql")[0]).Select(r => ((string)r[0]!, Convert.ToDouble(r[1]), Convert.ToDouble(r[2]), Convert.ToDouble(r[3]))).ToArray();

        // G1001: 100 + 250 + 90 − 60 + 500 = 880 before, 100 + 250 gone → 530; G2002: 400 + 2024 = 2424 before, both gone → 0; G3003 untouched
        Assert.Equal([("G1001", 880d, 530d, -350d), ("G2002", 2424d, 0d, -2424d)], changed);
    }

    [Fact] // run again (the kit starts by clearing its own work tables): the second run finds nothing more to delete
    public void The_kit_can_be_run_again_and_finds_nothing_more()
    {
        using var connection = Open(BuildCopy());
        RunKit(connection);

        RunKit(connection);

        Assert.Equal(0, Convert.ToInt32(Scalar(connection, "SELECT COUNT(*) FROM bak_P_ST_dup_2569")));
        Assert.Equal(5, Convert.ToInt32(Scalar(connection, "SELECT COUNT(*) FROM P_ST")));
        Assert.Equal(0, Convert.ToInt32(Scalar(connection, "SELECT COUNT(*) FROM chk_P_ST_ambiguous_2569")));
    }

    [Fact] // before the row goes the user sees it: the quantity in P_ST (to delete) beside the quantity in P_ADDST (closed, kept)
    public void The_row_with_a_different_quantity_is_shown_with_both_quantities_before_it_is_deleted()
    {
        using var connection = Open(BuildCopy());
        foreach (var sql in Statements("00-drop-working-tables.sql")) Execute(connection, sql, ignoreErrors: true);
        foreach (var file in new[] { "02-find-duplicates.sql", "03-find-ambiguous.sql" }) Execute(connection, Statements(file)[0]);

        var count = Rows(connection, Statements("04b-check-ambiguous.sql")[0]).Single();
        var detail = Rows(connection, Statements("04c-ambiguous-detail.sql")[0]).Single();

        Assert.Equal((1, 2024d), (Convert.ToInt32(count[0]), Convert.ToDouble(count[1])));
        Assert.Equal(("D00004", "G2002", 2024d, 224d), ((string)detail[1]!, (string)detail[3]!, Convert.ToDouble(detail[4]), Convert.ToDouble(detail[5])));
    }

    private static List<object?[]> Rows(OleDbConnection connection, string sql)
    {
        using var reader = new OleDbCommand(sql, connection).ExecuteReader();
        var rows = new List<object?[]>();
        while (reader.Read())
        {
            var row = new object?[reader.FieldCount];
            reader.GetValues(row!);
            rows.Add(row.Select(v => v is DBNull ? null : v).ToArray());
        }
        return rows;
    }
}
