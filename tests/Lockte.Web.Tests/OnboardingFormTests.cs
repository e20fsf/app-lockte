using System.Text.RegularExpressions;
using Lockte.Hr;
using Lockte.SharedKernel;
using Lockte.Web.Reports;
using Lockte.Web.Reports.Hr;

namespace Lockte.Web.Tests;

/// <summary>
/// The ระดับ A forms of #66 (RPT-56, RPT-22) and the ระดับ B lists (RPT-20, RPT-21) as HTML, without Chromium or a
/// database: which values print, where, and in which format. How close a form lies on the legacy print is checked by
/// overlaying the samples (#41: วางซ้อนตัวอย่างเดิมคลาด ≤ ~2 มม. + HR เซ็นรับ), not here.
/// </summary>
public sealed class OnboardingFormTests
{
    private static readonly DateOnly Today = new(2026, 10, 15);

    private static readonly OnboardingEmployee Monthly = new()
    {
        Id = 7, Code = "A340", Name = "นายชัยณรงค์ ครองสัตย์", BirthDate = new(1983, 1, 1), NationalId = "1600400006742", IdCardPlace = "หนองบัว",
        IdCardIssued = new(2008, 3, 3), RegisteredAddress = "10 หมู่ที่ 1", StartDate = new(2010, 1, 16), EmployeeType = EmployeeTypes.Monthly,
        Salary = 12340, DailyWage = 411.33, Position = "รองหัวหน้า", DepartmentName = "ประกอบ", SectionName = "ผลิต", ShiftCode = "D",
        SupervisorName = "นางสาวอัมพร เจริญสุข", ManagerName = "นายสมบัติ ตัวอย่างดี", FactoryManagerName = "นายโรงงาน ใหญ่",
    };

    private static readonly OnboardingEmployee Daily = Monthly with { EmployeeType = EmployeeTypes.Daily, Salary = 0, DailyWage = 372, StartDate = new(2026, 8, 13) };

