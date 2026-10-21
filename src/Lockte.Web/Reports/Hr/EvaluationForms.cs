using System.Globalization;
using Lockte.Hr;
using Lockte.SharedKernel;

namespace Lockte.Web.Reports.Hr;

/// <summary>
/// RPT-43 ใบรายงานการประเมินผลงานประจำปี (reports พิมพ์ใบประเมินประจำปีรายวัน / …รายเดือน, BR-067), ระดับ A (#40 ตรงแบบเดิม):
/// A5 ตั้ง, one page per employee, 14 pt. Every text, rule and box is where the legacy sample printed it (positions in mm,
/// baseline for texts, measured on the PDFs of specs/employee/print-samples.md). The two differ only in the title, the
/// เลขเอกสารควบคุม and หัวข้อ 5–6. The employee's สถิติ × คะแนนที่หักต่อหน่วย and คะแนนหักรวม (2) are printed; ผลคะแนน,
/// คะแนนที่ได้, the grade, จุดเด่น / จุดด้อย and the signatures are written by hand, as before. Changes from the legacy, on
/// #40's list: TH Sarabun New at Angsana New's size; เลขเอกสาร from <see cref="DocumentNumberService"/>; long values squeezed
/// into their cell instead of cut (D-1).
/// </summary>
public static class EvaluationForms
{
    public const string Title = "ใบประเมินผลประจำปี";

    private const double Pt = 14;

    /// <summary>The 10 หัวข้อ of ใบประเมินพนักงาน (รายวัน); the หัวหน้า form has its own 5 and 6.</summary>
    public static IReadOnlyList<string> StaffTopics { get; } =
    [
        "การเชื่อฟังผู้บังคับบัญชา", "การแก้ปัญหาเฉพาะหน้า", "ความคิดสร้างสรรค์", "ความรับผิดชอบและเข้าใจในงานที่ได้รับมอบหมาย",
        "การศึกษาหาความรู้เพิ่มเติมตลอดเวลา", "ซื่อสัตย์สุจริตในการทำงาน", "ความเข้าใจในระบบบริหารคุณภาพ",
        "ทักษะในการใช้เครื่องมือและอุปกรณ์", "มนุษยสัมพันธ์กับเพื่อนร่วมงาน", "การปฏิบัติตามระเบียบและการรักษาทรัพย์สินบริษัทฯ",
    ];

    public static IReadOnlyList<string> SupervisorTopics { get; } =
        [.. StaffTopics.Take(4), "การควบคุมงาน", "การจัดทำเอกสารคุณภาพ", .. StaffTopics.Skip(6)];

    public static string DocumentKeyOf(YearlyEvaluationKind kind)
        => kind == YearlyEvaluationKind.Staff ? DocumentKeys.YearlyEvaluationDaily : DocumentKeys.YearlyEvaluationMonthly;

    /// <summary>Format Standard, 2 decimals: "1,234.50"; a zero is "0.00", never "-0.00".</summary>
    public static string Number(double value) => (value == 0 ? 0 : value).ToString("#,##0.00", CultureInfo.InvariantCulture);

