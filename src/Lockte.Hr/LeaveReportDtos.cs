namespace Lockte.Hr;

/// <summary>
/// RPT-31 รายงานการลางานแผนก (มาโคร ลางานเฉพาะแผนก → รายงาน พิมพ์ลางานทั้งแผนก1): every employee of the แผนก who has not
/// resigned, with วันลา by SR 1–16 of the leaves within <see cref="From"/>–<see cref="To"/>.
/// </summary>
/// <param name="DepartmentName">null = ทุกแผนก (the legacy DEP "*", printed "ทั้งหมด").</param>
/// <param name="Employees">จำนวนคน (EMP_QTY): filled from the แผนก's working employees, editable on the form as before.</param>
/// <param name="WorkDays">จำนวนวันทำงาน (DATE_QTY) typed on the form; blank leaves the header and the % row blank.</param>
public sealed record DepartmentLeaveReport(DateOnly From, DateOnly To, string? DepartmentName, int Employees, int? WorkDays,
    IReadOnlyList<DepartmentLeaveLine> Lines)
{
    /// <summary>The 16 columns, in the order of T-ประเภทการลางาน.</summary>
    public static IReadOnlyList<string> Types => LeaveTypes.Codes;

    /// <summary>
    /// ทุกแผนก (#355): the lines in groups by แผนก (by name), for the per-แผนก totals above the รวมทั้งหมด of the company; one group when the
    /// report is of a single แผนก or the lines have no แผนก.
    /// </summary>
    public IReadOnlyList<IGrouping<string?, DepartmentLeaveLine>> ByDepartment
        => DepartmentName is null && Lines.Any(l => l.DepartmentName is not null)
            ? [.. Lines.GroupBy(l => l.DepartmentName).OrderBy(g => g.Key, StringComparer.Ordinal)]
            : [];

    /// <summary>Σ of one SR over the lines.</summary>
    public double TotalOf(string type) => Lines.Sum(l => l.DaysOf(type));

    /// <summary>รวม of the footer: Σ SR 1–15 (as the line total).</summary>
    public double Total => Lines.Sum(l => l.Total);

    /// <summary>คิดเป็นเปอร์เซ็นต์: Σ / EMP_QTY / DATE_QTY × 100; null without จำนวนวันทำงาน or คน.</summary>
    public double? Percent(double days) => PercentOf(days, Employees);

    /// <summary>The same share for <paramref name="employees"/> people — the headcount of one แผนก in the report of ทุกแผนก (#355).</summary>
    public double? PercentOf(double days, int employees) => WorkDays is > 0 && employees > 0 ? days / employees / WorkDays.Value * 100 : null;
}

/// <summary>A line of RPT-31: รหัส ชื่อ-นามสกุล เริ่มทำงาน, วันลา by SR (0 when none) and รวม = SR 1–15 (Expr1, not 16).</summary>
/// <param name="DepartmentName">The employee's แผนก (#355): the report of ทุกแผนก is grouped and totalled by it.</param>
public sealed record DepartmentLeaveLine(string EmployeeCode, string EmployeeName, DateOnly? StartDate, IReadOnlyDictionary<string, double> Days,
    string? DepartmentName = null)
{
    public double DaysOf(string type) => Days.GetValueOrDefault(type);

    public double Total => LeaveReportDirectory.TotalTypes.Sum(DaysOf);
}

/// <summary>RPT-31 ปุ่ม "พิมพ์ลางานตามวันที่" (รายงาน พิมพ์ลางานทั้งแผนก100): the leave rows themselves, by รหัส.</summary>
public sealed record LeaveListReport(DateOnly From, DateOnly To, string? DepartmentName, IReadOnlyList<LeaveListLine> Lines);

/// <summary>A line of พิมพ์ลางานทั้งแผนก100: รหัส ชื่อ-นามสกุล จำนวนวัน ประเภท (the SR code as printed) แผนก.</summary>
public sealed record LeaveListLine(string EmployeeCode, string EmployeeName, double Days, string Type, string? DepartmentName);

/// <summary>
/// RPT-32 การลางานของพนักงาน (รายงาน ลางานรายคน): the employee's leaves with FROM_D within <see cref="From"/>–<see cref="To"/>
/// (the legacy printed its current table, the year since ย้ายข้อมูลการลางาน; one table for every year, #41).
/// </summary>
/// <param name="DailyWage">ค่าแรง/วัน (SA_DAY) of the employee now.</param>
public sealed record EmployeeLeaveReport(DateOnly From, DateOnly To, string EmployeeCode, string EmployeeName, DateOnly? StartDate,
    double DailyWage, IReadOnlyList<EmployeeLeaveLine> Lines)
{
    public double TotalDays => Lines.Sum(l => l.Days);
    public double TotalPay => Lines.Sum(l => l.Pay);
}

