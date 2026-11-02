using Lockte.SharedKernel.Persistence;

namespace Lockte.Cash.Data;

/// <summary>
/// ประเภทค่าใช้จ่าย (T-ค่าใช้จ่ายต่างๆ, BR-001): the account heading a line of ค่าใช้จ่ายเงินสดย่อย is grouped under. Lines refer to it by <see cref="Id"/> (the legacy
/// copied the name as text), so renaming it renames it on every line. A type that was used is never deleted, only <see cref="IsActive"/> = false: it leaves the pick lists but
/// the lines and reports still show it.
/// </summary>
internal sealed class ExpenseType : IAudited
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    /// <summary>False = เลิกใช้: hidden from the pick lists, still readable.</summary>
    public bool IsActive { get; set; } = true;

    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    string IHasBusinessKey.BusinessKey => Name;
}
