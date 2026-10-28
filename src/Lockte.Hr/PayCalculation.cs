using Lockte.SharedKernel;

namespace Lockte.Hr;

/// <summary>
/// คำนวณค่าแรงของพนักงานหนึ่งคนในงวด (BR-003 PAY_W, BR-004 PAY_OT/TT_OT, HOT) on the BR-002 totals: no database, so
/// the legacy formulas are in one place. Each branch is the IIf of its query, term by term and in the same order
/// (คำนวณค่าแรง2 รายเดือน, 3 รายวัน, 31 รายวัน HOT, 4/41 CLAS 3), in <see cref="double"/> as Access computed it, with
/// RND-A / RND-B where the queries had them; the amounts come out as <see cref="decimal"/> (#41). Every ⚠ of the
/// legacy is kept (Q-09 POINT_1, Q-19 ขาดไม่หัก, Q-20 เริ่มงานกลางงวด, Q-21 H × 1).
/// </summary>
public static class PayCalculation
{
    /// <summary>CLAS 3: the legacy has a formula for it but no employee (Q-06), so the type has no name.</summary>
    public const int Type3 = 3;

    /// <summary>
    /// The pay of one employee, or null for a type no legacy query picks (blank, or not 1–3). <paramref name="time"/> is
    /// one of the period's BR-002 totals (BR-005 chose who).
    /// </summary>
    public static EmployeePay? Calculate(PayPeriodFacts period, EmployeeTimeTotal time, PayEmployee employee)
    {
        var dates = period.Dates;
        double days = dates.To.DayNumber - dates.From.DayNumber + 1; // [to_da]-[from_da]+1
        double w = time.Normal, l = time.PaidLeave, unpaid = time.UnpaidLeave;
        double holi = time.PublicHolidays, sun = time.WeeklyHolidays, sunDay = time.WeeklyHolidaysToResign;
        var expr1 = w + l;
        var saDay = employee.DailyWage;
        var resign = employee.ResignDate;
        // [resign]>[dat]: Null when either is blank, and IIf takes Null as false.
        var resignAfterHoliday = resign is { } r && period.FirstPublicHoliday is { } dat && r > dat;

        // min(Expr1 + HOLI, D − SUN) × SA_DAY, written as the queries wrote it.
        double UpToWorkingDays() => expr1 + holi >= days - sun ? saDay * (days - sun) : (expr1 + holi) * saDay;

        double payNormal;
        double sundayFactor, holidayFactor;
        var hot = 0.0;
        switch (employee.EmployeeType)
        {
            case EmployeeTypes.Monthly: // คำนวณค่าแรง2
                sundayFactor = 1;
                holidayFactor = 1;
                // คำนวณค่าแรง2ช่วง (#58, BR-008): a ช่วง of the งวดคร่อมปีใหม่ pays min(W + L + HOLI + SUN, DAY) days; no POINT_1, no resignation.
                payNormal = period.PartDays is { } partDays ? Math.Min(expr1 + holi + sun, partDays) * saDay
                    : employee.FixedPay is { } monthlyFixed ? monthlyFixed
                    : resign is null ? (expr1 + holi >= days - sun ? employee.Salary / 2 : (15 - unpaid) * saDay)
                    : expr1 == 0 ? 0
                    : resign == dates.To.AddDays(1) ? employee.Salary / 2
                    : resignAfterHoliday ? (expr1 + sunDay + holi) * saDay
                    : (expr1 + sunDay) * saDay;
                break;
            case EmployeeTypes.Daily when employee.HotAllowance: // คำนวณค่าแรง31
                sundayFactor = 2;
                holidayFactor = 1;
                payNormal = UpToWorkingDays();
                hot = Rounding.RndB(w * 10, 2);
                break;
            case EmployeeTypes.Daily: // คำนวณค่าแรง3
                sundayFactor = 2;
                holidayFactor = 1;
                payNormal = employee.FixedPay is { } dailyFixed ? dailyFixed
                    : resign is null || resignAfterHoliday ? UpToWorkingDays()
                    : expr1 * saDay;
                break;
            case Type3: // คำนวณค่าแรง4 / 41
                sundayFactor = 2;
                holidayFactor = 2;
                payNormal = expr1 >= days - sun - holi ? saDay * (days - sun - holi) : expr1 * saDay;
                if (employee.HotAllowance) hot = Rounding.RndB(w * 10, 2);
                break;
            default:
                return null;
        }

        // [hours]*[sa_day]/8*factor, a factor of 1 not written; each part RND-A, then the sum RND-A again (Expr3–Expr9).
        double Part(double hours, double factor) => Rounding.RndA(factor == 1 ? hours * saDay / 8 : hours * saDay / 8 * factor);
        double overtime = Part(time.Overtime, 1.5), sunday = Part(time.Sunday, sundayFactor), sundayOvertime = Part(time.SundayOvertime, 3),
            holiday = Part(time.Holiday, holidayFactor), holidayOvertime = Part(time.HolidayOvertime, 3);
        var payOvertime = Rounding.RndA(overtime + sunday + sundayOvertime + holiday + holidayOvertime);
        var holidayHours = Rounding.RndA(time.Sunday + time.SundayOvertime + time.Holiday + time.HolidayOvertime); // Expr8

        return new EmployeePay(time.EmployeeId, time.Code, time.Name,
            employee.EmployeeType == EmployeeTypes.Monthly ? employee.Salary : saDay,
            w, l, time.Overtime, (int)holidayHours,
            (decimal)Rounding.RndA(payNormal), (decimal)payOvertime, (decimal)hot,
            time.PublicHolidays, period.WeeklyHolidays,
            new OvertimeParts((decimal)overtime, (decimal)sunday, (decimal)sundayOvertime, (decimal)holiday, (decimal)holidayOvertime));
    }
}

