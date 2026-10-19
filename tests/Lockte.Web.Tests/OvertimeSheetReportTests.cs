using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Lockte.Hr;
using Lockte.Web.Reports;
using Lockte.Web.Reports.Hr;

namespace Lockte.Web.Tests;

/// <summary>RPT-16 ใบขออนุมัติทำงานล่วงเวลา (#60, รายงาน OT, print-samples.md): the HTML, without Chromium, and the PDF.</summary>
[Collection(ChromiumCollection.Name)]
public sealed class OvertimeSheetReportTests(ChromiumFixture chromium)
{
    private static OvertimeSheet Sheet(DateOnly date, params OvertimeSheetLine[] lines)
        => new(date, "ประกอบ", new TimeOnly(17, 20), new TimeOnly(20, 20), WorkTypes.Overtime, lines);

    private static readonly OvertimeSheetLine[] Lines =
    [
        new("A001", "นาย ทดสอบ หนึ่ง", "D11 ฉีดก้ามเบรก"),
        new("A002", "นางสาว ทดสอบ สอง", null),
    ];

    /// <summary>The body as text: HtmlRenderer writes the Thai of the data as character references.</summary>
    private static async Task<string> HtmlAsync(OvertimeSheet sheet)
        => WebUtility.HtmlDecode(await ReportBody.RenderAsync<OvertimeSheetView>(new() { [nameof(OvertimeSheetView.Sheet)] = sheet }));

    [Theory] // หัว: "เรื่อง ขออนุมัติทำงานล่วงเวลา  แผนก …  วัน<ชื่อวัน>ที่ <วันที่>" — the legacy picks the day name from DATE
    [InlineData(2026, 10, 18, "วันอาทิตย์ที่ 18/10/2569")]
    [InlineData(2026, 10, 19, "วันจันทร์ที่ 19/10/2569")]
    [InlineData(2026, 10, 24, "วันเสาร์ที่ 24/10/2569")]
    public async Task The_header_has_the_department_and_the_day_name_with_the_date(int y, int m, int d, string day)
    {
        var html = await HtmlAsync(Sheet(new DateOnly(y, m, d), Lines));

        Assert.Contains("แผนก ประกอบ", html);
        Assert.Contains(day, html);
    }

    [Fact] // ตารางเส้นเต็ม: ที่, รหัส, ชื่อ-นามสกุล, เริ่มงาน, เลิกงาน, ลายเซ็นต์, หมายเหตุ · ท้าย: ห้ามใส่รองเท้าแตะ… ผู้จัดการ
    public async Task The_table_has_the_legacy_columns_a_row_per_employee_and_the_manager_line()
    {
        var html = await HtmlAsync(Sheet(new DateOnly(2026, 10, 18), Lines));

        var headers = new[] { "ที่", "รหัส", "ชื่อ-นามสกุล", "เริ่มงาน", "เลิกงาน", "ลายเซ็นต์", "หมายเหตุ" };
        var at = headers.Select(h => html.IndexOf($">{h}</th>", StringComparison.Ordinal)).ToList();
        Assert.All(at, i => Assert.True(i >= 0));
        Assert.Equal(at.Order(), at);
        Assert.Contains(">A001<", html);
        Assert.Contains(">นาย ทดสอบ หนึ่ง<", html);
        Assert.Contains(">D11 ฉีดก้ามเบรก<", html);
        Assert.Equal(2, CountOf(html, ">17:20<"));
        Assert.Equal(2, CountOf(html, ">20:20<"));
        Assert.True(html.IndexOf(">1<", StringComparison.Ordinal) < html.IndexOf(">2<", StringComparison.Ordinal));
        Assert.Contains("ห้ามใส่รองเท้าแตะมาทำงาน ผู้ใดใส่มาไม่ให้ทำงานล่วงเวลา", html);
        Assert.Contains("ผู้จัดการ", html);
    }

    [Fact] // D-6: ไม่มีข้อมูล → "ไม่มีข้อมูล" แทนแถวเดียวเลข 1
    public async Task An_empty_sheet_says_there_is_no_data()
    {
        var html = await HtmlAsync(Sheet(new DateOnly(2026, 10, 18)));

        Assert.Contains("ไม่มีข้อมูล", html);
        Assert.DoesNotContain("<tbody>", html);
    }

    [Fact] // A5 ตั้ง (#40)
    public async Task The_sheet_prints_on_A5_portrait()
    {
        var body = await ReportBody.RenderAsync<OvertimeSheetView>(new() { [nameof(OvertimeSheetView.Sheet)] = Sheet(new DateOnly(2026, 10, 18), Lines) });
        var html = ReportHtml.Compose(new ReportDocument(OvertimeSheetView.Title, OvertimeSheetView.Paper), new PrintStamp(DateTimeOffset.Now, "tester"), body);

        var pdf = Encoding.Latin1.GetString(await chromium.Renderer.RenderAsync(html, OvertimeSheetView.Paper));

        Assert.Equal(Paper.A5Portrait, OvertimeSheetView.Paper);
        var box = Regex.Match(pdf, @"/MediaBox \[0 0 ([\d.]+) ([\d.]+)\]");
        Assert.Equal(148 * 72 / 25.4, double.Parse(box.Groups[1].Value, CultureInfo.InvariantCulture), 0);
        Assert.Equal(210 * 72 / 25.4, double.Parse(box.Groups[2].Value, CultureInfo.InvariantCulture), 0);
    }

    private static int CountOf(string text, string part)
    {
        var n = 0;
        for (var i = text.IndexOf(part, StringComparison.Ordinal); i >= 0; i = text.IndexOf(part, i + 1, StringComparison.Ordinal)) n++;
        return n;
    }
}
