using Lockte.SharedKernel.Persistence;

namespace Lockte.Prod.Data;

/// <summary>ชนิดคอลัมน์ผลการวัด (Variable) ของใบตรวจ: 1–8, คู่ที่ 1–10, แผ่นที่ 1–10, or split มีเขา / ไม่มีเขา.</summary>
public enum QaResultColumns : byte
{
    /// <summary>ชิ้นที่ 1–8.</summary>
    Pieces8 = 0,
    /// <summary>คู่ที่ 1–10.</summary>
    Pairs10 = 1,
    /// <summary>แผ่นที่ 1–10.</summary>
    Sheets10 = 2,
    /// <summary>แยกมีเขา / ไม่มีเขา.</summary>
    WithAndWithoutHorn = 3,
}

/// <summary>โครงหน้าของใบตรวจ: เต็ม, ย่อ, บรรจุ 1 แถบ, บรรจุฟอร์มเปล่า 4 แถบ.</summary>
public enum QaPageLayout : byte
{
    Full = 0,
    Short = 1,
    PackingOneStrip = 2,
    PackingBlankFourStrips = 3,
}

/// <summary>
/// ชนิดเอกสารใบตรวจ QA (เทมเพลต, #128): the control number of the form (รหัสเอกสาร + ฉบับที่ — master data, copied character for character from the legacy), its name, the
/// department and step, the unit sampled and how the sheet is laid out. One template serves many models (<see cref="QaModelSpec"/>). Internal to the module (ADR 0002).
/// </summary>
internal sealed class QaDocumentType : IAudited
{
    public int Id { get; set; }
    /// <summary>รหัสเอกสาร (เช่น <c>WI-QA022</c>) — business key.</summary>
    public string Code { get; set; } = "";
    /// <summary>ฉบับที่ — printed after the code exactly as typed.</summary>
    public string? Revision { get; set; }
    /// <summary>ชื่อใบตรวจ.</summary>
    public string Name { get; set; } = "";
    /// <summary>แผนก / ขั้นตอนที่ตรวจ.</summary>
    public string? DepartmentStep { get; set; }
    /// <summary>หน่วยสุ่ม (ชิ้น / คู่ / แผ่น).</summary>
    public string? SampleUnit { get; set; }
    public QaResultColumns ResultColumns { get; set; }
    /// <summary>ช่วงคอลัมน์ของรายการตำหนิ (Attribute), เช่น <c>1-10,11-20,…</c>; one or two sets of ranges separated by <c>;</c>.</summary>
    public string? AttributeRanges { get; set; }
    public QaPageLayout PageLayout { get; set; }
    /// <summary>ขนาดกระดาษ (A4 ตั้ง / A4 นอน / Letter …) as the sample is printed.</summary>
    public string? Paper { get; set; }

    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    string IHasBusinessKey.BusinessKey => Code;
}

/// <summary>
/// สเปคตรวจรายรุ่น (#128): the fixed values a model's sheet used to carry in its report labels, now data the QA edits — the models it serves, its measure points and defects,
/// the picture of the piece with the points numbered, and the model's own sequence number of the document (<c>-n</c>).
/// </summary>
internal sealed class QaModelSpec : IAudited
{
    public int Id { get; set; }
    public int DocumentTypeId { get; set; }
    public QaDocumentType DocumentType { get; set; } = null!;
    /// <summary>ชื่อสเปค (the report the sheet came from / the model) — unique within the document type.</summary>
    public string Name { get; set; } = "";
    /// <summary>รุ่นที่ใช้ได้ร่วม: a comma-separated list, printed as typed.</summary>
    public string? Models { get; set; }
    /// <summary>ลำดับเลขเอกสารของรุ่น (<c>-n</c>).</summary>
    public int? DocumentSequence { get; set; }
    public byte[]? ImageData { get; set; }
    public string? ImageContentType { get; set; }

    public List<QaMeasurePoint> Points { get; set; } = [];
    public List<QaDefect> Defects { get; set; } = [];

    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    string IHasBusinessKey.BusinessKey => Name;
}

/// <summary>จุดวัดของสเปค: the number, the text of the required value and its limits, both as text so they print exactly as written (ค่ากลาง / พิกัด / ต่ำสุด–สูงสุด / มากกว่า–น้อยกว่า).</summary>
internal sealed class QaMeasurePoint : IAudited
{
    public int Id { get; set; }
    public int QaModelSpecId { get; set; }
    public QaModelSpec QaModelSpec { get; set; } = null!;
    /// <summary>ลำดับ (and the order it prints in).</summary>
    public int Sequence { get; set; }
    /// <summary>เลขจุดวัดในรูป.</summary>
    public string? PointNo { get; set; }
    /// <summary>ข้อความค่าที่กำหนด.</summary>
    public string? Description { get; set; }
    /// <summary>ค่ากลาง / พิกัด / ต่ำสุด–สูงสุด / มากกว่า–น้อยกว่า — as printed.</summary>
    public string? Standard { get; set; }

    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    string IHasBusinessKey.BusinessKey => $"{QaModelSpec?.Name} #{Sequence}";
}

/// <summary>รายการตำหนิ (Attribute) ของสเปค.</summary>
internal sealed class QaDefect : IAudited
{
    public int Id { get; set; }
    public int QaModelSpecId { get; set; }
    public QaModelSpec QaModelSpec { get; set; } = null!;
    public int Sequence { get; set; }
    public string Name { get; set; } = "";

    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    string IHasBusinessKey.BusinessKey => $"{QaModelSpec?.Name} {Name}";
}

/// <summary>
/// จำนวนสุ่มตามขนาด Lot (BR-017): a lot of up to <see cref="UpTo"/> good pieces is sampled <see cref="SampleSize"/>; the row with no limit is the last. Reference data a migration
/// fills; the QA reads it, and the sample size of a lot is the first row whose limit is not below it.
/// </summary>
internal sealed class QaSampleSize : IAudited
{
    public int Id { get; set; }
    /// <summary>จำนวนดีสูงสุดของช่วง (รวม); null = ไม่มีขีดจำกัด.</summary>
    public int? UpTo { get; set; }
    public int SampleSize { get; set; }

    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    string IHasBusinessKey.BusinessKey => UpTo is { } limit ? $"≤ {limit}" : "ไม่จำกัด";
}
