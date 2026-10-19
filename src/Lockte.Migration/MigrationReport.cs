namespace Lockte.Migration;

/// <summary>
/// One row of รายงานข้อยกเว้น DQ (#47, NF-06): a legacy row or value that was not loaded as it is.
/// <see cref="Rule"/> is the data-model problem it is (DQ-x), or NF-06 for the other checks of the migration.
/// </summary>
public sealed record DqException(string Table, string Key, string Rule, string Problem, string Action);

/// <summary>
/// One row of รายงานตรวจยอด: legacy rows read = excluded by design + rejected (in the DQ report) + loaded, and loaded =
/// rows now in the new table (counted back through the module's public service).
/// </summary>
public sealed record TableCount(string LegacyTable, string NewTable, int SourceRows, int Excluded, int Rejected, int Loaded, int InDatabase)
{
    public bool Balanced => SourceRows == Excluded + Rejected + Loaded && Loaded == InDatabase;
}

/// <summary>What a legacy value became, and whether the code map or the legacy value itself gave it.</summary>
public sealed record CodeMapUse(string Kind, string Legacy, string Code, string? Name, string? Prefix, bool FromMap);

public sealed class MigrationReport
{
    public const string DqFile = "dq-exceptions.csv";
    public const string CountsFile = "row-counts.csv";
    public const string CodeMapFile = "code-map-used.csv";

    public const string NotLoaded = "ไม่โหลด";
    public const string LoadedBlank = "โหลดโดยเว้นช่องนี้ว่าง";

    public List<DqException> Exceptions { get; } = [];
    public List<TableCount> Tables { get; } = [];
    public List<CodeMapUse> CodeMapUsed { get; } = [];

    public bool Balanced => Tables.All(t => t.Balanced);

    internal void Add(string table, string key, string rule, string problem, string action)
        => Exceptions.Add(new DqException(table, key, rule, problem, action));

    /// <summary>Writes the three CSV files into <paramref name="folder"/> (UTF-8 with BOM for Excel).</summary>
    public void WriteTo(string folder)
    {
        Directory.CreateDirectory(folder);
        Csv.Write(Path.Combine(folder, DqFile), ["ตาราง", "รายการ", "ปัญหา DQ", "รายละเอียด", "การจัดการ"],
            Exceptions.Select(e => new object?[] { e.Table, e.Key, e.Rule, e.Problem, e.Action }));
        Csv.Write(Path.Combine(folder, CountsFile),
            ["ตารางเดิม", "ตารางใหม่", "แถวต้นทาง", "ไม่ย้ายตามกติกา", "ข้อยกเว้น DQ", "โหลด", "ในฐานข้อมูล", "ตรง"],
            Tables.Select(t => new object?[]
                { t.LegacyTable, t.NewTable, t.SourceRows, t.Excluded, t.Rejected, t.Loaded, t.InDatabase, t.Balanced ? "ตรง" : "ไม่ตรง" }));
        Csv.Write(Path.Combine(folder, CodeMapFile), [.. CodeMap.Columns, "source"],
            CodeMapUsed.Select(u => new object?[]
                { u.Kind, u.Legacy, u.Code, u.Name, u.Prefix, u.FromMap ? "ตารางจับคู่" : "ค่าเดิม" }));
    }
}
