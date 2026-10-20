using System.Globalization;
using Lockte.Hr;
using Lockte.SharedKernel;

namespace Lockte.Web.Reports.Hr;

/// <summary>What HR types on the legacy form <c>แบบฟอร์มการรับพนักงาน</c> before printing (BR-072): วันที่สมัคร, วันที่สัมภาษณ์. Not stored.</summary>
public sealed record HiringFormInput(DateOnly? AppliedOn, DateOnly? InterviewedOn);

/// <summary>What HR types on SCR-06 ใบอนุมัติบรรจุพนักงาน (BR-071): ฝ่าย (TYPE), วันที่ครบกำหนด (DATE1), วันที่ส่งกลับ (DATE2). Not stored.</summary>
public sealed record ProbationApprovalInput(string? Section, DateOnly? DueOn, DateOnly? ReturnBy);

/// <summary>
/// The onboarding documents of #66, ระดับ A (#40 ตรงแบบเดิม): RPT-56 แบบฟอร์มการรับพนักงานเข้าทดลองงาน and สัญญาจ้างแรงงาน
/// รายวัน / รายเดือน (A4 ตั้ง), RPT-22 ใบนำส่งเอกสาร, ใบรายงานการประเมินผลงานพนักงานทดลองงาน and ใบขอบรรจุเป็นพนักงานประจำ
/// (A5 ตั้ง). Every text, line and box is where the legacy report printed it — positions in mm (baseline for texts)
/// measured on the samples of specs/employee/print-samples.md (the legacy print is 98.8 % of the report's cm
/// horizontally). Changes from the legacy, all on #40's list: TH Sarabun New at Angsana New's size (as wide, measured);
/// เลขเอกสารควบคุม from <see cref="DocumentNumberService"/>; long values squeezed into their line instead of cut (D-1);
/// a signer's "( )" is left out when the แผนก has no name for it; the A5 ใบนำส่ง prints one page, not a second blank one.
/// </summary>
public static class OnboardingForms
{
    public const string HiringTitle = "แบบฟอร์มการรับพนักงานเข้าทดลองงาน";
    public const string ContractTitle = "สัญญาจ้างแรงงาน";
    public const string ProbationTitle = "ใบอนุมัติบรรจุพนักงาน";

    private const string Company = "บริษัท ล็อคเต้ จำกัด";

    /// <summary>วันที่ Long Date: "15 ตุลาคม 2569".</summary>
    public static string? LongDate(DateOnly? date) => date?.ToString("d MMMM yyyy", ThaiDate.Culture);

    /// <summary>วันที่ Short Date: "13/8/2569".</summary>
    public static string? ShortDate(DateOnly? date) => date is { } d ? ThaiDate.Format(d) : null;

    /// <summary>A number in Access's General Number format: no thousands separator, decimals only when there are (12340, 1.5, −3).</summary>
    public static string Number(double value) => (value == 0 ? 0 : value).ToString("G15", CultureInfo.InvariantCulture);

    private static string? Signer(string? name) => string.IsNullOrWhiteSpace(name) ? null : $"({name.Trim()})";

