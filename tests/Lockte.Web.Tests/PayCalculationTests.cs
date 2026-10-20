using System.Globalization;
using Lockte.Hr;

namespace Lockte.Web.Tests;

/// <summary>
/// คำนวณค่าแรง (BR-003 PAY_W, BR-004 PAY_OT/TT_OT, HOT) without a database: the worked examples of
/// specs/employee/business-rules.md as tables, then the branches of the legacy queries คำนวณค่าแรง2/3/31/4/41 they do
/// not show. Every example uses BR-002's period 27/08 – 11/09 (D = 16, SUN = 2, no วันหยุดประเพณี) unless it says.
/// </summary>
public sealed class PayCalculationTests
{
    private static DateOnly D(string iso) => DateOnly.Parse(iso, CultureInfo.InvariantCulture);

    private static readonly PayPeriodFacts Example = new(new PayPeriodDates(D("2026-08-27"), D("2026-09-11"), D("2026-09-16")), 2, null);

    private static EmployeeTimeTotal Time(double w = 0, double l = 0, double lno = 0, double o1 = 0, double s = 0, double o2 = 0,
        double h = 0, double o3 = 0, int holi = 0, int sun = 2, int sunDay = 0)
        => new(1, "D001", "นาย ทดสอบ ค่าแรง", w, l, lno, o1, s, o2, h, o3, holi, sun, sunDay);

    private static PayEmployee Monthly(double salary = 15000, double saDay = 500, int? fixedPay = null, DateOnly? resign = null)
        => new(EmployeeTypes.Monthly, salary, saDay, fixedPay, false, resign);

    private static PayEmployee Daily(double saDay = 400, int? fixedPay = null, DateOnly? resign = null, bool hot = false)
        => new(EmployeeTypes.Daily, 0, saDay, fixedPay, hot, resign);

    private static EmployeePay Pay(EmployeeTimeTotal time, PayEmployee employee, PayPeriodFacts? period = null)
        => PayCalculation.Calculate(period ?? Example, time, employee) ?? throw new InvalidOperationException("not paid");

    public static TheoryData<string, EmployeeTimeTotal, PayEmployee, decimal> NormalPayExamples => new()
    {
        { "1 รายเดือนมาครบ", Time(w: 14), Monthly(), 7500 },
        { "2 รายเดือนขาด มีลาไม่ได้ค่าแรง", Time(w: 12, l: 1, lno: 1), Monthly(), 7000 },
        { "3 รายเดือนขาดโดยไม่บันทึกลา (Q-19)", Time(w: 12), Monthly(), 7500 },
        { "4 รายเดือน POINT_1 (Q-09)", Time(w: 14), Monthly(fixedPay: 8000), 8000 },
        { "5 รายวันมาครบ+ลา", Time(w: 14, l: 1), Daily(), 5600 },
        { "6 รายวันขาดบางวัน", Time(w: 11.5, l: 1), Daily(), 5000 },
        { "7 รายเดือนเริ่มงานกลางงวด SUN=0 (Q-20)", Time(w: 8, sun: 0), Monthly(), 7500 },
        { "8 รายวัน มีวันหยุดประเพณี 1 วัน", Time(w: 13, holi: 1), Daily(), 5600 },
    };

    [Theory] // BR-003 ตัวอย่าง 1–8
    [MemberData(nameof(NormalPayExamples))]
    public void Normal_pay_follows_the_worked_examples(string example, EmployeeTimeTotal time, PayEmployee employee, decimal payNormal)
        => Assert.True(Pay(time, employee).PayNormal == payNormal, $"{example}: {Pay(time, employee).PayNormal} ≠ {payNormal}");

    public static TheoryData<string, EmployeeTimeTotal, PayEmployee, decimal, int> OvertimeExamples => new()
    {
        { "1 รายวัน O1 3.5 ชม.", Time(w: 14, o1: 3.5), Daily(400), 263, 0 },
        { "2 รายเดือน S 8 ชม. O2 2 ชม.", Time(w: 14, s: 8, o2: 2), Monthly(saDay: 500), 875, 10 },
        { "3 รายวัน SA_DAY 386 O1 2.8333", Time(w: 14, o1: 2.8333), Daily(386), 205, 0 },
    };

    [Theory] // BR-004 ตัวอย่าง 1–3: PAY_OT, TT_OT
    [MemberData(nameof(OvertimeExamples))]
    public void Overtime_pay_follows_the_worked_examples(string example, EmployeeTimeTotal time, PayEmployee employee, decimal payOvertime,
        int holidayHours)
    {
        var pay = Pay(time, employee);
        Assert.True((pay.PayOvertime, pay.HolidayHours) == (payOvertime, holidayHours),
            $"{example}: {pay.PayOvertime}/{pay.HolidayHours} ≠ {payOvertime}/{holidayHours}");
    }

