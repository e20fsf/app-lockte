using Lockte.SharedKernel.Persistence;

namespace Lockte.Cash.Data;

/// <summary>
/// ผู้ซื้อของเก่า (T-ผู้ซื้อของเก่า, BR-010): a register of its own, apart from customers (CONTEXT.md). A receipt keeps a copy of name, address and phone (Q-30), so editing or
/// deleting a buyer never changes a receipt already issued.
/// </summary>
internal sealed class ScrapBuyer : IAudited
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Address { get; set; }
    /// <summary>โทรศัพท์; several numbers are separated by a comma.</summary>
    public string? Tel { get; set; }

    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    string IHasBusinessKey.BusinessKey => Name;
}
