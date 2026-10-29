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

/// <summary>
/// ผู้ขาย (T-SP, SCR-02): the one register of suppliers, shared by the whole system (calibration uses it later).
/// Internal to the module: others see <see cref="SupplierDto"/> from <see cref="SupplierDirectory"/>.
/// </summary>
internal sealed class Supplier : IAudited
{
    public int Id { get; set; }
    /// <summary>รหัสผู้ขาย (upper case, unique — the legacy allowed repeats, DQ-01).</summary>
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Address1 { get; set; }
    /// <summary>ที่อยู่บรรทัด 2: เขต(อำเภอ)และจังหวัด.</summary>
    public string? Address2 { get; set; }
    public string? Tel1 { get; set; }
    public string? Tel2 { get; set; }
    public string? Fax { get; set; }
    /// <summary>ชื่อผู้ติดต่อ (MG).</summary>
    public string? Contact { get; set; }
    /// <summary>เครดิตเป็นจำนวนวัน; 0 = เงินสด; null = not stated.</summary>
    public int? CreditDays { get; set; }
    /// <summary>คิด VAT หรือไม่ (YES/NO).</summary>
    public bool Vat { get; set; }
    /// <summary>ประเภทผลิตภัณฑ์ (free text).</summary>
    public string? ProductType { get; set; }
    /// <summary>ประเภทธุรกิจ, one of <see cref="SupplierBusinessType"/> (kept as text, as the legacy did).</summary>
    public string? BusinessType { get; set; }
    /// <summary>ผู้ขายมีใบรับรองระบบคุณภาพ (มี/ไม่มี).</summary>
    public bool Certificate { get; set; }
    /// <summary>ใช้ในการผลิต (ใช่/ไม่ใช่): ใช่ = in the approved list and evaluated.</summary>
    public bool UsedInProduction { get; set; }

    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    string IHasBusinessKey.BusinessKey => Code;
}

/// <summary>ตัวเลือกเครดิต (T-CREDIT): a number of days, 0 = เงินสด.</summary>
internal sealed class SupplierCreditOption : IAudited
{
    public int Id { get; set; }
    public int Days { get; set; }

    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    string IHasBusinessKey.BusinessKey => Days == 0 ? "เงินสด" : Days.ToString();
}

/// <summary>ตัวเลือกประเภทธุรกิจ (T-ประเภทธุรกิจ).</summary>
internal sealed class SupplierBusinessType : IAudited
{
    public int Id { get; set; }
    public string Name { get; set; } = "";

    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    string IHasBusinessKey.BusinessKey => Name;
}

/// <summary>
/// วัตถุดิบ (T-MTR_NAME, SCR-17): the register of purchasable items — วัตถุดิบ, spare parts, services, สินค้าซื้อมาขาย.
/// The fields the legacy no longer used (U_PACK, CATEGORY, MAXIMUM, R-W, REMARK) are not kept (Q-11).
/// </summary>
internal sealed class Material : IAudited
{
    public int Id { get; set; }
    /// <summary>รหัสวัตถุดิบ (upper case, unique), e.g. <c>R0019</c>.</summary>
    public string Code { get; set; } = "";
    /// <summary>ชื่อวัตถุดิบ; need not be unique (DQ-02).</summary>
    public string Name { get; set; } = "";
    /// <summary>หน่วยนับ.</summary>
    public string? Unit { get; set; }
    /// <summary>จุดสั่งซื้อต่ำสุด (MINIMUM): shown in the list only.</summary>
    public decimal? MinimumQuantity { get; set; }
    /// <summary>ราคาล่าสุดต่อหน่วย ไม่รวม VAT (COST); the goods receipt overwrites it (BR-031).</summary>
    public decimal Cost { get; set; }
    /// <summary>ผู้ขายล่าสุด; set by the goods receipt, shown read-only here.</summary>
    public int? LatestSupplierId { get; set; }
    public Supplier? LatestSupplier { get; set; }
    /// <summary>วัตถุดิบหลัก (INCOMING = ใช่): decides stock entry and incoming reports (BR-003).</summary>
    public bool IsMain { get; set; }
    /// <summary>ราคาซื้อคืนส่วนเกินต่อ กก. (BUY): only for recycled aluminium (BR-060).</summary>
    public decimal BuyBackPrice { get; set; }
    /// <summary>สินค้าของ module ผลิต for a สินค้าซื้อมาขาย (ข้อสมมติ ญ); checked on save, no database key across modules.</summary>
    public int? ProductId { get; set; }

    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    string IHasBusinessKey.BusinessKey => Code;
}

/// <summary>กลุ่มรหัสวัตถุดิบ (T-รหัสตัวแรกของวัตถุดิบ): a code prefix the list can be printed by (RPT-28).</summary>
internal sealed class MaterialGroup : IAudited
{
    public int Id { get; set; }
    /// <summary>รหัสขึ้นต้น, upper case (the legacy wrote them as patterns like <c>E0*</c>).</summary>
    public string Prefix { get; set; } = "";
    public string? Name { get; set; }

    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    string IHasBusinessKey.BusinessKey => Prefix;
}

/// <summary>
/// prefix เลขที่ใบขอซื้อของแผนก (T-รายชื่อแผนก.CODE, SCR-10). The แผนก itself is the register of module องค์กร, so only its id is kept
/// (checked on save; no database key across modules); the prefix is data (<c>B</c>, <c>DM</c>…), not a search pattern (Q-17).
/// </summary>
internal sealed class DepartmentPrefix : IAudited
{
    public int Id { get; set; }
    public int DepartmentId { get; set; }
    /// <summary>Upper case, unique across departments.</summary>
    public string Prefix { get; set; } = "";

    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    string IHasBusinessKey.BusinessKey => Prefix;
}

/// <summary>ผู้จัดซื้อ (T-PURC): the place that issues purchase orders — a code (<c>F</c>, <c>O</c>) and a name (โรงงาน, สำนักงานสีลม).</summary>
internal sealed class PurchasingLocation : IAudited
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";

    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    string IHasBusinessKey.BusinessKey => Code;
}

/// <summary>ข้อความเหตุผลในการขอซื้อที่เลือกได้ (T-เหตุผลในการขอซื้อ); the form also takes free text.</summary>
internal sealed class RequestReason : IAudited
{
    public int Id { get; set; }
    public string Text { get; set; } = "";

    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    string IHasBusinessKey.BusinessKey => Text;
}

/// <summary>ข้อความหมายเหตุที่เลือกได้ (T-หมายเหตุ); the form also takes free text.</summary>
internal sealed class RequestNote : IAudited
{
    public int Id { get; set; }
    public string Text { get; set; } = "";

    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    string IHasBusinessKey.BusinessKey => Text;
}
