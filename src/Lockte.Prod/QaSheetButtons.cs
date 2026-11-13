namespace Lockte.Prod;

/// <summary>A button of a model as the legacy form laid it out: its caption, the spec (report) it prints, and the block it stands in (อัดผ้าเบรค has ผ้าเบรค and ดิสเบรค).</summary>
public sealed record QaSheetButton(string Caption, string SpecName, string? Section = null);

/// <summary>A button to show: the caption and the spec it prints.</summary>
public sealed record QaSheetButtonView(string Caption, QaSpecSummary Spec, string? Section);

/// <summary>
/// ปุ่มรุ่นของฟอร์มพิมพ์ใบตรวจ QA (UAT-45, #442): the buttons of <c>P_REP6ไดคาสท์</c>, <c>P_REP6ตกแต่ง</c>, <c>P_REP6อัดผ้าเบรค</c> and <c>ตรวจสอบพิมพ์</c> in the order they stand on the form
/// (row by row, left to right) with their captions. Some buttons print the sheet of another model — XL078 and 043 print C100's, BELL, MATE U and SPARK115I print RX100's — as the legacy did.
/// A spec of the department that no button names (one the QA added later) gets a button of its own after these, by name.
/// </summary>
public static class QaSheetButtons
{
    private static QaSheetButton[] Production(string group) =>
    [
        new("029", group + "029"), new("052", group + "052"), new("061", group + "061"), new("063", group + "063"),
        new("147", group + "147"), new("168", group + "168"), new("233", group + "233"), new("A100,GTO R", group + "A100"),
        new("AR125", group + "AR125"), new("C100,WAVE", group + "C100"), new("C100 DAS", group + "C100 DAS"), new("CLICK,016", group + "CLICK"),
        new("DT100,105", group + "DT100"), new("JOG", group + "JOG"), new("K125", group + "K125"), new("NH50,SS50,098,048", group + "NH50"),
        new("NOUVO", group + "NOUVO"), new("RX100", group + "RX100"), new("SPACY,310", group + "SPACY"), new("TACT", group + "TACT"),
        new("TS100,GTOF", group + "TS100"), new("V100", group + "V100"), new("WING", group + "WING"), new("XL078,078", group + "C100"),
        new("043", group + "C100"), new("BELL,200", group + "RX100"), new("MATE U,087", group + "RX100"), new("KAZE", group + "KAZE"),
        new("068", group + "068"), new("ATTILA", group + "ATTILA"), new("143", group + "143"), new("SPARK115I", group + "RX100"),
    ];

    private const string Pad = "ผ้าเบรค";
    private const string Disc = "ดิสเบรค";

    private static readonly QaSheetButton[] Press =
    [
        new("TS100", "อัดผ้าเบรคTS100", Pad), new("DT100", "อัดผ้าเบรคDT100", Pad), new("NOUVO", "อัดผ้าเบรคNOUVO", Pad), new("A100,GTO R", "อัดผ้าเบรคA100", Pad),
        new("K125", "อัดผ้าเบรคK125", Pad), new("WING", "อัดผ้าเบรคWING", Pad), new("NH50,SS50", "อัดผ้าเบรคSS50", Pad), new("SPACY", "อัดผ้าเบรคSPACY", Pad),
        new("CLICK", "อัดผ้าเบรคCLICK", Pad),
        new("WAVE", "อัดผ้าเบรคWAVE", Pad), new("C100,C100 DAS", "อัดผ้าเบรคC100", Pad), new("JOG", "อัดผ้าเบรคJOG", Pad), new("233", "อัดผ้าเบรค233", Pad),
        new("TACT,143", "อัดผ้าเบรคTACT", Pad), new("V100", "อัดผ้าเบรคV100", Pad), new("KAZE", "อัดผ้าเบรคKAZE", Pad), new("ATTILA", "อัดผ้าเบรคATTILA", Pad),
        new("068", "อัดผ้าเบรค068", Pad),
        new("AKIRA F", "อัดดิสเบรคAKIRA'F", Disc), new("AKIRA R", "อัดดิสเบรคAKIRA'R", Disc), new("AR125", "อัดดิสเบรคAR125", Disc), new("BELLE R", "อัดดิสเบรคBELLE R", Disc),
        new("CLICK", "อัดดิสเบรคCLICK", Disc), new("COSMO", "อัดดิสเบรคCOSMO", Disc), new("DIO", "อัดดิสเบรคDIO", Disc), new("FD0080", "อัดดิสเบรคFD0080", Disc),
        new("FD0223", "อัดดิสเบรคFD0223", Disc), new("KR F", "อัดดิสเบรคKR F", Disc), new("KR R", "อัดดิสเบรคKR R", Disc), new("LP204", "อัดดิสเบรคLP204", Disc),
        new("LP218", "อัดดิสเบรคLP218", Disc), new("LP305", "อัดดิสเบรคLP305", Disc),
        new("LP401", "อัดดิสเบรคLP401", Disc), new("LP402", "อัดดิสเบรคLP402", Disc), new("LP817", "อัดดิสเบรคLP817", Disc), new("MIO", "อัดดิสเบรคMIO", Disc),
        new("NOVA", "อัดดิสเบรคNOVA", Disc), new("NSR", "อัดดิสเบรคNSR", Disc), new("RGV", "อัดดิสเบรคRGV", Disc), new("SONIC", "อัดดิสเบรคSONIC", Disc),
        new("SPARK135", "อัดดิสเบรคSPARK135", Disc), new("TIARA F", "อัดดิสเบรคTIARA F", Disc), new("TORNADO", "อัดดิสเบรคTORNADO", Disc), new("TZM F", "อัดดิสเบรคTZM F", Disc),
        new("TZM R", "อัดดิสเบรคTZM R", Disc), new("VICTOR", "อัดดิสเบรคVICTOR", Disc),
        new("VR150", "อัดดิสเบรคVR150", Disc), new("WAVE", "อัดดิสเบรคWAVE", Disc), new("XLR250", "อัดดิสเบรคXLR250", Disc), new("ICON", "อัดดิสเบรคICON", Disc),
        new("CLICKI", "อัดดิสเบรคCLICKI", Disc), new("CZI", "อัดดิสเบรคCZI", Disc), new("KSR", "อัดดิสเบรคKSR", Disc), new("PCX", "อัดดิสเบรคPCX", Disc),
        new("CBR150I", "อัดดิสเบรคCBR 150 I", Disc), new("CBR250F", "อัดดิสเบรคCBR 250 F", Disc), new("CBR250R", "อัดดิสเบรคCBR 250 R", Disc),
    ];

