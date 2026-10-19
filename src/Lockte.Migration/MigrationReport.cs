using System.Globalization;

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

/// <summary>
/// One line of ยอดสรุปรายเดือน (AC-10): a measure of one month in the legacy table as read (<see cref="Source"/>), what
/// should be loaded after the DQ report (<see cref="Expected"/>), and what the new table holds. Source − Expected is
/// explained by the DQ report; Expected must equal InDatabase.
/// </summary>
public sealed record MonthlyTotal(string Table, string Month, string Measure, double Source, double Expected, double InDatabase)
{
    public bool Balanced => MigrationReport.Same(Expected, InDatabase);
}

/// <summary>A migrated เวลาทำงาน row whose WORK_T (BR-001) is not the legacy WORK_T at 6 decimals (AC-01).</summary>
public sealed record WorkTMismatch(string Table, string EmployeeCode, DateOnly Date, string? Type, TimeOnly WorkIn, TimeOnly WorkOut,
    int RestMinutes, double Legacy, double New);

/// <summary>What a legacy value became, and whether the code map or the legacy value itself gave it.</summary>
public sealed record CodeMapUse(string Kind, string Legacy, string Code, string? Name, string? Prefix, bool FromMap);

public sealed class MigrationReport
{
    public const string DqFile = "dq-exceptions.csv";
    public const string CountsFile = "row-counts.csv";
    public const string CodeMapFile = "code-map-used.csv";
    public const string MonthlyFile = "monthly-totals.csv";
    public const string WorkTFile = "work-t-check.csv";

    public const string NotLoaded = "ไม่โหลด";
    public const string LoadedBlank = "โหลดโดยเว้นช่องนี้ว่าง";

    public List<DqException> Exceptions { get; } = [];
    public List<TableCount> Tables { get; } = [];
    public List<CodeMapUse> CodeMapUsed { get; } = [];
    public List<MonthlyTotal> MonthlyTotals { get; } = [];

    /// <summary>AC-01: migrated rows whose legacy WORK_T was compared (rows of the open period have none yet).</summary>
    public int WorkTChecked { get; internal set; }
    public List<WorkTMismatch> WorkTMismatches { get; } = [];

    /// <summary>Every table counted back and every monthly total of 2026 as expected (AC-10).</summary>
    public bool Balanced => Tables.All(t => t.Balanced) && MonthlyTotals.All(m => m.Balanced);

    /// <summary>AC-01 on the migrated rows.</summary>
    public bool WorkTMatches => WorkTMismatches.Count == 0;

    /// <summary>The same at 6 decimals: less than half a millionth apart, so float noise on a rounding edge does not count.</summary>
    public static bool Same(double a, double b) => Math.Abs(a - b) < 0.0000005;

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
        Csv.Write(Path.Combine(folder, MonthlyFile),
            ["ตาราง", "เดือน", "ยอด", "ต้นทาง", "ต้นทางหลังข้อยกเว้น DQ", "ในฐานข้อมูล", "ตรง"],
            MonthlyTotals.Select(m => new object?[]
                { m.Table, m.Month, m.Measure, Number(m.Source), Number(m.Expected), Number(m.InDatabase), m.Balanced ? "ตรง" : "ไม่ตรง" }));
        Csv.Write(Path.Combine(folder, WorkTFile),
            ["ตาราง", "รหัส", "วันที่", "ประเภท", "เวลาเข้า", "เวลาออก", "นาทีพัก", "WORK_T เดิม", "WORK_T ใหม่"],
            WorkTMismatches.Select(m => new object?[]
                { m.Table, m.EmployeeCode, m.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), m.Type,
                  m.WorkIn.ToString("HH:mm:ss", CultureInfo.InvariantCulture), m.WorkOut.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
                  m.RestMinutes, Number(m.Legacy), Number(m.New) })
                .Prepend([$"ตรวจ {WorkTChecked} แถว", $"ไม่ตรง {WorkTMismatches.Count} แถว", null, null, null, null, null, null, null]));
    }

    private static string Number(double value) => value.ToString("0.######", CultureInfo.InvariantCulture);
}
