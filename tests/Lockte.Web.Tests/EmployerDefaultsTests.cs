using System.Text.RegularExpressions;
using Lockte.Hr;
using Lockte.Web.Reports;
using Lockte.Web.Reports.Forms;
using Lockte.Web.Reports.Hr;
using Microsoft.Extensions.Configuration;

namespace Lockte.Web.Tests;

/// <summary>UAT7-1 (#462): the committed appsettings.json carries the company's signers, phone and road, and a config override still wins.</summary>
public sealed class EmployerDefaultsTests
{
    private static string AppSettingsPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "src", "Lockte.Web", "appsettings.json"))) dir = dir.Parent;
        return Path.Combine(dir!.FullName, "src", "Lockte.Web", "appsettings.json");
    }

    private static EmployerOptions Load(params (string Key, string Value)[] overrides)
    {
        var config = new ConfigurationBuilder().AddJsonFile(AppSettingsPath())
            .AddInMemoryCollection(overrides.Select(o => new KeyValuePair<string, string?>(o.Key, o.Value))).Build();
        return config.GetSection(EmployerOptions.Section).Get<EmployerOptions>()!;
    }

    private static string Text(string html) => System.Net.WebUtility.HtmlDecode(Regex.Replace(html, "<[^>]+>", "\n"));

    [Fact]
    public async Task The_resignation_letter_prints_the_personnel_officer_from_the_defaults()
    {
        var employer = Load();
        var letter = new ResignationLetterData("A005", "นางสาวสมมุติ  ทดสอบ", "ประกอบ", "D", new DateOnly(1992, 10, 26),
            new DateOnly(2026, 10, 2), "กลับบ้าน", "นางสาวสมมุติ  หัวหน้า", "นายสมมุติ ผู้จัดการฝ่าย", "นายสมมุติ  โรงงาน");
        var html = await ReportBody.RenderAsync<ResignationLetterView>(new()
        {
            ["Letter"] = letter, ["Today"] = new DateOnly(2026, 10, 15), [nameof(ResignationLetterView.PersonnelOfficerName)] = employer.PersonnelOfficerName,
        });

        Assert.Contains("(นางสาวณัฏฐกานต์ วงษ์ไว)", Text(html).Replace("\n", ""));
    }

    [Fact]
    public void Kor2_prints_the_phone_road_and_manager_from_the_defaults()
    {
        var employer = Load();
        var employee = new Kor2Employee(1, "A001", "นายเด็ก  ทดสอบ", new DateOnly(2009, 3, 31), "0000000000001", "10 หมู่ที่ 1 ต.ตัวอย่าง", new DateOnly(2026, 1, 16), "พนักงาน", "ประกอบ", 363, 187);
        var svg = Text(Kor2Form.Layout(employee, new(null, null, null, null), employer).ToSvg());

        Assert.Contains("02-2336002-3", svg);
        Assert.Contains("พุทธมณฑลสาย 5", svg);
        Assert.Contains("(นายนิวาส  โรจเมธินทร์)", svg);
    }

    [Fact]
    public void Config_overrides_the_defaults_and_a_saved_name_wins_over_both()
    {
        var employer = Load(("Employer:PersonnelOfficerName", "นางสมมุติ ใหม่"), ("Employer:Workplace:Road", "ตัวอย่างสาย 9"));
        Assert.Equal(("นางสมมุติ ใหม่", "ตัวอย่างสาย 9"), (employer.PersonnelOfficerName, employer.Workplace.Road));

        var saved = Load().WithSigners(new FormSignersDto(null, null, null, null, "บุคคลที่บันทึกในแอป", null));
        Assert.Equal("บุคคลที่บันทึกในแอป", saved.PersonnelOfficerName);
    }
}
