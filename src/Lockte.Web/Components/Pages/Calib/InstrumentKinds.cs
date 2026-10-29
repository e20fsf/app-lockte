using Lockte.Calib;
using Lockte.Web.Menus;

namespace Lockte.Web.Components.Pages.Calib;

/// <summary>
/// What differs between the six kinds on the screens (SCR-02/03/04): the key in the URL, the menu page they return to, the headings
/// (spelt as the legacy forms had them), and which fields the form shows (screens.md SCR-02 "ความต่างของชนิดอื่น").
/// </summary>
public sealed record InstrumentKindInfo(
    InstrumentKind Kind,
    string Key,
    int MenuNumber,
    string AddHeading,
    string EditHeading,
    string NameLabel,
    bool ShowMaker = true,
    bool ShowRange = true,
    bool ShowResolution = true,
    bool ShowUnit = true,
    bool ShowAxis = false,
    bool ShowManual = false,
    string StandardLabel = "หมายเลขเครื่องมือที่ใช้ (ผ่านการสอบเทียบแล้ว)",
    bool ShowStandard = true,
    bool Internal = true)
{
    public string AddHref => $"calib/instruments/{Key}/new";
    public string EditHref => $"calib/instruments/{Key}";

    public static IReadOnlyList<InstrumentKindInfo> All { get; } =
    [
        new(InstrumentKind.External, "ext", CalibMenu.External, "เพิ่มเครื่องมือวัดสอบเทียบภายนอก", "แก้ไขและลบเครื่องมือวัดสอบเทียบภายนอก", "ชื่อเครื่องมือวัด",
            ShowStandard: false, Internal: false),
        new(InstrumentKind.Caliper, "caliper", CalibMenu.Caliper, "เพิ่มรายชื่อเครื่องมือวัดสอบเทียบภายใน", "แก้ไขและลบเครื่องมือวัด (เวอร์เนีย, ไฮเกจ, ไมโครมิเตอร์)", "ชื่อเครื่องมือวัด"),
        new(InstrumentKind.PressureGauge, "pg", CalibMenu.PressureGauge, "เพิ่มรายชื่อเครื่องมือวัดสอบเทียบภายใน", "แก้ไขและลบเครื่องมือวัด (เพรสเซอร์เกจ)", "ชื่อเครื่องมือวัด"),
        new(InstrumentKind.Scale, "scale", CalibMenu.Scale, "เพิ่มรายชื่อเครื่องมือวัดสอบเทียบภายใน", "แก้ไขและลบเครื่องมือวัด (เครื่องชั่ง)", "ชื่อเครื่องมือวัด"),
        new(InstrumentKind.Jig, "jig", CalibMenu.Jig, "เพิ่มรายชื่อ JIG", "แก้ไขและลบรายชื่อ JIG", "ชื่อ JIG",
            ShowMaker: false, ShowRange: false, ShowResolution: false, ShowUnit: false, ShowAxis: true, ShowManual: true,
            StandardLabel: "หมายเลขเวอร์เนียที่ใช้ (ผ่านการสอบเทียบแล้ว)"),
        new(InstrumentKind.TempControl, "tc", CalibMenu.TempControl, "เพิ่มรายชื่อเครื่องมือวัดสอบเทียบภายใน", "แก้ไขและลบเครื่องมือวัด (ตัวควบคุมอุณหภูมิ)", "ชื่อเครื่องมือวัด"),
    ];

    public static InstrumentKindInfo? Find(string? key) => All.FirstOrDefault(k => string.Equals(k.Key, key, StringComparison.OrdinalIgnoreCase));

    public static InstrumentKindInfo Of(InstrumentKind kind) => All.First(k => k.Kind == kind);
}

/// <summary>The fields of the instrument form, as the screens edit them.</summary>
public sealed class InstrumentFormModel
{
    public int? Id { get; set; }
    public string Code { get; set; } = "";
    public string? Name { get; set; }
    public DateTime? RegisteredOn { get; set; }
    public string? Maker { get; set; }
    public int? DepartmentId { get; set; }
    public HashSet<int> MachineIds { get; set; } = [];
    public string? Range { get; set; }
    public string? Resolution { get; set; }
    public string? Limit { get; set; }
    public string? Unit { get; set; }
    public string? StandardUsed { get; set; }
    public CalibEmployeeRef? Calibrator { get; set; }
    public string? Axis { get; set; }
    public string? ManualNo { get; set; }
    public string? Note { get; set; }
    public byte[]? RowVersion { get; set; }

    public static InstrumentFormModel From(InstrumentDto i) => new()
    {
        Id = i.Id, Code = i.Code, Name = i.Name, RegisteredOn = i.RegisteredOn?.ToDateTime(TimeOnly.MinValue), Maker = i.Maker, DepartmentId = i.DepartmentId,
        MachineIds = i.MachineIds.ToHashSet(), Range = i.Range, Resolution = i.Resolution, Limit = i.Limit, Unit = i.Unit, StandardUsed = i.StandardUsed,
        Calibrator = i.CalibratorEmployeeId is { } id ? new CalibEmployeeRef(id, "", i.CalibratorName ?? "") : null,
        Axis = i.Axis, ManualNo = i.ManualNo, Note = i.Note, RowVersion = i.RowVersion,
    };

    public InstrumentInput ToInput(InstrumentKind kind) => new(Id, kind, Code, RowVersion, Name,
        RegisteredOn is { } d ? DateOnly.FromDateTime(d) : null, Maker, DepartmentId, MachineIds.ToList(), Range, Resolution, Limit, Unit,
        StandardUsed, Calibrator?.Id, Axis, ManualNo, Note);

    /// <summary>Whether the user typed anything: an untouched form needs no save when leaving.</summary>
    public bool IsEmpty => string.IsNullOrWhiteSpace(Code) && string.IsNullOrWhiteSpace(Name) && RegisteredOn is null && string.IsNullOrWhiteSpace(Maker)
        && DepartmentId is null && MachineIds.Count == 0 && string.IsNullOrWhiteSpace(Range) && string.IsNullOrWhiteSpace(Resolution)
        && string.IsNullOrWhiteSpace(Limit) && string.IsNullOrWhiteSpace(Unit) && string.IsNullOrWhiteSpace(StandardUsed) && Calibrator is null
        && string.IsNullOrWhiteSpace(Axis) && string.IsNullOrWhiteSpace(ManualNo) && string.IsNullOrWhiteSpace(Note);
}
