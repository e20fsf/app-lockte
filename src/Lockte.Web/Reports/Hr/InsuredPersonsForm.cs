using Lockte.Hr;
using Lockte.SharedKernel;

namespace Lockte.Web.Reports.Hr;

/// <summary>
/// RPT-51 ทะเบียนผู้ประกันตน สปส.6-07 (BR-073, INT-04), ระดับ A (#40): A4 นอน, as the legacy report
/// <c>ทะเบียนผู้ประกันตน</c> printed it — positions in mm measured on the sample <c>ทะเบียนผู้ประกันตน.pdf</c>.
/// Every page repeats the heading (สถานประกอบการ, สาขา, เลขที่บัญชีและลำดับที่สาขาในกล่องทีละตัว), the ruled table head
/// and the หมายเหตุ; 18 rows a page and the table ends at the last row. Changes from the legacy, on #40's list:
/// TH Sarabun New; the employer's details come from <see cref="EmployerOptions"/>; long names and positions are
/// squeezed into their column instead of cut (D-1); a register without rows prints its heading and "ไม่มีข้อมูล" (D-6).
/// </summary>
public static class InsuredPersonsForm
{
    public const string Title = "ทะเบียนผู้ประกันตน";
    public const int RowsPerPage = 18;

    /// <summary>Angsana New 14 / 16 pt as wide as TH Sarabun New these sizes (measured on the sample).</summary>
    public const double Body = 13.5, Heading = 16;

    /// <summary>Table rules (x of each vertical line) and rows (y).</summary>
    private static readonly double[] Columns = [14.99, 29.17, 69.17, 123.71, 141.70, 169.69, 193.70, 227.69, 249.70, 279.72];
    private const double TableTop = 45.5, HeadBottom = 52.4, RowHeight = 7.179, FirstBaseline = 57.6;

    /// <summary>Left edge of each digit box: เลขที่บัญชี 10 หลัก (2 + 7 + 1) and ลำดับที่สาขา 6 หลัก.</summary>
    private static readonly double[] AccountBoxes = [211.81, 217.77, 227.81, 233.79, 239.81, 245.8, 251.78, 257.8, 263.8, 272.83];
    private static readonly double[] BranchBoxes = [227.81, 233.79, 239.81, 245.8, 251.78, 257.8];

    public static IReadOnlyList<FormPage> Layout(IReadOnlyList<InsuredPersonLine> lines, EmployerOptions employer)
    {
        if (lines.Count == 0)
        {
            var empty = Page(employer, 0);
            empty.Text(148.5, FirstBaseline + RowHeight, "ไม่มีข้อมูล", Heading, TextAnchor.Middle);
            return [empty];
        }
        var pages = new List<FormPage>();
        for (var first = 0; first < lines.Count; first += RowsPerPage)
        {
            var rows = lines.Skip(first).Take(RowsPerPage).ToList();
            var page = Page(employer, rows.Count);
            for (var i = 0; i < rows.Count; i++) Row(page, FirstBaseline + i * RowHeight, first + i + 1, rows[i]);
            pages.Add(page);
        }
        return pages;
    }

    private static void Row(FormPage page, double y, int number, InsuredPersonLine line)
    {
        static string? Date(DateOnly? d) => d is { } date ? ThaiDate.Format(date) : null;
        page.Text(27.5, y, number.ToString(ThaiDate.Culture), Body, TextAnchor.End)
            .Text(30.3, y, line.NationalId, Body, fit: 68.6 - 30.3)
            .Text(70.3, y, line.Name, Body, fit: 123.2 - 70.3)
            .Text(125.3, y, line.Nationality, Body, fit: 141.2 - 125.3)
            .Text(168.5, y, Date(line.BirthDate), Body, TextAnchor.End)
            .Text(192.75, y, Date(line.StartDate), Body, TextAnchor.End)
            .Text(195.1, y, line.Position, Body, fit: 227.2 - 195.1)
            .Text(248.4, y, ReportText.Decimal2(line.Wage), Body, TextAnchor.End, fit: 248.4 - 228.2)
            .Text(278.6, y, Date(line.ResignDate), Body, TextAnchor.End);
    }

    /// <summary>A page's heading, table head and rules for <paramref name="rows"/> rows, and หมายเหตุ.</summary>
    private static FormPage Page(EmployerOptions employer, int rows)
    {
        var page = new FormPage(Paper.A4Landscape)
            .Text(90.9, 20.4, "ทะเบียนผู้ประกันตน ตามพระราชบัญญัติประกันสังคม พ.ศ. 2533 มาตรา 84", Heading)
            .Text(260.6, 21.4, "สปส.6-07", Heading)
            .Text(15.3, 29.3, "ชื่อสถานประกอบการ", Heading).Leader(47.8, 182.3, 29.3, Heading)
            .Text(54.3, 28.3, employer.Name, Heading, fit: 182.3 - 54.3)
            .Text(15.3, 38.2, "ชื่อสาขา", Heading).Leader(27.7, 181.7, 38.2, Heading)
            .Text(54.3, 37.2, employer.BranchName, Heading, fit: 181.7 - 54.3)
            .Text(194.2, 29.3, "เลขที่บัญชี", Heading)
            .Text(194.2, 38.2, "ลำดับที่สาขา", Heading);
        Digits(page, AccountBoxes, 25.08, 6.3, 29.3, employer.SocialSecurityAccountNo);
        Digits(page, BranchBoxes, 33.98, 6.27, 38.2, employer.SocialSecurityBranchNo);

        string[] heads = ["ลำดับที่", "เลขประจำตัวประชาชน", "ชื่อ - ชื่อสกุล", "สัญชาติ", "วัน เดือน ปี เกิด", "วันเริ่มจ้าง", "ตำแหน่ง", "อัตราค่าจ้าง", "วันสิ้นสุดการจ้าง"];
        for (var c = 0; c < heads.Length; c++)
            page.Text((Columns[c] + Columns[c + 1]) / 2, 51.0, heads[c], Heading, TextAnchor.Middle);

        var bottom = HeadBottom + Math.Max(rows, 0) * RowHeight;
        page.Line(Columns[0], TableTop, Columns[^1], TableTop).Line(Columns[0], HeadBottom, Columns[^1], HeadBottom);
        for (var r = 1; r <= rows; r++) page.Line(Columns[0], HeadBottom + r * RowHeight, Columns[^1], HeadBottom + r * RowHeight);
        foreach (var x in Columns) page.Line(x, TableTop - FormPage.Rule / 2, x, bottom + FormPage.Rule / 2);

        page.Text(15.3, 195.3, "หมายเหตุ", Heading).Line(15.26, 195.6, 29.56, 195.6, 0.2)
            .Text(30.45, 195.3, "สำหรับคนต่างด้าวให้กรอกเลขที่ใบอนุญาตทำงาน ในช่องเลขประจำตัวประชาชน", Heading);
        return page;
    }

    /// <summary>One box per digit, the digit centred in it; only the digits of <paramref name="number"/> count.</summary>
    private static void Digits(FormPage page, double[] boxes, double top, double height, double baseline, string number)
    {
        var digits = number.Where(char.IsAsciiDigit).ToArray();
        for (var i = 0; i < boxes.Length; i++)
        {
            page.Box(boxes[i], top, 4.86, height);
            if (i < digits.Length) page.Text(boxes[i] + 2.43, baseline, digits[i].ToString(), Heading, TextAnchor.Middle);
        }
    }
}
