namespace Lockte.Hr;

/// <summary>
/// The steps of ปิดงวดค่าแรงตามปกติ in the order of เมนู 6 (#22 แบบ B หน้าเดียว): a step can be done once the one
/// before it is. Doing a calculation step again (Q-22) undoes the steps after it: their results came from the old one.
/// </summary>
public enum PayPeriodStep
{
    /// <summary>ตั้งงวด (SCR-40 dates).</summary>
    Set = 0,
    /// <summary>พิมพ์เวลาทำงาน: BR-007, BR-001, BR-002 → RPT-01.</summary>
    TimeCalculated = 1,
    /// <summary>พิมพ์ค่าแรง: BR-003..006 → RPT-02 (SCR-41).</summary>
    PayCalculated = 2,
    /// <summary>พิมพ์สรุปค่าแรง (RPT-03).</summary>
    SummaryPrinted = 3,
    /// <summary>พิมพ์ค่าแรงส่งแบงค์ (SCR-42, RPT-05).</summary>
    BankPrinted = 4,
    /// <summary>พิมพ์ SLIP (RPT-04).</summary>
    SlipPrinted = 5,
}

public static class PayPeriodSteps
{
    /// <summary>The labels of เมนู 6.</summary>
    public static IReadOnlyDictionary<PayPeriodStep, string> Names { get; } = new Dictionary<PayPeriodStep, string>
    {
        [PayPeriodStep.Set] = "ตั้งงวด",
        [PayPeriodStep.TimeCalculated] = "พิมพ์เวลาทำงาน",
        [PayPeriodStep.PayCalculated] = "พิมพ์ค่าแรง",
        [PayPeriodStep.SummaryPrinted] = "พิมพ์สรุปค่าแรง",
        [PayPeriodStep.BankPrinted] = "พิมพ์ค่าแรงส่งแบงค์",
        [PayPeriodStep.SlipPrinted] = "พิมพ์ SLIP",
    };

    /// <summary>Steps that calculate: doing one again sends the period back to it.</summary>
    public static bool Calculates(PayPeriodStep step) => step is PayPeriodStep.TimeCalculated or PayPeriodStep.PayCalculated;

    /// <summary>#22 ล็อกไม่ให้ข้ามขั้น: a step may be done (again) once every step before it is done.</summary>
    public static bool Allowed(PayPeriodStep completed, PayPeriodStep step) => step == PayPeriodStep.Set || completed >= step - 1;
}

/// <summary>
/// วันที่ของงวดค่าแรง. The defaults follow the factory's pattern (#23 Q-04): 12–26 จ่ายวันที่ 1 ของเดือนถัดไป and
/// 27–11 จ่ายวันที่ 16; HR may type other dates (งวดพิเศษ).
/// </summary>
public sealed record PayPeriodDates(DateOnly From, DateOnly To, DateOnly PayDate)
{
    /// <summary>D = TO_DA − FROM_DA + 1.</summary>
    public int Days => To.DayNumber - From.DayNumber + 1;

    /// <summary>The usual period that has <paramref name="date"/>.</summary>
    public static PayPeriodDates Containing(DateOnly date) => date.Day switch
    {
        >= 12 and <= 26 => StartingOn(new DateOnly(date.Year, date.Month, 12)),
        >= 27 => StartingOn(new DateOnly(date.Year, date.Month, 27)),
        _ => StartingOn(new DateOnly(date.Year, date.Month, 27).AddMonths(-1)),
    };

    /// <summary>A period from <paramref name="from"/> to the next cut day (the 26th or the 11th), with its pay day.</summary>
    public static PayPeriodDates StartingOn(DateOnly from)
    {
        var month = new DateOnly(from.Year, from.Month, 1);
        return from.Day switch
        {
            >= 12 and <= 26 => new(from, month.AddDays(25), month.AddMonths(1)),
            >= 27 => new(from, month.AddMonths(1).AddDays(10), month.AddMonths(1).AddDays(15)),
            _ => new(from, month.AddDays(10), month.AddDays(15)),
        };
    }

    /// <summary>The period after <paramref name="previous"/>: from the next day to the next cut day.</summary>
    public static PayPeriodDates After(PayPeriodDates previous) => StartingOn(previous.To.AddDays(1));

    /// <summary>The newest usual period that has ended by <paramref name="today"/> — the one HR closes now.</summary>
    public static PayPeriodDates LastEndedBy(DateOnly today)
    {
        var current = Containing(today);
        return current.To <= today ? current : Containing(current.From.AddDays(-1));
    }
}

/// <summary>A งวดค่าแรง and how far its close got.</summary>
public sealed record PayPeriodDto(int Id, DateOnly From, DateOnly To, DateOnly PayDate, PayPeriodStep CompletedStep,
    DateTimeOffset? TimeCalculatedAt, string? TimeCalculatedBy, byte[] RowVersion, DateTimeOffset? PayCalculatedAt = null,
    string? PayCalculatedBy = null, bool Migrated = false)
{
    public int Days => Dates.Days;

    public PayPeriodDates Dates => new(From, To, PayDate);

    /// <summary>The step may be done (again): every step before it is done, and a migrated period is never calculated.</summary>
    public bool CanDo(PayPeriodStep step) => PayPeriodSteps.Allowed(CompletedStep, step) && !(Migrated && PayPeriodSteps.Calculates(step));
}

