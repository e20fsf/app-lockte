using System.Globalization;
using Lockte.SharedKernel.Persistence;

namespace Lockte.Hr.Data;

/// <summary>
/// อัตราเงินสมทบกองทุนสงเคราะห์ลูกจ้าง (UAT-43): one range of dates with the rate in force, set by HR on the settings page because the
/// law changes it (0.25% from 1 ม.ค. 2570, 0.50% from 1 ต.ค. 2574). The same rate applies to the employee's and the company's portion
/// (#180: the slip prints equal amounts). Ranges never overlap; <see cref="EndDate"/> null = เป็นต้นไป. Every change is in the history.
/// </summary>
internal sealed class WelfareRate : IAudited
{
    public int Id { get; set; }
    /// <summary>วันเริ่มมีผล.</summary>
    public DateOnly StartDate { get; set; }
    /// <summary>วันสิ้นสุด (รวมวันนั้น); null = เป็นต้นไป.</summary>
    public DateOnly? EndDate { get; set; }
    /// <summary>อัตรา % (0.25 = 0.25%), ฝ่ายละ.</summary>
    public decimal Percent { get; set; }

    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    string IHasBusinessKey.BusinessKey => StartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
