namespace Lockte.Hr;

/// <summary>
/// RPT-36 รายชื่อพนักงานจ่ายคืนเงินพักร้อนประจำปี (มาโคร คำนวณพักร้อน → รายงาน คำนวณพักร้อน, BR-031): the employees
/// paid for the พักร้อน they did not take, with the leaves of <see cref="From"/>–<see cref="To"/>.
/// </summary>
public sealed record VacationPayReport(DateOnly From, DateOnly To, IReadOnlyList<VacationPayLine> Lines)
{
    /// <summary>รวม (=Sum([expr2])): Σ of the unrounded amounts, rounded only when printed.</summary>
    public double Total => Lines.Sum(l => l.Amount);
}

/// <summary>
/// A line of RPT-36: รหัส ชื่อ-นามสกุล แผนก ค่าแรง/วัน (SA_DAY now), วันลาที่ใช้ไป, จำนวนวันลาคงเหลือ (Expr1 = 6 − ใช้ไป) and
/// จำนวนเงินที่ต้องจ่ายคืน (Expr2 = Expr1 × SA_DAY, not rounded — the report prints it to the baht).
/// </summary>
public sealed record VacationPayLine(string Code, string Name, string? DepartmentName, double DailyWage, double UsedDays)
{
    public double RemainingDays => VacationPay.Entitlement - UsedDays;
    public double Amount => RemainingDays * DailyWage;
}
