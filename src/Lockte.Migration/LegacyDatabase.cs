using System.Data.OleDb;
using System.Runtime.Versioning;

namespace Lockte.Migration;

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

    public double? Number(string column) => this[column] is { } value ? Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture) : null;
}

/// <summary>
/// The tables of <c>พนักงาน.accdb</c> that the migration reads, in the order Access returns their rows (the legacy
/// tables have no primary key, so this order is what keeps two runs on the same file alike).
/// </summary>
public sealed class LegacyDatabase(IReadOnlyDictionary<string, IReadOnlyList<LegacyRow>> tables)
{
    /// <summary>Tables of the master data (#47), named as in data-model.md.</summary>
    public static IReadOnlyList<string> MasterDataTables { get; } =
        ["EMPLO", "แผนก", "แผนก2", "ฝ่าย", "SHIFT", "ชื่อลูกค้า", "SUNDAY", "HOLIDAY"];

    public IReadOnlyList<LegacyRow> this[string table]
        => tables.TryGetValue(table, out var rows) ? rows : throw new KeyNotFoundException($"ไม่พบตาราง {table} ในไฟล์ต้นทาง");

    /// <summary>
    /// Reads <paramref name="tableNames"/> from a <b>copy</b> of <paramref name="accdbPath"/> made in a temp folder,
    /// opened read-only, so the source file is never touched (opening it in Access once changed it, see the extractor
    /// fix of 2026-10-13). Runs on the PC, never on the server (ADR 0001: ACE is not supported server-side).
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static LegacyDatabase ReadAccessFile(string accdbPath, IEnumerable<string>? tableNames = null)
    {
        if (!File.Exists(accdbPath)) throw new FileNotFoundException($"ไม่พบไฟล์ {accdbPath}", accdbPath);
        var folder = Directory.CreateTempSubdirectory("lockte-migration-");
        try
        {
            var copy = Path.Combine(folder.FullName, "source" + Path.GetExtension(accdbPath));
            File.Copy(accdbPath, copy);
            File.SetAttributes(copy, FileAttributes.ReadOnly);
            var tables = new Dictionary<string, IReadOnlyList<LegacyRow>>(StringComparer.OrdinalIgnoreCase);
            using (var connection = new OleDbConnection($"Provider=Microsoft.ACE.OLEDB.16.0;Data Source={copy};Mode=Read;"))
            {
                connection.Open();
                foreach (var table in tableNames ?? MasterDataTables)
                    tables[table] = Read(connection, table);
            }
            OleDbConnection.ReleaseObjectPool();
            return new LegacyDatabase(tables);
        }
        finally
        {
            foreach (var file in folder.GetFiles()) file.Attributes = FileAttributes.Normal;
            folder.Delete(recursive: true);
        }
    }

    [SupportedOSPlatform("windows")]
    private static List<LegacyRow> Read(OleDbConnection connection, string table)
    {
        using var command = new OleDbCommand($"SELECT * FROM [{table}]", connection);
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