    /// <summary>RPT-56 แบบฟอร์มการรับพนักงานเข้าทดลองงาน (report <c>แบบฟอร์มการรับพนักงาน</c>), A4 ตั้ง, 16 pt.</summary>
    public static FormPage HiringForm(OnboardingEmployee e, HiringFormInput input, string? documentNumber)
    {
        const double pt = 16;
        var page = new FormPage(Paper.A4Portrait);
        void T(double x, double y, string? text, TextAnchor anchor = TextAnchor.Start, double? fit = null) => page.Text(x, y, text, pt, anchor, fit: fit);

        page.Text(95.9, 30.7, Company, pt, TextAnchor.Middle, bold: true);
        T(100.85, 41.6, HiringTitle, TextAnchor.Middle);
        T(145.5, 41.7, documentNumber);

        T(25.1, 56.6, "ชื่อ"); T(31.1, 56.6, e.Name, fit: 112.8 - 31.1);
        T(113.0, 56.6, "รหัสพนักงาน"); T(134.8, 56.6, e.Code);
        T(150.6, 56.6, "กะ"); T(155.6, 56.6, e.ShiftCode);
        page.Dots(31.1, 112.8, 57.2).Dots(134.7, 150.9, 57.2).Dots(155.6, 161.1, 57.2);

        T(25.1, 67.6, "แผนก"); T(35.0, 67.6, e.DepartmentName, fit: 74.2 - 35.0);
        T(75.5, 67.6, "ฝ่าย"); T(82.5, 67.6, e.SectionName, fit: 129.8 - 82.5);
        T(130.8, 67.6, "ตำแหน่ง"); T(145.7, 67.6, e.Position, fit: 169.7 - 145.7);
        page.Dots(34.7, 74.2, 68.2).Dots(82.4, 129.8, 68.2).Dots(145.4, 169.7, 68.2);

        T(25.1, 79.6, "วันที่สมัคร"); T(41.9, 79.6, LongDate(input.AppliedOn));
        T(94.3, 79.6, "วันที่สัมภาษณ์"); T(118.0, 79.6, LongDate(input.InterviewedOn));
        page.Dots(41.6, 93.2, 80.2).Dots(119.0, 164.3, 80.2);

        T(25.1, 90.6, "เงินเดือน"); T(51.8, 90.6, Number(e.Salary), TextAnchor.Middle); T(64.6, 90.6, "บาท");
        T(82.4, 90.6, "ค่าแรง"); T(103.4, 90.6, e.DailyWage.ToString("0.00", CultureInfo.InvariantCulture), TextAnchor.Middle); T(112.0, 90.6, "บาท");
        page.Dots(40.6, 63.0, 91.2).Dots(95.0, 111.8, 91.2);

        T(25.2, 101.6, "ระยะทดลองงาน 90 วัน เริ่มวันที่"); T(75.5, 101.6, LongDate(e.StartDate));
        T(114.0, 101.6, "ถึงวันที่"); T(126.9, 101.6, LongDate(e.NinetyDayDate));
        page.Dots(75.2, 112.2, 102.2).Dots(124.6, 166.6, 102.2);

        T(25.1, 112.6, "เมื่อผ่านการทดลองงานแล้วปรับเงินใหม่เป็น...........................บาท");
        T(25.1, 126.6, "เงื่อนไขอื่นๆ 1.ต้องปฏิบัติตามกฏระเบียบข้อบังคับเกี่ยวกับการทำงานของบริษัทฯอย่างเคร่งครัด");

        T(25.1, 147.6, "ลงชื่อ..............................................ผู้สมัคร");
        T(106.1, 147.6, "ลงชื่อ..............................................ผู้จัดการฝ่าย");
        T(54.65, 156.6, Signer(e.Name), TextAnchor.Middle); T(135.75, 156.6, Signer(e.ManagerName), TextAnchor.Middle);
        T(26.1, 184.6, "ลงชื่อ..............................................ผู้สัมภาษณ์");
        T(106.1, 184.6, "ลงชื่อ.............................................ผู้อนุมัติ");
        T(54.15, 193.6, Signer(e.SupervisorName), TextAnchor.Middle); T(135.75, 193.6, Signer(e.FactoryManagerName), TextAnchor.Middle);
        return page;
    }