    private static readonly QaSheetButton[] Mold =
    [
        new("029", "พิมพ์029"), new("052", "พิมพ์052"), new("061", "พิมพ์061"), new("063", "พิมพ์063"),
        new("147", "พิมพ์147"), new("168", "พิมพ์168"), new("233", "พิมพ์233"), new("A100,GTOR", "พิมพ์A100"),
        new("AR125", "พิมพ์AR125"), new("C100,WAVE", "พิมพ์C100"), new("C100 DAS", "พิมพ์C100 DAS"), new("CLICK", "พิมพ์CLICK"),
        new("DT100,105", "พิมพ์DT100"), new("JOG", "พิมพ์JOG"), new("K125", "พิมพ์K125"), new("NH50,SS50", "พิมพ์NH50"),
        new("NOUVO", "พิมพ์NOUVO"), new("RX100", "พิมพ์RX100"), new("SPACY", "พิมพ์SPACY"), new("TACT", "พิมพ์TACT"),
        new("TS100,GTOF", "พิมพ์TS100"), new("V100", "พิมพ์V100"), new("WING", "พิมพ์WING"), new("XL078,078", "พิมพ์C100"),
        new("043", "พิมพ์C100"), new("BELLE,200", "พิมพ์RX100"), new("MATE U,087", "พิมพ์RX100"), new("KAZE", "พิมพ์KAZE"),
        new("068", "พิมพ์068"), new("ATTILA", "พิมพ์ATTILA"), new("143", "พิมพ์143"), new("SPARK115I", "พิมพ์RX100"),
    ];

    /// <summary>The buttons of the legacy form of a department (D, T, B); none for a department whose form is not laid out here — its specs show by name.</summary>
    public static IReadOnlyList<QaSheetButton> OfDepartment(string departmentCode) => departmentCode.Trim().ToUpperInvariant() switch
    {
        "D" => Production("ไดคาสท์"),
        "T" => Production("ตกแต่ง"),
        "B" => Press,
        _ => [],
    };

    /// <summary>The buttons of <c>ตรวจสอบพิมพ์</c>.</summary>
    public static IReadOnlyList<QaSheetButton> OfMold { get; } = Mold;

    /// <summary>
    /// The buttons to show: those of the layout whose spec is there, in the layout's order; then a button for each spec no button names, by name, with the short name as caption.
    /// </summary>
    public static IReadOnlyList<QaSheetButtonView> Arrange(IReadOnlyList<QaSheetButton> layout, IReadOnlyList<QaSpecSummary> specs)
    {
        var byName = specs.GroupBy(s => s.Name, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var buttons = layout.Where(b => byName.ContainsKey(b.SpecName)).Select(b => new QaSheetButtonView(b.Caption, byName[b.SpecName], b.Section)).ToList();
        var named = layout.Select(b => b.SpecName).ToHashSet(StringComparer.Ordinal);
        var extraSection = layout.Count > 0 && layout.Any(b => b.Section is not null) ? "อื่น ๆ" : null;
        buttons.AddRange(specs.Where(s => !named.Contains(s.Name)).OrderBy(s => s.Name, StringComparer.Ordinal).Select(s => new QaSheetButtonView(ShortName(s.Name), s, extraSection)));
        return buttons;
    }

    /// <summary>The name of a spec without its group — <c>ไดคาสท์029</c> → <c>029</c>.</summary>
    public static string ShortName(string name)
    {
        var group = QaSpecGroups.GroupOf(name);
        return group.Length > 0 && name.StartsWith(group, StringComparison.Ordinal) && name.Length > group.Length ? name[group.Length..] : name;
    }
}
