using Lockte.SharedKernel;

namespace Lockte.Hr;

/// <summary>An การลา row with the employee's code (subform LEAVE1 of SCR-30).</summary>
public sealed record LeaveDto(int Id, int EmployeeId, string EmployeeCode, DateOnly RecordedOn, DateOnly FromDate, DateOnly ToDate,
    TimeOnly FromTime, TimeOnly ToTime, double Days, string Type, bool? Paid, string? Remark, double DailyWage, double Pay,
    byte[] RowVersion)
{
    public string? TypeName => LeaveTypes.Names.GetValueOrDefault(Type);
}

/// <summary>
/// An การลา row to save (SCR-30). <see cref="Id"/> null = new; editing needs <see cref="RowVersion"/> as loaded.
/// Blank <see cref="RecordedOn"/> = today, blank times = 08:00–17:00, blank <see cref="DailyWage"/> = the employee's
/// ค่าแรง/วัน (the legacy defaults). <see cref="Paid"/>: Y = true, N = false.
/// </summary>
public sealed record LeaveInput(int? Id, int? EmployeeId, DateOnly? RecordedOn, DateOnly? FromDate, DateOnly? ToDate,
    TimeOnly? FromTime, TimeOnly? ToTime, double? Days, string? Type, bool? Paid, string? Remark, double? DailyWage, byte[]? RowVersion);

/// <summary>
/// Outcome of saving an การลา row. <see cref="Quota"/> is the BR-020 check when ได้ค่าแรง was chosen (a new row with Y
/// or N, or a saved row whose Y/N changed) and null otherwise; a failed check is also the one error of
/// <see cref="Save"/>, and the row is not saved.
/// </summary>
public sealed record LeaveSaveResult(ServiceResult Save, LeaveQuotaCheck? Quota);

/// <summary>
/// สรุปการลาของพนักงานคนหนึ่งในปีปฏิทิน (สรุปการลา subform1 of SCR-30): ได้ค่าแรง days by SR, their total and pay, and
/// each BR-020 limit against what is used.
/// </summary>
public sealed record LeaveSummary(int Year, IReadOnlyDictionary<string, double> PaidDaysByType, double PaidDays, double PaidPay,
    IReadOnlyList<LeaveQuotaLine> Quotas);

/// <summary>
/// One BR-020 limit for the year: <see cref="Used"/> of <see cref="Limit"/> days. <see cref="Note"/> says why the
/// employee has no right at all (ยังไม่มีพักร้อน, เพศชายลาคลอดไม่ได้).
/// </summary>
public sealed record LeaveQuotaLine(string Name, double Used, double Limit, string? Note)
{
    public double? Left => Note is null ? Limit - Used : null;
}
