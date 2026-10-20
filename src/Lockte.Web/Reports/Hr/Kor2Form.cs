using Lockte.Hr;
using Lockte.SharedKernel;

namespace Lockte.Web.Reports.Hr;

/// <summary>
/// What HR types on the legacy form <c>แบบ คร2</c> before printing (BR-072): the name to print (filled from the
/// employee, editable), ลักษณะงาน, ชื่อบิดา, ชื่อมารดา. The legacy kept them in a one-row table; nothing is stored now.
/// </summary>
public sealed record Kor2Input(string? Name, string? Job, string? Father, string? Mother);

/// <summary>
/// RPT-57 แบบ คร.2 — แบบแจ้งการจ้างลูกจ้างเด็กอายุต่ำกว่า 18 ปี (BR-072, INT-05), ระดับ A (#40): A4 ตั้ง, one page,
/// every text and dotted line where the legacy report <c>แบบ คร2</c> printed it — positions in mm measured on the
/// sample <c>แบบ คร2.pdf</c> (the legacy print is 98.8% of the report's cm horizontally). Changes from the legacy,
/// all on #40's list: TH Sarabun New instead of Angsana New; the employer's details come from
/// <see cref="EmployerOptions"/> instead of being typed into the labels; จำนวนลูกจ้างทั้งหมด is counted instead of a
/// fixed 200; ค่าจ้าง prints its amount instead of <c>#####</c> (D-2) and long values are squeezed into their line
/// instead of cut (D-1); the label typo "เดิอน" reads "เดือน".
/// </summary>
public static class Kor2Form
{
    public const string Title = "แบบ คร.2";

    /// <summary>Angsana New 14 pt as wide as TH Sarabun New this size (measured on the sample).</summary>
    public const double Body = 14;

    /// <summary>The right end of the lines to write on.</summary>
    private const double LineEnd = 177.8;

