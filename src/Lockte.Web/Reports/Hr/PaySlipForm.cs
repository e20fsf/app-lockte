using System.Globalization;
using Lockte.Hr;
using Lockte.SharedKernel;

namespace Lockte.Web.Reports.Hr;

/// <summary>
/// RPT-04 สลิปเงินเดือน (report <c>พิมพ์SLIP</c>, BR-034), ระดับ A (#40 ตรงแบบเดิม): A4 ตั้ง, three slips a page, one every
/// 95 mm (the report's 9.5 cm detail section), to cut apart. Positions in mm — the baseline for texts — measured on the
/// sample <c>พิมพ์SLIP.pdf</c> (lot 2) for the first slip; Access printed the labels at Angsana New 12 pt (the company 16 pt
/// bold, the title 14 pt). Changes from the legacy, all on #40's list: TH Sarabun New at Angsana's size; the company logo
/// is docs/brand/logo.svg; a long name is squeezed into its space instead of running into the next label (D-1); the legacy
/// ค่าแรงค้างงวดก่อน column is gone and เงินสงเคราะห์ลูกจ้าง/บริษัท and รวมเงินสะสม are in (#180, the slip of lot 4); a signer with a signature picture prints the
/// picture over the line instead of the typed name (#55, the legacy ตาราง3.SIGN that the report never showed).
/// </summary>
public static class PaySlipForm
{
    public const string Title = "สลิปเงินเดือน";
    public const int SlipsPerPage = 3;
    /// <summary>The report's detail section: 9.50 cm.</summary>
    public const double SlipHeight = 95;

    private const double Body = 12;
    private const double Weight = 0.35;
    /// <summary>The left and right rule of the tables.</summary>
    private const double Left = 26.77, Right = 187.85;

    /// <summary>The rules between the ten columns of the totals row, left to right (ATM … เงินได้สุทธิ).</summary>
    private static readonly double[] TotalsEdges = [Left, 37.63, 52.0, 73.0, 94.0, 112.0, 122.5, 133.0, 148.0, 164.5, Right];

    private static readonly Lazy<byte[]> Logo = new(() =>
    {
        using var stream = typeof(PaySlipForm).Assembly.GetManifestResourceStream("Lockte.Web.brand.logo.svg")
            ?? throw new InvalidOperationException("The logo is not embedded in Lockte.Web.");
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    });

    /// <summary>The slips three to a page, by รหัส as given.</summary>
    public static IReadOnlyList<FormPage> Layout(PaySlipBatch batch)
    {
        if (batch.Slips.Count == 0) throw new ArgumentException("No slip to print.", nameof(batch));
        return batch.Slips.Chunk(SlipsPerPage).Select(slips =>
        {
            var page = new FormPage(Paper.A4Portrait);
            for (var i = 0; i < slips.Length; i++) Slip(page, i * SlipHeight, batch, slips[i]);
            return page;
        }).ToList();
    }

    /// <summary>Hours: Format Fixed, 1 decimal (no thousands separator).</summary>
    public static string Hours(double value)
        => Math.Round((decimal)value, 1, MidpointRounding.AwayFromZero).ToString("0.0", CultureInfo.InvariantCulture);

