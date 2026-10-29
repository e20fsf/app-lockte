using Lockte.SharedKernel.Persistence;

namespace Lockte.Purch.Data;

/// <summary>
/// ค่าตั้งค่าของ module จัดซื้อและวัตถุดิบ — the values the legacy hard-coded (data-model "ค่าที่ hard-code"). One row;
/// until someone saves it the defaults of <see cref="PurchSettingsDefaults"/> apply. Internal to the module: others read
/// <see cref="PurchSettingsDto"/> from <see cref="PurchSettingsDirectory"/>.
/// </summary>
internal sealed class PurchSettings : IAudited
{
    public int Id { get; set; }
    /// <summary>อัตรา VAT เป็นสัดส่วน (0.07 = 7%).</summary>
    public decimal VatRate { get; set; }
    /// <summary>เกณฑ์ใบสั่งซื้อเล็ก (บาท): ใบที่ยอดต่ำกว่านี้อยู่ใน RPT-11 (BR-063).</summary>
    public decimal SmallOrderLimit { get; set; }
    /// <summary>ผู้อนุมัติใบสั่งซื้อเล็ก; blank until set (no person's name is kept in the repository).</summary>
    public string? SmallOrderApprover { get; set; }
    /// <summary>prefix รหัสที่ไม่ยกยอดตอนตัดสต็อก, คั่นด้วย comma (BR-042).</summary>
    public string NoCarryPrefixes { get; set; } = "";
    /// <summary>เกณฑ์เกรด A/B/C เป็นเปอร์เซ็นต์ (BR-050); below C is D.</summary>
    public decimal GradeA { get; set; }
    public decimal GradeB { get; set; }
    public decimal GradeC { get; set; }

    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    string IHasBusinessKey.BusinessKey => "ค่าตั้งค่า";
}

/// <summary>กลุ่มสินค้าซื้อมาขาย (BR-062): รหัสขึ้นต้น → ชื่อกลุ่ม, in the order the report prints them.</summary>
internal sealed class ResaleGroup : IAudited
{
    public int Id { get; set; }
    /// <summary>รหัสขึ้นต้น, lower case, unique.</summary>
    public string Prefix { get; set; } = "";
    public string Name { get; set; } = "";
    public int SortOrder { get; set; }

    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    string IHasBusinessKey.BusinessKey => Prefix;
}