    /// <summary>
    /// One ใบประเมิน. The year is ถึงวันที่'s (TO, Format yyyy — พ.ศ.); วันที่ is today (=Date()); แผนก is the one chosen or
    /// "ทั้งหมด"; ฝ่าย the employee's.
    /// </summary>
    public static FormPage Sheet(YearlyEvaluationReport report, YearlyEvaluationSheet s, DateOnly today, string? documentNumber)
    {
        var staff = report.Kind == YearlyEvaluationKind.Staff;
        var page = new FormPage(Paper.A5Portrait);
        void T(double x, double y, string? text, TextAnchor anchor = TextAnchor.Start, bool bold = false, double? fit = null)
            => page.Text(x, y, text, Pt, anchor, bold, fit);
        void H(double y) => page.Line(6.0, y, 139.35, y);
        void V(double x, double y0, double y1) => page.Line(x, y0, x, y1);

        T(72.65, 10.1, "บริษัท ล็อคเต้ จำกัด", TextAnchor.Middle, bold: true);
        T(6.2, 16.0, staff ? "ใบรายงานการประเมินผลงานพนักงานประจำปี" : "ใบรายงานการประเมินผลงานหัวหน้าหรือรองหัวหน้าประจำปี");
        var dots = staff ? 67.1 : 86.3;
        page.Leader(dots, dots + 11.8, 16.0, Pt);
        T(staff ? 68.5 : 87.2, 15.0, report.To.ToString("yyyy", ThaiDate.Culture));
        T(104.1, 16.0, documentNumber);

        H(18.0); H(25.0);
        for (var y = 32.0; y <= 165.0; y += y < 39 ? 7 : 6) H(y);
        H(202.0);
        V(6.0, 18.0, 202.0); V(139.35, 18.0, 202.0);
        V(57.3, 18.0, 25.0); V(104.75, 18.0, 105.0); V(16.85, 32.0, 105.0); V(122.5, 32.0, 105.0);
        foreach (var x in new[] { 62.25, 75.1, 84.95, 115.6 }) V(x, 105.0, 153.0);
        foreach (var x in new[] { 19.75, 37.6, 56.35, 71.15, 84.95, 98.8, 124.5 }) V(x, 153.0, 165.0);

        T(6.2, 23.0, "วันที่"); T(13.2, 23.0, ThaiDate.Format(today));
        T(57.6, 23.0, "แผนก"); T(66.5, 23.0, report.DepartmentName ?? "ทั้งหมด", fit: 104.0 - 66.5);
        T(105.0, 23.0, "ฝ่าย"); T(111.9, 23.0, s.SectionName, fit: 138.8 - 111.9);
        T(6.2, 29.9, "ชื่อ-นามสกุล"); T(24.0, 29.9, s.Name, fit: 104.0 - 24.0);
        T(105.1, 30.1, "รหัส"); T(112.9, 30.1, s.Code);

        T(6.2, 36.9, "รายการ"); T(48.2, 36.9, "หัวข้อที่ทำการประเมิน"); T(106.2, 36.9, "คะแนนเต็ม"); T(124.1, 36.9, "ผลคะแนน");
        var topics = staff ? StaffTopics : SupervisorTopics;
        for (var i = 0; i < topics.Count; i++)
        {
            var y = 43.9 + 6 * i;
            T(11.4, y, (i + 1).ToString(CultureInfo.InvariantCulture), TextAnchor.Middle);
            T(18.1, y, topics[i]);
            T(113.6, y, "10", TextAnchor.Middle);
        }
        T(85.5, 103.9, "คะแนนรวม(1)"); T(113.55, 103.9, "100", TextAnchor.Middle);

        T(29.2, 109.9, "รายการ"); T(64.1, 109.9, "จำนวน"); T(86.5, 109.9, "คะแนนที่หักต่อหน่วย"); T(119.4, 109.9, "คะแนนที่หัก");
        (string Label, double Count, string Unit, double Points)[] rows =
        [
            ("ลากิจ", s.PersonalLeave, "วัน", YearlyEvaluationSheet.Points.PersonalLeave),
            ("ลาป่วยมีใบรับรองแพทย์", s.SickLeaveWithCertificate, "วัน", YearlyEvaluationSheet.Points.SickLeaveWithCertificate),
            ("ลาป่วยไม่มีใบรับรองแพทย์", s.SickLeaveWithoutCertificate, "วัน", YearlyEvaluationSheet.Points.SickLeaveWithoutCertificate),
            ("มาสาย", s.LateCount, "วัน", YearlyEvaluationSheet.Points.Late),
            ("ขาดงาน", s.Absent, "วัน", YearlyEvaluationSheet.Points.Absent),
            ("ใบเตือนด้วยวาจา", s.VerbalWarnings, "ใบ", YearlyEvaluationSheet.Points.VerbalWarning),
            ("ใบเตือนเป็นหนังสือ", s.WrittenWarnings, "ใบ", YearlyEvaluationSheet.Points.WrittenWarning),
        ];
        for (var i = 0; i < rows.Length; i++)
        {
            var (label, count, unit, points) = rows[i];
            var y = 115.9 + 6 * i;
            T(6.2, y, label);
            T(75.1, y + 0.1, Number(count), TextAnchor.End);
            T(unit == "ใบ" ? 78.1 : 77.9, y, unit);
            T(points == YearlyEvaluationSheet.Points.Late ? 99.7 : 100.4, y, points.ToString(CultureInfo.InvariantCulture), TextAnchor.Middle);
            T(139.0, y + 0.1, Number(count * points), TextAnchor.End);
        }

        T(12.9, 157.9, "คะแนน", TextAnchor.Middle);
        foreach (var (x, band) in new[] { (23.7, "0-49.99"), (41.2, "50-59.99"), (58.0, "60-69.99"), (72.3, "70-79.99"), (87.4, "80-100") })
            T(x, 157.9, band);
        T(101.1, 157.9, "คะแนนหักรวม(2)");
        T(139.1, 158.0, Number(s.TotalDeduction), TextAnchor.End);
        T(12.9, 163.9, "เกรด", TextAnchor.Middle);
        foreach (var (x, grade) in new[] { (21.0, "F = ตกเกรด"), (38.4, "D = ปรับปรุง"), (57.3, "C = พอใช้"), (74.3, "B = ดี"), (85.3, "A = ดีมาก") })
            T(x, 163.9, grade, fit: 13.5);
        T(100.3, 164.0, "คะแนนที่ได้(1)-(2)", bold: true);

        T(6.2, 169.9, "ผลการประเมิน"); T(27.0, 169.9, "ได้เกรด");
        foreach (var (x, grade) in new[] { (42.2, "A"), (50.2, "B"), (58.2, "C"), (66.2, "D"), (74.2, "F") })
        {
            page.Box(x, 167.1, 3.5, 3.1);
            T(x + 4.1, 169.9, grade);
        }
        page.Box(84.8, 166.8, 5.3, 4.4); T(91.2, 169.9, "ผ่านเกณฑ์");
        page.Box(111.5, 166.8, 5.2, 4.4); T(117.8, 169.9, "ไม่ผ่านเกณฑ์");
        T(6.2, 175.9, "จุดเด่น"); page.Line(16.8, 175.95, 139.2, 175.95);
        T(6.2, 181.9, "จุดด้อย"); page.Line(16.8, 181.95, 139.2, 181.95);
        foreach (var (x0, x1) in new[] { (16.8, 35.5), (61.3, 79.9), (108.7, 127.3) }) page.Line(x0, 195.0, x1, 195.0);
        T(18.0, 199.9, "ผู้ถูกประเมิน"); T(54.8, 199.9, "ผู้ประเมิน/หัวหน้าแผนก"); T(110.3, 199.9, "ผู้จัดการฝ่าย");
        return page;
    }
}
