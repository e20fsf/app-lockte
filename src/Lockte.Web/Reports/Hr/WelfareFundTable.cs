using Lockte.Hr;
using Lockte.Web.Reports.Order;

namespace Lockte.Web.Reports.Hr;

/// <summary>
/// ยอดเงินส่งกองทุนสงเคราะห์ลูกจ้าง (UAT-23) as a <see cref="SummaryTable"/> for the Excel button of SCR-43, laid out as the fund's sheet:
/// header lines (ชื่อสถานประกอบกิจการ, รอบการส่งเงิน ปี พ.ศ./เดือน, วันที่ดำเนินการนำส่ง) then one row per ลูกจ้าง — คำนำหน้า | ชื่อ | สกุล |
/// อายุ | สัญชาติ | เลขบัตรประชาชน/เลขหนังสือเดินทาง | ประเภทการจ้าง | ค่าจ้าง (บาท) | วันที่เริ่มงาน | วันที่สิ้นสุดงาน | สาเหตุการออกจากงาน/หมายเหตุอื่นๆ.
/// The register keeps คำนำหน้า + ชื่อ + สกุล in one field, so <see cref="SplitName"/> cuts it again. ⚠ The register has no
/// สาเหตุการออกจากงาน: a resignation inside the dates is noted "ลาออก", else blank. UAT-43: after the fund's columns, เงินสมทบลูกจ้าง and
/// เงินสมทบนายจ้าง at the rate HR set for the งวด (<see cref="WelfareFundReport.RatePercent"/>, ฝ่ายละเท่ากัน), the rate on a header line.
/// </summary>
public static class WelfareFundTable
{
    public const string Title = "ยอดเงินส่งกองทุนสงเคราะห์ลูกจ้าง";

    /// <summary>Longest first, so "นางสาว" is not read as "นาง".</summary>
    private static readonly string[] Titles =
        ["เด็กหญิง", "เด็กชาย", "นางสาว", "น.ส.", "นาง", "นาย", "ด.ช.", "ด.ญ.", "Miss", "Mrs.", "Mr.", "Ms.", "Mrs", "Mr", "Ms"];

    public static SummaryTable Build(WelfareFundReport report, string employerName, DateOnly deliveredOn)
    {
        var columns = new List<SummaryColumn>
        {
            new("คำนำหน้า"), new("ชื่อ"), new("สกุล"), new("อายุ", SummaryFormat.Integer), new("สัญชาติ"),
            new("เลขบัตรประชาชน/เลขหนังสือเดินทาง"), new("ประเภทการจ้าง (รายวัน/รายเดือน)"), new("ค่าจ้าง (บาท)", SummaryFormat.Money),
            new("วันที่เริ่มงาน"), new("วันที่สิ้นสุดงาน"), new("สาเหตุการออกจากงาน/หมายเหตุอื่นๆ"),
            new("เงินสมทบลูกจ้าง (บาท)", SummaryFormat.Money), new("เงินสมทบนายจ้าง (บาท)", SummaryFormat.Money),
        };
        var rows = report.Lines.Select(l =>
        {
            var (title, first, last) = SplitName(l.FullName);
            var resigned = l.EndDate is { } end && end >= report.From && end <= report.To;
            return new object?[]
            {
                title, first, last, l.Age is { } age ? (object)age : null, l.Nationality, l.NationalId,
                l.EmployeeType is { } t && EmployeeTypes.Names.TryGetValue(t, out var type) ? type : null,
                l.Wage, l.StartDate is { } s ? ReportText.Date(s) : null, l.EndDate is { } e ? ReportText.Date(e) : null,
                resigned ? "ลาออก" : null,
                l.Contribution, l.Contribution,
            };
        }).ToList();
        var header = $"ชื่อสถานประกอบกิจการ {employerName}\n"
            + $"รอบการส่งเงิน ปี พ.ศ. {report.To.ToString("yyyy", Lockte.SharedKernel.ThaiDate.Culture)} เดือน {report.To.ToString("MMMM", Lockte.SharedKernel.ThaiDate.Culture)}\n"
            + $"วันที่ดำเนินการนำส่ง {ReportText.Date(deliveredOn)}"
            + (report.RatePercent is { } rate ? $"\nอัตราเงินสมทบ ลูกจ้างและนายจ้าง ฝ่ายละ {WelfareRateTable.PercentText(rate)} ของค่าจ้าง" : "");
        return new SummaryTable(Title, header, columns, rows);
    }

    /// <summary>"นายสมชาย ใจดี" / "นาย สมชาย ใจดี" → (นาย, สมชาย, ใจดี); no known คำนำหน้า → ("", first word, the rest).</summary>
    public static (string Title, string First, string Last) SplitName(string full)
    {
        var text = (full ?? "").Trim();
        var title = Titles.FirstOrDefault(t => text.StartsWith(t, StringComparison.OrdinalIgnoreCase)) ?? "";
        text = text[title.Length..].Trim();
        var parts = text.Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries);
        return (title, parts.Length > 0 ? parts[0] : "", parts.Length > 1 ? parts[1].Trim() : "");
    }
}
