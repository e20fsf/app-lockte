namespace Lockte.Hr;

/// <summary>ระดับใบเตือน (CODE of ใบเตือน, T-ชื่อใบเตือน): codes 1–4 with their legacy names (CODE_NAME).</summary>
public static class WarningLevels
{
    public const int Length = 1;

    public const string Verbal = "1";
    public const string Written = "2";

    public static IReadOnlyDictionary<string, string> Names { get; } = new Dictionary<string, string>
    {
        [Verbal] = "ใบเตือนด้วยวาจา",
        [Written] = "ใบเตือนเป็นหนังสือ",
        ["3"] = "พักงานโดยไม่จ่ายค่าจ้าง ไม่เกิน 7 วัน",
        ["4"] = "เลิกจ้าง",
    };

    public static IReadOnlyList<string> Codes { get; } = [.. Names.Keys];

    public static string? NameOf(string? level) => level is null ? null : Names.GetValueOrDefault(level);
}

/// <summary>A ใบเตือน with its employee and แผนกตอนบันทึก (subform ใบเตือน1 of SCR-32).</summary>
public sealed record WarningDto(int Id, DateOnly Date, int EmployeeId, string EmployeeCode, string EmployeeName, int? DepartmentId,
    string? DepartmentName, string? Level, string? Remark, byte[] RowVersion)
{
    public string? LevelName => WarningLevels.NameOf(Level);
}

/// <summary>
/// A ใบเตือน to save (SCR-32). <see cref="Id"/> null = new; editing needs <see cref="RowVersion"/> as loaded. The แผนก is
/// taken from the employee when the warning is new or its employee changes, as the legacy form filled DEPART.
/// </summary>
public sealed record WarningInput(int? Id, DateOnly? Date, int? EmployeeId, string? Level, string? Remark, byte[]? RowVersion);

/// <summary>RPT-30 รายงานพนักงานที่ได้รับใบเตือน: the warnings of <see cref="From"/>–<see cref="To"/>.</summary>
public sealed record WarningReport(DateOnly From, DateOnly To, IReadOnlyList<WarningReportLine> Lines);

/// <summary>A line of RPT-30: วันที่ รหัส ชื่อ-นามสกุล แผนก ใบเตือน เรื่อง.</summary>
public sealed record WarningReportLine(DateOnly Date, string EmployeeCode, string EmployeeName, string? DepartmentName, string? LevelName,
    string? Remark);