    [Theory] // BR-004 ตัวคูณ: O1 1.5 · S 1/2/2 · O2 3 · H 1/1/2 · O3 3 (รายเดือน/รายวัน/CLAS 3), 8 ชม. at 800/วัน = 800 per 1×
    [InlineData(EmployeeTypes.Monthly, 1200, 800, 2400, 800, 2400)]
    [InlineData(EmployeeTypes.Daily, 1200, 1600, 2400, 800, 2400)]
    [InlineData(PayCalculation.Type3, 1200, 1600, 2400, 1600, 2400)]
    public void Each_kind_of_hours_has_its_multiplier_per_employee_type(int type, int o1, int s, int o2, int h, int o3)
    {
        PayEmployee employee = new(type, 24000, 800, null, false, null);
        decimal Part(EmployeeTimeTotal time) => Pay(time, employee).PayOvertime;

        Assert.Equal(((decimal)o1, (decimal)s, (decimal)o2, (decimal)h, (decimal)o3),
            (Part(Time(w: 14, o1: 8)), Part(Time(w: 14, s: 8)), Part(Time(w: 14, o2: 8)), Part(Time(w: 14, h: 8)), Part(Time(w: 14, o3: 8))));
    }

    [Fact] // BR-004: each part rounded (RND-A), then the sum rounded again
    public void Overtime_parts_are_rounded_one_by_one_before_they_are_added()
    {
        // O1 0.1 × 101 / 8 × 1.5 = 1.89375 → 2, S 0.1 × 101 / 8 × 2 = 2.525 → 3: 5, where the unrounded 4.41875 would give 4.
        var pay = Pay(Time(w: 14, o1: 0.1, s: 0.1), Daily(101));

        Assert.Equal(5m, pay.PayOvertime);
    }

    [Fact] // TT_OT = RND-A(S + O2 + H + O3) ชั่วโมง, OT = O1 ชั่วโมง as is
    public void Holiday_hours_are_rounded_and_overtime_hours_are_kept()
    {
        var pay = Pay(Time(w: 14, o1: 2.8333, s: 4.25, o2: 1.25, h: 0, o3: 0), Daily());

        Assert.Equal((6, 2.8333), (pay.HolidayHours, pay.Overtime));
    }

    [Fact] // RPT-02 ค่าแรง (อัตรา) = SA: รายเดือน SALARY, อื่น SA_DAY; W, L as calculated
    public void The_rate_is_the_salary_for_monthly_and_the_daily_wage_for_daily()
    {
        var monthly = Pay(Time(w: 13.9375, l: 1), Monthly());

        Assert.Equal((15000.0, 13.9375, 1.0), (monthly.Rate, monthly.Normal, monthly.PaidLeave));
        Assert.Equal(400.0, Pay(Time(w: 14), Daily()).Rate);
    }

    // ---- รายเดือนที่ลาออก (คำนวณค่าแรง2) ----

    private static readonly PayPeriodFacts WithHoliday = Example with { FirstPublicHoliday = D("2026-09-04") };

    [Fact] // ลาออกแล้วแต่ไม่มีวันมีสิทธิ์ → 0
    public void A_monthly_employee_who_resigned_with_no_days_gets_nothing()
        => Assert.Equal(0m, Pay(Time(sun: 2), Monthly(resign: D("2026-08-27"))).PayNormal);

    [Fact] // RESIGN = TO_DA + 1 → SALARY / 2
    public void A_monthly_employee_who_resigns_the_day_after_the_period_gets_half_a_salary()
        => Assert.Equal(7500m, Pay(Time(w: 10), Monthly(resign: D("2026-09-12"))).PayNormal);

    [Fact] // BR-002 ตัวอย่าง 4: ลาออก 03/09 SUN_DAY 1, no DAT → (Expr1 + SUN_DAY) × SA_DAY
    public void A_monthly_employee_who_resigns_in_the_period_gets_the_days_and_the_weekly_holidays_up_to_then()
        => Assert.Equal(3000m, Pay(Time(w: 5, sunDay: 1), Monthly(resign: D("2026-09-03"))).PayNormal);

    [Fact] // RESIGN > DAT → (Expr1 + SUN_DAY + HOLI) × SA_DAY
    public void A_monthly_employee_who_resigns_after_the_first_public_holiday_is_paid_it()
        => Assert.Equal(4000m, Pay(Time(w: 6, sunDay: 1, holi: 1), Monthly(resign: D("2026-09-07")), WithHoliday).PayNormal);

