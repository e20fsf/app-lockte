using System.Data.OleDb;
using System.Runtime.Versioning;

namespace Lockte.Migration;

/// <summary>The legacy file as it was when read: name, size, last write and the time of reading (UTC).</summary>
public sealed record LegacySource(string FileName, long Bytes, DateTime LastWriteUtc, DateTime ReadAtUtc);

/// <summary>One row of a legacy Access table, by column name (case-insensitive). DBNull is null.</summary>
public sealed class LegacyRow(IReadOnlyDictionary<string, object?> values)
{
    public object? this[string column] => values.TryGetValue(column, out var value) ? value : null;

    /// <summary>Trimmed text; blank = null.</summary>
    public string? Text(string column) => this[column] switch
    {
        null => null,
        string s => string.IsNullOrWhiteSpace(s) ? null : s.Trim(),
        var other => Convert.ToString(other, System.Globalization.CultureInfo.InvariantCulture)?.Trim(),
    };

    public DateTime? Date(string column) => this[column] as DateTime?;

    /// <summary>A Single column (SUM_D, SALA_DAY, PAY) is read by its shortest text, so 10.4 stays 10.4 and not 10.3999996.</summary>
    public double? Number(string column) => this[column] switch
    {
        null => null,
        float f => double.Parse(f.ToString(System.Globalization.CultureInfo.InvariantCulture), System.Globalization.CultureInfo.InvariantCulture),
        var value => Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture),
    };
}

/// <summary>
/// The tables of <c>พนักงาน.accdb</c> that the migration reads, in the order Access returns their rows (the legacy
/// tables have no primary key, so this order is what keeps two runs on the same file alike).
/// </summary>
public sealed class LegacyDatabase(IReadOnlyDictionary<string, IReadOnlyList<LegacyRow>> tables, IReadOnlyDictionary<string, int>? sourceRows = null)
{
    /// <summary>Tables of the master data (#47), named as in data-model.md.</summary>
    public static IReadOnlyList<string> MasterDataTables { get; } =
        ["EMPLO", "แผนก", "แผนก2", "ฝ่าย", "SHIFT", "ชื่อลูกค้า", "SUNDAY", "HOLIDAY"];

    /// <summary>
    /// งวดที่เปิดอยู่ (#51): the one row of ตั้งวันที่ตัดWEEK and NO_WORK (20k rows), read in full — NO_WORK is filtered by the
    /// period's first day in <see cref="HistoryMigration"/>.
    /// </summary>
    public static IReadOnlyList<string> OpenPeriodTables { get; } = ["ตั้งวันที่ตัดWEEK", "NO_WORK"];

    /// <summary>ประวัติที่ย้าย (#51, ข้อตัดสิน #17): เวลาทำงานตั้งแต่ <see cref="WorkTimeFrom"/>, การลาปี 2569, ผลค่าแรงตั้งแต่ <see cref="PayFrom"/>.</summary>
    public static readonly DateOnly WorkTimeFrom = new(2025, 1, 1);
    public static readonly DateOnly LeaveFrom = new(2026, 1, 1);
    /// <summary>
    /// ผลค่าแรงของงวดที่เริ่มหรือจ่ายตั้งแต่ 1 ม.ค. 2568 (#51): from the day เวลาทำงาน starts, plus the periods of ธ.ค. 2567
    /// paid in 2568 — 12–26 ธ.ค. paid 1 ม.ค. and the งวด 2 ช่วง part 27–31 ธ.ค. paid 16 ม.ค. — so RPT-09 (by PAY_D, BR-062)
    /// and RPT-10 of 2568 match the legacy for any range from 1 ม.ค. 2568 (decided 2026-10-20).
    /// </summary>
    public static readonly DateOnly PayFrom = new(2025, 1, 1);

    /// <summary>
    /// The history tables, read only from their first date on (ADD_EW has 528,745 rows back to 2016): a row with the date
    /// blank is read too, for the DQ report. The rows left in Access are counted, as ไม่ย้ายตามกติกา.
    /// </summary>
    public static IReadOnlyDictionary<string, string> HistoryTables { get; } = new Dictionary<string, string>
    {
        ["E_WORK"] = WhereFrom("DATE", WorkTimeFrom),
        ["ADD_EW"] = WhereFrom("DATE", WorkTimeFrom),
        ["LEAVE"] = WhereFrom("FROM_D", LeaveFrom),
        ["LEAVE_ADD"] = WhereFrom("FROM_D", LeaveFrom),
        [PayHistoryMigration.CurrentTable] = WherePaidFrom(PayFrom),
        [PayHistoryMigration.HistoryTable] = WherePaidFrom(PayFrom),
    };

