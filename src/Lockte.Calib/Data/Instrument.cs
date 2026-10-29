using Lockte.SharedKernel.Persistence;

namespace Lockte.Calib.Data;

/// <summary>
/// เครื่องมือวัด — one row for every instrument of the six kinds (#191 "โครงข้อมูล"; the legacy had six near-identical tables).
/// Ranges, resolutions and limits are text, printed as typed (BR-006: no calculation). Internal to the module: others read
/// <see cref="InstrumentDto"/> from <see cref="InstrumentDirectory"/>.
/// </summary>
internal sealed class Instrument : IAudited
{
    public int Id { get; set; }
    public InstrumentKind Kind { get; set; }
    /// <summary>หมายเลขเครื่อง: required, unique over all kinds, trimmed (case does not matter when comparing).</summary>
    public string Code { get; set; } = "";
    /// <summary>ชื่อเครื่องมือวัด (JIG: ชื่อ JIG).</summary>
    public string? Name { get; set; }
    /// <summary>วันที่ขึ้นบัญชี.</summary>
    public DateOnly? RegisteredOn { get; set; }
    public string? Maker { get; set; }
    /// <summary>แผนกที่ใช้งาน, of module org.</summary>
    public int? DepartmentId { get; set; }
    /// <summary>พิกัดใช้งาน.</summary>
    public string? Range { get; set; }
    /// <summary>ค่าความละเอียด.</summary>
    public string? Resolution { get; set; }
    /// <summary>ขีดจำกัดที่ยอมรับได้.</summary>
    public string? Limit { get; set; }
    public string? Unit { get; set; }
    /// <summary>หมายเลขเครื่องมือที่ใช้ (ผ่านการสอบเทียบแล้ว) — BR-021; JIG: หมายเลขเวอร์เนียที่ใช้.</summary>
    public string? StandardUsed { get; set; }
    /// <summary>ผู้สอบเทียบ (ภายใน), an employee of module hr.</summary>
    public int? CalibratorEmployeeId { get; set; }
    /// <summary>ผู้สอบเทียบ (ภายนอก), a บริษัทที่สอบเทียบ (supplier of purch).</summary>
    public int? ProviderSupplierId { get; set; }
    /// <summary>แกน (mm) — JIG only (Q-06).</summary>
    public string? Axis { get; set; }
    /// <summary>คู่มือวิธีการที่ใช้เลขที่ — JIG only.</summary>
    public string? ManualNo { get; set; }
    public string? Note { get; set; }
    public List<InstrumentMachine> Machines { get; set; } = [];

    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    string IHasBusinessKey.BusinessKey => Code;
}

/// <summary>เครื่องจักรที่เกี่ยวข้องกับเครื่องมือวัด (0..n per instrument) — replaces the legacy's <c>DC (D02)</c> text.</summary>
internal sealed class InstrumentMachine : IAudited
{
    public int Id { get; set; }
    public int InstrumentId { get; set; }
    public Instrument? Instrument { get; set; }
    /// <summary>เครื่องจักร of module org.</summary>
    public int MachineId { get; set; }

    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    string IHasBusinessKey.BusinessKey => Instrument?.Code ?? InstrumentId.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