    /// <summary>
    /// RPT-56 สัญญาจ้างแรงงาน (reports <c>สัญญาจ้างแรงงานรายวัน</c> / <c>รายเดือน</c>), A4 ตั้ง, 14 pt: one text with both
    /// paragraphs, only the employee's own filled (รายเดือน: เงินเดือน, รายวัน: ค่าแรง/วัน, each with its words and
    /// ระยะทดลองงาน START − START + 89); the other keeps its "(……)". วันที่ = the day printed.
    /// </summary>
    public static FormPage Contract(OnboardingEmployee e, bool monthly, DateOnly today, string? documentNumber)
    {
        const double pt = 14;
        var page = new FormPage(Paper.A4Portrait);
        void T(double x, double y, string? text, TextAnchor anchor = TextAnchor.Start, double? fit = null) => page.Text(x, y, text, pt, anchor, fit: fit);
        const string blankWords = "(......................................................)";

        page.Text(98.8, 10.7, Company, 16, TextAnchor.Middle, bold: true);
        page.Text(99.65, 17.5, ContractTitle, 16, TextAnchor.Middle);
        page.Text(147.7, 18.5, documentNumber, 16);
        T(152.4, 25.9, "ทำที่ บริษัท ล็อคเต้ จำกัด");
        T(149.5, 31.9, "วันที่"); T(157.0, 31.9, LongDate(today));
        page.Dots(156.4, 186.2, 32.1);

        T(25.6, 38.9, "สัญญาฉบับนี้ทำขึ้นระหว่างบริษัท ล็อคเต้ จำกัด ตั้งอยู่ที่ 85/36 ม.5 ต.อ้อมน้อย อ.กระทุ่มแบน จ.สมุทรสาคร ซึ่งต่อไปนี้ใน", fit: 181.7 - 25.6);
        T(10.2, 45.5, "สัญญานี้เรียกว่า \"นายจ้าง\" ฝ่ายหนึ่งกับ"); T(61.6, 45.5, e.Name, fit: 107.7 - 61.6);
        T(108.0, 45.8, "อายุ"); T(118.5, 45.9, e.AgeOn(today)?.ToString(CultureInfo.InvariantCulture), TextAnchor.Middle); T(121.8, 45.9, "ปี");
        page.Dots(61.3, 107.7, 46.1).Dots(115.7, 121.3, 46.1);
        T(10.2, 52.9, "อยู่บ้านเลขที่"); T(29.0, 52.9, e.RegisteredAddress, fit: 148.2 - 29.0);
        page.Dots(26.7, 148.2, 53.1);
        T(10.2, 59.9, "ถือบัตรประชาชน    เลขที่"); T(44.8, 59.9, e.NationalId, fit: 76.1 - 44.8);
        T(77.4, 59.9, "ออกให้ ณ เขต"); T(99.2, 59.9, e.IdCardPlace, fit: 128.6 - 99.2);
        T(128.8, 59.9, "เมื่อวันที่"); T(142.6, 59.9, ShortDate(e.IdCardIssued));
        page.Dots(43.5, 76.1, 60.1).Dots(96.9, 128.6, 60.1).Dots(141.7, 172.1, 60.1);
        T(10.2, 66.9, "ซึ่งต่อไปนี้สัญญานี้เรียกว่า \"ลูกจ้าง\" อีกฝ่ายหนึ่ง");
        T(25.6, 73.9, "คู่สัญญาทั้งสองฝ่ายตกลงทำสัญญากันมีข้อความต่อไปนี้");
        T(26.3, 80.9, "ข้อ1)  นายจ้างตกลงจ้างและลูกจ้างตกลงรับจ้างเข้าทำงานในตำแหน่ง"); T(117.9, 80.9, e.Position, fit: 138.1 - 117.9);
        T(138.7, 80.9, "แผนก"); T(149.6, 80.9, e.DepartmentName, fit: 182.0 - 149.6);
        page.Dots(116.6, 138.1, 81.1).Dots(147.3, 182.0, 81.1);

        // ในอัตราเงินเดือน (รายเดือน) and ในอัตราค่าจ้างรายวัน (รายวัน): the same four lines, 27 mm apart.
        void Rate(double y, string label, string per, double perX, double amountX, double unitX, double wordsX, (double From, double To) amountDots,
            double endDotsTo, bool filled, double amount)
        {
            page.Text(10.2, y + 0.1, label, pt, bold: true);
            T(perX, y, per);
            if (filled) T(amountX, y, Number(amount), TextAnchor.Middle);
            T(unitX, y, "บาท");
            T(wordsX, y, filled ? $"({ThaiNumber.Baht(amount)})" : blankWords, fit: 128.0 - wordsX);
            T(128.8, y, "ค่าล่วงเวลาไม่น้อยกว่าเท่าครึ่งของอัตราค่า");
            page.Dots(amountDots.From, amountDots.To, y + 0.2);
            T(10.2, y + 6.9, "จ้างต่อชั่วโมงในวันทำงานปกติตามจำนวนชั่วโมงที่ทำ  การทำงานในวันหยุดจะได้รับค่าจ้างเพิ่มขึ้นอีกไม่น้อยกว่าหนึ่งเท่าของอัตราค่าจ้าง", fit: 176.0);
            T(10.2, y + 13.6, "ในวันทำงานปกติตามจำนวนชั่วโมงที่ทำงานในวันหยุด  ค่าล่วงเวลาในวันหยุดจ่ายไม่น้อยกว่าสามเท่าของอัตราค่าจ้างต่อชั่วโมงในวัน", fit: 170.3);
            T(10.2, y + 19.9, "ทำงานปกติตามจำนวนชั่วโมงที่ทำ  ระยะเวลาทดลองงาน  90  วัน  เริ่มวันที่");
            T(136.7, y + 19.9, "ถึงวันที่");
            if (filled)
            {
                T(107.1, y + 20.0, ShortDate(e.StartDate));
                T(147.6, y + 20.0, ShortDate(e.NinetyDayDate));
            }
            page.Dots(107.2, 136.0, y + 20.2).Dots(147.3, endDotsTo, y + 20.2);
        }
        Rate(87.9, "ในอัตราเงินเดือน", "เดือนละ", 35.9, 54.0, 60.5, monthly ? 69.5 : 70.5, (47.6, 60.4), 173.6, monthly, e.Salary);
        Rate(114.8, "ในอัตราค่าจ้างรายวัน", "วันละ", 41.8, 58.85, 67.4, 75.4, (51.8, 65.9), 176.8, !monthly, e.DailyWage);
        page.Dots(75.1, 112.7, 141.8); // a box border of the legacy layout that printed across ข้อ2

        T(25.0, 141.9, "ข้อ2)  นายจ้างจะจ่ายค่าจ้างโดยจ่าย  ณ  โรงงานบริษัท ล็อคเต้ จำกัด   ทุกวันที่  1  และ  16  ของเดือน  ถ้าวันที่  1  และ  16  ตรง", fit: 160.4);
        T(10.2, 148.9, "กับวันหยุดจะเลื่อนเป็นวันที่ทำการถัดไป หรือลูกจ้างที่มีบัตร ATM ของธนาคารทหารไทย จะจ่ายผ่านบัตร ATM");
        T(25.0, 155.9, "ข้อ3)  นายจ้างอาจพิจารณาโยกย้ายตำแหน่งหน้าที่ตามที่นายจ้างเห็นสมควรและคำนึงถึงความเหมาะสมความสามารถในการ", fit: 157.9);
        T(10.2, 162.9, "ทำงาน");
        T(25.0, 169.8, "ข้อ4)  ลูกจ้างมีสิทธิจะได้รับสวัสดิการดังต่อไปนี้");
        T(39.8, 176.8, "4.1  สิทธิประกันสังคมทางบริษัทรับผิดชอบจ่ายให้");
        T(39.8, 183.9, "4.2  สิทธิกองทุนเงินทดแทนทางบริษัทรับผิดชอบจ่ายให้");
        T(39.8, 190.9, "4.3  สิทธิการลาป่วยพนักงานมีสิทธิตั้งแต่วันแรกที่เริ่มทำงานแต่ต้องเป็นไปตามพระราชบัญญัติคุ้มครองแรงงาน", fit: 142.6);
        T(10.2, 197.9, "พ.ศ.2541");
        T(39.8, 204.9, "4.4  สิทธิในวันหยุดตามประเพณีพนักงานมีสิทธิได้รับค่าแรงในวันหยุดตั้งแต่วันแรกที่เริ่มทำงาน");
        T(25.0, 211.8, "ข้อ5)  กฏระเบียบข้อบังคับอื่นฯนอกจากนี้  ให้เป็นไปตามข้อบังคับเกี่ยวกับการทำงานของนายจ้างที่ได้กำหนดไว้");
        T(25.0, 218.8, "ข้อ6)  หากลูกจ้างทำผิดข้อบังคับเกี่ยวกับการทำงานข้อหนึ่งข้อใดแล้วนายจ้างสามารถบอกเลิกสัญญาจ้างได้ทันที่โดยไม่จ่าย", fit: 156.9);
        T(10.2, 225.8, "ค่าชดเชยใดๆทั้งสิ้น อายุของสัญญาจ้างแรงงานฉบับนี้จะหมดลงเมื่อลูกจ้างพ้นสภาพการจ้างแรงงานโดยทันที");
        T(25.0, 232.9, "สัญญานี้ทำขึ้นเป็นสองฉบับมีข้อความถูกต้องตรงกัน  คู่สัญญาทั้งสองฝ่ายได้อ่านและเข้าใจดีแล้วจึงลงลายมือชื่อไว้ต่อหน้า", fit: 156.5);
        T(10.2, 239.9, "พยานเป็นสำคัญและเก็บสัญญาไว้ฝ่ายละฉบับ");

        T(25.0, 249.9, "ลงชื่อ..................................................ผู้ว่าจ้าง");
        T(114.0, 249.9, "ลงชื่อ..................................................ผู้รับจ้าง");
        T(52.05, 255.9, Signer(e.FactoryManagerName), TextAnchor.Middle); T(142.0, 255.9, Signer(e.Name), TextAnchor.Middle);
        T(25.0, 269.9, "ลงชื่อ..................................................พยาน");
        T(114.0, 269.9, "ลงชื่อ..................................................พยาน");
        return page;
    }

