using Lockte.SharedKernel.Persistence;

namespace Lockte.Cash.Data;

/// <summary>
/// บรรทัดค่าใช้จ่ายเงินสดย่อย (T-ซื้อของเงินสด, BR-002): one item paid from the petty cash. There is no header table: a ใบสำคัญจ่าย is the group of lines with the same
/// (<see cref="Date"/>, <see cref="VoucherNo"/>), and the number repeats across days and years (Q-03, assumption จ), so it is kept as typed and is not unique. The amount is kept
/// as given — it is what was paid, not always quantity × price (4,980 legacy lines have price 0).
/// </summary>
internal sealed class ExpenseLine : IAudited
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    /// <summary>เลขที่ใบสำคัญจ่าย as typed (<c>MM/NN</c>, <c>MM/NNN</c> or whatever the old data has).</summary>
    public string VoucherNo { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal Qty { get; set; }
    public string? Unit { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Amount { get; set; }
    public int ExpenseTypeId { get; set; }
    public ExpenseType ExpenseType { get; set; } = null!;

    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    string IHasBusinessKey.BusinessKey => $"{Date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)} {VoucherNo} {Name}";
}
