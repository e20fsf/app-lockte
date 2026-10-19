using System.Globalization;
using Lockte.SharedKernel.Persistence;

namespace Lockte.Hr.Data;

/// <summary>
/// ค่าแรงของพนักงานหนึ่งคนในงวด (BR-003/004, the legacy ค่าแรง and its history สำเนาของ ค่าแรง in one table for every
/// year, #41): written by คำนวณค่าแรง, replaced by the next one (BR-006, Q-22), read by RPT-02/03 and the steps after.
/// The amounts are <see cref="decimal"/>; the days and hours stay <see cref="double"/> as calculated.
/// </summary>
internal sealed class PayResult : IAudited
{
    public int Id { get; set; }
    public int PayPeriodId { get; set; }
    public PayPeriod PayPeriod { get; set; } = null!;
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    /// <summary>
    /// แผนกตอนคำนวณ (ค่าแรง1.DEPARTMENT): RPT-02/03 print a period by the departments it was paid under, even after a
    /// move.
    /// </summary>
    public int? DepartmentId { get; set; }
    /// <summary>ประเภท (CLAS) ตอนคำนวณ.</summary>
    public int EmployeeType { get; set; }
    /// <summary>SA: อัตรา — รายเดือน SALARY, อื่น SA_DAY.</summary>
    public double Rate { get; set; }
    /// <summary>W: วันทำงาน.</summary>
    public double Normal { get; set; }
    /// <summary>L: วันลาได้ค่าแรง.</summary>
    public double PaidLeave { get; set; }
    /// <summary>OT: ชั่วโมง O1.</summary>
    public double Overtime { get; set; }
    /// <summary>TT_OT: ชั่วโมง S+O2+H+O3 (RND-A).</summary>
    public int HolidayHours { get; set; }
    /// <summary>PAY_W: ค่าแรง.</summary>
    public decimal PayNormal { get; set; }
    /// <summary>PAY_OT: ค่าล่วงเวลาและค่าทำงานวันหยุด.</summary>
    public decimal PayOvertime { get; set; }
    /// <summary>HOT.</summary>
    public decimal Hot { get; set; }
    /// <summary>HOLIDAY: the employee's HOLI.</summary>
    public int PublicHolidays { get; set; }
    /// <summary>SUNDAY: the period's วันหยุดประจำสัปดาห์.</summary>
    public int WeeklyHolidays { get; set; }

    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    string IHasBusinessKey.BusinessKey => $"{PayPeriod?.FromDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)} {Employee?.Code}";
}
