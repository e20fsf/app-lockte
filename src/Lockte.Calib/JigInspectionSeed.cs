using System.Globalization;
using System.Reflection;

namespace Lockte.Calib;

/// <summary>ชนิดแบบตรวจ JIG: RPT-05 วัดศูนย์ก้ามเบรก (รวม PIN/TAPER GAUGE) หรือ RPT-06 กรอก้ามเบรก.</summary>
public enum JigSheetType
{
    ZeroCheck = 1,
    Grinding = 2,
}

/// <summary>ชนิดจุดวัด (print-samples): Ø Diameter, Wide, Dimension, Length.</summary>
public enum JigPointKind
{
    Diameter = 1,
    Wide = 2,
    Dimension = 3,
    Length = 4,
}

/// <summary>พิกัดของค่าที่กำหนด: ± x mm. or "มากกว่า" (<c>&gt;15.00</c>, no ±).</summary>
public enum JigToleranceKind
{
    PlusMinus = 1,
    GreaterThan = 2,
}

/// <summary>A เลขเอกสารควบคุม of a sheet as the program of reports seeds it: its key, name, code and edition as printed.</summary>
public sealed record JigDocumentSeed(string Key, string Name, string Code, string Edition);

public sealed record JigPointSeed(string Label, JigPointKind Kind, decimal Value, JigToleranceKind Tolerance, decimal? ToleranceValue);

public sealed record JigSheetSeed(JigSheetType Type, string ModelKey, string Title, int DocumentNo, IReadOnlyList<JigPointSeed> Points)
{
    public string DocumentKey => JigInspectionSeed.DocumentKey(Type, DocumentNo);
}

/// <summary>
/// The starting data of the inspection sheets, read from <c>Seed/jig-inspection-sheets.tsv</c> (embedded): the values of reports.md RPT-05/06 and
/// print-samples.md. The first migration loads it; after that QA owns the values.
/// </summary>
public static class JigInspectionSeed
{
    /// <summary>The tolerance every ± point printed (± 0.20 mm.).</summary>
    public const decimal DefaultTolerance = 0.20m;

    /// <summary>The key of a sheet's เลขเอกสารควบคุม: <c>JIG-Z-4</c> for WI-QA049-4/2550, <c>JIG-G-35</c> for WI-QA049-35/2560.</summary>
    public static string DocumentKey(JigSheetType type, int documentNo)
        => $"JIG-{(type == JigSheetType.ZeroCheck ? "Z" : "G")}-{documentNo.ToString(CultureInfo.InvariantCulture)}";

    public static IReadOnlyList<JigSheetSeed> Sheets { get; } = Load();

    /// <summary>The 42 numbers: <c>WI-QA049-n/2550</c> (วัดศูนย์) and <c>WI-QA049-n/2560</c> (กรอ), both ฉบับที่ 2.</summary>
    public static IReadOnlyList<JigDocumentSeed> Documents { get; } = Sheets.Select(s => new JigDocumentSeed(
        s.DocumentKey,
        $"{(s.Type == JigSheetType.ZeroCheck ? "ใบตรวจ JIG วัดศูนย์" : "ใบตรวจ JIG กรอ")} {s.ModelKey}",
        $"WI-QA049-{s.DocumentNo.ToString(CultureInfo.InvariantCulture)}/{(s.Type == JigSheetType.ZeroCheck ? "2550" : "2560")}",
        " ฉบับที่ 2")).ToList();

    private static IReadOnlyList<JigSheetSeed> Load()
    {
        var assembly = typeof(JigInspectionSeed).Assembly;
        var name = assembly.GetManifestResourceNames().Single(n => n.EndsWith("jig-inspection-sheets.tsv", StringComparison.Ordinal));
        using var reader = new StreamReader(assembly.GetManifestResourceStream(name)!);
        var sheets = new List<JigSheetSeed>();
        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0 || line[0] == '#') continue;
            var cells = line.Split('\t');
            var points = cells[4].Split('|').Select(p =>
            {
                var part = p.Split(':');
                var kind = part[1] switch { "D" => JigPointKind.Diameter, "W" => JigPointKind.Wide, "M" => JigPointKind.Dimension, _ => JigPointKind.Length };
                var tolerance = part[3] == "gt" ? JigToleranceKind.GreaterThan : JigToleranceKind.PlusMinus;
                return new JigPointSeed(part[0], kind, decimal.Parse(part[2], CultureInfo.InvariantCulture), tolerance,
                    tolerance == JigToleranceKind.PlusMinus ? DefaultTolerance : null);
            }).ToList();
            sheets.Add(new JigSheetSeed(cells[0] == "Z" ? JigSheetType.ZeroCheck : JigSheetType.Grinding, cells[1], cells[2],
                int.Parse(cells[3], CultureInfo.InvariantCulture), points));
        }
        return sheets;
    }
}