    /// <summary>
    /// RPT-22 ใบนำส่งเอกสารแผนกบุคคลและธุรการ (report <c>ใบอนุมัติบรรจุพนักงาน</c>), A5 ตั้ง: วันที่ = the day printed,
    /// ครบกำหนด and ส่งกลับ as HR typed them (BR-071 does not compute them).
    /// </summary>
    public static FormPage Transmittal(OnboardingEmployee e, ProbationApprovalInput input, DateOnly today, string? documentNumber)
    {
        var page = new FormPage(Paper.A5Portrait);
        void T(double x, double y, string? text, double pt = 16, TextAnchor anchor = TextAnchor.Start, double? fit = null)
            => page.Text(x, y, text, pt, anchor, fit: fit);
        void Underline(double x0, double x1, double y) => page.Line(x0, y, x1, y);

        page.Text(72.65, 15.8, Company, 16, TextAnchor.Middle, bold: true);
        page.Text(72.8, 23.1, "ใบนำส่งเอกสารแผนกบุคคลและธุรการ", 14, TextAnchor.Middle, bold: true);
        T(104.8, 22.9, documentNumber, 14);
        T(96.1, 33.6, "วันที่"); T(105.0, 33.6, LongDate(today));
        T(6.2, 40.8, "เรื่อง  ขอนำส่งใบอนุมัติบรรจุพนักงาน");
        T(6.2, 48.8, "เรียน  ท่านผู้จัดการฝ่าย");
        T(22.1, 60.8, "เนื่องจากพนักงานชื่อ"); T(55.6, 60.8, e.Name, fit: 112.7 - 55.6);
        T(112.9, 60.8, "รหัส"); T(120.7, 60.8, e.Code);
        Underline(53.4, 112.7, 62.2); Underline(120.5, 135.4, 62.2);
        T(13.1, 67.8, "แผนก"); T(23.1, 67.8, e.DepartmentName, fit: 66.3 - 23.1);
        T(67.5, 67.8, "เริ่มทำงานวันที่"); T(92.2, 67.8, LongDate(e.StartDate));
        Underline(22.8, 66.3, 69.2); Underline(91.9, 130.4, 69.2);
        T(13.1, 74.8, "ได้ครบกำหนดระยะเวลาในการทดลองงาน คือ 90 วัน"); T(91.1, 74.8, "ในวันที่"); T(104.1, 74.8, LongDate(input.DueOn));
        Underline(103.7, 137.0, 76.2);
        T(13.1, 81.8, "ทางฝ่ายบุคคลและธุรการจึงขอเรียนให้ผู้จัดการฝ่ายรับทราบ");
        T(13.1, 88.8, "เอกสารใบขออนุมัติบรรจุพนักงานจะต้องส่งกลับแผนกบุคคลและธุรการภายในวันที่", fit: 136.1 - 13.1);
        T(13.2, 95.8, LongDate(input.ReturnBy));
        Underline(12.9, 46.5, 97.2);
        T(75.4, 103.8, "จึงเรียนมาเพื่อทราบ");
        T(87.2, 123.8, "ลงชื่อ.........................................."); T(96.1, 130.8, "แผนกบุคคลและธุรการ");
        T(75.4, 148.8, "ผู้จัดการฝ่ายเซ็นต์รับทราบ");
        T(87.2, 165.8, "ลงชื่อ.........................................."); T(96.1, 173.8, "ผู้จัดการฝ่าย");
        return page;
    }

