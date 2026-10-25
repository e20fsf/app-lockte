using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Lockte.Hr;
using Lockte.Web.Reports;
using Lockte.Web.Reports.Hr;
using Microsoft.AspNetCore.Components;

namespace Lockte.Web.Tests;

/// <summary>
/// ระดับ A forms of #65 (RPT-50, 53, 54, 55): what each page prints, and that each becomes one sheet of its paper.
/// Where things sit was checked by overlaying the legacy PDFs (≤ ~2 mm, #41); that is not automated.
/// </summary>
[Collection(ChromiumCollection.Name)]
public sealed class HrFormTests(ChromiumFixture chromium)
{
    private static readonly DateOnly Today = new(2026, 10, 15);

    private static readonly CertificateData Person = new("นางสาวหนูกานต์  สุขเทวี", "28  เดือน  กันยายน  พ.ศ.  2525", 44, "พนักงาน", "ตกแต่ง",
        "22  เดือน  กรกฎาคม  พ.ศ.  2562", "7  ปี  3  เดือน", 9672, "นายสมบัติ  ตัวอย่างดี", "15  เดือน  ตุลาคม  พ.ศ.  2569");

    private static readonly SeveranceData Severance = new(Person, 575, 400, 230000m, "ป่วยบ่อย", SeveranceScale.Legacy);

    private static readonly LeaveFormData Leave = new("A005", "นางสาวทองใส  โชยรัมย์", "ประกอบ", "D", new DateOnly(1992, 10, 26), false,
        "มีสิทธิลาพักร้อน 6 วัน", "ลากิจได้ค่าแรง 6 วัน", 2026, 22.25, new Dictionary<string, double> { ["1"] = 4.5, ["6"] = 5.75 }, 1, 0);

    private static readonly ResignationLetterData Letter = new("A005", "นางสาวทองใส  โชยรัมย์", "ประกอบ", "D", new DateOnly(1992, 10, 26),
        new DateOnly(2026, 10, 2), "กลับบ้าน", "นางสาวอัมพร   เจริญสุข", "นายสมชาย ใจดี", "นายสมบัติ  ตัวอย่างดี");

    private static Task<string> Html<T>(Dictionary<string, object?> parameters) where T : IComponent => ReportBody.RenderAsync<T>(parameters);

    private static string Text(string html) => System.Net.WebUtility.HtmlDecode(Regex.Replace(html, "<[^>]+>", "\n"));

    [Fact] // RPT-53: หนังสือรับรองเงินเดือน = เดือนละ + amount + ThaiNumber in brackets; การทำงาน has the standard sentence instead
    public async Task The_salary_certificate_prints_the_amount_in_figures_and_thai_words()
    {
        var salary = Text(await Html<CertificateView>(new() { ["Person"] = Person, ["Salary"] = true, ["DocumentNumber"] = "WI-PE003-1/2547 ฉบับที่ 1" }));
        var work = Text(await Html<CertificateView>(new() { ["Person"] = Person }));

        Assert.Contains("หนังสือรับรองเงินเดือน", salary);
        Assert.Contains("WI-PE003-1/2547 ฉบับที่ 1", salary);
        Assert.Contains("9,672.00", salary);
        Assert.Contains("(เก้าพันหกร้อยเจ็ดสิบสองบาทถ้วน)", salary);
        Assert.Contains("7  ปี  3  เดือน", salary);
        Assert.Contains("(นายสมบัติ  ตัวอย่างดี)", salary);
        Assert.Contains("หนังสือรับรองการทำงาน", work);
        Assert.Contains("ตลอดเวลาที่ทำงานกับบริษัทฯ", work);
        Assert.DoesNotContain("บาทถ้วน", work);
    }

    [Fact] // RPT-54: ค่าชดเชย in figures and words, the 7-line table; ปลดออก adds เหตุผล and รองผู้จัดการโรงงาน
    public async Task The_severance_forms_print_the_compensation_and_the_scale()
    {
        var retire = Text(await Html<SeveranceView>(new() { ["Data"] = Severance, ["Today"] = Today }));
        var dismiss = Text(await Html<SeveranceView>(new() { ["Data"] = Severance, ["Dismissal"] = true, ["Today"] = Today }));

        Assert.Contains("15 ตุลาคม 2569", retire);
        Assert.Contains("เรื่อง พนักงานขอเกษียณการทำงาน", retire);
        Assert.Contains("575.00", retire);
        Assert.Contains("230,000.00", retire);
        Assert.Contains("(สองแสนสามหมื่นบาทถ้วน)", retire);
        Assert.Contains("อายุงานตั้งแต่ 20 ปีขึ้นไป", retire);
        Assert.DoesNotContain("ป่วยบ่อย", retire);
        Assert.Contains("\nผู้จัดการโรงงาน", retire);
        Assert.Contains("เรื่อง ขอปลดพนักงานโดยจ่ายค่าจ้าง", dismiss);
        Assert.Contains("ป่วยบ่อย", dismiss);
        Assert.Contains("รองผู้จัดการโรงงาน", dismiss);
    }

