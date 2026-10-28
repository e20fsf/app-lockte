using System.Globalization;
using Lockte.SharedKernel.Persistence;

namespace Lockte.Hr.Data;

/// <summary>
/// การคำนวณโบนัสครั้งล่าสุด (SCR-50, BR-030): the legacy kept the parameters in ลางานทั้งแผนก and the results in EMPLOYEEโบนัส, both
/// overwritten by the next run — one run here too, replaced whole. BR-035 pays from it.
/// </summary>
internal sealed class BonusRun : IAudited
{
    public int Id { get; set; }
    /// <summary>FORM / TO: the leaves counted and the day อายุงาน is counted to.</summary>
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    /// <summary>DEP: null = ทุกแผนก (the legacy "*").</summary>
    public int? DepartmentId { get; set; }
    /// <summary>PAY_M: ตัวคูณรายเดือน (× SALARY).</summary>
    public decimal MonthlyMultiplier { get; set; }
    /// <summary>PAY_D: ตัวคูณรายวัน (× SA_DAY).</summary>
    public decimal DailyMultiplier { get; set; }
    public List<BonusResult> Results { get; set; } = [];

    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    string IHasBusinessKey.BusinessKey => $"{FromDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)} – {ToDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";
}

/// <summary>โบนัสของพนักงานหนึ่งคนในการคำนวณ (EMPLOYEEโบนัส): the figures the report printed and the amount paid.</summary>
internal sealed class BonusResult : IAudited
{
    public int Id { get; set; }
    public int BonusRunId { get; set; }
    public BonusRun BonusRun { get; set; } = null!;
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    /// <summary>วันลา: Round(Σ SUM_D, 2).</summary>
    public double LeaveDays { get; set; }
    /// <summary>อายุงาน (ปี) Round(…, 3); null without วันเริ่มงาน.</summary>
    public double? ServiceYears { get; set; }
    /// <summary>% โบนัส.</summary>
    public double Percent { get; set; }
    /// <summary>โบนัส (บาทเต็ม).</summary>
    public decimal Amount { get; set; }

    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    string IHasBusinessKey.BusinessKey => Employee?.Code ?? EmployeeId.ToString(CultureInfo.InvariantCulture);
}
