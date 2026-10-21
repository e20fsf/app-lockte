using System.Globalization;
using Lockte.SharedKernel.Persistence;

namespace Lockte.Hr.Data;

/// <summary>
/// งวดค่าแรง (CONTEXT.md): the dates HR sets on SCR-40 (the legacy one-row table ตั้งวันที่ตัดWEEK: FROM_DA, TO_DA,
/// PAY_D) — one row per period instead, so every period keeps its dates and how far its close got (#22).
/// </summary>
internal sealed class PayPeriod : IAudited
{
    public int Id { get; set; }
    /// <summary>จากวันที่ (FROM_DA); unique.</summary>
    public DateOnly FromDate { get; set; }
    /// <summary>ถึงวันที่ (TO_DA).</summary>
    public DateOnly ToDate { get; set; }
    /// <summary>วันที่จ่าย (PAY_D).</summary>
    public DateOnly PayDate { get; set; }
    /// <summary>The last step of เมนู 6 done (#22: no step before the one before it is done).</summary>
    public PayPeriodStep CompletedStep { get; set; }
    /// <summary>When and by whom the เวลาทำงาน totals were last calculated (#41: อธิบายได้เมื่อยอดเปลี่ยน).</summary>
    public DateTimeOffset? TimeCalculatedAt { get; set; }
    public string? TimeCalculatedBy { get; set; }
    /// <summary>When and by whom ค่าแรง was last calculated.</summary>
    public DateTimeOffset? PayCalculatedAt { get; set; }
    public string? PayCalculatedBy { get; set; }
    /// <summary>
    /// ย้ายมาจากระบบเดิม (#51): a closed period the migration created with the legacy ค่าแรง as its results. Its time and
    /// pay are never calculated again here and its dates never change (<see cref="PayPeriodRegistry"/>); its reports print.
    /// </summary>
    public bool Migrated { get; set; }

    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    string IHasBusinessKey.BusinessKey => FromDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}

/// <summary>
/// เวลาทำงานรวมของพนักงานหนึ่งคนในงวด (BR-002, the legacy คิวรี คำนวณเวลาทำงาน3–19 → 13): written by the
/// calculation, replaced by the next one (Q-22), read by RPT-01 and by ค่าแรง (BR-003/004).
/// </summary>
internal sealed class PayPeriodTimeTotal : IAudited
{
    public int Id { get; set; }
    public int PayPeriodId { get; set; }
    public PayPeriod PayPeriod { get; set; } = null!;
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    /// <summary>W: วันทำงานปกติ (Σ WORK_T of TYPE W).</summary>
    public double Normal { get; set; }
    /// <summary>L: วันลาได้ค่าแรง.</summary>
    public double PaidLeave { get; set; }
    /// <summary>L(NO): วันลาไม่ได้ค่าแรง.</summary>
    public double UnpaidLeave { get; set; }
    /// <summary>O1 ชั่วโมง.</summary>
    public double Overtime { get; set; }
    /// <summary>S ชั่วโมง.</summary>
    public double Sunday { get; set; }
    /// <summary>O2 ชั่วโมง.</summary>
    public double SundayOvertime { get; set; }
    /// <summary>H ชั่วโมง.</summary>
    public double Holiday { get; set; }
    /// <summary>O3 ชั่วโมง.</summary>
    public double HolidayOvertime { get; set; }
    /// <summary>HOLI.</summary>
    public int PublicHolidays { get; set; }
    /// <summary>SUN.</summary>
    public int WeeklyHolidays { get; set; }
    /// <summary>SUN_DAY.</summary>
    public int WeeklyHolidaysToResign { get; set; }

    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    string IHasBusinessKey.BusinessKey => $"{PayPeriod?.FromDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)} {Employee?.Code}";
}
