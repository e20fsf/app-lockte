using System.Globalization;
using System.Text.RegularExpressions;
using Lockte.Hr;
using Lockte.Web.Reports;
using Lockte.Web.Reports.Hr;

namespace Lockte.Web.Tests;

/// <summary>
/// ระดับ A (#40, #64): แบบ คร.2 (RPT-57) and ทะเบียนผู้ประกันตน (RPT-51) as SVG pages in mm, before Chromium. That
/// they lie on the legacy sample within ~2 mm is checked by overlaying the PDFs (tools/form-overlay, #40 ตรวจรับ).
/// </summary>
public sealed class GovernmentFormTests
{
    private static DateOnly D(string iso) => DateOnly.ParseExact(iso, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static readonly EmployerOptions Employer = new()
    {
        ManagerFirstName = "สมมติ", ManagerLastName = "ทดสอบ", Business = "ผลิตผ้าเบรก",
        Office = new() { HouseNo = "99", Road = "ถนนตัวอย่าง", Province = "กรุงเทพฯ", Phone = "02-000-0000" },
        Workplace = new() { HouseNo = "88/8", Province = "สมุทรสาคร" },
        SocialSecurityAccountNo = "10-0000001-2", SocialSecurityBranchNo = "000001",
    };

    private static Kor2Employee Child(double wage = 363) => new(1, "A001", "นายเด็ก  ทดสอบ", D("2009-03-31"), "0000000000001",
        "10 หมู่ที่ 1 ต.ตัวอย่าง", D("2026-01-16"), "พนักงาน", "ประกอบ", wage, 187);

    private static string Svg(FormPage page) => page.ToSvg();

    /// <summary>The text placed at (x, baseline), or null.</summary>
    private static string? At(string svg, double x, double baseline)
    {
        var m = Regex.Match(svg, $"<text x=\"{FormPage.N(x)}\" y=\"{FormPage.N(baseline)}\"[^>]*>([^<]*)</text>");
        return m.Success ? System.Net.WebUtility.HtmlDecode(m.Groups[1].Value) : null;
    }

    [Fact] // A4 ตั้ง หน่วย มม. จากมุมซ้ายบน
    public void Kor2_is_one_A4_portrait_page_in_millimetres()
    {
        var svg = Svg(Kor2Form.Layout(Child(), new("นายเด็ก  ทดสอบ", null, null, null), Employer));

        Assert.Contains("width=\"210mm\" height=\"297mm\" viewBox=\"0 0 210 297\"", svg);
        Assert.Equal("แบบ คร.2", At(svg, 157.5, 21.3));
    }

    [Fact] // ข้อมูลลูกจ้าง ณ ตำแหน่งเดิม: วันเกิด/วันเริ่มงานแยก วัน เดือน พ.ศ., เลขบัตร, ภูมิลำเนา, หน้าที่ แผนก
    public void Kor2_prints_the_employee_where_the_legacy_did()
    {
        var svg = Svg(Kor2Form.Layout(Child(), new("นายเด็ก  ทดสอบ", "ประกอบชิ้นงาน", "นายพ่อ ทดสอบ", "นางแม่ ทดสอบ"), Employer));

        Assert.Equal("นายเด็ก  ทดสอบ", At(svg, 77.5, 75));
        Assert.Equal(("31", "มีนาคม", "2552"), (At(svg, 43.9, 82), At(svg, 65.4, 82), At(svg, 81.5, 82)));
        Assert.Equal("0000000000001", At(svg, 157.7, 82));
        Assert.Equal("10 หมู่ที่ 1 ต.ตัวอย่าง", At(svg, 59.7, 89));
        Assert.Equal(("นายพ่อ ทดสอบ", "นางแม่ ทดสอบ"), (At(svg, 45.9, 96), At(svg, 127.9, 96)));
        Assert.Equal(("16", "มกราคม", "2569"), (At(svg, 70.6, 110), At(svg, 91.6, 110), At(svg, 110.1, 110)));
        Assert.Equal(("พนักงาน", "ประกอบ", "ประกอบชิ้นงาน"), (At(svg, 115.6, 131), At(svg, 125.9, 131), At(svg, 59.7, 139)));
    }

    [Fact] // นายจ้างจากข้อมูลตั้งค่า + จำนวนลูกจ้างที่ยังทำงานอยู่ (เดิมพิมพ์ 200 ตายตัว) + ผู้ลงนาม
    public void Kor2_prints_the_employer_from_settings_and_the_working_head_count()
    {
        var svg = Svg(Kor2Form.Layout(Child(), new(null, null, null, null), Employer));

        Assert.Equal(("ข้าพเจ้า นาย", "สมมติ", "ทดสอบ"), (At(svg, 49.8, 40), At(svg, 75.4, 40), At(svg, 132.4, 40)));
        Assert.Equal(("บริษัท ล็อคเต้ จำกัด", "ผลิตผ้าเบรก", "99"), (At(svg, 84.8, 47), At(svg, 51.9, 54), At(svg, 136.5, 54)));
        Assert.Equal(("ถนนตัวอย่าง", "กรุงเทพฯ", "02-000-0000"), (At(svg, 40.3, 61), At(svg, 155.8, 61), At(svg, 47.5, 68)));
        Assert.Equal("187", At(svg, 107.35, 68));
        Assert.Equal(("88/8", "สมุทรสาคร"), (At(svg, 61.9, 189), At(svg, 124.7, 196)));
        Assert.Equal("(  นายสมมติ ทดสอบ  )", At(svg, 113.5, 258));
        Assert.Equal("นายเด็ก  ทดสอบ", At(svg, 77.5, 75)); // ชื่อว่าง → ชื่อในทะเบียน
    }

    [Fact] // D-2: เดิมพิมพ์ ##### · ค่าว่างไม่พิมพ์อะไร
    public void Kor2_prints_the_daily_wage_instead_of_hashes_and_leaves_blank_lines_blank()
    {
        var svg = Svg(Kor2Form.Layout(Child(wage: 412.5), new(null, null, null, null), new EmployerOptions()));

        Assert.Equal("412.5", At(svg, 98.6, 146));
        Assert.DoesNotContain("#", svg, StringComparison.Ordinal);
        Assert.Null(At(svg, 45.9, 96)); // บิดา
        Assert.Null(At(svg, 136.5, 54)); // เลขที่ของนายจ้าง
    }

    [Fact] // D-1: ข้อความยาวบีบให้ลงช่อง ไม่ตัดทิ้ง · ข้อความถูก escape
    public void Long_values_are_marked_to_fit_their_line_and_text_is_escaped()
    {
        var svg = Svg(Kor2Form.Layout(Child(), new(null, "<b>ประกอบ & บรรจุ</b>", null, null), Employer));

        Assert.Contains("<text x=\"59.7\" y=\"139\" font-size=\"4.94\" data-fit=\"118.1\">&lt;b&gt;ประกอบ &amp; บรรจุ&lt;/b&gt;</text>", svg);
        Assert.Contains("getComputedTextLength()>w", FormPageHtml.FitScript);
    }

    [Fact] // ช่องติ๊กเอกสารแนบ 4 ช่อง และเส้นจุดแบบเดิม
    public void Kor2_has_the_four_checkboxes_and_dotted_lines()
    {
        var svg = Svg(Kor2Form.Layout(Child(), new(null, null, null, null), Employer));

        Assert.Equal(4, Regex.Matches(svg, "<rect ").Count);
        Assert.True(Regex.Matches(svg, "class=\"dots\"").Count > 50);
    }

    private static InsuredPersonLine Line(int i) => new($"{i:D13}", $"นายคนที่ {i}", "ไทย", D("1990-02-16"), D("2024-10-18"), "พนักงาน",
        i % 2 == 0 ? 363 : 15_000, i == 1 ? D("2025-01-07") : null);

    [Fact] // 18 แถวต่อหน้า เลขลำดับต่อเนื่อง หัวและหมายเหตุซ้ำทุกหน้า ตารางจบที่แถวสุดท้าย
    public void The_register_has_18_rows_a_page_with_the_heading_and_note_on_every_page()
    {
        var pages = InsuredPersonsForm.Layout(Enumerable.Range(1, 40).Select(Line).ToList(), Employer);

        Assert.Equal(3, pages.Count);
        Assert.All(pages, p => Assert.Equal(Paper.A4Landscape, p.Paper));
        var svgs = pages.Select(Svg).ToList();
        Assert.All(svgs, s =>
        {
            Assert.Equal("สปส.6-07", At(s, 260.6, 21.4));
            Assert.Equal("หมายเหตุ", At(s, 15.3, 195.3));
            Assert.Equal("เลขประจำตัวประชาชน", At(s, 49.17, 51));
        });
        Assert.Equal(("1", "18"), (At(svgs[0], 27.5, 57.6), At(svgs[0], 27.5, 57.6 + 17 * 7.179)));
        Assert.Equal(("19", "37", "40"), (At(svgs[1], 27.5, 57.6), At(svgs[2], 27.5, 57.6), At(svgs[2], 27.5, 57.6 + 3 * 7.179)));
        Assert.Equal(2 + 18, HorizontalRules(svgs[0]));
        Assert.Equal(2 + 4, HorizontalRules(svgs[2]));
    }

    private static int HorizontalRules(string svg) => Regex.Matches(svg, "<line x1=\"14.99\" y1=\"([\\d.]+)\" x2=\"279.72\" y2=\"\\1\"").Count;

    [Fact] // ค่าจ้างทศนิยม 2 คั่นหลัก, วันที่ d/M/yyyy พ.ศ. ชิดขวา, วันสิ้นสุดการจ้าง = วันลาออก
    public void A_register_row_prints_the_wage_with_two_decimals_and_buddhist_dates()
    {
        var svg = Svg(InsuredPersonsForm.Layout([Line(1), Line(2)], Employer)[0]);

        Assert.Equal(("0000000000001", "นายคนที่ 1", "ไทย"), (At(svg, 30.3, 57.6), At(svg, 70.3, 57.6), At(svg, 125.3, 57.6)));
        Assert.Equal(("16/2/2533", "18/10/2567", "พนักงาน"), (At(svg, 168.5, 57.6), At(svg, 192.75, 57.6), At(svg, 195.1, 57.6)));
        Assert.Equal(("15,000.00", "7/1/2568"), (At(svg, 248.4, 57.6), At(svg, 278.6, 57.6)));
        Assert.Equal("363.00", At(svg, 248.4, 57.6 + 7.179));
        Assert.Null(At(svg, 278.6, 57.6 + 7.179));
    }

    [Fact] // เลขที่บัญชี 10 หลัก / ลำดับที่สาขา 6 หลัก ในกล่องทีละตัว
    public void The_employer_account_and_branch_print_one_digit_per_box()
    {
        var svg = Svg(InsuredPersonsForm.Layout([Line(1)], Employer)[0]);

        Assert.Equal(16, Regex.Matches(svg, "<rect ").Count);
        Assert.Equal(("1", "0", "2"), (At(svg, 214.24, 29.3), At(svg, 220.2, 29.3), At(svg, 275.26, 29.3)));
        Assert.Equal(("0", "1"), (At(svg, 230.24, 38.2), At(svg, 260.23, 38.2)));
        Assert.Equal("บริษัท ล็อคเต้ จำกัด", At(svg, 54.3, 28.3));
    }

    [Fact] // D-6: ไม่มีข้อมูล → หน้าเดียวพร้อมหัว และ "ไม่มีข้อมูล"
    public void An_empty_register_prints_one_page_saying_there_is_no_data()
    {
        var pages = InsuredPersonsForm.Layout([], Employer);

        Assert.Contains("ไม่มีข้อมูล", Svg(Assert.Single(pages)));
    }

    [Fact] // ไม่มีหัว/ท้ายแบบกลาง ไม่มีขอบกระดาษ — หน้าละ SVG เท่ากระดาษ
    public void A_form_document_has_no_margins_and_one_svg_per_page()
    {
        var html = FormPageHtml.Compose("ทะเบียนผู้ประกันตน", InsuredPersonsForm.Layout(Enumerable.Range(1, 20).Select(Line).ToList(), Employer));

        Assert.Contains("@page{size:A4 landscape;margin:0}", html);
        Assert.Equal(2, Regex.Matches(html, "<svg class=\"page\"").Count);
        Assert.DoesNotContain("@top-center", html);
        Assert.Contains("THSarabunNew", html);
    }
}