    public static FormPage Layout(Kor2Employee employee, Kor2Input input, EmployerOptions employer)
    {
        var page = new FormPage(Paper.A4Portrait);
        void T(double x, double y, string? text, TextAnchor anchor = TextAnchor.Start, double? fit = null) => page.Text(x, y, text, Body, anchor, fit: fit);
        void Dots(double y, params (double From, double To)[] runs) { foreach (var (from, to) in runs) page.Dots(from, to, y); }
        string? Day(DateOnly? d) => d?.ToString("%d", ThaiDate.Culture);
        string? Month(DateOnly? d) => d?.ToString("MMMM", ThaiDate.Culture);
        string? Year(DateOnly? d) => d?.ToString("yyyy", ThaiDate.Culture);
        var office = employer.Office;
        var workplace = employer.Workplace;

        page.Text(157.5, 21.3, "แบบ คร.2", 17.5);
        page.Text(71.6, 30.8, "แบบแจ้งการจ้างลูกจ้างเด็กอายุต่ำกว่า 18 ปี", 14.5, bold: true);

        // นายจ้าง
        T(49.8, 40, $"ข้าพเจ้า {employer.ManagerTitle}".TrimEnd()); T(75.4, 40, employer.ManagerFirstName, fit: 32);
        T(108.4, 40, "นามสกุล"); T(132.4, 40, employer.ManagerLastName, fit: LineEnd - 132.4);
        Dots(40.2, (67.5, 110.6), (121.8, LineEnd));
        T(30.1, 47, "ผู้จัดการสถานประกอบกิจการชื่อ"); T(84.8, 47, employer.Name, fit: LineEnd - 84.8);
        Dots(47.2, (72.7, LineEnd));
        T(30.1, 54, "ประกอบกิจการ"); T(51.9, 54, employer.Business, fit: 120.3 - 51.9);
        T(120.0, 54, "ตั้งอยู่เลขที่"); T(136.5, 54, office.HouseNo, fit: 4.8);
        T(141.3, 54, "หมู่ที่"); T(149.0, 54, office.Moo, fit: 4.2);
        T(153.3, 54, "ตรอก/ซอย"); T(168.0, 54, office.Soi, fit: LineEnd - 168.0);
        Dots(54.2, (49.7, 120.3), (134.5, 143.3), (147.4, 154.6), (169.1, LineEnd));
        T(30.1, 61, "ถนน"); T(40.3, 61, office.Road, fit: 63.4 - 40.3);
        T(63.4, 61, "แขวง/ตำบล"); T(82.1, 61, office.Subdistrict, fit: 105.2 - 82.1);
        T(105.2, 61, "เขต/อำเภอ"); T(125.5, 61, office.District, fit: 145.0 - 125.5);
        T(145.0, 61, "จังหวัด"); T(155.8, 61, office.Province, fit: 178.8 - 155.8);
        Dots(61.2, (37.1, 63.8), (82.1, 107.7), (122.9, 148.5), (158.5, 178.8));
        T(30.1, 68, "โทรศัพท์"); T(47.5, 68, office.Phone, fit: 73.1 - 47.5);
        T(73.1, 68, "จำนวนลูกจ้างทั้งหมด"); T(107.35, 68, employee.WorkingEmployees.ToString("#,##0", ThaiDate.Culture), TextAnchor.Middle);
        T(113.8, 68, "คน"); T(122.6, 68, "สถานที่ใกล้เคียง");
        Dots(68.2, (41.7, 72.1), (102.0, 114.6), (145.3, 178.8));

        // ลูกจ้าง
        T(49.8, 75, "ข้าพเจ้าได้ว่าจ้าง"); T(77.5, 75, string.IsNullOrWhiteSpace(input.Name) ? employee.Name : input.Name, fit: LineEnd - 77.5);
        Dots(75.2, (72.7, LineEnd));
        T(30.1, 82, "เกิดวันที่"); T(43.9, 82, Day(employee.BirthDate));
        T(48.8, 82, "เดือน"); T(65.4, 82, Month(employee.BirthDate), TextAnchor.Middle, fit: 73.2 - 57.0);
        T(73.5, 82, "พ.ศ."); T(81.5, 82, Year(employee.BirthDate));
        T(90.3, 82, "เลขหมายประจำตัวผู้ถือบัตรประจำตัวประชาชน"); T(157.7, 82, employee.NationalId, fit: 178.8 - 157.7);
        Dots(82.2, (40.6, 48.1), (57.0, 73.2), (80.2, 89.9), (152.3, 178.8));
        T(30.1, 89, "ภูมิลำเนาบ้านเลขที่"); T(59.7, 89, employee.RegisteredAddress, fit: LineEnd - 59.7);
        Dots(89.2, (55.9, LineEnd));
        T(30.1, 96, "บิดาชื่อนาย"); T(45.9, 96, input.Father, fit: 108.8 - 45.9);
        T(109.1, 96, "มารดาชื่อนาง"); T(127.9, 96, input.Mother, fit: LineEnd - 127.9);
        Dots(96.2, (44.6, 108.8), (127.1, LineEnd));
        T(30.1, 103, "หรือผู้ปกครองชื่อ"); T(109.1, 103, "อาชีพ");
        Dots(102.9, (52.8, 108.8), (116.7, LineEnd));
        T(30.2, 110, "เข้าทำงานเป็นลูกจ้างเมื่อวันที่"); T(70.6, 110, Day(employee.StartDate));
        T(76.5, 110, "เดือน"); T(91.6, 110, Month(employee.StartDate), TextAnchor.Middle, fit: 103.1 - 84.2);
        T(102.2, 110, "พ.ศ."); T(110.1, 110, Year(employee.StartDate));
        T(123.0, 110, "ข้าพเจ้ารับเด็กมาจาก นาย/นาง/นางสาว");
        Dots(110.2, (68.5, 76.0), (84.2, 103.1), (107.2, 121.3));
        T(94.3, 117, "ที่อยู่");
        Dots(117.2, (29.8, 94.0), (100.9, LineEnd));
        T(101.2, 124, "เกี่ยวพันกับลูกจ้างเป็น");
        Dots(124.2, (29.8, 101.3), (130.6, LineEnd));

        // งาน ค่าจ้าง เวลาทำงาน — the legacy labels' fixed text (the company's working hours and welfare)
        T(49.8, 131, "ข้าพเจ้าให้เด็กทำงานในหน้าที่"); T(115.6, 131, employee.Position, TextAnchor.End, fit: 115.6 - 90.4);
        T(116.1, 131, "แผนก"); T(125.9, 131, employee.DepartmentName, fit: LineEnd - 125.9);
        Dots(131.2, (89.4, LineEnd));
        T(30.1, 139, "โดยมีลักษณะงานดังนี้"); T(59.7, 139, input.Job, fit: LineEnd - 59.7);
        Dots(139.2, (59.3, LineEnd));
        T(30.1, 146, "อัตราค่าจ้างเป็นรายเดือน/รายวัน/ชั่วโมงละ");
        T(98.6, 146, employee.DailyWage.ToString("#,##0.##", ThaiDate.Culture), TextAnchor.End, fit: 98.6 - 88.4);
        T(99.3, 145.9, "บาท หรือตามผลงานหน่วยละ"); T(146.2, 145.9, "บาท เวลาทำงานตั้งแต่");
        Dots(146.2, (85.2, 99.1), (139.6, 147.4));
        Line(153, (30.1, "เวลา"), (38.3, "8.00"), (47.1, "น."), (51.8, "ถึงเวลา"), (64.2, "17.00"), (74.7, "น."), (79.4, "เวลาพัก"),
            (91.7, "1.20"), (99.8, "ชั่วโมง/วัน"), (115.0, "แบ่งเวลาพักดังนี้"), (138.0, "ช่วงที่1"), (148.6, "เวลา"), (157.6, "10.00"), (168.1, "น. ถึง"));
        Dots(153.2, (37.1, 46.0), (61.4, 74.3), (90.5, 100.1), (156.4, 167.9));
        Line(160, (30.1, "เวลา"), (38.3, "10.10"), (48.8, "น."), (53.4, "ช่วงที่2 เวลา"), (72.2, "12.00"), (82.8, "น."), (87.4, "ถึงเวลา"),
            (99.0, "13.00"), (110.3, "น."), (115.8, "ช่วงที่3 เวลา"), (134.6, "15.00"), (145.1, "น. ถึงเวลา"), (161.4, "15.10"), (171.9, "น."));
        Dots(160.2, (36.1, 49.7), (69.5, 82.6), (97.0, 111.9), (131.6, 145.3), (158.5, 171.5));
        Line(167, (30.1, "วันทำงาน"), (46.7, "6"), (52.3, "วัน/สัปดาห์"), (69.4, "วันหยุดประจำสัปดาห์"), (99.5, "คือ"), (104.9, "วัน"),
            (115.0, "อาทิตย์"), (130.3, "วันหยุดตามประเพณี"), (160.4, "14"), (166.9, "วัน/ปี"));
        Dots(167.2, (43.6, 53.3), (109.3, 132.8), (160.5, 170.0));
        Line(174, (30.1, "วันหยุดพักผ่อนประจำปี"), (64.3, "6"), (69.9, "วันทำงาน/ปี"), (88.0, "สวัสดิการ"), (104.1, "1."),
            (108.1, "ทางบริษัทฯรับผิดชอบจ่ายเงินประกันสังคม"));
        Dots(174.2, (61.4, 71.1), (104.1, LineEnd));
        Line(181, (30.1, "2. จัดหาน้ำดื่มที่สะอาด"), (72.2, "3. ห้องน้ำและห้องสุขาที่สะอาดถูกสุขลักษณะ"));
        Dots(181.2, (29.8, LineEnd));

        // สถานที่ทำงาน
        T(30.1, 189, "สถานที่ทำงานเลขที่"); T(61.9, 189, workplace.HouseNo, fit: 75.6 - 61.9);
        T(75.6, 189, "หมู่ที่"); T(84.6, 189, workplace.Moo, fit: 90.2 - 84.6);
        T(90.2, 189, "ตรอก/ซอย"); T(108.0, 189, workplace.Soi, fit: 133.0 - 108.0);
        T(133.0, 189, "ถนน"); T(144.9, 189, workplace.Road, fit: LineEnd - 144.9);
        Dots(189.2, (57.4, 77.2), (84.2, 92.0), (105.1, 132.5), (142.8, LineEnd));
        T(30.1, 196, "แขวง/ตำบล"); T(50.8, 196, workplace.Subdistrict, fit: 70.2 - 50.8);
        T(70.2, 196, "เขต/อำเภอ"); T(88.8, 196, workplace.District, fit: 111.6 - 88.8);
        T(111.6, 196, "จังหวัด"); T(124.7, 196, workplace.Province, fit: 142.2 - 124.7);
        T(142.2, 196, "โทรศัพท์"); T(159.1, 196, workplace.Phone, fit: LineEnd - 159.1);
        Dots(196.2, (45.6, 73.2), (85.2, 114.8), (122.9, 145.3), (156.4, LineEnd));
        T(30.1, 203, "สถานที่พักของลูกจ้าง");
        Dots(202.9, (58.0, LineEnd));
        T(30.1, 210, "พักอาศัยอยู่กับ"); T(102.2, 210, "เกี่ยวข้องเป็น");
        Dots(210.2, (49.7, 101.4), (120.8, LineEnd));

        // เอกสารแนบ
        T(43.9, 217, "ข้าพเจ้าได้แนบ");
        page.Box(64.7, 214.4, 2.9, 2.9); T(68.0, 217, "สำเนาบัตรประจำตัวประชาชน");
        page.Box(107.7, 214.4, 2.8, 2.9); T(111.2, 217, "สำเนาทะเบียนบ้าน หรือ");
        page.Box(143.8, 214.4, 2.8, 2.9); T(147.2, 217, "สำเนาใบสุทธิ"); T(166.6, 217, "และ");
        page.Box(64.7, 221.4, 2.9, 2.8); T(68.5, 224, "สำเนาเอกสารหลักฐานการว่าจ้างเด็กเข้าทำงาน (ถ้ามี) มาพร้อมนี้");

        // ลงชื่อ
        T(84.6, 251, "ลงชื่อ"); page.Leader(92.0, 139.5, 251, Body); T(139.7, 251, "ผู้จัดการสถานประกอบกิจการ");
        var manager = $"{employer.ManagerTitle}{employer.ManagerFirstName} {employer.ManagerLastName}".Trim();
        T(113.5, 258, $"(  {manager}  )", TextAnchor.Middle);
        Dots(258.2, (97.0, 129.7));
        T(85.5, 265, "วันที่"); page.Leader(91.7, 103.6, 265, Body);
        T(103.7, 265, "เดือน"); page.Leader(110.6, 132.8, 265, Body);
        T(132.9, 265, "พ.ศ"); page.Leader(137.8, 154.5, 265, Body);
        return page;

        void Line(double y, params (double X, string Text)[] pieces) { foreach (var (x, text) in pieces) T(x, y, text); }
    }
}
