using Lockte.Calib;

namespace Lockte.Web.Reports.Calib;

/// <summary>
/// What the head of a แบบฟอร์มบันทึกผลการสอบเทียบ (RPT-03) prints; every field may be blank. <see cref="Calibrator"/> goes in "ผู้สอบเทียบ" and <see cref="Provider"/> in
/// "หน่วยงานสอบเทียบ" (the external sheet: company there, the calibrator box empty).
/// </summary>
public sealed record RecordHead(
    string? Name,
    string? Code,
    string? Range,
    string? Limit,
    string? StandardUsed,
    string? Calibrator,
    string? Provider,
    bool External);

/// <summary>
/// RPT-03 แบบฟอร์มบันทึกผลการสอบเทียบ (เปล่า, PC-QA009-4/2547 ฉบับที่ 2), ระดับ A (#205 #206, Q-26): A4 portrait, one instrument to a page, the head typed from the register and a
/// results table of 20 empty rows to fill in by hand (the system keeps no result, BR-031). Layout in mm, baselines for texts, as print-samples.md describes the sample; the
/// positions are checked by overlaying the PDF on the sample, and the user signs the form off.
/// </summary>
public static class RecordForm
{
    public const string Title = "บันทึกผลการสอบเทียบ";
    public const int Rows = 20;

    private const double Left = 12;
    private const double Right = 198;
    private const double Mid = 106;
    private const double Row = 7;
    private const double TableTop = 118;

    /// <summary>One page for each head, in the order given.</summary>
    public static IReadOnlyList<FormPage> Layout(IReadOnlyList<RecordHead> heads, string? documentNumber)
        => heads.Select(h => Page(h, documentNumber)).ToList();

    public static FormPage Page(RecordHead head, string? documentNumber)
    {
        var page = new FormPage(Paper.A4Portrait);
        // title band: company, name of the form, the control number on the line of the title, a thick rule under it
        page.Text(105, 17, "บริษัท ล็อคเต้ จำกัด", 20, TextAnchor.Middle, bold: true);
        page.Text(105, 26, Title, 18, TextAnchor.Middle, bold: true);
        page.Text(Right, 26, documentNumber, 12, TextAnchor.End);
        page.Line(Left, 30, Right, 30, 1.2);

        Field(page, 41, Left, 34, Mid - 3, "ชื่อเครื่องมือ", head.Name);
        Field(page, 41, Mid, 138, Right, "หมายเลขเครื่องมือ", head.Code);
        Field(page, 50, Left, 31, Mid - 3, "พิกัดใช้งาน", head.Range);
        Field(page, 50, Mid, 140, Right, "ขีดจำกัดที่ยอมรับได้", head.Limit);
        Field(page, 59, Left, 31, Mid - 3, "ผู้สอบเทียบ", head.Calibrator);
        Field(page, 59, Mid, 134, Right, "หน่วยงานสอบเทียบ", head.Provider);
        Field(page, 68, Left, 78, Right, "หมายเลขเครื่องมือที่ใช้(ผ่านการสอบเทียบแล้ว)", head.StandardUsed);
        Field(page, 77, Left, 36, 75, "วันที่สอบเทียบ", null);
        Tick(page, 82, 75, "สอบเทียบภายใน", !head.External);
        Tick(page, 152, 75, "สอบเทียบภายนอก", head.External);
        Field(page, 86, Left, 54, Mid - 3, "คู่มือวิธีการที่ใช้เลขที่", null);
        Field(page, 86, Mid, 143, Right, "หมายเลขเอกสารสอบเทียบ", null);
        page.Text(Left, 95, "สภาวะการสอบเทียบ", 14);
        Field(page, 95, 46, 75, 98, "อุณหภูมิ", null, "°C");
        Field(page, 95, 102, 125, 150, "ความชื้น", null, "%RH");
        Table(page);
        Summary(page);
        return page;
    }

    /// <summary>A label, the dotted line to write on and the value typed over it.</summary>
    private static void Field(FormPage page, double baseline, double labelX, double lineFrom, double lineTo, string label, string? value, string? suffix = null)
    {
        page.Text(labelX, baseline, label, 14);
        page.Dots(lineFrom, lineTo, baseline + 0.6);
        page.Text(lineFrom + 1, baseline - 0.4, value, 14, fit: lineTo - lineFrom - 2);
        if (suffix is not null) page.Text(lineTo + 1, baseline, suffix, 14);
    }

