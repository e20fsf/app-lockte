using System.Globalization;
using Lockte.SharedKernel.Persistence;

namespace Lockte.Hr.Data;

/// <summary>
/// การลา (T-LEAVE and its history T-LEAVE_ADD in one table for every year, #41): one leave of one employee. The quota
/// counts the calendar year by <see cref="FromDate"/> (Q-27). Rows may repeat, as in the legacy (BR-007). MARK (a
/// temporary flag of the legacy delete/move) and CODE_A (the BR-007 key, built from the other columns) are not kept.
/// </summary>
internal sealed class Leave : IAudited
{
    public int Id { get; set; }
    /// <summary>วันที่บันทึก (DATE).</summary>
    public DateOnly RecordedOn { get; set; }
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public TimeOnly FromTime { get; set; }
    public TimeOnly ToTime { get; set; }
    /// <summary>จำนวนวันลา (SUM_D), keyed by hand in 1/8 days (0.125 = 1 ชม., Q-13).</summary>
    public double Days { get; set; }
    /// <summary>ประเภทการลา (SR): <see cref="LeaveTypes"/>.</summary>
    public string Type { get; set; } = "";
    /// <summary>ได้ค่าแรง (SAL): Y = true, N = false, blank = null.</summary>
    public bool? Paid { get; set; }
    /// <summary>เหตุผล (REMARK).</summary>
    public string? Remark { get; set; }
    /// <summary>ค่าแรงต่อวัน ณ วันบันทึก (SALA_DAY), copied from the employee.</summary>
    public double DailyWage { get; set; }
    /// <summary>ค่าแรงระหว่างลา (PAY, BR-021).</summary>
    public double Pay { get; set; }

    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    string IHasBusinessKey.BusinessKey => $"{FromDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)} {Employee?.Code} {Type}";
}
