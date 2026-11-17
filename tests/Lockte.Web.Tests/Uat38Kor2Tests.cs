using System.Text.RegularExpressions;
using Lockte.Hr;
using Lockte.Web.Reports;
using Lockte.Web.Reports.Hr;

namespace Lockte.Web.Tests;

/// <summary>
/// UAT-38 (#431) แบบ คร.2 without a database: the addresses HR saves on ผู้ลงนามและที่อยู่บนแบบฟอร์ม laid over the server's, where the
/// factory's เลขที่ / หมู่ที่ / ถนน print, a phone with room on its line, and the manager's name in brackets. Names and numbers are made up.
/// </summary>
public sealed class Uat38Kor2Tests
{
    private static readonly Kor2Employee Child = new(1, "A001", "นายเด็ก  ทดสอบ", new(2009, 3, 31), "0000000000001",
        "10 หมู่ที่ 1 ต.ตัวอย่าง", new(2026, 1, 16), "พนักงาน", "ประกอบ", 363, 187);

    private static readonly EmployerOptions Server = new()
    {
        ManagerFirstName = "เซิร์ฟเวอร์",
        Office = new() { HouseNo = "1", Soi = "ซอยเซิร์ฟเวอร์", Province = "กรุงเทพฯ", Phone = "021111111" },
        Workplace = new() { HouseNo = "2", Soi = "ซอยโรงงาน", Road = "ถนนเซิร์ฟเวอร์", Province = "จังหวัดเซิร์ฟเวอร์" },
    };

    private static string Svg(EmployerOptions employer) => Kor2Form.Layout(Child, new(null, null, null, null), employer).ToSvg();

    private static Match TextAt(string svg, double x, double baseline)
        => Regex.Match(svg, $"<text x=\"{FormPage.N(x)}\" y=\"{FormPage.N(baseline)}\"([^>]*)>([^<]*)</text>");

    private static string? At(string svg, double x, double baseline)
        => TextAt(svg, x, baseline) is { Success: true } m ? System.Net.WebUtility.HtmlDecode(m.Groups[2].Value) : null;

    [Fact] // an address with anything saved is HR's whole (a field left blank prints blank); one never saved is the server's
    public void A_saved_address_replaces_the_servers_whole()
    {
        var saved = Server.WithSigners(new FormSignersDto(null, null, null, null, null, null)
        {
            Workplace = new("47", "5", null, "ตัวอย่างสาย 5", "ตำบลตัวอย่าง", "อำเภอตัวอย่าง", "จังหวัดตัวอย่าง", "020000000"),
        });

        Assert.Equal(("47", "5", "", "ตัวอย่างสาย 5", "020000000"),
            (saved.Workplace.HouseNo, saved.Workplace.Moo, saved.Workplace.Soi, saved.Workplace.Road, saved.Workplace.Phone));
        Assert.Same(Server.Office, saved.Office);
        Assert.Equal("ซอยโรงงาน", Server.Workplace.Soi); // the server's own settings are not touched
    }

    [Fact] // the factory's เลขที่ / หมู่ที่ / ถนน on the สถานที่ทำงาน line, each in its own field
    public void The_workplace_prints_house_number_moo_and_road_in_their_fields()
    {
        var employer = Server.WithSigners(new FormSignersDto(null, null, null, null, null, null)
        {
            Workplace = new("47", "5", null, "ถนนตัวอย่างสาย 5", "ตำบลตัวอย่าง", "อำเภอตัวอย่าง", "จังหวัดตัวอย่าง", null),
        });
        var svg = Svg(employer);

        Assert.Equal(("47", "5", "ตัวอย่างสาย 5"), (At(svg, 61.9, 189), At(svg, 84.6, 189), At(svg, 144.9, 189)));
        Assert.Null(At(svg, 108.0, 189)); // no ซอย saved → blank, not the server's
        Assert.Equal(("ตำบลตัวอย่าง", "อำเภอตัวอย่าง", "จังหวัดตัวอย่าง"), (At(svg, 50.8, 196), At(svg, 88.8, 196), At(svg, 124.7, 196)));
    }

    [Fact] // the office phone starts just after its label and has the whole line up to จำนวนลูกจ้างทั้งหมด, so 02-xxxxxxx-x is not squeezed
    public void The_office_phone_has_its_whole_line()
    {
        var employer = Server.WithSigners(new FormSignersDto(null, null, null, null, null, null)
        {
            Office = new("9", null, null, null, null, null, "กรุงเทพฯ", "0200000001"),
        });
        var phone = TextAt(Svg(employer), 43.6, 68);

        Assert.Equal("02-0000000-1", phone.Groups[2].Value);
        Assert.Contains("data-fit=\"28.5\"", phone.Groups[1].Value);
    }

    [Fact] // ลงชื่อ … ผู้จัดการสถานประกอบกิจการ with the saved manager's name in brackets under the line
    public void The_manager_saved_on_the_settings_page_prints_in_brackets_under_the_signature()
    {
        var employer = Server.WithSigners(new FormSignersDto("นาย", "ตัวอย่าง", "ผู้จัดการ", null, null, null));

        Assert.Equal("(นายตัวอย่าง  ผู้จัดการ)", At(Svg(employer), 113.5, 258));
    }
}