/// <summary>ตั้งงวด (SCR-40) to save. <see cref="Id"/> null = new; editing needs <see cref="RowVersion"/> as loaded.</summary>
public sealed record PayPeriodInput(int? Id, DateOnly? From, DateOnly? To, DateOnly? PayDate, byte[]? RowVersion);

/// <summary>
/// เวลาทำงานรวมของพนักงานหนึ่งคนในงวด (BR-002): days for W/L, hours for O1/S/O2/H/O3, counts of วันหยุด. Only the
/// employees BR-005 calculates (W+L+S+H+HOLI+SUN &gt; 0, รหัสไม่ใช่ LT*).
/// </summary>
public sealed record EmployeeTimeTotal(int EmployeeId, string Code, string Name,
    double Normal, double PaidLeave, double UnpaidLeave, double Overtime, double Sunday, double SundayOvertime,
    double Holiday, double HolidayOvertime, int PublicHolidays, int WeeklyHolidays, int WeeklyHolidaysToResign)
{
    /// <summary>Expr1: วันมีสิทธิ์ได้ค่าแรง = W + L.</summary>
    public double EntitledDays => Normal + PaidLeave;
}

/// <summary>
/// RPT-01 เวลาทำงานรายคน (คำนวณเวลาทำงาน13): the period, its วันหยุด counts for the header and the calculated
/// employees by รหัส ↑.
/// </summary>
public sealed record TimeReport(PayPeriodDto Period, int WeeklyHolidays, int PublicHolidays, DateOnly? FirstPublicHoliday,
    IReadOnlyList<EmployeeTimeTotal> Lines)
{
    /// <summary>รวมทั้งหมด: TO − FROM + 1.</summary>
    public int TotalDays => Period.Days;

    /// <summary>วันทำงาน: TO − FROM + 1 − HOLI − SUN.</summary>
    public int WorkDays => TotalDays - PublicHolidays - WeeklyHolidays;
}

/// <summary>
/// คำเตือนก่อนคำนวณ (#22): วันทำงานในงวดที่ยังไม่มีเวลาปกติของใครเลย (ไม่นับวันหยุด) and มาสาย rows. They do not
/// stop the calculation.
/// </summary>
public sealed record PayPeriodCheck(IReadOnlyList<DateOnly> DaysWithoutTime, IReadOnlyList<LateArrival> LateArrivals)
{
    public bool HasWarnings => DaysWithoutTime.Count > 0 || LateArrivals.Count > 0;
}

/// <summary>A เวลาทำงาน row with a เวลาเข้าจริง (LATE).</summary>
public sealed record LateArrival(DateOnly Date, string EmployeeCode, string EmployeeName, TimeOnly? WorkIn, TimeOnly Late);

/// <summary>A line of RPT-02 ค่าแรงรายคน: the employee's <see cref="EmployeePay"/> as calculated.</summary>
public sealed record PayLine(EmployeePay Pay, int? DepartmentId, string? DepartmentName);

/// <summary>
/// RPT-02 ค่าแรงรายคน (ตามแผนก), รายงาน ค่าแรง1 ← คิวรี คำนวณค่าแรง73: one department (null = ทุกแผนก, the legacy
/// <c>*</c>), the lines with PAY_W + PAY_OT &gt; 0 by รหัส ↑.
/// </summary>
public sealed record PayReport(PayPeriodDto Period, string? DepartmentName, IReadOnlyList<PayLine> Lines);

/// <summary>A line of RPT-03: one department's จำนวนพนักงาน and Σ of the period (BR-060).</summary>
public sealed record DepartmentPayTotal(string? DepartmentName, int Employees, decimal PayNormal, decimal PayOvertime, decimal Hot)
{
    /// <summary>รวม: ΣPAY_W + ΣPAY_OT + ΣHOT.</summary>
    public decimal Total => PayNormal + PayOvertime + Hot;

    /// <summary>% ค่าล่วงเวลา: ΣPAY_OT / ΣPAY_W × 100 (blank when ΣPAY_W is 0: Access printed an error there).</summary>
    public decimal? OvertimePercent => PayNormal == 0 ? null : PayOvertime / PayNormal * 100;
}

/// <summary>
/// RPT-03 สรุปค่าแรงรายแผนก (BR-060, รายงาน สรุปค่าแรง): every paid employee counted in the department they were paid
/// under, by department name (Thai order, blank first), with the period's วันหยุด counts for the header.
/// </summary>
public sealed record PaySummaryReport(PayPeriodDto Period, int WeeklyHolidays, int PublicHolidays, IReadOnlyList<DepartmentPayTotal> Lines)
{
    /// <summary>รวม: TO − FROM + 1.</summary>
    public int TotalDays => Period.Days;

    /// <summary>วันทำงาน: TO − FROM + 1 − SUNDAY − HOLIDAY.</summary>
    public int WorkDays => TotalDays - WeeklyHolidays - PublicHolidays;

    /// <summary>คิดค่าแรง: TO − FROM + 1 − SUNDAY.</summary>
    public int PaidDays => TotalDays - WeeklyHolidays;

    public DepartmentPayTotal Sum => new(null, Lines.Sum(l => l.Employees), Lines.Sum(l => l.PayNormal), Lines.Sum(l => l.PayOvertime),
        Lines.Sum(l => l.Hot));
}