    private static string WhereFrom(string column, DateOnly from) => $"[{column}] >= {Literal(from)} OR [{column}] IS NULL";

    private static string WherePaidFrom(DateOnly from) => $"{WhereFrom("FROM_DATE", from)} OR [PAY_D] >= {Literal(from)}";

    private static string Literal(DateOnly date) => $"#{date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)}#";

    /// <summary>The master-data tables of <c>ผลผลิตและORDER.accdb</c> (#107), named as in data-model.md.</summary>
    public static IReadOnlyList<string> OrderMasterDataTables { get; } =
        ["เซล", "P_CUS", "P_NAME", "MC_N", "รหัสพิมพ์", "แผนก", "ประเภท", "TYPE", "MODEL", "รหัสสินค้าย่อ", "EMPO"];

    /// <summary>The master-data tables of <c>วัตถุดิบ.accdb</c> (#217), named as in data-model.md without the "T-".</summary>
    public static IReadOnlyList<string> PurchMasterDataTables { get; } =
        ["SP", "MTR_NAME", "รายชื่อแผนก", "PURC", "CREDIT", "ประเภทธุรกิจ", "เหตุผลในการขอซื้อ", "หมายเหตุ", "รหัสตัวแรกของวัตถุดิบ"];

    /// <summary>The history tables of <c>วัตถุดิบ.accdb</c> that the migration reads (#268, and the ones the tickets after it add).</summary>
    public static IReadOnlyList<string> PurchHistoryTables { get; } = ["ประเมินผู้ขาย"];

    /// <summary>ประวัติ ORDER and ประวัติการส่ง are moved from 1 Jan 2025 (#112, ข้อตัดสิน #17); ORDER ค้างส่ง (ODD) all.</summary>
    public static readonly DateOnly OrderHistoryFrom = new(2025, 1, 1);

    /// <summary>ORDER ค้างส่ง, ประวัติการรับ ORDER and ประวัติการส่งของ of <c>ผลผลิตและORDER.accdb</c> (#112): the table, and the filter on its date.</summary>
    public static IReadOnlyDictionary<string, string?> OrderHistoryTables { get; } = new Dictionary<string, string?>
    {
        ["ODD"] = null,
        ["ORDER"] = WhereFrom("OD_DATE", OrderHistoryFrom),
        ["ODDส่งออก"] = WhereFrom("DL_DATE", OrderHistoryFrom),
        // ใบรายงานผลผลิต 7 แผนก and the transferred copy (#122), from the same date
        ["P_REPไดคาสท์"] = WhereFrom("DATE", OrderHistoryFrom),
        ["P_REPตกแต่ง"] = WhereFrom("DATE", OrderHistoryFrom),
        ["P_REPอัดผ้าเบรก"] = WhereFrom("DATE", OrderHistoryFrom),
        ["P_REPฝนผ้าเบรก"] = WhereFrom("DATE", OrderHistoryFrom),
        ["P_REPผ้าคลัทช์และดิสเบรค"] = WhereFrom("DATE", OrderHistoryFrom),
        ["P_REPประกอบ"] = WhereFrom("DATE", OrderHistoryFrom),
        ["P_REPบรรจุ"] = WhereFrom("DATE", OrderHistoryFrom),
        ["P_REP"] = WhereFrom("DATE", OrderHistoryFrom),
        // สมุดสต็อกและการใช้งานแม่พิมพ์ (#126): the period now open in full (a wrong year is a DQ row), the closed ones and the cut history from the same date
        ["P_ST"] = null,
        ["P_ADDST"] = WhereFrom("DATE", OrderHistoryFrom),
        ["ต้นงวดP_ST"] = WhereFrom("DATE", OrderHistoryFrom),
        ["การใช้งานของพิมพ์0"] = null,
        ["ตันงวดการใช้งานของพิมพ์0"] = WhereFrom("DAY", OrderHistoryFrom),
    };

    /// <summary>The file the rows were read from — set when read by <see cref="ReadOrderFile"/>.</summary>
    public LegacySource? Source { get; init; }

    public IReadOnlyList<LegacyRow> this[string table]
        => tables.TryGetValue(table, out var rows) ? rows : throw new KeyNotFoundException($"ไม่พบตาราง {table} ในไฟล์ต้นทาง");

    /// <summary>Every row of the legacy table, also the ones not read because of the date filter.</summary>
    public int SourceRows(string table) => sourceRows?.TryGetValue(table, out var count) == true ? count : this[table].Count;