    /// <summary>The 8 หัวข้อประเมิน, 10 คะแนนเต็ม each (คะแนนรวม (1) = 80).</summary>
    public static IReadOnlyList<string> EvaluationTopics { get; } =
    [
        "การแก้ปัญหาเฉพาะหน้า", "ความคิดริเริ่มสร้างสรรค์", "ความเอาใจใส่ในงานที่ทำ", "ความเข้าใจในงานที่ทำ", "การเขียนรายงาน",
        "ทักษะในการใช้เครื่องมือและอุปกรณ์", "การเรียนรู้งานใหม่", "มนุษยสัมพันธ์กับเพื่อนร่วมงาน",
    ];

    /// <summary>
    /// RPT-22 ใบรายงานการประเมินผลงานพนักงานทดลองงาน (report <c>ใบอนุมัติบรรจุพนักงาน1</c>), A5 ตั้ง, ตารางเส้นเต็ม: the
    /// employee's การลา / มาสาย / ใบเตือน × ตัวคูณ and คะแนนหักรวม (2) (BR-071); ผลคะแนน, คะแนนที่ได้ and the result are
    /// written by hand, as before. A count that is not known (ใบเตือน before #59) is left blank, and so is the total.
    /// </summary>
    public static FormPage Evaluation(OnboardingEmployee e, ProbationApprovalInput input, ProbationStatistics stats, DateOnly today,
        string? documentNumber)
    {
        const double pt = 14;
        var page = new FormPage(Paper.A5Portrait);
        void T(double x, double y, string? text, TextAnchor anchor = TextAnchor.Start, bool bold = false, double? fit = null)
            => page.Text(x, y, text, pt, anchor, bold, fit);
        void H(double y) => page.Line(6.0, y, 139.3, y);
        void V(double x, double y0, double y1) => page.Line(x, y0, x, y1);

        page.Text(72.7, 10.8, Company, 16, TextAnchor.Middle, bold: true);
        T(72.95, 17.1, "ใบรายงานการประเมินผลงานพนักงานทดลองงาน", TextAnchor.Middle, bold: true);
        page.Text(109.0, 16.8, documentNumber, 12);

        foreach (var y in new[] { 21.0, 28.0, 35.0, 42.0, 48.0, 54.0, 60.0, 66.0, 72.0, 78.0, 84.0, 91.0, 97.0, 103.0, 109.0, 115.0, 121.0,
                     127.0, 133.0, 139.0, 145.0, 151.0, 157.0, 163.0, 170.0, 177.0, 202.0 })
            H(y);
        V(6.0, 21.0, 202.0); V(139.35, 21.0, 202.0);
        V(64.2, 21.0, 28.0); V(103.75, 21.0, 97.0); V(19.75, 35.0, 97.0); V(120.5, 35.0, 97.0);
        foreach (var x in new[] { 57.3, 80.05, 90.9, 116.55 }) V(x, 103.0, 163.0);
        foreach (var x in new[] { 20.8, 38.55, 56.35, 71.15, 84.95, 97.8, 127.4 }) V(x, 163.0, 177.1);

        T(7.2, 26.0, "วันที่"); T(16.1, 26.0, LongDate(today));
        T(65.5, 26.0, "แผนก"); T(75.4, 26.0, e.DepartmentName, fit: 103.0 - 75.4);
        T(105.8, 26.0, "ฝ่าย"); T(111.9, 26.0, input.Section, fit: 138.6 - 111.9);
        T(7.2, 32.9, "ชื่อ-นามสกุล"); T(27.0, 32.9, e.Name, fit: 103.0 - 27.0);
        T(105.0, 32.9, "รหัส"); T(113.9, 32.9, e.Code);

        T(7.2, 39.9, "รายการ"); T(48.2, 39.8, "หัวข้อที่ทำการประเมิน"); T(105.0, 39.1, "คะแนนเต็ม"); T(121.9, 39.1, "ผลคะแนน");
        for (var i = 0; i < EvaluationTopics.Count; i++)
        {
            var y = 46.9 + 6 * i;
            T(13.4, y, (i + 1).ToString(CultureInfo.InvariantCulture), TextAnchor.Middle);
            T(21.0, y, EvaluationTopics[i]);
            T(112.65, y, "10", TextAnchor.Middle);
        }
        T(83.3, 95.9, "คะแนนรวม (1)"); T(112.65, 96.1, "80", TextAnchor.Middle, bold: true);
        page.Line(111.0, 96.5, 114.3, 96.5);

        T(7.2, 101.9, "สถิติการลางานและใบเตือน : จะนำไปหักจากคะแนนรวม (1)");
        T(22.8, 107.9, "ประเภทการลา"); T(64.1, 107.9, "จำนวน"); T(82.0, 107.9, "หน่วย"); T(95.2, 107.9, "ตัวคูณ/หน่วย"); T(119.7, 107.9, "คะแนนที่หัก");
        (string Label, double? Count, string Unit, int Points)[] rows =
        [
            ("ลากิจ", stats.PersonalLeave, "วัน", ProbationStatistics.Points.PersonalLeave),
            ("ลาป่วยมีใบรับรองแพทย์", stats.SickLeaveWithCertificate, "วัน", ProbationStatistics.Points.SickLeaveWithCertificate),
            ("ลาป่วยไม่มีใบรับรองแพทย์", stats.SickLeaveWithoutCertificate, "วัน", ProbationStatistics.Points.SickLeaveWithoutCertificate),
            ("ลาคลอด", stats.MaternityLeave, "วัน", ProbationStatistics.Points.MaternityLeave),
            ("ลาอุบัติเหตุในงาน", stats.WorkAccidentLeave, "วัน", ProbationStatistics.Points.WorkAccidentLeave),
            ("มาสาย", stats.LateCount, "วัน", ProbationStatistics.Points.Late),
            ("ขาดงาน", stats.Absent, "วัน", ProbationStatistics.Points.Absent),
            ("ใบเตือนด้วยวาจา", stats.VerbalWarnings, "ใบ", ProbationStatistics.Points.VerbalWarning),
            ("ใบเตือนเป็นหนังสือ", stats.WrittenWarnings, "ใบ", ProbationStatistics.Points.WrittenWarning),
        ];
        for (var i = 0; i < rows.Length; i++)
        {
            var (label, count, unit, points) = rows[i];
            var y = 113.9 + 6 * i;
            T(7.2, y, label);
            if (count is { } c) T(68.75, y + 0.1, Number(c), TextAnchor.Middle);
            T(unit == "ใบ" ? 86.55 : 85.95, y, unit, TextAnchor.Middle);
            T(103.7, y, points.ToString(CultureInfo.InvariantCulture), TextAnchor.Middle);
            if (count is { } d) T(128.0, y + 0.1, Number(-d * points), TextAnchor.Middle);
        }

        T(13.95, 168.1, "คะแนน", TextAnchor.Middle, bold: true);
        foreach (var (x, band) in new[] { (29.7, "0-39.99"), (47.45, "40-49.99"), (63.75, "50-59.99"), (78.05, "60-69.99"), (91.4, "70-80") })
            T(x, 168.1, band, TextAnchor.Middle, bold: true);
        T(102.4, 167.9, "คะแนนหักรวม (2)");
        if (stats.TotalDeduction is { } total) T(133.3, 168.0, Number(total), TextAnchor.Middle);
        T(13.95, 175.1, "เกรด", TextAnchor.Middle, bold: true);
        foreach (var (x, grade) in new[] { (29.7, "F=ตกเกณฑ์"), (47.45, "D=ปรับปรุง"), (63.75, "C=พอใช้"), (78.05, "B=ดี"), (91.4, "A=ดีมาก") })
            T(x, 175.1, grade, TextAnchor.Middle, bold: true, fit: 13.5);
        T(113.2, 175.1, "คะแนนที่ได้ (1) - (2)", TextAnchor.Middle, bold: true);

        T(8.3, 181.9, "ผลการประเมิน :"); T(31.7, 181.9, "ได้เกรด");
        foreach (var (x, grade) in new[] { (43.3, "A"), (50.2, "B"), (57.2, "C"), (64.1, "D"), (71.0, "F") })
        {
            page.Box(x, 179.4, 3.2, 2.7);
            T(x + 3.5, 181.9, grade);
        }
        page.Box(81.9, 179.8, 5.3, 2.7); T(88.2, 181.9, "ผ่านเกณฑ์");
        page.Box(108.5, 179.8, 5.3, 2.7); T(114.9, 181.9, "ไม่ผ่านเกณฑ์");
        foreach (var (x, role) in new[] { (21.8, "ผู้ถูกประเมิน"), (65.3, "ผู้ประเมิน"), (108.7, "ผู้จัดการฝ่าย") })
        {
            T(x, 194.9, "....................");
            T(x + (role == "ผู้ประเมิน" ? 2.0 : 0), 199.9, role);
        }
        return page;
    }

