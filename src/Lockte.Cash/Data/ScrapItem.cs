using Lockte.SharedKernel.Persistence;

namespace Lockte.Cash.Data;

/// <summary>ชนิดของเก่า (T-รายชื่อของเก่า, BR-010): the name and unit offered on a receipt line, which keeps a copy of both.</summary>
internal sealed class ScrapItem : IAudited
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Unit { get; set; }

    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    string IHasBusinessKey.BusinessKey => Name;
}
