using System.Globalization;
using Lockte.Hr;
using Lockte.Web.Reports;
using Lockte.Web.Reports.Hr;

namespace Lockte.Web.Tests;

/// <summary>
/// แบบกลางระดับ B (#40) and the HTML of RPT-01, without Chromium: what the page carries before it becomes a PDF.
/// </summary>
public sealed class ReportLayoutTests
{
    private static readonly PrintStamp Stamp = new(new DateTimeOffset(2026, 10, 19, 14, 5, 0, TimeSpan.FromHours(7)), "สมหญิง ใจดี");

    private static DateOnly D(string iso) => DateOnly.ParseExact(iso, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    [Fact] // หัว "บริษัท ล็อคเต้ จำกัด" + ชื่อเอกสาร + เลขเอกสารควบคุมขวาบน · ท้าย วันเวลาที่พิมพ์ + ผู้พิมพ์ / "หน้า x จาก y"
    public void Every_page_has_the_company_title_document_number_print_stamp_and_page_x_of_y()
    {
        var html = ReportHtml.Compose(new ReportDocument("ใบลางาน", Paper.A5Landscape, "WI-PE-004-1/2547 ฉบับที่ 5"), Stamp, "<p>เนื้อหา</p>");

        Assert.Contains("@page{size:A5 landscape", html);
        Assert.Contains("\"บริษัท ล็อคเต้ จำกัด\\A ใบลางาน\"", html);
        Assert.Contains("\"WI-PE-004-1/2547 ฉบับที่ 5\"", html);
        Assert.Contains("\"พิมพ์เมื่อ 19/10/2569 14:05 โดย สมหญิง ใจดี\"", html);
        Assert.Contains("\"หน้า \" counter(page) \" จาก \" counter(pages)", html);
        Assert.Contains("<p>เนื้อหา</p>", html);
        Assert.DoesNotContain("logo", html); // ระดับ B ไม่มีโลโก้
    }

    [Theory]
    [InlineData(Paper.A4Portrait, "A4 portrait")]
    [InlineData(Paper.A4Landscape, "A4 landscape")]
    [InlineData(Paper.A5Portrait, "A5 portrait")]
    [InlineData(Paper.A5Landscape, "A5 landscape")]
    public void The_page_size_follows_the_paper(Paper paper, string css)
        => Assert.Contains($"@page{{size:{css}", ReportHtml.Compose(new ReportDocument("x", paper), Stamp, ""));

    [Fact]
    public void Without_a_document_number_the_top_right_is_empty()
        => Assert.DoesNotContain("@top-right", ReportHtml.Compose(new ReportDocument("คำนวณเวลาทำงาน", Paper.A4Portrait), Stamp, ""));

    [Fact] // a display name is typed by the admin: it must not break out of the CSS string
    public void Text_in_the_margins_is_escaped()
    {
        var html = ReportHtml.Compose(new ReportDocument("a\"b", Paper.A4Portrait), Stamp with { PrintedBy = "x\\\"}</style><script>" }, "");

        Assert.Contains("a\\\"b", html);
        Assert.Contains("x\\\\\\\"}\\3C /style\\3E \\3C script\\3E ", html);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(html, "</style>"));
    }

    [Fact] // ตารางกว้างเกิน → ย่อพอดีหน้า; ข้อความยาว → ตัดบรรทัด
    public void Wide_tables_shrink_to_the_page_and_long_text_wraps()
    {
        var html = ReportHtml.Compose(new ReportDocument("x", Paper.A4Portrait), Stamp, "");

        Assert.Contains("overflow-wrap:anywhere", html);
        Assert.Contains(ReportHtml.FitToPageScript, html);
    }

    [Theory] // ชื่อเดือนในหัวคำนวณจากช่วงวันที่ (D-5)
    [InlineData("2026-10-01", "2026-10-31", "ตุลาคม 2569")]
    [InlineData("2026-10-12", "2026-11-11", "ตุลาคม–พฤศจิกายน 2569")]
    [InlineData("2026-12-27", "2027-01-11", "ธันวาคม 2569–มกราคม 2570")]
    public void The_month_title_comes_from_the_dates(string from, string to, string title)
        => Assert.Equal(title, ReportText.MonthTitle(D(from), D(to)));

