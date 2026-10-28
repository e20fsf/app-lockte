namespace Lockte.Hr;

/// <summary>
/// What HR types on SCR-50 คำนวณโบนัส (the legacy table ลางานทั้งแผนก): the leaves of <see cref="From"/>–<see cref="To"/> are
/// counted and อายุงาน is to <see cref="To"/>; <see cref="DepartmentId"/> null = ทุกแผนก (the "*"); ตัวคูณ PAY_M × SALARY (รายเดือน)
/// and PAY_D × SA_DAY (อื่น ๆ).
/// </summary>
public sealed record BonusInput(DateOnly From, DateOnly To, int? DepartmentId, decimal MonthlyMultiplier = BonusInput.DefaultMonthly,
    decimal DailyMultiplier = BonusInput.DefaultDaily)
{
    /// <summary>The last values of the legacy (screens.md SCR-50): 0.5 and 13.</summary>
    public const decimal DefaultMonthly = 0.5m;
    public const decimal DefaultDaily = 13;
}

/// <summary>RPT-35 คำนวณโบนัส (BR-030): one line per employee by รหัส ↑, and the แผนก name chosen (null = ทุกแผนก).</summary>
public sealed record BonusReport(BonusInput Input, string? DepartmentName, DateTimeOffset CalculatedAt, string CalculatedBy, IReadOnlyList<BonusLine> Lines)
{
    public decimal Total => Lines.Sum(l => l.Amount);
}

/// <summary>One employee of RPT-35: <see cref="LeaveDays"/> วันลา, <see cref="ServiceYears"/> อายุงาน (null without วันเริ่มงาน), % โบนัส, โบนัส.</summary>
public sealed record BonusLine(string Code, string Name, string? DepartmentName, DateOnly? StartDate, double? ServiceYears, double LeaveDays,
    double Percent, decimal Amount);

/// <summary>
/// What BR-035 reads of one employee: the accounts, the งวด's pay (zero when not paid in the งวด) and the bonus of the latest
/// <see cref="BonusRegistry.CalculateAsync"/>.
/// </summary>
public sealed record BonusPayInput(string Code, string? AccountTtb, string? AccountKbank, decimal PayNormal, decimal PayOvertime, decimal Hot, decimal Bonus);

/// <summary>
/// BR-035 ค่าแรงรวมโบนัส (RPT-40 ทหารไทย, RPT-41 กสิกร) of one งวด: <see cref="Rows"/> by list (<see cref="BankTransfers.Ttb"/>,
/// <see cref="BankTransfers.Kbank"/>), one line per เลขบัญชี.
/// </summary>
public sealed record BonusPayment(PayPeriodDto Period, BonusInput Bonus, IReadOnlyList<BankTransferRow> Rows, IReadOnlyList<BankTransferSummary> Summary);
