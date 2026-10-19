using Lockte.SharedKernel.Persistence;

namespace Lockte.Sales.Data;

/// <summary>
/// ลูกค้า — one register for every kind of work (CONTEXT.md). For now only what the employee program needs (#41):
/// รหัส + ชื่อ of the งานรับจ้างผลิต customers (T-ชื่อลูกค้า, BR-009). The ORDER program adds the rest.
/// Internal to the module (ADR 0002): other modules see <see cref="CustomerDto"/> from <see cref="CustomerDirectory"/>.
/// </summary>
internal sealed class Customer : IAudited
{
    public int Id { get; set; }
    /// <summary>รหัสลูกค้า (business key, unique, upper case).</summary>
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";

    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    string IHasBusinessKey.BusinessKey => Code;
}
