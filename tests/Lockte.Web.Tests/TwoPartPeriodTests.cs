using Lockte.Hr;

namespace Lockte.Web.Tests;

/// <summary>#58 งวดคร่อมปีใหม่ (BR-008) without a database: the รายเดือน formula of a ช่วง and the สรุปค่าแรงช่วง 1+2.</summary>
public sealed class TwoPartPeriodTests
{
    private static readonly PayPeriodDates Part1 = new(new DateOnly(2025, 12, 27), new DateOnly(2025, 12, 31), new DateOnly(2026, 1, 16));

    private static PayPeriodFacts Facts(int? partDays) => new(Part1, 1, new DateOnly(2025, 12, 31), partDays);

    private static EmployeeTimeTotal Time(double w, double l, int holi, int sun, double s = 0)
        => new(1, "A001", "นาย ทดสอบ", w, l, 0, 0, s, 0, 0, 0, holi, sun, 0);

    private static readonly PayEmployee Monthly = new(EmployeeTypes.Monthly, 15000, 500, null, false, null);

    [Fact] // BR-008 ตัวอย่าง 1: SA_DAY 500, W=3, HOLI=1, SUN=1, DAY=5 → min(5,5) × 500
    public void A_part_pays_a_monthly_employee_up_to_DAY_days()
        => Assert.Equal(2500m, PayCalculation.Calculate(Facts(5), Time(3, 0, 1, 1), Monthly)!.PayNormal);

    [Fact] // BR-008 ตัวอย่าง 2: W=2, L=0, HOLI=1, SUN=1 → 4 × 500
    public void Days_short_of_DAY_are_paid_as_they_are()
        => Assert.Equal(2000m, PayCalculation.Calculate(Facts(5), Time(2, 0, 1, 1), Monthly)!.PayNormal);

    [Fact] // more days than DAY (a leave day on top of a full week) are cut at DAY
    public void Days_over_DAY_are_cut()
        => Assert.Equal(1500m, PayCalculation.Calculate(Facts(3), Time(3, 1, 1, 1), Monthly)!.PayNormal);

    [Fact] // no POINT_1 and no resignation in the formula of a ช่วง
    public void A_part_ignores_a_set_pay_and_a_resignation()
    {
        var odd = Monthly with { FixedPay = 9999, ResignDate = new DateOnly(2025, 12, 29) };

        Assert.Equal(2500m, PayCalculation.Calculate(Facts(5), Time(3, 0, 1, 1), odd)!.PayNormal);
    }

    [Fact] // an ordinary period keeps the legacy monthly formulas, and a daily employee is paid as in any period
    public void An_ordinary_period_and_daily_employees_are_unchanged()
    {
        var daily = new PayEmployee(EmployeeTypes.Daily, 0, 400, null, false, null);

        Assert.Equal(7500m, PayCalculation.Calculate(Facts(null), Time(3, 0, 1, 1), Monthly)!.PayNormal); // BR-003: a full period pays half the salary
        Assert.Equal(PayCalculation.Calculate(Facts(null), Time(3, 0, 1, 1), daily)!.PayNormal, PayCalculation.Calculate(Facts(5), Time(3, 0, 1, 1), daily)!.PayNormal);
    }

    private static PayLine Line(string code, string? department, decimal pay, decimal overtime = 0)
        => new(new EmployeePay(1, code, "นาย " + code, 500, 3, 0, 0, 0, pay, overtime, 0, 0, 0, OvertimeParts.None), null, department);

    [Fact] // BR-008 ข้อ 6: one person counted once, the pay of both ช่วง added, in the department of the later ช่วง
    public void The_summary_of_both_parts_adds_each_employee_once()
    {
        var first = new[] { Line("A001", "ไดคาสท์", 2500, 100), Line("A002", "ไดคาสท์", 2000), Line("A003", "ตกแต่ง", 1000) };
        var second = new[] { Line("A001", "ตกแต่ง", 5000, 50), Line("A002", "ไดคาสท์", 4000) };

        var lines = PayCombination.Departments(first, second);

        Assert.Equal(2, lines.Count);
        Assert.Equal(("ไดคาสท์", 1, 6000m, 0m), (lines[0].DepartmentName, lines[0].Employees, lines[0].PayNormal, lines[0].PayOvertime));
        Assert.Equal(("ตกแต่ง", 2, 8500m, 150m), (lines[1].DepartmentName, lines[1].Employees, lines[1].PayNormal, lines[1].PayOvertime));
    }
}
