using System.Globalization;
using System.Text.RegularExpressions;
using Lockte.Hr;
using Lockte.Web.Reports;
using Lockte.Web.Reports.Forms;
using Lockte.Web.Reports.Hr;
using Microsoft.AspNetCore.Components;

namespace Lockte.Web.Tests;

/// <summary>
/// UAT-29 (#409) แบบฟอร์มเอกสารบุคคล without a database: the phone and road formats, the signers laid over the server's settings, and where the
/// letters put what the third round of corrections asked for. Names and numbers here are made up.
/// </summary>
public sealed class Uat29FormTests
{
    private static readonly DateOnly Today = new(2026, 10, 15);

    private static readonly CertificateData Person = new("นางสาวสมมุติ  ทดสอบ", "28  เดือน  กันยายน  พ.ศ.  2525", 44, "พนักงาน", "ตกแต่ง",
        "22  เดือน  กรกฎาคม  พ.ศ.  2562", "7  ปี  3  เดือน", 9672, "นายสมมุติ  โรงงาน", "15  เดือน  ตุลาคม  พ.ศ.  2569");

    private static readonly SeveranceData Severance = new(Person, 575, 400, 230000m, "ป่วยบ่อย", SeveranceScale.Legacy);

    private static readonly ResignationLetterData Letter = new("A005", "นางสาวสมมุติ  ทดสอบ", "ประกอบ", "D", new DateOnly(1992, 10, 26),
        new DateOnly(2026, 10, 2), "กลับบ้าน", "นางสาวสมมุติ  หัวหน้า", "นายสมมุติ ผู้จัดการฝ่าย", "นายสมมุติ  โรงงาน");

    private static Task<string> Html<T>(Dictionary<string, object?> parameters) where T : IComponent => ReportBody.RenderAsync<T>(parameters);

    private static string Text(string html) => System.Net.WebUtility.HtmlDecode(Regex.Replace(html, "<[^>]+>", "\n"));

    /// <summary>The styles of the texts that are exactly <paramref name="text"/> (the markup HTML-encodes Thai).</summary>
    private static List<string> StylesOf(string html, string text)
        => Regex.Matches(html, @"<div class=""t"" style=""([^""]*)"">([^<]*)</div>")
            .Where(m => System.Net.WebUtility.HtmlDecode(m.Groups[2].Value) == text).Select(m => m.Groups[1].Value).ToList();

    private static string StyleOf(string html, string text) => StylesOf(html, text).FirstOrDefault() ?? "";

    /// <summary>The <c>top</c> (mm) FormText gives a 16 pt text whose baseline is at <paramref name="baseline"/>.</summary>
    private static string TopOf(double baseline) => FormHtml.Mm(baseline - FormHtml.BaselineEm * 16 * FormHtml.MillimetresPerPoint);

    [Theory] // 02 + 7 digits, then a second line / extension; mobile 3-3-4; typed-over numbers stay as typed
    [InlineData("024204554", "02-4204554")]
    [InlineData("02-4204554", "02-4204554")]
    [InlineData("0223360023", "02-2336002-3")]
    [InlineData("022336002-3", "02-2336002-3")]
    [InlineData("02 233 6002", "02-2336002")]
    [InlineData("0812345678", "081-234-5678")]
    [InlineData("034123456", "03-4123456")]
    [InlineData("02-4204554, 02-2336002", "02-4204554, 02-2336002")]
    [InlineData("4204554", "4204554")]
    [InlineData("02-4204554 ต่อ 12", "02-4204554 ต่อ 12")]
    [InlineData(" ", "")]
    [InlineData(null, "")]
    public void Phones_get_their_dashes(string? typed, string printed) => Assert.Equal(printed, FormFormat.Phone(typed));

    [Theory] // the form prints the word ถนน itself, so the setting may carry it or not
    [InlineData("ตัวอย่างสาย 5", "ตัวอย่างสาย 5")]
    [InlineData("ถนนตัวอย่างสาย 5", "ตัวอย่างสาย 5")]
    [InlineData("ถนน ตัวอย่างสาย 5", "ตัวอย่างสาย 5")]
    [InlineData("ถ.สมมุติ", "สมมุติ")]
    [InlineData("ถนน", "ถนน")]
    [InlineData("", "")]
    public void The_road_does_not_repeat_the_word_ถนน(string typed, string printed) => Assert.Equal(printed, FormFormat.Road(typed));

    [Fact] // UAT-29: the signers HR saved win over the server's settings; blank ones keep the server's
    public void Saved_signers_are_laid_over_the_server_settings()
    {
        var server = new EmployerOptions
        {
            Name = "บริษัทตัวอย่าง", ManagerTitle = "นาย", ManagerFirstName = "เซิร์ฟเวอร์", FactoryManagerName = "ผจก.เซิร์ฟเวอร์", PersonnelOfficerName = "บุคคลเซิร์ฟเวอร์",
            Workplace = new() { Road = "ถนนตัวอย่าง" },
        };

        var saved = server.WithSigners(new FormSignersDto("นาง", "สมมุติ", " ", null, "บุคคลใหม่", null));

        Assert.Equal(("นาง", "สมมุติ", ""), (saved.ManagerTitle, saved.ManagerFirstName, saved.ManagerLastName));
        Assert.Equal(("ผจก.เซิร์ฟเวอร์", "บุคคลใหม่"), (saved.FactoryManagerName, saved.PersonnelOfficerName));
        Assert.Equal(("บริษัทตัวอย่าง", "ถนนตัวอย่าง"), (saved.Name, saved.Workplace.Road)); // the rest is the server's
        Assert.Equal("เซิร์ฟเวอร์", server.ManagerFirstName); // the server's own object is not touched
    }