    /// <summary>
    /// RPT-22 ใบขอบรรจุเป็นพนักงานประจำ (report of the same name), A5 ตั้ง, 14 pt: ชื่อ แผนก, ความคิดเห็นของหัวหน้าแผนก
    /// three boxes to tick and lines to write on, then three signatures.
    /// </summary>
    public static FormPage Request(OnboardingEmployee e, string? documentNumber)
    {
        const double pt = 14;
        var page = new FormPage(Paper.A5Portrait);
        void T(double x, double y, string? text, TextAnchor anchor = TextAnchor.Start, double? fit = null) => page.Text(x, y, text, pt, anchor, fit: fit);

        page.Text(72.65, 22.8, Company, 16, TextAnchor.Middle, bold: true);
        page.Text(72.75, 32.1, "ใบขอบรรจุเป็นพนักงานประจำ", pt, TextAnchor.Middle, bold: true);
        T(103.5, 32.0, documentNumber);
        T(6.2, 43.0, "ชื่อ"); T(13.2, 43.0, e.Name, fit: 75.1 - 13.2);
        T(81.3, 43.0, "แผนก"); T(92.3, 43.0, e.DepartmentName, fit: 134.4 - 92.3);
        page.Line(12.9, 45.0, 75.1, 45.0).Line(91.9, 45.0, 134.4, 45.0);
        T(6.2, 50.0, "ความคิดเห็นของหัวหน้าแผนก:");
        page.Box(15.7, 53.8, 4.4, 5.1).Box(15.7, 61.9, 4.4, 5.0).Box(15.7, 69.9, 4.4, 5.0);
        T(26.0, 58.0, "เห็นสมควรให้บรรจุเป็นพนักงานประจำ");
        T(26.0, 66.0, "ไม่เห็นสมควรให้บรรจุเป็นพนักงานประจำ");
        T(26.0, 74.0, "เห็นสมควร แต่ต้องปรับปรุงและต่อระยะเวลาในการทดลองงาน");
        T(26.0, 82.0, "- จุดดีและจุดเด่น:");
        page.Leader(6.2, 139.4, 90.0, pt).Leader(6.2, 139.4, 98.0, pt);
        T(26.0, 106.0, "- สิ่งที่ควรปรับปรุง:");
        page.Leader(6.2, 139.4, 114.0, pt).Leader(6.2, 139.4, 121.0, pt);
        foreach (var x in new[] { 6.2, 55.6, 103.0 }) T(x, 142.0, "ลงชื่อ..............................");
        T(16.1, 149.0, "หัวหน้าแผนก"); T(66.5, 149.0, "ผู้จัดการฝ่าย"); T(131.3, 149.0, "ผู้จัดการโรงงาน", TextAnchor.End);
        return page;
    }
}