/// <summary>
/// What the pay formulas read of the period besides each employee's totals: D from the dates, the วันหยุดประจำสัปดาห์
/// in it (ค่าแรง.SUNDAY) and DAT, the first วันหยุดประเพณี in it (คิวรี คำนวณค่าแรง: Min(HOLIDAY.DATE), blank if none).
/// </summary>
public sealed record PayPeriodFacts(PayPeriodDates Dates, int WeeklyHolidays, DateOnly? FirstPublicHoliday, int? PartDays = null);

/// <summary>
/// What the pay formulas read of an employee (T-EMPLO): CLAS, SALARY, SA_DAY, POINT_1 (⚠ Q-09: a score used as a set
/// pay), HOT1 and RESIGN.
/// </summary>
public sealed record PayEmployee(int? EmployeeType, double Salary, double DailyWage, int? FixedPay, bool HotAllowance, DateOnly? ResignDate);

/// <summary>
/// ค่าแรงของพนักงานหนึ่งคนในงวด — a row of the legacy ค่าแรง / สำเนาของ ค่าแรง: <see cref="Rate"/> = SA (รายเดือน
/// SALARY, อื่น SA_DAY), W, L, OT (ชั่วโมง O1), TT_OT (ชั่วโมง S+O2+H+O3, RND-A), PAY_W, PAY_OT, HOT, HOLIDAY (the
/// employee's HOLI) and SUNDAY (the period's วันหยุดประจำสัปดาห์); <see cref="Parts"/> = PAY_OT part by part, for the
/// slip (RPT-04).
/// </summary>
public sealed record EmployeePay(int EmployeeId, string Code, string Name, double Rate, double Normal, double PaidLeave, double Overtime,
    int HolidayHours, decimal PayNormal, decimal PayOvertime, decimal Hot, int PublicHolidays, int WeeklyHolidays, OvertimeParts Parts)
{
    /// <summary>ยอดรวม: PAY_W + PAY_OT + HOT.</summary>
    public decimal Total => PayNormal + PayOvertime + Hot;
}

/// <summary>
/// เงินสงเคราะห์ลูกจ้าง (#180): the slip of today prints 0.25% of รวมค่าแรง withheld from the employee and 0.25% the company
/// adds (photo of lot 4: รวมค่าแรง 4,692 → 12 and 12, เงินได้สุทธิ 4,680). ⚠ ฐานและการปัดเป็นข้อสมมติ — ยังไม่ได้ยืนยันกับ
/// ฝ่ายบุคคล (รายการ UAT ใน #102): ฐาน = รวมค่าแรง (PAY_W + PAY_OT + HOT), ปัด RND-A เป็นบาทเต็ม, ทั้งสองฝั่งเท่ากัน.
/// </summary>
public static class Welfare
{
    public const decimal Rate = 0.0025m;

    /// <summary>The contribution of one side on a รวมค่าแรง, whole baht, half away from zero.</summary>
    public static decimal Contribution(decimal total) => Math.Round(total * Rate, 0, MidpointRounding.AwayFromZero);
}

/// <summary>
/// PAY_OT part by part, each RND-A (Expr3–Expr7 of คำนวณค่าแรง2/3/31/4/41, the slip's OT, S_W, OT2, H_W, OT3):
/// ชั่วโมง O1 × SA_DAY/8 × 1.5, S × 1 or 2, O2 × 3, H × 1 or 2, O3 × 3. Zero for a period moved from the legacy history,
/// which kept only their sum.
/// </summary>
public sealed record OvertimeParts(decimal Overtime, decimal Sunday, decimal SundayOvertime, decimal Holiday, decimal HolidayOvertime)
{
    public static OvertimeParts None { get; } = new(0, 0, 0, 0, 0);

    public decimal Total => Overtime + Sunday + SundayOvertime + Holiday + HolidayOvertime;
}