    /// <summary>
    /// Reads the master-data, history and open-period tables from a <b>copy</b> of <paramref name="accdbPath"/> made in a temp folder,
    /// opened read-only, so the source file is never touched (opening it in Access once changed it, see the extractor
    /// fix of 2026-10-13). Runs on the PC, never on the server (ADR 0001: ACE is not supported server-side).
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static LegacyDatabase ReadAccessFile(string accdbPath)
        => ReadCopy(accdbPath, (connection, tables, counts) =>
        {
            foreach (var table in MasterDataTables.Concat(OpenPeriodTables))
                tables[table] = Read(connection, table, null);
            foreach (var (table, where) in HistoryTables)
            {
                tables[table] = Read(connection, table, where);
                using var count = new OleDbCommand($"SELECT COUNT(*) FROM [{table}]", connection);
                counts[table] = Convert.ToInt32(count.ExecuteScalar());
            }
            foreach (var table in WarningMigration.Tables) // ใบเตือน, every year (#59)
                tables[table] = Read(connection, table, null);
        });

    /// <summary>
    /// The master-data tables of <c>ผลผลิตและORDER.accdb</c> (#107), read from a copy the same way (<see cref="ReadAccessFile"/>).
    /// The file's name, size and time are kept in <see cref="Source"/> for the report.
    /// </summary>
    /// <summary>The master-data tables of <c>วัตถุดิบ.accdb</c> (#217), read from a copy the same way.</summary>
    [SupportedOSPlatform("windows")]
    public static LegacyDatabase ReadPurchFile(string accdbPath)
        => ReadCopy(accdbPath, (connection, tables, counts) =>
        {
            foreach (var table in PurchMasterDataTables)
                tables[table] = Read(connection, table, null);
        });

    /// <summary>The history tables of <c>วัตถุดิบ.accdb</c> (#268…), read from a copy the same way.</summary>
    [SupportedOSPlatform("windows")]
    public static LegacyDatabase ReadPurchHistoryFile(string accdbPath)
        => ReadCopy(accdbPath, (connection, tables, counts) =>
        {
            foreach (var table in PurchHistoryTables)
                tables[table] = Read(connection, table, null);
        });

    [SupportedOSPlatform("windows")]
    public static LegacyDatabase ReadOrderFile(string accdbPath)
        => ReadCopy(accdbPath, (connection, tables, counts) =>
        {
            foreach (var table in OrderMasterDataTables)
                tables[table] = Read(connection, table, null);
            foreach (var (table, where) in OrderHistoryTables)
            {
                tables[table] = Read(connection, table, where);
                using var count = new OleDbCommand($"SELECT COUNT(*) FROM [{table}]", connection);
                counts[table] = Convert.ToInt32(count.ExecuteScalar());
            }
        });

    [SupportedOSPlatform("windows")]
    private static LegacyDatabase ReadCopy(string accdbPath,
        Action<OleDbConnection, Dictionary<string, IReadOnlyList<LegacyRow>>, Dictionary<string, int>> read)
    {
        if (!File.Exists(accdbPath)) throw new FileNotFoundException($"ไม่พบไฟล์ {accdbPath}", accdbPath);
        var info = new FileInfo(accdbPath);
        var source = new LegacySource(info.Name, info.Length, info.LastWriteTimeUtc, DateTime.UtcNow);
        var folder = Directory.CreateTempSubdirectory("lockte-migration-");
        try
        {
            var copy = Path.Combine(folder.FullName, "source" + Path.GetExtension(accdbPath));
            File.Copy(accdbPath, copy);
            File.SetAttributes(copy, FileAttributes.ReadOnly);
            var tables = new Dictionary<string, IReadOnlyList<LegacyRow>>(StringComparer.OrdinalIgnoreCase);
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            using (var connection = new OleDbConnection($"Provider=Microsoft.ACE.OLEDB.16.0;Data Source={copy};Mode=Read;"))
            {
                connection.Open();
                read(connection, tables, counts);
            }
            OleDbConnection.ReleaseObjectPool();
            return new LegacyDatabase(tables, counts) { Source = source };
        }
        finally
        {
            foreach (var file in folder.GetFiles()) file.Attributes = FileAttributes.Normal;
            folder.Delete(recursive: true);
        }
    }

    [SupportedOSPlatform("windows")]
    private static List<LegacyRow> Read(OleDbConnection connection, string table, string? where)
    {
        using var command = new OleDbCommand($"SELECT * FROM [{table}]{(where is null ? "" : " WHERE " + where)}", connection);
        using var reader = command.ExecuteReader();
        var rows = new List<LegacyRow>();
        while (reader.Read())
        {
            var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < reader.FieldCount; i++)
                values[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            rows.Add(new LegacyRow(values));
        }
        return rows;
    }
}
