using Lockte.SharedKernel.Persistence;

namespace Lockte.Sales.Data;

/// <summary>
/// ORDER หนึ่งใบ (T-ODD / T-ORDER, #108) — one row for the head the legacy file repeated on every line. The number is
/// unique (#28). Internal to the module (ADR 0002): other modules see <see cref="SalesOrderDto"/> from <see cref="SalesOrderDirectory"/>.
/// </summary>
internal sealed class SalesOrder : IAudited
{
    public int Id { get; set; }
    /// <summary>เลขที่ ORDER (business key, unique): <c>SS-YY-NNNN</c>, or what the user typed for an old / customer's own order.</summary>
    public string OrderNo { get; set; } = "";
    /// <summary>เลขเดิม: the number the order had in Access when the migration had to change it (a duplicate, #28).</summary>
    public string? OriginalNo { get; set; }
    public DateOnly OrderDate { get; set; }
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    /// <summary>ชื่อลูกค้า ณ วันที่สั่ง (CUS_N): a copy the user can change.</summary>
    public string CustomerName { get; set; } = "";
    public int? SalesTerritoryId { get; set; }
    public SalesTerritory? SalesTerritory { get; set; }
    /// <summary>กำหนดส่ง (DATE_DL).</summary>
    public DateOnly? DeliveryDate { get; set; }
    /// <summary>เงื่อนไขการชำระเงิน (DUERE_D): text, filled from the customer's เครดิต (ข้อสมมติ จ).</summary>
    public string? PaymentTerms { get; set; }
    /// <summary>ชื่อ + ที่อยู่ลูกค้าสำหรับพิมพ์ (REMARK).</summary>
    public string? Remark { get; set; }
    /// <summary>When the order entered ประวัติการรับ ORDER (the "พิมพ์" button); null = not in the history (AC-22).</summary>
    public DateTimeOffset? RecordedAt { get; set; }

    public List<SalesOrderLine> Lines { get; set; } = [];

    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    string IHasBusinessKey.BusinessKey => OrderNo;
}

/// <summary>
/// บรรทัดของ ORDER with its two sets of values (#28): <b>รับ</b> — what was received, set when the order enters the history
/// and edited apart from the other (แก้ไขประวัติ ORDER) — and <b>ค้างส่ง</b> — what is still to deliver.
/// </summary>
internal sealed class SalesOrderLine : IAudited
{
    public int Id { get; set; }
    public int SalesOrderId { get; set; }
    public SalesOrder SalesOrder { get; set; } = null!;
    /// <summary>ลำดับ (SUF_NO).</summary>
    public int LineNo { get; set; }
    /// <summary>สินค้า of module ผลิต, checked on save (no database key across modules, like HR).</summary>
    public int ProductId { get; set; }
    /// <summary>ชื่อสินค้า ณ วันที่สั่ง (PD_N).</summary>
    public string ProductName { get; set; } = "";
    public string? Unit { get; set; }
    /// <summary>ราคาต่อหน่วย (U_PRICE): typed on every line, never taken from the product.</summary>
    public decimal? UnitPrice { get; set; }
    /// <summary>รับ: จำนวน — null until the order enters the history.</summary>
    public decimal? ReceivedQuantity { get; set; }
    public decimal? ReceivedAmount { get; set; }
    /// <summary>ค้างส่ง: ยอดค้างส่ง (UND_QTY / QTY of T-ODD).</summary>
    public decimal OutstandingQuantity { get; set; }
    public decimal? OutstandingAmount { get; set; }

    // การส่งที่กรอกไว้แต่ยังไม่กด "บันทึก" (SCR-08: DL_DATE … DISC_PS of T-ODD): kept as soon as typed, processed by the
    // button for every line that has a PendingQuantity (BR-003), then cleared.
    public DateOnly? PendingDeliveryDate { get; set; }
    public string? PendingInvoiceNo { get; set; }
    public int? PendingInvoiceLine { get; set; }
    public decimal? PendingQuantity { get; set; }
    public decimal? PendingAmount { get; set; }
    public string? PendingLot { get; set; }
    public decimal? PendingBoxes { get; set; }

    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    // Loaded with the order by SalesOrderRegistry before every save.
    string IHasBusinessKey.BusinessKey => $"{SalesOrder?.OrderNo} #{LineNo}";
}
