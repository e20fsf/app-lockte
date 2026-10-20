namespace Lockte.Hr;

/// <summary>RPT-12 รายงานพนักงานที่มาทำงานสาย (รายงาน มาสาย1): เวลาทำงาน rows with a เวลาเข้าจริง (LATE).</summary>
/// <param name="EmployeeCode">null = ทุกคน (the legacy EMPID LIKE "*").</param>
public sealed record LateReport(DateOnly From, DateOnly To, string? EmployeeCode, IReadOnlyList<LateReportLine> Lines)
{
    /// <summary>Σ นาที (=Sum(Minute([LATE]))).</summary>
    public int TotalMinutes => Lines.Sum(l => l.Minutes);
}

/// <summary>
/// A line of RPT-12: วันที่ รหัส ชื่อ-นามสกุล, เวลาเข้า (the LATE time, as printed) and นาที = ⚠ the minute part of LATE
/// (=Minute([LATE])), not the minutes after the shift's start.
/// </summary>
public sealed record LateReportLine(DateOnly Date, string EmployeeCode, string EmployeeName, TimeOnly Late)
{
    public int Minutes => Late.Minute;
}

/// <summary>
/// RPT-13 สรุปชั่วโมงทำงานปกติและชั่วโมงทำงานล่วงเวลาประจำเดือน (รายงาน สรุปชั่วโมงการทำงาน2): the working employees of one แผนก
/// with their วันทำงาน and OT hours of the งวดค่าแรง within <see cref="From"/>–<see cref="To"/>.
/// </summary>
/// <param name="Employees">จำนวนพนักงาน (QTY): the แผนก's working employees, editable on the form as before.</param>
/// <param name="WorkDays">จำนวนวันทำงานทั้งหมด (TT_D) typed on the form.</param>
public sealed record WorkHoursReport(DateOnly From, DateOnly To, string DepartmentName, int Employees, int? WorkDays,
    IReadOnlyList<WorkHoursLine> Lines)
{
    public double Days => Lines.Sum(l => l.Days);
    public double Hours => Lines.Sum(l => l.Hours);
    public double OvertimeHours => Lines.Sum(l => l.OvertimeHours);

    /// <summary>%OT of the footer: Σ ชม.ล่วงเวลา / Σ วัน / 8 × 100; null when there are no days.</summary>
    public double? OvertimePercent => Days == 0 ? null : OvertimeHours / Days / 8 * 100;
}

/// <summary>
/// A line of RPT-13: Σ W (จำนวนวันทำงาน), W × 8 (จำนวนชม.ทำงาน), Σ (TT_OT + OT) (จำนวนชม.ล่วงเวลา) and %OT.
/// </summary>
public sealed record WorkHoursLine(string EmployeeCode, string EmployeeName, double Days, double OvertimeHours)
{
    public double Hours => Days * 8;

    /// <summary>ล่วงเวลา / วัน / 8 × 100; null when there are no days (Access printed #Div/0!).</summary>
    public double? OvertimePercent => Days == 0 ? null : OvertimeHours / Days / 8 * 100;
}

/// <summary>RPT-14 รายงานพนักงานทำงานล่วงเวลาจริง (รายงาน ทำล่วงเวลาจริง): the เวลาทำงาน rows of one day that are not W.</summary>
public sealed record OvertimeReport(DateOnly Date, IReadOnlyList<OvertimeReportLine> Lines);

/// <summary>A line of RPT-14: วันที่ รหัส ชื่อ-นามสกุล แผนก เข้า ออก.</summary>
public sealed record OvertimeReportLine(DateOnly Date, string EmployeeCode, string EmployeeName, string? DepartmentName, TimeOnly? WorkIn,
    TimeOnly? WorkOut);

/// <summary>RPT-15 รายงานการมาทำงานของพนักงาน (รายงาน รายงานการมาทำงาน): the W rows within the dates.</summary>
/// <param name="EmployeeCode">null = ทุกคน.</param>
public sealed record AttendanceReport(DateOnly From, DateOnly To, string? EmployeeCode, IReadOnlyList<AttendanceReportLine> Lines)
{
    /// <summary>รวมทั้งหมด … วัน (=Sum([work_t]); a row without both times counts nothing).</summary>
    public double TotalDays => Lines.Sum(l => l.Days ?? 0);
}

/// <summary>A line of RPT-15: วันที่ รหัส ชื่อ-นามสกุล เวลาเข้า เวลาออก จำนวนวัน (WORK_T of BR-001) แผนก.</summary>
public sealed record AttendanceReportLine(DateOnly Date, string EmployeeCode, string EmployeeName, TimeOnly? WorkIn, TimeOnly? WorkOut,
    double? Days, string? DepartmentName);
