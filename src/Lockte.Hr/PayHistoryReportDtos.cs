namespace Lockte.Hr;

/// <summary>
/// A line of RPT-08 บัญชีคำนวณค่าจ้าง (คิวรี พิมพ์ค่าแรงสิ้นเดือน6): one employee over the งวด whose ถึงวันที่ is within
/// the dates. ชื่อ, เลขประจำตัวประชาชน (ID_CODE), ประเภท, เงินเดือน and ค่าแรง/วัน are the employee's today, as the legacy
/// joined EMPLO when printing.
/// </summary>
/// <param name="Rate">วันละหรือเดือนละ (Expr1): รายเดือน SALARY, อื่น SA_DAY.</param>
/// <param name="Days">จำนวนวัน (Expr4): Round(ΣPAY_W / SA_DAY, 1); null for รายเดือน, and when SA_DAY is 0 (Access printed an error).</param>
/// <param name="Wage">ค่าจ้าง รวมเงิน (Expr3): รายเดือน SALARY ⚠ (not what was paid), อื่น ΣPAY_W.</param>
public sealed record MonthEndPayLine(string Code, string Name, string? NationalId, string? DepartmentName, double Rate, double? Days,
    decimal Wage, decimal PayNormal, decimal PayOvertime)
{
    /// <summary>Expr5: ΣPAY_W + ΣPAY_OT — the cost summary's จำนวนเงิน.</summary>
    public decimal Total => PayNormal + PayOvertime;
}

/// <summary>
/// RPT-08 บัญชีคำนวณค่าจ้าง ค่าล่วงเวลา ค่าทำงานในวันหยุด (BR-063, รายงาน พิมพ์ค่าแรงสิ้นเดือน5): one department or all
/// (null, the legacy <c>*</c> printed "ทั้งหมด"), by รหัส ↑.
/// </summary>
public sealed record MonthEndPayReport(DateOnly From, DateOnly To, string? DepartmentName, IReadOnlyList<MonthEndPayLine> Lines)
{
    public decimal Wages => Lines.Sum(l => l.Wage);
}

/// <summary>A line of the RPT-08 summaries (ตาราง สรุปบัญชีคำนวณค่าจ้าง, คิวรี พิมพ์ค่าแรงสิ้นเดือน9): one department.</summary>
/// <param name="Wages">AMOUNT: Σ ค่าจ้าง (Expr3) — สรุปบัญชีคำนวณค่าจ้าง.</param>
/// <param name="Total">AMOUNT1: Σ (PAY_W + PAY_OT) (Expr5) — สรุปค่าแรงประจำเดือน (ต้นทุน).</param>
public sealed record MonthEndDepartmentTotal(string? DepartmentName, int Employees, decimal Wages, decimal Total);

/// <summary>
/// The two RPT-08 summaries, by department (Thai order, blank first): สรุปบัญชีคำนวณค่าจ้าง prints <see
/// cref="MonthEndDepartmentTotal.Wages"/>, สรุปค่าแรงประจำเดือน (ต้นทุน) <see cref="MonthEndDepartmentTotal.Total"/>.
/// </summary>
public sealed record MonthEndPaySummary(DateOnly From, DateOnly To, IReadOnlyList<MonthEndDepartmentTotal> Lines)
{
    public MonthEndDepartmentTotal Sum => new(null, Lines.Sum(l => l.Employees), Lines.Sum(l => l.Wages), Lines.Sum(l => l.Total));
}

/// <summary>A line of RPT-09: one แผนก by the first letter of รหัส, Σ(PAY_W + PAY_OT) per month (index 0 = มกราคม).</summary>
/// <param name="Months">null where nothing was paid that month (printed blank).</param>
public sealed record MonthlyPayLine(string Department, IReadOnlyList<decimal?> Months)
{
    public decimal Total => Months.Sum(m => m ?? 0);
}

/// <summary>
/// RPT-09 สรุปค่าแรงแต่ละเดือนปี … (BR-062, รายงาน สรุปค่าแรงแต่ละเดือน22222): the year in the header is ตั้งแต่วันที่'s
/// (the legacy MO = F_D).
/// </summary>
public sealed record MonthlyPayReport(DateOnly From, DateOnly To, IReadOnlyList<MonthlyPayLine> Lines)
{
    public IReadOnlyList<decimal?> MonthSums => Enumerable.Range(0, 12)
        .Select(m => Lines.Any(l => l.Months[m] is not null) ? Lines.Sum(l => l.Months[m] ?? 0) : (decimal?)null).ToList();

    public decimal Total => Lines.Sum(l => l.Total);
}

/// <summary>A line of RPT-10: one department's ΣPAY_W and ΣPAY_OT.</summary>
public sealed record DepartmentPaySum(string? DepartmentName, decimal PayNormal, decimal PayOvertime)
{
    public decimal Total => PayNormal + PayOvertime;
}

/// <summary>RPT-10 ค่าแรงพนักงาน (รวมค่าแรง, BR-062): the งวด within the dates, by department (Thai order, blank first).</summary>
public sealed record PayTotalsReport(DateOnly From, DateOnly To, IReadOnlyList<DepartmentPaySum> Lines)
{
    public DepartmentPaySum Sum => new(null, Lines.Sum(l => l.PayNormal), Lines.Sum(l => l.PayOvertime));
}