    [Fact] // RPT-50: วันลา Standard 1 decimal, blank for types without leave, X in บรรจุ, ใบเตือน counts, entitlement texts
    public async Task The_leave_form_prints_this_years_days_and_the_entitlements()
    {
        var html = await Html<LeaveFormView>(new() { ["Form"] = Leave, ["PrintedAt"] = new DateTime(2026, 10, 15, 11, 16, 0) });
        var text = Text(html);

        Assert.Contains("22.3", text); // 22.25 → Standard 1 decimal, half away from zero
        Assert.Contains("4.5", text);
        Assert.Contains("5.8", text);
        Assert.Contains("11:16", text);
        Assert.Contains("มีสิทธิลาพักร้อน 6 วัน", text);
        Assert.Contains("ลากิจได้ค่าแรง 6 วัน", text);
        Assert.Contains("ประวัติการลางานปี", text);
        Assert.Single(Regex.Matches(text, "\nX\n"));
        Assert.Contains("ลาป่วยเป็นโรคระบาด", text);
    }

    [Fact] // RPT-55: date, reason and the names in brackets
    public async Task The_resignation_letter_prints_the_typed_date_and_reason()
    {
        var text = Text(await Html<ResignationLetterView>(new() { ["Letter"] = Letter, ["Today"] = Today, ["DocumentNumber"] = "WI-PE006-1/2547 ฉบับที่ 1" }));

        Assert.Contains("กลับบ้าน", text);
        Assert.Contains("\nตุลาคม\n", text);
        Assert.Contains("\n2535\n", text);
        Assert.Contains("(นางสาวอัมพร   เจริญสุข)", text);
        Assert.Contains("(นายสมชาย ใจดี)", text);
        Assert.Contains("WI-PE006-1/2547 ฉบับที่ 1", text);
    }

    [Fact] // RPT-55 list: 34 rows a page, "หน้า x จาก y" on every page, "รวม n คน" once; none → ไม่มีข้อมูล (D-6)
    public async Task The_resigned_list_pages_by_34_rows_and_counts_them()
    {
        var rows = Enumerable.Range(1, 40).Select(i => new ResignedEmployee($"A{i:D3}", "ชื่อ " + i, new DateOnly(2020, 2, 10), new DateOnly(2021, 1, 4), 329 / 365.0, "กลับบ้าน")).ToList();
        var text = Text(await Html<ResignedListView>(new() { ["Rows"] = rows, ["Today"] = Today }));
        var empty = Text(await Html<ResignedListView>(new() { ["Rows"] = Array.Empty<ResignedEmployee>(), ["Today"] = Today }));

        Assert.Contains("หน้า 1 จาก 2", text);
        Assert.Contains("หน้า 2 จาก 2", text);
        Assert.Contains("0.90", text);
        Assert.Contains("10/2/2563", text);
        Assert.Contains("\n40\n", text);
        Assert.Single(Regex.Matches(text, "\nรวม\n"));
        Assert.Contains("ไม่มีข้อมูล", empty);
        Assert.Contains("หน้า 1 จาก 1", empty);
    }

    public static TheoryData<string, Paper, int> Sheets => new()
    {
        { "leave", Paper.A5Landscape, 1 },
        { "work", Paper.A4Portrait, 1 },
        { "dismiss", Paper.A4Portrait, 1 }, // the legacy printed a blank page 2
        { "letter", Paper.A4Portrait, 1 },  // Letter → A4 (D-7)
        { "list", Paper.A4Portrait, 2 },
    };

    [Theory] // each form is exactly its sheets of its paper, with TH Sarabun New embedded
    [MemberData(nameof(Sheets))]
    public async Task Each_form_prints_on_its_own_paper(string form, Paper paper, int pages)
    {
        var body = form switch
        {
            "leave" => await Html<LeaveFormView>(new() { ["Form"] = Leave, ["PrintedAt"] = new DateTime(2026, 10, 15, 11, 16, 0) }),
            "work" => await Html<CertificateView>(new() { ["Person"] = Person }),
            "dismiss" => await Html<SeveranceView>(new() { ["Data"] = Severance, ["Dismissal"] = true, ["Today"] = Today }),
            "letter" => await Html<ResignationLetterView>(new() { ["Letter"] = Letter, ["Today"] = Today }),
            _ => await Html<ResignedListView>(new()
            {
                ["Rows"] = Enumerable.Range(1, 40).Select(i => new ResignedEmployee("A001", "ชื่อ", null, new DateOnly(2021, 1, 4), null, null)).ToList(),
                ["Today"] = Today,
            }),
        };

        var pdf = Encoding.Latin1.GetString(await chromium.Renderer.RenderAsync(FormHtml.Compose(form, paper, body), paper));

        Assert.Equal(pages, Regex.Matches(pdf, @"/Type /Page\b").Count);
        var box = Regex.Match(pdf, @"/MediaBox \[0 0 ([\d.]+) ([\d.]+)\]");
        var (width, height) = ReportHtml.PageMillimetres(paper);
        Assert.Equal(width, double.Parse(box.Groups[1].Value, CultureInfo.InvariantCulture) * 25.4 / 72, 0);
        Assert.Equal(height, double.Parse(box.Groups[2].Value, CultureInfo.InvariantCulture) * 25.4 / 72, 0);
        Assert.Contains("THSarabunNew", pdf);
    }

    [Fact] // ระดับ A has no แบบกลาง header/footer and no margins: the form places everything itself
    public void A_form_page_has_no_margins_header_or_footer()
    {
        var html = FormHtml.Compose("ใบลางาน", Paper.A5Landscape, "<div class=\"page\"></div>");

        Assert.Contains("@page{size:A5 landscape;margin:0}", html);
        Assert.DoesNotContain("@top-center", html);
        Assert.DoesNotContain("@bottom-left", html);
        Assert.Contains("width:210mm;height:148mm", html);
    }
}
