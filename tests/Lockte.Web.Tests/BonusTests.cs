using Lockte.Hr;

namespace Lockte.Web.Tests;

/// <summary>#62 โบนัส (BR-030) and จ่ายรวมกับค่าแรงงวด (BR-035) without a database.</summary>
public sealed class BonusTests
{
    private static decimal Monthly(double years, double leave, double salary = 15000)
        => Bonus.Amount(EmployeeTypes.Monthly, salary, 0, Bonus.Percent(years, leave), 0.5m, 13);

    private static decimal Daily(double? years, double leave, double wage = 400)
        => Bonus.Amount(EmployeeTypes.Daily, 0, wage, Bonus.Percent(years, leave), 0.5m, 13);

    [Fact] // BR-030 ตัวอย่าง 1–2
    public void A_monthly_employee_gets_a_half_month_less_four_percent_a_day_over_twelve()
    {
        Assert.Equal((100.0, 7500m), (Bonus.Percent(6, 5), Monthly(6, 5)));
        Assert.Equal((84.0, 6300m), (Bonus.Percent(6, 16), Monthly(6, 16)));
    }

    [Fact] // ตัวอย่าง 3: รายวัน 400, อายุงาน 0.75, ลา 3 → 75 − 12 = 63% → 400 × 0.63 × 13
    public void A_daily_employee_under_a_year_gets_the_service_percent()
        => Assert.Equal((63.0, 3276m), (Bonus.Percent(0.75, 3), Daily(0.75, 3)));

    [Theory] // ตัวอย่าง 4–6 and the edges
    [InlineData(0.4, 0, 0)]      // อายุงาน < 0.5
    [InlineData(6, 25, 0)]       // ลา 25 วันพอดี
    [InlineData(0.6, 20, 0)]     // 60 − 80 → 0
    [InlineData(0.5, 0, 50)]     // อายุงาน 0.5 พอดี
    [InlineData(1, 12, 100)]     // ลา 12 ยังได้เต็ม
    [InlineData(1, 24.5, 50)]    // 100 − 12.5 × 4
    public void The_percent_edges(double years, double leave, double expected) => Assert.Equal(expected, Bonus.Percent(years, leave));

    [Fact] // no วันเริ่มงาน: the อายุงาน tests are Null (false) as the legacy IIf — a long service unless วันลา ≥ 25
    public void Without_a_start_date_the_service_tests_fall_through()
    {
        Assert.Equal(100.0, Bonus.Percent(null, 5));
        Assert.Equal(0.0, Bonus.Percent(null, 25));
        Assert.Null(Bonus.ServiceYears(null, new DateOnly(2026, 10, 1)));
    }

    [Fact] // Round((TO − START − 1) / 365, 3)
    public void Service_years_are_counted_to_the_end_of_the_range_less_a_day()
        => Assert.Equal(0.75, Math.Round(Bonus.ServiceYears(new DateOnly(2025, 12, 31), new DateOnly(2026, 10, 1))!.Value, 2));

    [Fact] // the SR 1–15 all count, SR 16 does not
    public void Every_leave_type_one_to_fifteen_counts()
    {
        Assert.Equal(15, Bonus.CountedTypes.Count);
        Assert.Contains("5", Bonus.CountedTypes);
        Assert.DoesNotContain("16", Bonus.CountedTypes);
    }

    private static BonusPayInput Pay(string code, string? ttb, string? kbank, decimal w, decimal ot, decimal hot, decimal bonus) => new(code, ttb, kbank, w, ot, hot, bonus);

    [Fact] // BR-035 ตัวอย่าง 1–3
    public void The_lists_add_the_bonus_to_the_period_pay()
    {
        var rows = Bonus.Payment([
            Pay("A001", "111", "901", 7500, 875, 0, 7500),   // รายเดือน, both accounts
            Pay("A002", "222", null, 5863, 0, 0, 3276),      // รายวัน, ACC only
            Pay("A003", null, "903", 5000, 263, 0, 3276),    // no ACC, ACC2 only
        ]);

        Assert.Equal([("KBANK", "901", 8375m), ("KBANK", "903", 3539m), ("TTB", "111", 15875m), ("TTB", "222", 9139m)],
            rows.Select(r => (r.Bank, r.Account, r.Amount)));
    }

    [Fact] // a bonus with no pay in the period is paid by the bonus alone; zero stays out; one account counted once
    public void Bonus_only_and_zero_rows()
    {
        var rows = Bonus.Payment([
            Pay("A001", "111", null, 0, 0, 0, 500),
            Pay("A002", "222", "902", 0, 0, 0, 0),
            Pay("A003", "111", null, 100, 0, 0, 0), // the same account again
        ]);

        Assert.Equal([("TTB", "111", 600m)], rows.Select(r => (r.Bank, r.Account, r.Amount)));
    }

    [Theory] // ⚠ BR-035: รายเดือน; รายวัน HOT1 = N with an account; not รายวัน without ACC, HOT1 = Y, CLAS 3
    [InlineData(EmployeeTypes.Monthly, false, false, true)]
    [InlineData(EmployeeTypes.Daily, false, true, true)]
    [InlineData(EmployeeTypes.Daily, false, false, false)]
    [InlineData(EmployeeTypes.Daily, true, true, false)]
    [InlineData(3, false, true, false)]
    public void Who_gets_the_bonus_on_the_slip(int type, bool hot, bool account, bool expected) => Assert.Equal(expected, Bonus.IsOnSlip(type, hot, account));

    [Fact] // BR-035 ตัวอย่าง 1: NET = ค่าแรงงวด + โบนัส, N = โบนัส, TT stays the ค่าแรง
    public void The_slip_with_the_bonus_adds_it_to_the_net()
    {
        var pay = PayCalculation.Calculate(new PayPeriodFacts(new PayPeriodDates(new DateOnly(2026, 8, 27), new DateOnly(2026, 9, 11), new DateOnly(2026, 9, 16)), 2, null),
            new EmployeeTimeTotal(1, "A001", "นาย ทดสอบ", 14, 0, 0, 0, 8, 2, 0, 0, 0, 2, 0), new PayEmployee(EmployeeTypes.Monthly, 15000, 500, null, false, null))!;
        var slip = PaySlips.Build(new PayPeriodDates(new DateOnly(2026, 8, 27), new DateOnly(2026, 9, 11), new DateOnly(2026, 9, 16)),
            new EmployeeTimeTotal(1, "A001", "นาย ทดสอบ", 14, 0, 0, 0, 8, 2, 0, 0, 0, 2, 0), pay, EmployeeTypes.Monthly, true) with { Bonus = 7500 };

        Assert.Equal((8375m, 7500m, 15875m), (slip.Total, slip.Bonus, slip.Net));
    }
}