    private static void Slip(FormPage page, double top, PaySlipBatch batch, PaySlip s)
    {
        void T(double x, double baseline, string? text, TextAnchor anchor = TextAnchor.Start, double points = Body, double? fit = null)
            => page.Text(x, top + baseline, text, points, anchor, fit: fit);
        void H(double x0, double x1, double y) => page.Line(x0, top + y, x1, top + y, Weight);
        void V(double x, double y0, double y1) => page.Line(x, top + y0, x, top + y1, Weight);
        var period = batch.Period;

        // หัว: โลโก้ (OLEUnbound25), บริษัท, ที่อยู่, งวด
        page.Image(29.04, top + 4.23, 45.99 - 29.04, 21.22 - 4.23, "image/svg+xml", Logo.Value);
        page.Text(91.29, top + 10.99, "บริษัท ล็อคเต้ จำกัด", 16, bold: true);
        T(50.75, 17.53, "85/36 ซ.สุขาภิบาล2 ถ.พุทธมณฑลสาย5 ต.อ้อมน้อย อ.กระทุ่มแบน จ.สมุทรสาคร โทรฯ024204554", fit: 159.88 - 50.75);
        T(74.48, 23.18, "รายละเอียดการจ่ายค่าแรงงวดวันที่", points: 14);
        T(120.96, 23.18, ThaiDate.Format(period.PayDate), points: 14);

        const double days = 29.5;
        T(33.45, days, "ตั้งแต่วันที่"); T(46.86, days, ThaiDate.Format(period.From), fit: 61.0 - 46.86); // TH Sarabun's digits are wider
        T(61.86, days, "ถึงวันที่"); T(71.53, days, ThaiDate.Format(period.To), fit: 87.0 - 71.53);
        T(87.91, days, "จำนวน"); T(101.53, days, Whole(s.Days), TextAnchor.End); T(104.22, days, "วัน");
        T(109.61, days, "วันอาทิตย์"); T(123.46, days, Whole(s.WeeklyHolidays), TextAnchor.End); T(124.83, days, "วัน");
        T(131.8, days, "วันหยุด"); T(143.07, days, Whole(s.PublicHolidays), TextAnchor.End); T(144.59, days, "วัน");
        T(151.08, days, "วันทำงาน"); T(165.86, days, Whole(s.WorkingDays), TextAnchor.End); T(167.4, days, "วัน");

        const double who = 35.52;
        T(32.98, who, "รหัสพนักงาน"); T(49.82, who, s.Code);
        T(65.59, who, "ชื่อ-นามสกุล"); T(81.44, who, s.Name, fit: 116.2 - 81.44);
        T(116.98, who, "ค่าแรงเดือนละหรือวันละ"); T(159.95, who, Forms.FormFormat.Standard(s.Rate, 2), TextAnchor.End); T(161.43, who, "บาท");

        // ตาราง: เวลาทำงาน | ค่าแรง, then the deductions and totals
        foreach (var y in new[] { 38.25, 45.23, 52.22, 58.21, 65.21, 73.2 }) H(Left, Right, y);
        V(Left, 38.24, 73.25);
        V(Right, 38.24, 73.25);
        V(106.82, 38.24, 58.2);
        foreach (var x in new[] { 36.66, 44.57, 56.41, 66.29, 77.19, 92.98, 120.68, 134.49, 144.37, 155.23, 172.07 }) V(x, 45.23, 58.42);
        foreach (var x in TotalsEdges.Skip(1).SkipLast(1)) V(x, 58.21, 73.2);

        T(60.64, 43.52, "เวลาทำงาน"); T(144.82, 43.52, "ค่าแรง");
        const double heads = 50.52, values = 56.49;
        T(27.03, heads, "วันปกติ"); T(37.63, heads, "วันลา"); T(45.09, heads, "วันอาทิตย์"); T(57.21, heads, "วันหยุด");
        T(67.28, heads, "OTปกติ"); T(77.44, heads, "OTวันอาทิตย์"); T(93.97, heads, "OTวันหยุด");
        T(108.86, heads, "วันปกติ"); T(121.9, heads, "วันอาทิตย์"); T(134.74, heads, "วันหยุด");
        T(145.34, heads, "OTปกติ"); T(156.22, heads, "OTวันอาทิตย์"); T(173.03, heads, "OTวันหยุด");
        T(34.47, values, Hours(s.Normal), TextAnchor.End); T(41.84, values, Hours(s.PaidLeave), TextAnchor.End);
        T(52.11, values, Hours(s.Sunday), TextAnchor.End); T(63.05, values, Hours(s.Holiday), TextAnchor.End);
        T(72.54, values, Hours(s.Overtime), TextAnchor.End); T(86.29, values, Hours(s.SundayOvertime), TextAnchor.End);
        T(101.06, values, Hours(s.HolidayOvertime), TextAnchor.End);
        T(113.76, values, ReportText.Baht(s.PayNormal), TextAnchor.Middle);
        T(132.32, values, ReportText.Baht(s.Parts.Sunday), TextAnchor.End); T(142.22, values, ReportText.Baht(s.Parts.Holiday), TextAnchor.End);
        T(153.94, values, ReportText.Baht(s.Parts.Overtime), TextAnchor.End);
        T(170.04, values, ReportText.Baht(s.Parts.SundayOvertime), TextAnchor.End);
        T(183.96, values, ReportText.Baht(s.Parts.HolidayOvertime), TextAnchor.End);

        // The totals row of today's slip (photo of lot 4, #180): the legacy ค่าแรงค้างงวดก่อน gives way to the เงินสงเคราะห์ and the
        // รวมเงินสะสม; ประกันสังคม and ภาษี stay blank (Q-02); เงินได้สุทธิ = รวมค่าแรง − เงินสงเคราะห์ลูกจ้าง.
        const double headOne = 63.51, headUp = 61.6, headDown = 65.6, totals = 70.5;
        void Head(int column, string? first, string? second = null)
        {
            double x0 = TotalsEdges[column], x1 = TotalsEdges[column + 1], middle = (x0 + x1) / 2, fit = x1 - x0 - 1;
            if (second is null) T(middle, headOne, first, TextAnchor.Middle, fit: fit);
            else { T(middle, headUp, first, TextAnchor.Middle, fit: fit); T(middle, headDown, second, TextAnchor.Middle, fit: fit); }
        }
        void Value(int column, decimal value) => T((TotalsEdges[column] + TotalsEdges[column + 1]) / 2, totals, ReportText.Baht(value), TextAnchor.Middle);
        Head(0, "ATM"); Head(1, "ประกันสังคม"); Head(2, "กองทุนสงเคราะห์", "ลูกจ้าง (0.25%)"); Head(3, "กองทุนสงเคราะห์", "บริษัท (0.25%)");
        Head(4, "รวมเงินสะสม", "ทั้งหมด"); Head(5, "ภาษี"); Head(6, "โบนัส"); Head(7, "ค่าพักร้อน"); Head(8, "รวมค่าแรง"); Head(9, "เงินได้สุทธิ");
        if (s.Atm is { } atm) T(TotalsEdges[1] - 1.03, totals, Forms.FormFormat.Standard((double)atm, 2), TextAnchor.End);
        Value(2, s.WelfareEmployee); Value(3, s.WelfareCompany); Value(4, s.WelfareAccumulated);
        Value(6, s.Bonus); Value(7, s.Hot); Value(8, s.Total); Value(9, s.Net);

        // ลงนาม: ผู้รับเงิน on its own line; แผนกบุคคล and ผู้จัดการโรงงาน by name or signature picture
        const double sign = 79.46;
        T(39.79, sign, "ผู้รับเงิน"); page.Line(50.37, top + 79.2, 77.06, top + 79.2, 0.1);
        T(78.37, sign, "แผนกบุคคล"); page.Line(93.06, top + 79.4, 119.79, top + 79.4, 0.1);
        T(124.82, sign, "ผู้จัดการโรงงาน"); page.Line(143.48, top + 79.4, 170.17, top + 79.4, 0.1);
        Signer(page, top, batch.Signer(SlipSignerRole.HrDepartment), 93.06, 119.79, 94.06);
        Signer(page, top, batch.Signer(SlipSignerRole.FactoryManager), 143.48, 170.17, 146.68);
    }

    /// <summary>The picture over the line (it may cross it, as a pen does), else the name where the legacy label had it.</summary>
    private static void Signer(FormPage page, double top, SlipSignerDto? signer, double x0, double x1, double nameX)
    {
        if (signer?.Signature is { Length: > 0 } picture && signer.SignatureContentType is { } type)
            page.Image(x0, top + 73.8, x1 - x0, 7.6, type, picture);
        else
            page.Text(nameX, top + 78.59, signer?.Name, Body, fit: x1 - nameX);
    }

    private static string Whole(int value) => value.ToString(CultureInfo.InvariantCulture);
}