    [Fact] // RESIGN ≤ DAT → (Expr1 + SUN_DAY) × SA_DAY
    public void A_monthly_employee_who_resigns_before_the_first_public_holiday_is_not_paid_it()
        => Assert.Equal(3000m, Pay(Time(w: 5, sunDay: 1, holi: 0), Monthly(resign: D("2026-09-03")), WithHoliday).PayNormal);

    [Fact] // POINT_1 comes before ลาออก (⚠ Q-09)
    public void A_set_pay_wins_over_resigning()
        => Assert.Equal(85m, Pay(Time(), Monthly(fixedPay: 85, resign: D("2026-09-01"))).PayNormal);

    [Fact] // มาครบ uses HOLI: Expr1 + HOLI ≥ D − SUN
    public void A_monthly_employee_counts_public_holidays_towards_a_full_period()
        => Assert.Equal(7500m, Pay(Time(w: 13, holi: 1), Monthly(), WithHoliday).PayNormal);

    // ---- รายวัน (คำนวณค่าแรง3/31), CLAS 3 (คำนวณค่าแรง4/41) ----

    [Fact] // ลาออกแล้ว and RESIGN ≤ DAT (or no DAT) → Expr1 × SA_DAY: no HOLI, no cap
    public void A_daily_employee_who_resigned_before_the_first_public_holiday_is_paid_the_days_only()
        => Assert.Equal(2000m, Pay(Time(w: 4, l: 1, holi: 1), Daily(resign: D("2026-09-03")), WithHoliday).PayNormal);

    [Fact] // RESIGN > DAT → min(Expr1 + HOLI, D − SUN) × SA_DAY
    public void A_daily_employee_who_resigned_after_the_first_public_holiday_is_paid_it()
        => Assert.Equal(2400m, Pay(Time(w: 5, holi: 1), Daily(resign: D("2026-09-07")), WithHoliday).PayNormal);

    [Fact] // POINT_1 for รายวัน HOT1 = N
    public void A_daily_employee_with_a_set_pay_gets_it()
        => Assert.Equal(1234m, Pay(Time(w: 14), Daily(fixedPay: 1234)).PayNormal);

    [Fact] // HOT1 = Y (คำนวณค่าแรง31): no POINT_1, no ลาออก branch; HOT = Round(W × 10, 2) (RND-B)
    public void A_daily_employee_with_HOT_gets_ten_baht_a_day_and_no_set_pay_or_resign_branch()
    {
        var pay = Pay(Time(w: 13.9375, l: 1), Daily(fixedPay: 1234, resign: D("2026-09-03"), hot: true), WithHoliday);

        Assert.Equal((5600m, 139.38m), (pay.PayNormal, pay.Hot)); // min(14.9375, 14) × 400; Round(139.375, 2) → .38 (to even)
    }

    [Fact] // CLAS 3: min(Expr1, D − SUN − HOLI) × SA_DAY — วันหยุดประเพณีไม่จ่ายถ้าไม่มาทำ; HOT only with HOT1 = Y
    public void Type_3_is_paid_the_days_worked_up_to_the_working_days()
    {
        Assert.Equal(5200m, Pay(Time(w: 13, holi: 1), new PayEmployee(PayCalculation.Type3, 0, 400, null, false, null), WithHoliday).PayNormal);
        Assert.Equal(5200m, Pay(Time(w: 14, holi: 1), new PayEmployee(PayCalculation.Type3, 0, 400, null, false, null), WithHoliday).PayNormal);
        Assert.Equal(140m, Pay(Time(w: 14), new PayEmployee(PayCalculation.Type3, 0, 400, null, true, null)).Hot);
    }

    [Fact] // HOT is for รายวัน/CLAS 3 only: the monthly query never fills it
    public void Monthly_employees_get_no_HOT()
        => Assert.Equal(0m, Pay(Time(w: 14), new PayEmployee(EmployeeTypes.Monthly, 15000, 500, null, true, null)).Hot);

    [Theory] // ประเภทอื่น/ว่าง: no legacy query picks them
    [InlineData(null)]
    [InlineData(4)]
    public void Other_employee_types_are_not_paid(int? type)
        => Assert.Null(PayCalculation.Calculate(Example, Time(w: 14), new PayEmployee(type, 15000, 500, null, false, null)));

    [Fact] // RND-A on PAY_W: SALARY / 2 and SA_DAY × days to whole baht, halves up
    public void Normal_pay_is_rounded_half_away_from_zero()
    {
        Assert.Equal(7501m, Pay(Time(w: 14), Monthly(salary: 15001)).PayNormal); // 7500.5
        Assert.Equal(4865m, Pay(Time(w: 12.5), Daily(389.2)).PayNormal); // 4865.0
        Assert.Equal(4639m, Pay(Time(w: 11.9375), Daily(388.6)).PayNormal); // 4638.9125
    }
}
