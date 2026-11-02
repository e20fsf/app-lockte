using Lockte.SharedKernel.Persistence;

namespace Lockte.Cash.Data;

/// <summary>
/// ค่าตั้งค่าของ module เงินสดย่อย — one row; until someone saves it nothing is set. It holds what the legacy kept in a scratch table: the number of the last receipt
/// whose amount was remitted (ตัดยอดนำส่ง, data-model "ขายของเก่า2"; BR-015/016). Internal to the module: others read <see cref="CashSettingsDto"/> from <see cref="CashSettingsDirectory"/>.
/// </summary>
internal sealed class CashSettings : IAudited
{
    public const int ReceiptNoLength = 50;

    public int Id { get; set; }
    /// <summary>เลขที่ใบเสร็จสุดท้ายที่ตัดยอดนำส่งแล้ว (null = ยังไม่เคยตัดยอด).</summary>
    public string? LastRemittedReceiptNo { get; set; }

    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    string IHasBusinessKey.BusinessKey => "ค่าตั้งค่า";
}
