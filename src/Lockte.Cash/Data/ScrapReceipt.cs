using Lockte.SharedKernel.Persistence;

namespace Lockte.Cash.Data;

/// <summary>
/// ใบเสร็จขายของเก่า (T-ขายของเก่า, BR-011–BR-013): the head of a receipt — there is no head table in the legacy, its date, buyer, address and phone were repeated on every line. The
/// number is unique and is reserved when the receipt is saved (no scratch table of numbers, no CODE glued from number and line). The buyer is a copy of what was on the receipt
/// (<see cref="BuyerName"/>, address, phone), so the register can change or lose the buyer without changing a receipt already issued (Q-30); <see cref="ScrapBuyerId"/> only remembers
/// which register entry it was picked from. A receipt is never deleted: it is <see cref="IsCancelled"/> (BR-017, assumption ง) and its number stays used.
/// </summary>
internal sealed class ScrapReceipt : IAudited
{
    public int Id { get; set; }
    /// <summary>เลขที่ <c>YYNNN</c>: the last two digits of the Buddhist year and a running number of at least three digits.</summary>
    public string No { get; set; } = "";
    public DateOnly Date { get; set; }
    public string BuyerName { get; set; } = "";
    public string? BuyerAddress { get; set; }
    public string? BuyerTel { get; set; }
    public int? ScrapBuyerId { get; set; }
    public bool IsCancelled { get; set; }
    public string? CancelReason { get; set; }
    public string? CancelledBy { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    /// <summary>The การนำส่ง this receipt was closed into (ตัดยอดนำส่ง); none = ยอดรอนำส่ง.</summary>
    public int? RemittanceId { get; set; }
    public ScrapRemittance? Remittance { get; set; }

    public List<ScrapReceiptLine> Lines { get; set; } = [];

    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    string IHasBusinessKey.BusinessKey => No;
}

/// <summary>A line of a receipt; <see cref="Seq"/> (ลำดับที่) is unique within the receipt. The item name and unit are copies, like the buyer's.</summary>
internal sealed class ScrapReceiptLine : IAudited
{
    public int Id { get; set; }
    public int ReceiptId { get; set; }
    public ScrapReceipt? Receipt { get; set; }
    public int Seq { get; set; }
    public string ItemName { get; set; } = "";
    public int? ScrapItemId { get; set; }
    public string? Unit { get; set; }
    public decimal Qty { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Amount { get; set; }

    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    string IHasBusinessKey.BusinessKey => $"{Receipt?.No} ลำดับ {Seq.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
}
