using Lockte.SharedKernel.Persistence;

namespace Lockte.Cash.Data;

/// <summary>
/// การนำส่งสำนักงาน (ตัดยอดนำส่ง, BR-016): one closing of the receipts of a range of numbers, to be sent to the office. It replaces the legacy's scratch tables — "ค้างส่ง" (a copy of
/// the receipt lines still waiting) and the last closing's lines whose highest number marked the line between sent and waiting. Here a receipt carries its own state: the
/// <see cref="ScrapReceipt.RemittanceId"/> of the closing it was in, or none. <see cref="FromNo"/> and <see cref="ToNo"/> are the range asked for.
/// </summary>
internal sealed class ScrapRemittance : IAudited
{
    public int Id { get; set; }
    public DateOnly MadeOn { get; set; }
    public string FromNo { get; set; } = "";
    public string ToNo { get; set; } = "";
    public string MadeBy { get; set; } = "";

    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    string IHasBusinessKey.BusinessKey => $"{MadeOn.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)} {FromNo}-{ToNo}";
}
