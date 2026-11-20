using System.Globalization;
using Lockte.SharedKernel.Persistence;

namespace Lockte.Hr.Data;

/// <summary>
/// สิทธิ์วันลาได้ค่าแรง (UAT 10): one range of dates with the days of every BR-020 limit in force, set by HR on the settings page
/// because the law may change them. A leave takes the range covering its FROM_D; a date no range covers takes
/// <see cref="LeaveEntitlementDays.Default"/> (the limits before UAT 10). Ranges never overlap; <see cref="EndDate"/> null = เป็นต้นไป.
/// </summary>
internal sealed class LeaveEntitlement : IAudited
{
    public int Id { get; set; }
    /// <summary>วันเริ่มมีผล.</summary>
    public DateOnly StartDate { get; set; }
    /// <summary>วันสิ้นสุด (รวมวันนั้น); null = เป็นต้นไป.</summary>
    public DateOnly? EndDate { get; set; }
    /// <summary>ลากิจ ก่อนทำงานครบ 1 ปี.</summary>
    public double PersonalFirstYear { get; set; }
    /// <summary>ลากิจ เมื่อทำงานครบ 1 ปี.</summary>
    public double Personal { get; set; }
    public double Sick { get; set; }
    /// <summary>ลาพักร้อนต่อปี (ปีที่ครบ 1 ปีได้ตามสัดส่วนเพิ่ม).</summary>
    public double Vacation { get; set; }
    public double Maternity { get; set; }
    public double Military { get; set; }
    public double SpecialMaternity { get; set; }
    public double SpouseMaternity { get; set; }

    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    string IHasBusinessKey.BusinessKey => StartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
