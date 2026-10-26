using Lockte.SharedKernel.Persistence;

namespace Lockte.Prod.Data;

// Internal to the module (ADR 0002): other modules see DTOs from ProductDirectory, never these.

/// <summary>
/// สินค้า (T-P_NAME, SCR-03) — one register for finished goods, resale items and each department's work in progress
/// (#16). The weights keep the legacy meaning of WEIGHT..WEIGHT4 (Q-09).
/// </summary>
internal sealed class Product : IAudited
{
    public int Id { get; set; }
    /// <summary>รหัสสินค้า PD_C (business key, unique, upper case); the leading letters tell the group.</summary>
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>หน่วยนับ U_NAME เช่น คู่ ชิ้น เส้น.</summary>
    public string? Unit { get; set; }
    /// <summary>หน่วยบรรจุ U_PACK.</summary>
    public string? Pack { get; set; }
    /// <summary>นน.อลูมิเนียม (WEIGHT), กก.</summary>
    public decimal? AluminumWeight { get; set; }
    /// <summary>นน.ผ้าเบรก (WEIGHT1), กก.</summary>
    public decimal? BrakePadWeight { get; set; }
    /// <summary>นน.ไม้ก๊อก (WEIGHT2), กก.</summary>
    public decimal? CorkWeight { get; set; }
    /// <summary>ค่าเหล็ก (WEIGHT3), บาท.</summary>
    public decimal? SteelCost { get; set; }
    /// <summary>นน.รวม 1 คู่ (WEIGHT4), กก.</summary>
    public decimal? PairWeight { get; set; }
    /// <summary>ราคาขาย (U_PRICE), บาท/หน่วย.</summary>
    public decimal? SellingPrice { get; set; }
    /// <summary>ต้นทุนต่อหน่วย (FML): typed by hand, no formula (#28).</summary>
    public decimal? UnitCost { get; set; }
    public int? ProductTypeId { get; set; }
    public ProductType? ProductType { get; set; }
    /// <summary>แม่พิมพ์ที่ใช้ผลิต (MOULD).</summary>
    public int? MoldId { get; set; }
    public Mold? Mold { get; set; }
    /// <summary>ลูกค้าเจ้าของสินค้า (CUS_ID): a customer of module ขายและส่งของ, checked on save.</summary>
    public int? CustomerId { get; set; }
    /// <summary>วันที่ผลิต (YEAR).</summary>
    public DateOnly? ProducedOn { get; set; }
    public int? ModelId { get; set; }
    public ProductModel? Model { get; set; }
    public string? Barcode { get; set; }
    /// <summary>เลข มอก. (มอก).</summary>
    public string? StandardNumber { get; set; }
    public int? BrakeTypeId { get; set; }
    public BrakeType? BrakeType { get; set; }
    /// <summary>ขนาดผ้าเบรก กว้าง×ยาว×สูง (SIZE).</summary>
    public string? Size { get; set; }
    /// <summary>สูตรผ้าเบรก (FORMULA).</summary>
    public string? Formula { get; set; }
    /// <summary>รุ่นสำหรับข้อมูล มอก. (MODEL1).</summary>
    public string? StandardModel { get; set; }

    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    string IHasBusinessKey.BusinessKey => Code;
}

/// <summary>ประเภทสินค้า (T-ประเภท).</summary>
internal sealed class ProductType : IAudited
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

/// <summary>ชนิดเบรก (T-TYPE): ดรัมเบรก = 2, ดิสเบรก = 4.</summary>
internal sealed class BrakeType : IAudited
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    /// <summary>รหัสชนิด (TYPE) <c>2</c> / <c>4</c> — what the legacy product table stored.</summary>
    public string Code { get; set; } = "";

    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    string IHasBusinessKey.BusinessKey => Code;
}

/// <summary>รุ่นรถ (T-MODEL) เช่น <c>C100 A</c>.</summary>
internal sealed class ProductModel : IAudited
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
/// กลุ่มสินค้า (T-รหัสสินค้าย่อ): a pattern on the product code (Access <c>LIKE</c>: <c>G1*</c>, <c>EX</c>, <c>*</c> =
/// all) and the group's name. Patterns overlap on purpose (Q-26), so a code can belong to several groups.
/// </summary>
internal sealed class ProductGroup : IAudited
{
    public int Id { get; set; }
    public string Pattern { get; set; } = "";
    public string Name { get; set; } = "";

    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    string IHasBusinessKey.BusinessKey => Pattern;
}