    private static TimeReport Rpt01(params EmployeeTimeTotal[] lines)
        => new(new PayPeriodDto(1, D("2026-08-27"), D("2026-09-11"), D("2026-09-16"), PayPeriodStep.TimeCalculated, null, null, []),
            WeeklyHolidays: 2, PublicHolidays: 1, FirstPublicHoliday: D("2026-09-01"), lines);

    private static EmployeeTimeTotal Line(string code, double normal) => new(1, code, "นาย ทดสอบ " + code, normal, 1, 0, 17.0 / 3, 8, 2, 4, 1.5, 1, 2, 0);

    [Fact] // RPT-01: หัว ตั้งแต่–ถึง วันอาทิตย์ วันหยุด วันทำงาน รวมทั้งหมด · ทศนิยม 2 ทุกช่อง · ท้าย "รวม n คน"
    public async Task The_time_report_shows_the_period_header_lines_with_two_decimals_and_the_head_count()
    {
        var html = await ReportBody.RenderAsync<TimeReportView>(new() { [nameof(TimeReportView.Report)] = Rpt01(Line("A001", 13.9375), Line("A002", 0.9375)) });

        Assert.Contains("ตั้งแต่วันที่ 27/8/2569 ถึงวันที่ 11/9/2569", html);
        Assert.Contains("วันอาทิตย์ 2 วัน", html);
        Assert.Contains("วันหยุด 1 วัน", html);
        Assert.Contains("วันทำงาน 13 วัน", html);
        Assert.Contains("รวมทั้งหมด 16 วัน", html);
        Assert.Contains(">13.94<", html);
        Assert.Contains(">0.94<", html);
        Assert.Contains(">5.67<", html);
        Assert.Contains("รวม 2 คน", html);
        Assert.True(html.IndexOf("A001", StringComparison.Ordinal) < html.IndexOf("A002", StringComparison.Ordinal));
    }

    private static readonly PayPeriodDto Paid = new(1, D("2026-08-27"), D("2026-09-11"), D("2026-09-16"), PayPeriodStep.PayCalculated, null, null, []);

    private static PayLine PayLine(string code, decimal payNormal, decimal payOvertime)
        => new(new EmployeePay(1, code, "นาย ทดสอบ " + code, 400, 13.9375, 1, 17.0 / 3, 10, payNormal, payOvertime, 0, 0, 2, OvertimeParts.None), 1, "ไดคาสท์");

    [Fact] // RPT-02: หัว "ค่าแรงงวดวันที่ TO แผนก …" · อัตรา/วัน ทศนิยม 2 · เงินเต็มบาทมีคั่นหลักพัน · ท้าย รวม n คน + ผลรวม
    public async Task The_pay_report_shows_the_lines_and_their_totals()
    {
        var report = new PayReport(Paid, "ไดคาสท์", [PayLine("A001", 5600, 263), PayLine("A002", 7500, 1000)]);

        var html = System.Net.WebUtility.HtmlDecode(await ReportBody.RenderAsync<PayReportView>(new() { [nameof(PayReportView.Report)] = report }));

        Assert.Contains("ค่าแรงงวดวันที่ 11/9/2569", html);
        Assert.Contains("แผนก ไดคาสท์", html);
        Assert.Contains(">400.00<", html);
        Assert.Contains(">13.94<", html);
        Assert.Contains(">5,863<", html);
        Assert.Contains("รวม 2 คน", html);
        Assert.Contains(">13,100<", html); // ΣPAY_W
        Assert.Contains(">14,363<", html); // Σยอดรวม
        Assert.Contains("ผู้รับเงิน", html);
    }

