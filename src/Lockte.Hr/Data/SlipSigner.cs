using Lockte.SharedKernel.Persistence;

namespace Lockte.Hr.Data;

/// <summary>
/// ผู้ลงนามบนสลิปเงินเดือน (RPT-04): one row per <see cref="SlipSignerRole"/>, with the name the legacy report typed into
/// its labels and the signature picture that ตาราง3.SIGN was meant to hold (#55). HR keeps them on the slip settings page.
/// </summary>
internal sealed class SlipSigner : IAudited
{
    public int Id { get; set; }
    public SlipSignerRole Role { get; set; }
    /// <summary>ชื่อที่พิมพ์เหนือเส้นลงนาม.</summary>
    public string? Name { get; set; }
    /// <summary>รูปลายเซ็น (PNG or JPEG); none prints the name only.</summary>
    public byte[]? Signature { get; set; }
    public string? SignatureContentType { get; set; }

    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    string IHasBusinessKey.BusinessKey => SlipSigners.Titles[Role];
}
