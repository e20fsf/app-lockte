using Lockte.SharedKernel.Persistence;

namespace Lockte.Sales.Data;

/// <summary>
/// การส่งของ (T-ODDส่งออก, #111) — one delivery of one ORDER line, kept apart from the order: the copy of the line it was made
/// from (order number, customer, product, price) stays when the order is edited or deleted (BR-012/BR-014 do not give the
/// backorder back). One table for ครบ and ไม่ครบ (#28); delivering does not touch the stock (#28); no เดือน column — the
/// legacy year report wrote it into this table (BR-024). Internal to the module (ADR 0002).
/// </summary>
internal sealed class Delivery : IAudited
{
    public int Id { get; set; }
    /// <summary>เลขที่ ORDER of the line (REF_NO), as it was.</summary>
    public string OrderNo { get; set; } = "";
    public DateOnly? OrderDate { get; set; }
    /// <summary>ลำดับบรรทัดใน ORDER (SUF_NO).</summary>
    public int LineNo { get; set; }
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    /// <summary>ชื่อลูกค้า ณ วันที่สั่ง (CUS_N).</summary>
    public string CustomerName { get; set; } = "";
    public int? SalesTerritoryId { get; set; }
    /// <summary>สินค้า of module ผลิต, checked on save (no database key across modules, like HR).</summary>
    public int ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string? Unit { get; set; }
    public decimal? UnitPrice { get; set; }
    /// <summary>กำหนดส่ง (DATE_DL).</summary>
    public DateOnly? DueDate { get; set; }
    public DateOnly? DeliveryDate { get; set; }
    /// <summary>เลขที่บิล (INV_NO): an invoice of the outside billing system, only referred to.</summary>
    public string? InvoiceNo { get; set; }
    /// <summary>ลำดับบรรทัดในบิล (INV_SUF).</summary>
    public int? InvoiceLine { get; set; }
    public string? Lot { get; set; }
    /// <summary>จำนวนส่ง (DL_QTY).</summary>
    public decimal Quantity { get; set; }
    /// <summary>มูลค่าส่ง (DL_NET) = จำนวนส่ง × ราคา.</summary>
    public decimal? Amount { get; set; }
    /// <summary>จำนวนกล่อง (DISC_PS — not a discount, Q-14).</summary>
    public decimal? Boxes { get; set; }
    /// <summary>ส่งครบ (MARK = a) or ไม่ครบ (b): whether it closed the line's backorder.</summary>
    public bool Complete { get; set; }

    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    string IHasBusinessKey.BusinessKey => $"{OrderNo} #{LineNo} {InvoiceNo}".TrimEnd();
}
