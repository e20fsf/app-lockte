namespace Lockte.Hr;

/// <summary>ประเภทการทำงาน (TYPE of เวลาทำงาน, T-ประเภทการทำงาน).</summary>
public static class WorkTypes
{
    /// <summary>เวลาปกติ — the type BR-010 creates.</summary>
    public const string Normal = "W";

    /// <summary>OT วันทำงาน — with <see cref="Normal"/> the only types the ลบ buttons of SCR-13 take (BR-015).</summary>
    public const string Overtime = "O1";

    /// <summary>วันอาทิตย์ / OT วันอาทิตย์ / วันหยุด / OT วันหยุด (ชั่วโมง, BR-002).</summary>
    public const string Sunday = "S", SundayOvertime = "O2", Holiday = "H", HolidayOvertime = "O3";

    public const int Length = 2;

    public static IReadOnlyList<string> Codes { get; } = [Normal, Overtime, Sunday, SundayOvertime, Holiday, HolidayOvertime];
}

/// <summary>A ไม่มาทำงาน row of SCR-10, with the employee's code and name.</summary>
public sealed record AbsenceDto(int Id, DateOnly Date, int EmployeeId, string EmployeeCode, string EmployeeName,
    TimeOnly WorkIn, string? Remark, byte[] RowVersion);

/// <summary>
/// A ไม่มาทำงาน row to save (SCR-10, BR-011). <see cref="Id"/> null = new; editing needs <see cref="RowVersion"/> as
/// loaded. A blank <see cref="WorkIn"/> takes 08:00, the value on every legacy row.
/// </summary>
public sealed record AbsenceInput(int? Id, DateOnly? Date, int? EmployeeId, TimeOnly? WorkIn, string? Remark, byte[]? RowVersion);

/// <summary>
/// รายงานไม่มาทำงานประจำวัน (RPT-17, BR-061) for <see cref="Date"/>: that day's day shift and the night shift that
/// started the day before, by date then code, against all พนักงานที่ทำงานอยู่.
/// </summary>
public sealed record AbsenceReport(DateOnly Date, IReadOnlyList<AbsenceReportLine> Lines, int WorkingEmployees)
{
    /// <summary>รวม … คน.</summary>
    public int Absent => Lines.Count;
    /// <summary>มาทำงาน … คน (qty − Count).</summary>
    public int Present => WorkingEmployees - Absent;
    /// <summary>คิดเป็น % (Count / qty × 100, shown with 2 decimals); 0 when nobody is working.</summary>
    public double AbsentPercent => WorkingEmployees == 0 ? 0 : (double)Absent / WorkingEmployees * 100;
}

/// <summary>A line of RPT-17: วันที่ รหัส ชื่อ-นามสกุล แผนก เหตุผล.</summary>
public sealed record AbsenceReportLine(DateOnly Date, string EmployeeCode, string EmployeeName, string? DepartmentName, string? Remark);

/// <summary>A เวลาทำงาน row with the employee's code and name.</summary>
public sealed record WorkTimeDto(int Id, DateOnly Date, int EmployeeId, string EmployeeCode, string EmployeeName, string? Type,
    TimeOnly? WorkIn, TimeOnly? WorkOut, TimeOnly? Late, int RestMinutes, string? Mark, byte[] RowVersion)
{
    /// <summary>The work ends the next day (e.g. กะ B 20:10–05:10).</summary>
    public bool CrossesMidnight => WorkIn is { } i && WorkOut is { } o && o < i;

    /// <summary>ชั่วโมงทำงาน (BR-001); blank without both times.</summary>
    public double? Hours => WorkIn is { } i && WorkOut is { } o ? WorkDuration.Hours(i, o, RestMinutes) : null;

    /// <summary>WORK_T (BR-001): W = วัน, other types = ชั่วโมง; blank without both times.</summary>
    public double? WorkT => WorkIn is { } i && WorkOut is { } o ? WorkDuration.WorkT(Type, i, o, RestMinutes) : null;
}

/// <summary>
/// A เวลาทำงาน row to save by hand (SCR-12/13, BR-013). <see cref="Id"/> null = new; editing needs
/// <see cref="RowVersion"/> as loaded. <see cref="WorkOut"/> earlier than <see cref="WorkIn"/> = the next day.
/// </summary>
public sealed record WorkTimeInput(int? Id, DateOnly? Date, int? EmployeeId, string? Type, TimeOnly? WorkIn, TimeOnly? WorkOut,
    int RestMinutes, TimeOnly? Late, string? Mark, byte[]? RowVersion);
