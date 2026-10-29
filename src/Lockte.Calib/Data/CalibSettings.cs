using Lockte.SharedKernel.Persistence;

namespace Lockte.Calib.Data;

/// <summary>
/// ค่าตั้งค่าของ module สอบเทียบ — one row; until someone saves it the defaults of <see cref="CalibSettingsDefaults"/> apply.
/// Today it holds the sticker size (Q-14: the real paper is not known yet, so the size is data, not code). Internal to the
/// module: others read <see cref="CalibSettingsDto"/> from <see cref="CalibSettingsDirectory"/>.
/// </summary>
internal sealed class CalibSettings : IAudited
{
    public int Id { get; set; }
    /// <summary>กว้างของสติกเกอร์ (ซม.).</summary>
    public decimal StickerWidthCm { get; set; }
    /// <summary>สูงของสติกเกอร์ (ซม.).</summary>
    public decimal StickerHeightCm { get; set; }

    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    string IHasBusinessKey.BusinessKey => "ค่าตั้งค่า";
}
