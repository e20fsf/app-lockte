using Lockte.SharedKernel.Persistence;

namespace Lockte.Hr.Data;

/// <summary>
/// แก้ครั้งที่ 12: a เวลาทำงาน row saved by printing ใบขออนุมัติทำ OT (SCR-14), with what the sheet prints and เวลาทำงาน does not keep — the
/// sheet's แผนก and the line's หมายเหตุ. A sheet is every entry of one date and one แผนก, so the people of several ช่วงเวลา printed one after
/// another (หลายกะ) come out on the same sheet, as the legacy's T-OT did. The times, type and employee are the เวลาทำงาน row's: an edit there shows
/// on the sheet, and deleting it removes the entry (cascade).
/// </summary>
internal sealed class OvertimeSheetEntry : IAudited
{
    public int Id { get; set; }
    public int WorkTimeId { get; set; }
    public WorkTime WorkTime { get; set; } = null!;
    /// <summary>แผนกของใบ (DEPART) in module องค์กร; null = the sheet was printed without one.</summary>
    public int? DepartmentId { get; set; }
    /// <summary>หมายเหตุ / งานที่ทำ (MEMO).</summary>
    public string? Memo { get; set; }

    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    string IHasBusinessKey.BusinessKey => ((IHasBusinessKey?)WorkTime)?.BusinessKey ?? WorkTimeId.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
