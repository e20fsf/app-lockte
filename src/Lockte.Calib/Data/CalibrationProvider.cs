using Lockte.SharedKernel.Persistence;

namespace Lockte.Calib.Data;

/// <summary>
/// บริษัทที่สอบเทียบ — a supplier of module purch that does calibrations (CONTEXT.md; ADR 0002: the supplier lives in the one register of
/// purch, this table only marks which of them are shown as บริษัทที่สอบเทียบ). <see cref="Name"/> is the name when it was marked, so the history reads.
/// </summary>
internal sealed class CalibrationProvider : IAudited
{
    public int Id { get; set; }
    /// <summary>ผู้ขาย of module purch.</summary>
    public int SupplierId { get; set; }
    public string Name { get; set; } = "";

    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    string IHasBusinessKey.BusinessKey => Name;
}