    /// <summary>The texts of an SVG page with their x, baseline and size (font-size in mm).</summary>
    private static List<(double X, double Y, string Text)> Texts(FormPage page)
        => Regex.Matches(page.ToSvg(), "<text x=\"([\\d.]+)\" y=\"([\\d.]+)\"[^>]*>([^<]*)</text>")
            .Select(m => (double.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture),
                double.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture), System.Net.WebUtility.HtmlDecode(m.Groups[3].Value)))
            .ToList();

    private static string TextAt(FormPage page, double x, double y)
        => Assert.Single(Texts(page), t => Math.Abs(t.X - x) < 0.01 && Math.Abs(t.Y - y) < 0.01).Text;

    [Fact] // RPT-56 แบบฟอร์มรับพนักงาน: วันที่ Long Date, เงินเดือน General, ค่าแรง Fixed 2, ครบ 90 วัน = START + 89, ผู้ลงนามจากแผนก
    public void Hiring_form_prints_the_employee_the_typed_dates_and_the_signers()
    {
        var page = OnboardingForms.HiringForm(Monthly, new HiringFormInput(new(2026, 8, 11), null), "PC-PE001-5/2547 ฉบับที่2");
        var texts = Texts(page).Select(t => t.Text).ToList();

        Assert.Equal(Paper.A4Portrait, page.Paper);
        Assert.Equal("PC-PE001-5/2547 ฉบับที่2", TextAt(page, 145.5, 41.7));
        Assert.Equal("นายชัยณรงค์ ครองสัตย์", TextAt(page, 31.1, 56.6));
        Assert.Equal("11 สิงหาคม 2569", TextAt(page, 41.9, 79.6));
        Assert.Equal("12340", TextAt(page, 51.8, 90.6));
        Assert.Equal("411.33", TextAt(page, 103.4, 90.6));
        Assert.Equal(("16 มกราคม 2553", "15 เมษายน 2553"), (TextAt(page, 75.5, 101.6), TextAt(page, 126.9, 101.6)));
        Assert.Contains("(นางสาวอัมพร เจริญสุข)", texts); // ผู้สัมภาษณ์ = หัวหน้ากะ
        Assert.Contains("(นายโรงงาน ใหญ่)", texts); // ผู้อนุมัติ = ผู้จัดการโรงงาน
        Assert.DoesNotContain(Texts(page), t => Math.Abs(t.X - 118.0) < 0.01); // วันที่สัมภาษณ์ not typed: blank
    }

    [Fact] // RPT-56 สัญญาจ้าง: only the employee's own rate paragraph is filled, with its words (ThaiNumber) and START − START + 89
    public void Contract_fills_the_paragraph_of_the_employee_type_and_keeps_the_other_blank()
    {
        var monthly = OnboardingForms.Contract(Monthly, true, Today, "PD-PE001-9/2547ฉบับที่1");
        var daily = OnboardingForms.Contract(Daily, false, Today, "PC-PE001-9/2547ฉบับที่1");

        Assert.Equal("12340", TextAt(monthly, 54.0, 87.9));
        Assert.Equal("(หนึ่งหมื่นสองพันสามร้อยสี่สิบบาทถ้วน)", TextAt(monthly, 69.5, 87.9));
        Assert.Equal(("16/1/2553", "15/4/2553"), (TextAt(monthly, 107.1, 107.9), TextAt(monthly, 147.6, 107.9)));
        Assert.Equal("(......................................................)", TextAt(monthly, 75.4, 114.8));
        Assert.DoesNotContain(Texts(monthly), t => Math.Abs(t.X - 58.85) < 0.01); // no ค่าแรง/วัน on a monthly contract

        Assert.Equal("372", TextAt(daily, 58.85, 114.8));
        Assert.Equal("(สามร้อยเจ็ดสิบสองบาทถ้วน)", TextAt(daily, 75.4, 114.8));
        Assert.Equal(("13/8/2569", "10/11/2569"), (TextAt(daily, 107.1, 134.8), TextAt(daily, 147.6, 134.8)));
        Assert.Equal("(......................................................)", TextAt(daily, 70.5, 87.9));

        Assert.Equal("15 ตุลาคม 2569", TextAt(daily, 157.0, 31.9)); // วันที่ = Date()
        Assert.Equal("44", TextAt(daily, 118.5, 45.9)); // อายุ (Date() − BORN) / 365
        Assert.Equal("3/3/2551", TextAt(daily, 142.6, 59.9)); // วันที่ออกบัตร Short Date
        Assert.Equal("PC-PE001-9/2547ฉบับที่1", TextAt(daily, 147.7, 18.5));
    }

    [Fact] // BR-071 คะแนนหักรวม (2) — the examples of business-rules.md
    public void Total_deduction_follows_the_points_of_the_evaluation_form()
    {
        static ProbationStatistics Stats(double personal = 0, double sickWithout = 0, double accident = 0, int late = 0, double absent = 0,
            int verbal = 0, int written = 0) => new(personal, 0, sickWithout, 0, accident, late, absent, verbal, written);

        Assert.Equal(-7, Stats(personal: 2, late: 3).TotalDeduction);
        Assert.Equal(-15, Stats(sickWithout: 1, absent: 1, written: 1).TotalDeduction);
        Assert.Equal(0, Stats(accident: 5).TotalDeduction);
    }

    [Fact] // RPT-22 ใบประเมินทดลองงาน: จำนวน × ตัวคูณ per row, คะแนนหักรวม
    public void Evaluation_prints_counts_deductions_and_the_total()
    {
        var input = new ProbationApprovalInput("ผลิต", new(2026, 9, 21), new(2026, 9, 14));
        var known = OnboardingForms.Evaluation(Monthly, input, new ProbationStatistics(1.5, 0, 0, 0, 0, 0, 10, 1, 0), Today, "PC-PE001-6/2547  ฉบับที่2");

        Assert.Equal(Paper.A5Portrait, known.Paper);
        Assert.Equal(("1.5", "-3"), (TextAt(known, 68.75, 114.0), TextAt(known, 128.0, 114.0))); // ลากิจ × 2
        Assert.Equal(("0", "0"), (TextAt(known, 68.75, 120.0), TextAt(known, 128.0, 120.0))); // never "-0"
        Assert.Equal(("10", "-30"), (TextAt(known, 68.75, 150.0), TextAt(known, 128.0, 150.0))); // ขาดงาน × 3
        Assert.Equal(("1", "-5"), (TextAt(known, 68.75, 156.0), TextAt(known, 128.0, 156.0))); // ใบเตือนวาจา × 5
        Assert.Equal("-38", TextAt(known, 133.3, 168.0));
        Assert.Equal("PC-PE001-6/2547  ฉบับที่2", TextAt(known, 109.0, 16.8));
        Assert.Equal(("15 ตุลาคม 2569", "ผลิต"), (TextAt(known, 16.1, 26.0), TextAt(known, 111.9, 26.0)));
        Assert.Equal(("0", "0"), (TextAt(known, 68.75, 162.0), TextAt(known, 128.0, 162.0))); // ใบเตือนหนังสือ × 10
    }

    [Fact] // RPT-22 ใบนำส่ง / ใบขอบรรจุ: the dates HR typed, the document numbers
    public void Transmittal_and_request_print_the_typed_dates_and_their_numbers()
    {
        var input = new ProbationApprovalInput("ผลิต", new(2026, 9, 21), new(2026, 9, 14));
        var transmittal = OnboardingForms.Transmittal(Monthly, input, Today, "PC-PE001-8/2547ฉบับที่3");
        var request = OnboardingForms.Request(Monthly, "PC-PE001-7/2547 ฉบับที่2");

        Assert.Equal(("15 ตุลาคม 2569", "21 กันยายน 2569", "14 กันยายน 2569"),
            (TextAt(transmittal, 105.0, 33.6), TextAt(transmittal, 104.1, 74.8), TextAt(transmittal, 13.2, 95.8)));
        Assert.Equal(("A340", "16 มกราคม 2553"), (TextAt(transmittal, 120.7, 60.8), TextAt(transmittal, 92.2, 67.8)));
        Assert.Equal("PC-PE001-8/2547ฉบับที่3", TextAt(transmittal, 104.8, 22.9));
        Assert.Equal(("นายชัยณรงค์ ครองสัตย์", "ประกอบ"), (TextAt(request, 13.2, 43.0), TextAt(request, 92.3, 43.0)));
        Assert.Equal("PC-PE001-7/2547 ฉบับที่2", TextAt(request, 103.5, 32.0));
        Assert.Equal(3, Regex.Matches(request.ToSvg(), "<rect ").Count); // three boxes to tick
    }

    [Fact] // a form is printed without the แบบกลาง header/footer and with no page margin (#40 ระดับ A)
    public void Forms_compose_without_the_level_B_header_and_footer()
    {
        var html = FormPageHtml.Compose(OnboardingForms.ProbationTitle,
            [OnboardingForms.Request(Monthly, null), OnboardingForms.Request(Monthly, null)]);

        Assert.Contains("@page{size:A5 portrait;margin:0}", html);
        Assert.DoesNotContain("@top-center", html);
        Assert.DoesNotContain("counter(page)", html);
        Assert.Equal(2, Regex.Matches(html, "<svg class=\"page\"").Count);
    }

    [Theory]
    [InlineData(12340, "12340")]
    [InlineData(1.5, "1.5")]
    [InlineData(-30, "-30")]
    [InlineData(0, "0")]
    [InlineData(409.83, "409.83")]
    public void Numbers_print_as_Access_General_Number(double value, string text) => Assert.Equal(text, OnboardingForms.Number(value));

    [Fact] // DateDiff("m"): month boundaries, days ignored
    public void Months_between_counts_month_boundaries()
    {
        Assert.Equal(1, OnboardingDirectory.MonthsBetween(new(2026, 1, 31), new(2026, 2, 1)));
        Assert.Equal(0, OnboardingDirectory.MonthsBetween(new(2026, 1, 1), new(2026, 1, 31)));
        Assert.Equal(27, OnboardingDirectory.MonthsBetween(new(2024, 7, 20), new(2026, 10, 15)));
    }

    [Fact] // RPT-20: by รหัส, ประเภท code, ถึงกำหนด = START + 90, "รวมทั้งหมด n คน"
    public async Task On_probation_report_lists_by_code_with_the_due_date()
    {
        EmployeeDto E(string code, DateOnly start) => new()
        {
            Id = 1, Code = code, Name = "ชื่อ " + code, Nationality = "ไทย", StartDate = start, EmployeeType = EmployeeTypes.Daily, ShiftCode = "D",
            DepartmentName = "สโตร์", RowVersion = [],
        };
        var html = System.Net.WebUtility.HtmlDecode(await ReportBody.RenderAsync<OnProbationReportView>(new()
        {
            [nameof(OnProbationReportView.Employees)] = new List<EmployeeDto> { E("S060", new(2026, 8, 13)), E("A100", new(2026, 9, 1)) },
        }));

        Assert.True(html.IndexOf("A100", StringComparison.Ordinal) < html.IndexOf("S060", StringComparison.Ordinal));
        Assert.Contains("<td>11/11/2569</td>", html); // 13/8/2569 + 90
        Assert.Contains("<td>2</td>", html);
        Assert.Contains("รวมทั้งหมด 2 คน", html);
        Assert.Contains("ไม่มีข้อมูล", await ReportBody.RenderAsync<OnProbationReportView>(new() { [nameof(OnProbationReportView.Employees)] = new List<EmployeeDto>() }));
    }

    [Fact] // RPT-21 รายชื่อพนักงานแผนก…: title, Standard money, footer ชาย / หญิง / อายุเฉลี่ย
    public async Task Department_roster_report_prints_title_lines_and_footer()
    {
        var roster = new DepartmentRoster("รายชื่อพนักงานแผนกประกอบ  กะD",
            [new("B136", "นายบรรลัง", new(2000, 5, 25), "หัวหน้า", 26, 5, 54, 18665, 622.17), new("B532", "นางสอง", null, null, null, null, 45, 12928, 0)],
            1, 1);
        var html = System.Net.WebUtility.HtmlDecode(await ReportBody.RenderAsync<DepartmentRosterView>(new() { [nameof(DepartmentRosterView.Roster)] = roster }));

        Assert.Contains("รายชื่อพนักงานแผนกประกอบ  กะD", html);
        Assert.Contains("18,665.00", html);
        Assert.Contains("622.17", html);
        Assert.Contains("ชาย 1 คน", html);
        Assert.Contains("อายุเฉลี่ย 50", html); // (54 + 45) / 2 = 49.5 → 50
        Assert.Equal("50", EmployeeListText.Whole(49.5));
    }
}

