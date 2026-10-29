using System.Text;

namespace Lockte.Migration;

/// <summary>Kinds of legacy values the code map can translate (#47).</summary>
public static class CodeKinds
{
    /// <summary>T-แผนก.DEPART / EMPLO.DEPARTMENT / T-แผนก2.DEPART (a name, DQ-5) → รหัสแผนก + ชื่อสะกดมาตรฐาน + ตัวอักษรนำหน้า.</summary>
    public const string Department = "department";
    /// <summary>T-ชื่อลูกค้า.CODE (= E_WORK.MARK) → รหัสลูกค้า in the register of module ขายและส่งของ + ชื่อ.</summary>
    public const string Customer = "customer";
    /// <summary>T-SHIFT.SHIFT / EMPLO.SHIFT / T-แผนก.SHIFT → รหัสกะ A–G.</summary>
    public const string Shift = "shift";

    /// <summary>
    /// T-EMPO.ID (พนักงานฝ่ายผลิต of ผลผลิตและORDER, #107) → รหัสพนักงาน in the HR register; code <c>-</c> = decided not to link.
    /// With a duplicated ID (<c>B179</c>) the name column says which of the rows is kept.
    /// </summary>
    public const string ProductionEmployee = "production-employee";
    /// <summary>
    /// T-เซล.ID (เขตการขาย, #107) → รหัสพนักงาน of its พนักงานขาย in the HR register; code <c>-</c> = the territory has no
    /// พนักงานขาย (00 ออฟฟิศ, 11 เซลออนไลน์).
    /// </summary>
    public const string SalesRepresentative = "sales-representative";

    /// <summary>
    /// T-ประเมินผู้ขาย.SUP (ชื่อผู้ขาย, #268) → รหัสผู้ขายในทะเบียนวัตถุดิบ, for a name that no supplier has or that more than one has. The map holds real names: keep it outside git.
    /// </summary>
    public const string SupplierName = "supplier-name";

    /// <summary>The code of a decision "link nobody" (<see cref="ProductionEmployee"/>, <see cref="SalesRepresentative"/>).</summary>
    public const string Nobody = "-";

    public static IReadOnlyList<string> All { get; } = [Department, Customer, Shift, ProductionEmployee, SalesRepresentative, SupplierName];
}

/// <summary>
/// One line of the code map: <see cref="Legacy"/> is the value as written in Access (compared trimmed, ignoring
/// case). Blank <see cref="Code"/> / <see cref="Name"/> / <see cref="Prefix"/> keep what the tool would use anyway.
/// </summary>
public sealed record CodeMapEntry(string Kind, string Legacy, string? Code, string? Name, string? Prefix);

/// <summary>
/// ตารางจับคู่รหัส (CSV, #47): for department, customer and shift values that are malformed or clash in the legacy
/// file. Columns <c>kind,legacy,code,name,prefix</c>; other columns are ignored, so the <c>code-map-used.csv</c> a run
/// writes can be edited and given back as the next run's map. Lines starting with <c>#</c> are comments.
/// </summary>
public sealed class CodeMap
{
    public static readonly string[] Columns = ["kind", "legacy", "code", "name", "prefix"];

    private readonly Dictionary<(string Kind, string Legacy), CodeMapEntry> entries = [];

    public static CodeMap Empty { get; } = new([]);

    public CodeMap(IEnumerable<CodeMapEntry> lines)
    {
        foreach (var line in lines)
        {
            if (!CodeKinds.All.Contains(line.Kind))
                throw new FormatException($"ตารางจับคู่รหัส: kind '{line.Kind}' ไม่รู้จัก (ใช้ {string.Join(", ", CodeKinds.All)})");
            if (!entries.TryAdd(Key(line.Kind, line.Legacy), line))
                throw new FormatException($"ตารางจับคู่รหัส: {line.Kind} '{line.Legacy}' มีมากกว่าหนึ่งบรรทัด");
        }
    }

    public CodeMapEntry? Find(string kind, string? legacy)
        => legacy is null ? null : entries.GetValueOrDefault(Key(kind, legacy));

    public static CodeMap Read(string path)
    {
        var rows = Csv.Parse(File.ReadAllText(path, Encoding.UTF8));
        if (rows.Count == 0) return Empty;
        var header = rows[0].Select(h => h.Trim().ToLowerInvariant()).ToList();
        var index = Columns.ToDictionary(c => c, c => header.IndexOf(c));
        if (index["kind"] < 0 || index["legacy"] < 0)
            throw new FormatException($"ตารางจับคู่รหัส {path}: บรรทัดแรกต้องเป็นหัวคอลัมน์ {string.Join(",", Columns)}");
        string? Cell(IReadOnlyList<string> row, string column)
            => index[column] is var i and >= 0 && i < row.Count && !string.IsNullOrWhiteSpace(row[i]) ? row[i].Trim() : null;
        return new CodeMap(rows.Skip(1)
            .Where(r => r.Count > 0 && !(r[0].TrimStart().StartsWith('#')) && r.Any(c => !string.IsNullOrWhiteSpace(c)))
            .Select(r => new CodeMapEntry(
                Cell(r, "kind")?.ToLowerInvariant() ?? "",
                Cell(r, "legacy") ?? throw new FormatException($"ตารางจับคู่รหัส {path}: บรรทัดที่ legacy ว่าง"),
                Cell(r, "code"), Cell(r, "name"), Cell(r, "prefix"))));
    }

    private static (string, string) Key(string kind, string legacy) => (kind, legacy.Trim().ToUpperInvariant());
}

/// <summary>RFC 4180 CSV, UTF-8 with BOM so Excel shows Thai.</summary>
public static class Csv
{
    public static List<List<string>> Parse(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var cell = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                else if (c == '"') quoted = false;
                else cell.Append(c);
            }
            else if (c == '"') quoted = true;
            else if (c == ',') { row.Add(cell.ToString()); cell.Clear(); }
            else if (c is '\r' or '\n')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                row.Add(cell.ToString()); cell.Clear();
                rows.Add(row); row = [];
            }
            else cell.Append(c);
        }
        if (cell.Length > 0 || row.Count > 0) { row.Add(cell.ToString()); rows.Add(row); }
        return rows;
    }

    public static void Write(string path, IEnumerable<string> header, IEnumerable<IEnumerable<object?>> rows)
    {
        var text = new StringBuilder();
        Line(text, header);
        foreach (var row in rows) Line(text, row);
        File.WriteAllText(path, text.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    private static void Line(StringBuilder text, IEnumerable<object?> cells)
    {
        text.AppendJoin(',', cells.Select(c => Escape(Convert.ToString(c, System.Globalization.CultureInfo.InvariantCulture) ?? "")));
        text.Append("\r\n");
    }

    private static string Escape(string value)
        => value.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
}
