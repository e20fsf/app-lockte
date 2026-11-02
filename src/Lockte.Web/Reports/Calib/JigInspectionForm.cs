using Lockte.Calib;

namespace Lockte.Web.Reports.Calib;

/// <summary>
/// รายงานการตรวจสอบ JIG (RPT-05 วัดศูนย์ก้ามเบรก / PIN GAUGE / TAPER GAUGE, RPT-06 กรอก้ามเบรก), ระดับ A (#207 #208, Q-26): A4 portrait, one sheet to a model of JIG, the head with the
/// name of the JIG typed on it, the picture of the JIG, a table of its measuring points with the specification typed and the measured values left to fill in by hand, the
/// result and three signatures (print-samples.md). The system keeps no result (BR-031). Positions in mm (baseline for texts) are checked by overlaying the PDF on the sample.
/// </summary>
public static class JigInspectionForm
{
    private const double Left = 12;
    private const double Right = 198;
    private const double TableTop = 150;
    private const double HeaderHeight = 12;
    private const double RowHeight = 8;

    public const string Company = "LOCKTE CO.,LTD";
    public const string Manual = "WI-QA049";

    public static string TitleOf(JigSheetDto sheet)
        => sheet.Type == JigSheetType.Grinding ? "รายงานการตรวจสอบ JIG กรอก้ามเบรก"
            : sheet.ModelKey == "PIN GAUGE" ? "รายงานการตรวจสอบ PIN GAUGE"
            : sheet.ModelKey == "TAPER GAUGE" ? "รายงานการตรวจสอบ TAPER GAUGE"
            : "รายงานการตรวจสอบ JIG วัดศูนย์ก้ามเบรก";

    /// <summary><c>Diameter 20.00 ± 0.20 mm.</c> or <c>Length &gt;15.00</c>: the kind of the point, the value and its tolerance as the sheet prints them.</summary>
    public static string SpecificationOf(JigPointDto point) => $"{point.Kind} {point.SpecificationText}";

    public static FormPage Page(JigSheetDto sheet, JigImageData? image, string? documentNumber)
    {
        var page = new FormPage(Paper.A4Portrait);
        page.Text(105, 16, Company, 18, TextAnchor.Middle, bold: true);
        page.Text(105, 25, TitleOf(sheet), 16, TextAnchor.Middle, bold: true);
        page.Text(Right, 16, documentNumber, 12, TextAnchor.End);
        page.Line(Left, 29, Right, 29, 1.2);

        Field(page, 40, Left, 31, 150, "ชื่อ JIG", sheet.Title);
        Field(page, 49, Left, 38, 105, "หมายเลขเครื่อง", null);
        Field(page, 49, 110, 134, Right, "วันที่สอบเทียบ", null);
        Field(page, 58, Left, 52, 105, "คู่มือวิธีการที่ใช้เลขที่", Manual);
        Field(page, 58, 110, 127, Right, "ผู้สอบเทียบ", null);
        // "สอบเทียบภายใน" is printed already ticked
        page.Box(Left, 63.4, 4.6, 4.6);
        page.Line(Left, 63.4, Left + 4.6, 68, 0.5);
        page.Line(Left + 4.6, 63.4, Left, 68, 0.5);
        page.Text(Left + 7, 67, "สอบเทียบภายใน", 14);
        Field(page, 67, 62, 112, 150, "หมายเลขเวอร์เนียที่ใช้(ผ่านการสอบเทียบแล้ว)", null);
        Field(page, 76, Left, 54, 105, "ขีดจำกัดที่ยอมรับได้", "± 0.20 mm.");

        // the picture of the JIG, an empty frame when the sheet has none
        page.Box(30, 82, 150, 62, 0.4);
        if (image is not null) page.Image(31, 83, 148, 60, image.ContentType, image.Data);

        Table(page, sheet);
        var y = TableTop + HeaderHeight + sheet.Points.Count * RowHeight + 10;
        page.Text(Left, y, "ผลการตรวจสอบ", 14, bold: true);
        page.Box(48, y - 3.6, 4.6, 4.6);
        page.Text(54, y, "ผ่าน", 14);
        page.Box(72, y - 3.6, 4.6, 4.6);
        page.Text(78, y, "ไม่ผ่าน", 14);
        Field(page, y + 9, Left, 28, Right, "หมายเหตุ", null);
        var sign = Math.Max(y + 28, 262);
        Signature(page, 12, 70, sign, "ผู้ตรวจสอบ");
        Signature(page, 74, 134, sign, "ผู้จัดการฝ่ายประกันคุณภาพ");
        Signature(page, 138, 198, sign, "ผู้จัดการฝ่ายวางแผนและควบคุมการผลิต");
        return page;
    }

    private static void Field(FormPage page, double baseline, double labelX, double lineFrom, double lineTo, string label, string? value)
    {
        page.Text(labelX, baseline, label, 14);
        page.Dots(lineFrom, lineTo, baseline + 0.6);
        page.Text(lineFrom + 1, baseline - 0.4, value, 14, fit: lineTo - lineFrom - 2);
    }

    private static void Signature(FormPage page, double from, double to, double baseline, string caption)
    {
        page.Text(from, baseline, "ลงชื่อ", 13);
        page.Leader(from + 10, to - 1, baseline, 13);
        page.Text((from + to) / 2, baseline + 6, $"({caption})", 12, TextAnchor.Middle, fit: to - from);
    }

    private static readonly (double From, double To, string Head)[] Columns =
    [
        (12, 28, "Point"), (28, 82, "ค่าที่กำหนด (Specification)"), (82, 102, "1"), (102, 122, "2"), (122, 142, "3"), (142, 160, "ค่าเฉลี่ย"), (160, 178, "ค่าผิดพลาด"), (178, 198, "หมายเหตุ"),
    ];

    private static void Table(FormPage page, JigSheetDto sheet)
    {
        var rows = sheet.Points.Count;
        var bottom = TableTop + HeaderHeight + rows * RowHeight;
        page.Box(Left, TableTop, Right - Left, bottom - TableTop);
        page.Line(Left, TableTop + HeaderHeight, Right, TableTop + HeaderHeight);
        page.Line(82, TableTop + 6, 142, TableTop + 6);
        foreach (var (from, to, head) in Columns)
        {
            if (from != Left) page.Line(from, from is 102 or 122 ? TableTop + 6 : TableTop, from, bottom);
            page.Text((from + to) / 2, from is >= 82 and < 142 ? TableTop + 11 : TableTop + 8, head, 12, TextAnchor.Middle, fit: to - from - 2);
        }
        page.Text(112, TableTop + 4.8, "ค่าที่วัดได้", 12, TextAnchor.Middle);
        for (var i = 0; i < rows; i++)
        {
            var top = TableTop + HeaderHeight + i * RowHeight;
            if (i > 0) page.Line(Left, top, Right, top, 0.25);
            var point = sheet.Points[i];
            page.Text(20, top + 5.6, point.Label, 13, TextAnchor.Middle);
            page.Text(55, top + 5.6, SpecificationOf(point), 13, TextAnchor.Middle, fit: 50);
        }
    }

    /// <summary>The name of the file the PDF is shown under; the commas of a model's name become underscores, as the legacy sample files had them.</summary>
    public static string FileName(JigSheetDto sheet) => $"{(sheet.Type == JigSheetType.Grinding ? "JIG กรอ" : "JIG วัดศูนย์")} {sheet.ModelKey.Replace(',', '_')}";
}