    [Fact] // UAT-29 ใบลาออก: the top part (วันที่ … body) 27 mm down, the applicant's signature 12 mm; the typed values centred or inset in their lines
    public async Task The_resignation_letter_moves_its_top_down_and_keeps_values_off_the_left_end_of_their_lines()
    {
        var html = await Html<ResignationLetterView>(new() { ["Letter"] = Letter, ["Today"] = Today });

        Assert.Contains("top:27mm;width:0;height:0", html);
        Assert.Contains("top:12mm;width:0;height:0", html);
        var months = StylesOf(html, "ตุลาคม");
        Assert.Equal(3, months.Count);
        Assert.All(months, m => Assert.Contains("translateX(-50%)", m)); // every month is centred on its line
        Assert.Contains(months, m => m.Contains("left:170.3mm")); // วันที่
        Assert.Contains(months, m => m.Contains("left:86.6mm")); // เริ่มทำงานตั้งแต่วันที่
        Assert.Contains(months, m => m.Contains("left:94.6mm")); // ตั้งแต่วันที่
        Assert.Contains("left:59.4mm", StyleOf(html, "นางสาวสมมุติ  ทดสอบ")); // the name starts 2.7 mm after its line does (56.7)
        Assert.Contains("left:80.8mm", StyleOf(html, "กลับบ้าน")); // the reason, 2 mm after its line
        // the dotted line under ตั้งแต่วันที่ … is there for each of day, month and year
        Assert.Equal(3, Regex.Matches(html, @"<div class=""dots"" style=""left:(60\.7|80\.7|115\.4)mm;[^""]*top:103\.6mm""").Count);
    }

    [Fact] // UAT-29 ใบลาออก: the manager under ผู้จัดการฝ่าย is the แผนก's, printed in brackets with the factory manager's beside it
    public async Task The_resignation_letter_prints_the_department_manager_and_the_factory_manager()
    {
        var text = Text(await Html<ResignationLetterView>(new() { ["Letter"] = Letter, ["Today"] = Today }));

        Assert.Contains("(นายสมมุติ ผู้จัดการฝ่าย)", text);
        Assert.Contains("(นายสมมุติ  โรงงาน)", text);
    }

    [Fact] // UAT-29 หนังสือรับรอง: ขอแสดงความนับถือ (base line 192 → 199) and the signer under it (217 → 224) are 7 mm lower
    public async Task The_certificates_close_a_little_lower()
    {
        foreach (var salary in new[] { false, true })
        {
            var html = await Html<CertificateView>(new() { ["Person"] = Person, ["Salary"] = salary });

            Assert.Contains($"top:{TopOf(199.0)}mm", StyleOf(html, "ขอแสดงความนับถือ"));
            Assert.Contains($"top:{TopOf(224.0)}mm", StyleOf(html, "(นายสมมุติ  โรงงาน)"));
            Assert.Contains($"top:{TopOf(231.0)}mm", StyleOf(html, "ผู้จัดการโรงงาน"));
        }
    }

    [Fact] // UAT-29 เงินเดือน: บาท follows the amount after a space, and the words after it
    public async Task The_salary_certificate_leaves_a_space_before_baht()
    {
        var html = await Html<CertificateView>(new() { ["Person"] = Person, ["Salary"] = true });

        Assert.Contains("left:87.6mm", StyleOf(html, "บาท")); // the amount ends at 85.1
        Assert.Contains("left:96mm", StyleOf(html, "(เก้าพันหกร้อยเจ็ดสิบสองบาทถ้วน)"));
    }

    [Fact] // UAT-29 เกษียณ/ปลดออก: วันที่เกษียณอายุงาน as typed (วันที่ปลดออก on the dismissal); none, no line; บาท set apart from the amounts
    public async Task The_severance_forms_print_the_retirement_date_and_space_baht()
    {
        var retire = await Html<SeveranceView>(new() { ["Data"] = Severance, ["Today"] = Today, ["RetireDate"] = new DateOnly(2026, 11, 30) });
        var dismiss = await Html<SeveranceView>(new() { ["Data"] = Severance, ["Dismissal"] = true, ["Today"] = Today, ["RetireDate"] = new DateOnly(2026, 11, 30) });
        var none = await Html<SeveranceView>(new() { ["Data"] = Severance, ["Today"] = Today });

        Assert.Contains("วันที่เกษียณอายุงาน", Text(retire));
        Assert.Contains("30 พฤศจิกายน 2569", Text(retire));
        Assert.Contains("วันที่ปลดออก", Text(dismiss));
        Assert.Contains("30 พฤศจิกายน 2569", Text(dismiss));
        Assert.DoesNotContain("วันที่เกษียณอายุงาน", Text(none));
        Assert.DoesNotContain("30 พฤศจิกายน", Text(none));
        // the amounts end at 79.2 / 163.0 and each บาท starts 2.4 / 2.2 mm after it
        Assert.Contains(StylesOf(retire, "บาท"), s => s.Contains("left:81.6mm"));
        Assert.Contains(StylesOf(retire, "บาท"), s => s.Contains("left:165.2mm"));
        Assert.Contains("left:163mm", StyleOf(retire, "230,000.00"));
    }

    [Fact] // the extra line moves the rest down but the 7-line table still ends on the page (A4 = 297 mm)
    public async Task The_scale_table_stays_on_the_page_with_the_retirement_date()
    {
        var html = await Html<SeveranceView>(new() { ["Data"] = Severance, ["Today"] = Today, ["RetireDate"] = new DateOnly(2026, 11, 30) });
        var tops = Regex.Matches(html, @"top:([\d.]+)mm").Select(m => double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)).ToList();

        Assert.True(tops.Max() < 290);
    }
}
