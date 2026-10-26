using Lockte.SharedKernel.Persistence;

namespace Lockte.Prod.Data;

/// <summary>
/// ใบรายงานผลผลิต หนึ่งใบ (T-P_REP&lt;แผนก&gt;, #116): one table for every แผนก (ข้อตัดสิน #27), the department being its
/// <see cref="DepartmentCode"/> (the letter of the number, BR-015). The legacy file repeated the head on every line; here it is one row.
/// Internal to the module (ADR 0002).
/// </summary>
internal sealed class ProductionReport : IAudited
{
    public int Id { get; set; }
    /// <summary>ตัวอักษรแผนก (D T B G K A P) — see <see cref="ProductionDepartments"/>.</summary>
    public string DepartmentCode { get; set; } = "";
    /// <summary>วันที่ผลิต (DATE).</summary>
    public DateOnly? ReportDate { get; set; }
    /// <summary>เลขที่ใบรายงาน (REF_NO, business key, unique): <c>LYYNNN</c>.</summary>
    public string ReportNo { get; set; } = "";
    /// <summary>เครื่องจักร (MC_NO) of module องค์กร, checked on save (no database key across modules, like HR).</summary>
    public int? MachineId { get; set; }
    /// <summary>เข้าสต็อกแล้ว: when โอนผลผลิตเข้าสต็อก last made this report's lines into stock entries (BR-005); null = not transferred.</summary>
    public DateTimeOffset? StockedAt { get; set; }

    public List<ProductionReportLine> Lines { get; set; } = [];

    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    string IHasBusinessKey.BusinessKey => ReportNo;
}

internal sealed class ProductionReportLine : IAudited
{
    public int Id { get; set; }
    public int ProductionReportId { get; set; }
    public ProductionReport ProductionReport { get; set; } = null!;
    /// <summary>ลำดับ (SUF_NO).</summary>
    public int LineNo { get; set; }
    /// <summary>กะ of module องค์กร (SHIFT).</summary>
    public int? ShiftId { get; set; }
    /// <summary>ชั่วโมงทำงาน (TT_T).</summary>
    public decimal? Hours { get; set; }
    /// <summary>Lot วัตถุดิบ 1 / 2 (LOT_MTR1 / LOT_MTR2): what the department received from the one before.</summary>
    public string? MaterialLot1 { get; set; }
    public string? MaterialLot2 { get; set; }
    public int? ProductId { get; set; }
    public Product? Product { get; set; }
    /// <summary>ชื่อสินค้า / หน่วย (PD_N, U_NAME): copies the user can change.</summary>
    public string ProductName { get; set; } = "";
    public string? Unit { get; set; }
    /// <summary>จำนวนดี (QTY).</summary>
    public decimal GoodQuantity { get; set; }
    /// <summary>เสีย (NG).</summary>
    public decimal? RejectQuantity { get; set; }
    /// <summary>จำนวนรวม (TOTAL) = ดี + เสีย, worked out on every save (AC-21).</summary>
    public decimal TotalQuantity { get; set; }
    /// <summary>พนักงาน of module บุคคล (EMP_ID).</summary>
    public int? EmployeeId { get; set; }
    public int? MoldId { get; set; }
    public Mold? Mold { get; set; }
    /// <summary>Lot ผลผลิต (LOT): typed by the user.</summary>
    public string? Lot { get; set; }

    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    // Loaded with the report by ProductionReportRegistry before every save.
    string IHasBusinessKey.BusinessKey => $"{ProductionReport?.ReportNo} #{LineNo}";
}