    /// <summary>A box with its label; <paramref name="ticked"/> boxes are printed already crossed (the legacy printed the mark of the kind).</summary>
    private static void Tick(FormPage page, double x, double baseline, string label, bool ticked)
    {
        page.Box(x - 7, baseline - 3.6, 4.6, 4.6);
        if (ticked)
        {
            page.Line(x - 7, baseline - 3.6, x - 2.4, baseline + 1, 0.5);
            page.Line(x - 2.4, baseline - 3.6, x - 7, baseline + 1, 0.5);
        }
        page.Text(x - 1, baseline, label, 14);
    }

    private static readonly (double From, double To, string Head)[] Columns =
    [
        (12, 25, "จุดที่"), (25, 62, "ค่ามาตรฐาน"), (62, 83, "1"), (83, 104, "2"), (104, 125, "3"), (125, 145, "ค่าเฉลี่ย"), (145, 165, "ค่าผิดพลาด"), (165, 198, "หมายเหตุ"),
    ];

    private static void Table(FormPage page)
    {
        var bottom = TableTop + 12 + Rows * Row;
        page.Box(Columns[0].From, TableTop, Right - Columns[0].From, bottom - TableTop);
        page.Line(Left, TableTop + 12, Right, TableTop + 12);
        page.Line(62, TableTop + 6, 125, TableTop + 6);
        foreach (var (from, to, head) in Columns)
        {
            if (from != Left) page.Line(from, from is 83 or 104 ? TableTop + 6 : TableTop, from, bottom);
            var inMeasured = from is >= 62 and < 125;
            page.Text((from + to) / 2, inMeasured ? TableTop + 11 : TableTop + 8, head, 13, TextAnchor.Middle);
        }
        page.Text(93.5, TableTop + 4.8, "ค่าที่วัดได้", 13, TextAnchor.Middle);
        for (var i = 1; i <= Rows; i++)
        {
            var top = TableTop + 12 + (i - 1) * Row;
            if (i > 1) page.Line(Left, top, Right, top, 0.25);
            page.Text((Columns[0].From + Columns[0].To) / 2, top + 5, i.ToString(System.Globalization.CultureInfo.InvariantCulture), 13, TextAnchor.Middle);
        }
    }

    private static void Summary(FormPage page)
    {
        var y = TableTop + 12 + Rows * Row + 11;
        page.Text(Left, y, "สรุปผลการสอบเทียบ", 14, bold: true);
        page.Box(52, y - 3.6, 4.6, 4.6);
        page.Text(58, y, "ผ่าน", 14);
        page.Box(76, y - 3.6, 4.6, 4.6);
        page.Text(82, y, "ไม่ผ่าน", 14);
        page.Text(Mid, y, "ลงชื่อ", 14);
        page.Leader(Mid + 10, Right - 2, y, 14);
        page.Text(150, y + 6.5, "(ผู้ประเมินผลการสอบเทียบ)", 13, TextAnchor.Middle);
        page.Text(Mid, y + 13, "วันที่", 14);
        page.Leader(Mid + 10, Right - 2, y + 13, 14);
    }

    /// <summary>The head of an instrument as the legacy printed it: ranges and limits with the unit, the calibrator of an internal one, the company of an external one.</summary>
    public static RecordHead HeadOf(InstrumentDto i)
    {
        var external = i.Kind == InstrumentKind.External;
        var name = i.Kind == InstrumentKind.Scale && !string.IsNullOrWhiteSpace(i.DepartmentText) ? $"{i.Name} {i.DepartmentText}".Trim() : i.Name; // เครื่องชั่ง: แผนกต่อท้ายชื่อ ไม่มีป้าย
        return new RecordHead(name, i.Code, WithUnit(i.Range, i.Unit), Limit(i.Limit, i.Unit), external ? null : i.StandardUsed, external ? null : i.CalibratorName,
            external ? i.ProviderName : null, external);
    }

    private static string? WithUnit(string? value, string? unit) => string.IsNullOrWhiteSpace(value) ? null : string.IsNullOrWhiteSpace(unit) ? value : $"{value} {unit}";

    private static string? Limit(string? value, string? unit) => string.IsNullOrWhiteSpace(value) ? null : "± " + WithUnit(value, unit);
}