    [Fact] // SCR-41 "*"
    public async Task The_pay_report_of_all_departments_says_so()
        => Assert.Contains("แผนก " + PayReportView.AllDepartments,
            System.Net.WebUtility.HtmlDecode(await ReportBody.RenderAsync<PayReportView>(new() { [nameof(PayReportView.Report)] = new PayReport(Paid, null, []) })));

    [Fact] // RPT-03: หัววันอาทิตย์/วันหยุด/วันทำงาน/รวม/คิดค่าแรง · %OT ทศนิยม 2 · แถวรวม · ลงนาม 4 ช่อง
    public async Task The_pay_summary_shows_each_department_the_totals_and_the_signatures()
    {
        var report = new PaySummaryReport(Paid, 2, 1, [new DepartmentPayTotal("ไดคาสท์", 12, 77463, 2480, 0), new DepartmentPayTotal("ซ่อมบำรุง", 1, 13228, 0, 0)]);

        var html = System.Net.WebUtility.HtmlDecode(await ReportBody.RenderAsync<PaySummaryView>(new() { [nameof(PaySummaryView.Report)] = report }));

        Assert.Contains("วันที่จ่าย 16/9/2569", html);
        Assert.Contains("วันทำงาน 13 วัน", html);
        Assert.Contains("คิดค่าแรง 14 วัน", html);
        Assert.Contains(">79,943<", html);
        Assert.Contains(">3.20<", html);
        Assert.Contains(">0.00<", html);
        Assert.Contains(">93,171<", html); // รวมทั้งหมด
        foreach (var sign in new[] { "บุคคล", "ผู้จัดการโรงงาน", "สมุห์บัญชี", "กรรมการผู้จัดการ" }) Assert.Contains(sign, html);
    }

    [Fact] // RPT-58: ชื่อ "ค่าแรงพนักงานทำงานลูกค้า" & ชื่อลูกค้า · หัววันหยุด · รวม = ค่าแรง + OT + HOT · ไม่มี %OT · ลงนาม 5 ช่อง
    public async Task The_contract_labor_summary_shows_each_department_the_totals_and_the_five_signatures()
    {
        var report = new ContractLaborReport(D("2026-08-27"), D("2026-09-11"), "Y", "ลูกค้าสมมุติ", 2, 1,
            [new DepartmentPayTotal("ตกแต่ง", 3, 4800, 450, 30), new DepartmentPayTotal("ไดคาสท์", 1, 1200, 0, 0)], []);

        var html = System.Net.WebUtility.HtmlDecode(await ReportBody.RenderAsync<ContractLaborView>(new() { [nameof(ContractLaborView.Report)] = report }));

        Assert.Equal("ค่าแรงพนักงานทำงานลูกค้าลูกค้าสมมุติ", ContractLaborView.TitleOf(report));
        Assert.Contains("ตั้งแต่วันที่ 27/8/2569", html);
        Assert.DoesNotContain("วันที่จ่าย", html);
        Assert.Contains("วันทำงาน 13 วัน", html);
        Assert.Contains("คิดค่าแรง 14 วัน", html);
        Assert.Contains(">5,280<", html); // ตกแต่ง 4,800 + 450 + 30
        Assert.Contains(">6,480<", html); // รวมทั้งหมด
        Assert.Equal(6, html.Split("</th>").Length - 1); // ลำดับ แผนก จำนวนพนักงาน ค่าแรง ค่าล่วงเวลา รวม
        foreach (var sign in new[] { "บุคคล", "ผู้จัดการโรงงาน", "สมุห์บัญชี", "ผู้ช่วยกรรมการผู้จัดการ", "กรรมการผู้จัดการ" }) Assert.Contains(sign, html);
    }

    [Fact] // D-6: ไม่มีข้อมูล → "ไม่มีข้อมูล" แทนแถวว่าง
    public async Task An_empty_report_says_there_is_no_data()
    {
        var html = await ReportBody.RenderAsync<TimeReportView>(new() { [nameof(TimeReportView.Report)] = Rpt01() });

        Assert.Contains("ไม่มีข้อมูล", html);
        Assert.DoesNotContain("รวม 0 คน", html);
    }
}