/// <summary>A line of RPT-32: วัน ตั้งแต่วันที่ ถึงวันที่ เวลา ถึงเวลา วันลา ประเภท ค่าแรง (ได้/ไม่ได้) จ่ายค่าแรง เหตุผล.</summary>
public sealed record EmployeeLeaveLine(DateOnly FromDate, DateOnly ToDate, TimeOnly FromTime, TimeOnly ToTime, double Days, string TypeName,
    bool Paid, double Pay, string? Remark)
{
    /// <summary>"วันจันทร์" … "วันอาทิตย์" of FROM_D.</summary>
    public string DayName => FromDate.DayOfWeek switch
    {
        DayOfWeek.Monday => "วันจันทร์",
        DayOfWeek.Tuesday => "วันอังคาร",
        DayOfWeek.Wednesday => "วันพุธ",
        DayOfWeek.Thursday => "วันพฤหัสบดี",
        DayOfWeek.Friday => "วันศุกร์",
        DayOfWeek.Saturday => "วันเสาร์",
        _ => "วันอาทิตย์",
    };

    /// <summary>T-YES,NO.NAMED.</summary>
    public string PaidName => Paid ? "ได้" : "ไม่ได้";
}

/// <summary>
/// RPT-33 สรุปการลางานของพนักงานเดือน… (มาโคร พิมพ์สรุปการลางาน → รายงาน พิมพ์สรุปการลางาน21): per แผนก, วันลา by SR
/// 1–15 of the leaves within <see cref="From"/>–<see cref="To"/>, by รวม มาก→น้อย.
/// </summary>
/// <param name="WorkDays">จำนวนวันทำงาน (DAY) typed on the form; blank leaves the % columns blank.</param>
/// <param name="PayByType">รวมจ่ายค่าแรง of each SR (ท้ายรายงาน 1Y–15Y) — of every employee, whatever the แผนก.</param>
public sealed record LeaveSummaryReport(DateOnly From, DateOnly To, int? WorkDays, IReadOnlyList<LeaveSummaryLine> Lines,
    IReadOnlyDictionary<string, double> PayByType)
{
    /// <summary>The 15 columns (SR 16 is not on this report).</summary>
    public static IReadOnlyList<string> Types => LeaveReportDirectory.TotalTypes;

    public int Employees => Lines.Sum(l => l.Employees);
    public double TotalOf(string type) => Lines.Sum(l => l.DaysOf(type));
    public double Total => Lines.Sum(l => l.Total);
    public double Pay => Lines.Sum(l => l.Pay);
    public double PayOf(string type) => PayByType.GetValueOrDefault(type);

    /// <summary>% การลา: days / จำนวนพนักงาน / DAY × 100; null without DAY.</summary>
    public double? Percent(double days, int employees) => WorkDays is > 0 && employees > 0 ? days / employees / WorkDays.Value * 100 : null;
}

/// <summary>A line of RPT-33: แผนก, จำนวนพนักงาน (working now), วันลา by SR 1–15, รวม, รวมจ่ายค่าแรง (Σ PAY of every leave).</summary>
public sealed record LeaveSummaryLine(string DepartmentName, int Employees, IReadOnlyDictionary<string, double> Days, double Pay)
{
    public double DaysOf(string type) => Days.GetValueOrDefault(type);
    public double Total => LeaveReportDirectory.TotalTypes.Sum(DaysOf);
}

/// <summary>RPT-34 รายงานพนักงานที่ขาดงาน (รายงาน ขาดงาน2): การลา SR 8 with FROM_D within the dates.</summary>
/// <param name="EmployeeCode">null = ทุกคน (the legacy EMPID LIKE "*").</param>
public sealed record AbsentLeaveReport(DateOnly From, DateOnly To, string? EmployeeCode, IReadOnlyList<AbsentLeaveLine> Lines);

/// <summary>A line of RPT-34: ตั้งแต่วันที่ ถึงวันที่ จำนวนวัน รหัส ชื่อ-นามสกุล.</summary>
public sealed record AbsentLeaveLine(DateOnly FromDate, DateOnly ToDate, double Days, string EmployeeCode, string EmployeeName);