/// <summary>The ระดับ A forms through Chromium (#52 pipeline): TH Sarabun New embedded, the legacy paper, one page per form.</summary>
[Collection(ChromiumCollection.Name)]
public sealed class OnboardingFormPdfTests(ChromiumFixture chromium)
{
    private static int Pages(byte[] pdf) => Regex.Matches(System.Text.Encoding.Latin1.GetString(pdf), @"/Type /Page\b").Count;

    [Fact] // RPT-22: ใบนำส่ง + ใบประเมิน + ใบขอบรรจุ = 3 A5 pages in one PDF; RPT-56 contract = 1 A4 page
    public async Task Probation_forms_print_three_A5_pages_and_a_contract_one_A4_page()
    {
        var e = new OnboardingEmployee { Id = 1, Code = "A001", Name = "นายทดสอบ ระบบ", StartDate = new(2026, 8, 1), DailyWage = 400 };
        var input = new ProbationApprovalInput("ผลิต", new(2026, 10, 29), new(2026, 10, 22));
        var today = new DateOnly(2026, 10, 15);
        FormPage[] probation =
        [
            OnboardingForms.Transmittal(e, input, today, "PC-PE001-8/2547ฉบับที่3"),
            OnboardingForms.Evaluation(e, input, new ProbationStatistics(0, 0, 0, 0, 0, 0, 0, 0, 0), today, null),
            OnboardingForms.Request(e, null),
        ];

        var pdf = await chromium.Renderer.RenderAsync(FormPageHtml.Compose(OnboardingForms.ProbationTitle, probation), Paper.A5Portrait);
        var contract = await chromium.Renderer.RenderAsync(
            FormPageHtml.Compose(OnboardingForms.ContractTitle, [OnboardingForms.Contract(e, false, today, null)]), Paper.A4Portrait);

        Assert.Equal(3, Pages(pdf));
        var box = Regex.Match(System.Text.Encoding.Latin1.GetString(pdf), @"/MediaBox \[0 0 ([\d.]+) ([\d.]+)\]");
        Assert.Equal(148 * 72 / 25.4, double.Parse(box.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), 0);
        Assert.Contains("THSarabunNew", System.Text.Encoding.Latin1.GetString(pdf));
        Assert.Equal(1, Pages(contract));
    }
}
