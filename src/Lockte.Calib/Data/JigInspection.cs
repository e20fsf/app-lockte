using Lockte.SharedKernel.Persistence;

namespace Lockte.Calib.Data;

/// <summary>
/// แบบตรวจ JIG รายรุ่น (RPT-05 วัดศูนย์ / RPT-06 กรอ): the legacy printed these as 42 reports with the specifications typed into each; here
/// they are master data QA edits. One sheet = one model of JIG, used for every instrument number of that model — not tied to the register (BR-031).
/// </summary>
internal sealed class JigInspectionSheet : IAudited
{
    public int Id { get; set; }
    public JigSheetType Type { get; set; }
    /// <summary>รุ่น: the label of the button that opened it in SCR-05 (<c>RX100</c>, <c>GTO F</c>). Unique within the type.</summary>
    public string ModelKey { get; set; } = "";
    /// <summary>ชื่อ JIG บนแบบ, printed on the sheet (<c>RX100,MATE U,BELL,087,200</c>).</summary>
    public string Title { get; set; } = "";
    /// <summary>The key of its เลขเอกสารควบคุม (the program of reports keeps the number itself).</summary>
    public string DocumentKey { get; set; } = "";
    /// <summary>เลขที่ในเอกสาร <c>WI-QA049-n</c>, the order SCR-05 lists them in.</summary>
    public int DocumentNo { get; set; }
    public List<JigInspectionPoint> Points { get; set; } = [];

    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    string IHasBusinessKey.BusinessKey => $"{(Type == JigSheetType.ZeroCheck ? "วัดศูนย์" : "กรอ")} {ModelKey}";
}

/// <summary>จุดวัด of a sheet: <c>P1</c>, <c>P2.1</c> … with its kind, specification and tolerance.</summary>
internal sealed class JigInspectionPoint : IAudited
{
    public int Id { get; set; }
    public int SheetId { get; set; }
    public JigInspectionSheet? Sheet { get; set; }
    public int SortOrder { get; set; }
    /// <summary>Point, e.g. <c>P2.1</c>.</summary>
    public string Label { get; set; } = "";
    public JigPointKind Kind { get; set; }
    /// <summary>ค่าที่กำหนด (mm).</summary>
    public decimal Value { get; set; }
    public JigToleranceKind Tolerance { get; set; }
    /// <summary>The ± in mm for <see cref="JigToleranceKind.PlusMinus"/>; null for "มากกว่า".</summary>
    public decimal? ToleranceValue { get; set; }

    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    string IHasBusinessKey.BusinessKey => $"{(Sheet?.ModelKey ?? SheetId.ToString(System.Globalization.CultureInfo.InvariantCulture))} {Label}";
}
