namespace Lockte.Hr;

/// <summary>
/// One slip of RPT-04 สลิปเงินเดือน — a row of the legacy table SLIP (BR-034, มาโคร SLIP: SLIP2–SLIP71), built from the
/// period's ค่าแรง and เวลาทำงานรวม by <see cref="PaySlips.Build"/>.
/// </summary>
/// <param name="Days">TT_D: TO_DA − FROM_DA + 1.</param>
/// <param name="WeeklyHolidays">SUN_D: the employee's SUN.</param>
/// <param name="PublicHolidays">H_D: the employee's HOLI.</param>
/// <param name="Rate">SA_D: รายเดือน SALARY, อื่น SA_DAY.</param>
/// <param name="Atm">ATM: 0 for a daily or CLAS 3 employee without a บัญชีทหารไทย (SLIP5/51/7/71), blank otherwise.</param>
public sealed record PaySlip(string Code, string Name, int Days, int WeeklyHolidays, int PublicHolidays, double Rate,
    double Normal, double PaidLeave, double Sunday, double Holiday, double Overtime, double SundayOvertime, double HolidayOvertime,
    decimal PayNormal, OvertimeParts Parts, decimal Hot, decimal? Atm)
{
    /// <summary>W_D: TT_D − HOLI − SUN.</summary>
    public int WorkingDays => Days - PublicHolidays - WeeklyHolidays;

    /// <summary>N โบนัส: 0 on the slip of a period; the slip รวมโบนัส (RPT-42, BR-035) fills it.</summary>
    public decimal Bonus { get; init; }

    /// <summary>TT รวมค่าแรง: SALA + OT + S_W + OT2 + H_W + OT3 + HOT.</summary>
    public decimal Total => PayNormal + Parts.Total + Hot;

    /// <summary>NET เงินได้สุทธิ = TT + โบนัส: nothing is deducted (SO_S, FA_L, INS, TAX stay blank, Q-02).</summary>
    public decimal Net => Total + Bonus;
}

/// <summary>The slips of one period, by รหัส ↑, with the signers printed on every slip.</summary>
public sealed record PaySlipBatch(PayPeriodDto Period, IReadOnlyList<PaySlip> Slips, IReadOnlyList<SlipSignerDto> Signers)
{
    public SlipSignerDto? Signer(SlipSignerRole role) => Signers.FirstOrDefault(s => s.Role == role);
}

/// <summary>Who signs a slip besides the employee (ผู้รับเงิน).</summary>
public enum SlipSignerRole
{
    /// <summary>แผนกบุคคล.</summary>
    HrDepartment = 1,
    /// <summary>ผู้จัดการโรงงาน.</summary>
    FactoryManager = 2,
}

public static class SlipSigners
{
    public static IReadOnlyDictionary<SlipSignerRole, string> Titles { get; } = new Dictionary<SlipSignerRole, string>
    {
        [SlipSignerRole.HrDepartment] = "แผนกบุคคล",
        [SlipSignerRole.FactoryManager] = "ผู้จัดการโรงงาน",
    };
}

/// <summary>A signer as saved; <see cref="Signature"/> is null when no picture was uploaded.</summary>
public sealed record SlipSignerDto(SlipSignerRole Role, string? Name, byte[]? Signature, string? SignatureContentType, byte[]? RowVersion);

/// <summary>
/// What HR saves for one signer. <see cref="Signature"/> null keeps the saved picture unless <see cref="RemoveSignature"/>.
/// <see cref="RowVersion"/> is the one loaded (null when the signer was never saved).
/// </summary>
public sealed record SlipSignerInput(SlipSignerRole Role, string? Name, byte[]? Signature, string? SignatureContentType, bool RemoveSignature,
    byte[]? RowVersion);

/// <summary>
/// BR-034 per employee: what the SLIP* queries inserted, from the same calculation as ค่าแรง (BR-003/004). No database,
/// so the golden master (AC-07) runs it on the legacy inputs too.
/// </summary>
public static class PaySlips
{
    public static PaySlip Build(PayPeriodDates dates, EmployeeTimeTotal time, EmployeePay pay, int employeeType, bool hasTtbAccount)
        => new(pay.Code, pay.Name, dates.Days, time.WeeklyHolidays, time.PublicHolidays, pay.Rate,
            time.Normal, time.PaidLeave, time.Sunday, time.Holiday, time.Overtime, time.SundayOvertime, time.HolidayOvertime,
            pay.PayNormal, pay.Parts,
            employeeType == EmployeeTypes.Monthly ? 0 : pay.Hot, // SLIP2/3 insert no HOT
            employeeType != EmployeeTypes.Monthly && !hasTtbAccount ? 0 : null);
}
