using Lockte.Hr;
using Lockte.Web.Reports;
using Lockte.Web.Reports.Hr;

namespace Lockte.Web.Tests;

/// <summary>
/// #392 (UAT-21): no cell of a printed report runs onto a second line — measured in Chromium, laid out as for the PDF — and
/// the roster prints เงินเดือน as a whole number. Checked on the two reports with the most columns and the longest text.
/// </summary>
[Collection(ChromiumCollection.Name)]
public sealed class ReportNoWrapTests(ChromiumFixture chromium)
{
    private static readonly PrintStamp Stamp = new(DateTimeOffset.Now, "tester");

    private const string Address = "99/9 หมู่ที่ 12 ตำบลบางพลีใหญ่ อำเภอบางพลี จังหวัดสมุทรปราการ 10540 ซอยวัดหนามแดง ถนนบางนา-ตราด กม. 20";

    private static async Task<string> Html<TReport>(string title, Paper paper, Dictionary<string, object?> parameters) where TReport : Microsoft.AspNetCore.Components.IComponent
        => ReportHtml.Compose(new ReportDocument(title, paper), Stamp, await ReportBody.RenderAsync<TReport>(parameters));

    [Fact] // the case of the ticket: ตำแหน่ง "พนักงานขับรถ" in the narrow ตำแหน่ง column (12 %) of the roster
    public async Task The_roster_keeps_every_cell_on_one_line_and_prints_the_salary_whole()
    {
        var lines = Enumerable.Range(1, 60).Select(i => new DepartmentRosterLine($"O{i:000}", "นางสาวเมตตาลักษณ์ อยู่สมบูรณ์พูนสุขเจริญ", new(2002, 12, 10),
            i % 2 == 0 ? "พนักงานขับรถยกสินค้าหนัก" : "พนักงานขับรถ", 23, 10, 47, 13285.49 + i, 593.51)).ToList();
        var html = await Html<DepartmentRosterView>("รายชื่อพนักงานทั้งหมด", Paper.A4Portrait, new() { [nameof(DepartmentRosterView.Roster)] = new DepartmentRoster("รายชื่อพนักงานทั้งหมด", lines, 0, 60) });

        Assert.Empty(await chromium.Renderer.WrappedCellsAsync(html, Paper.A4Portrait));
        Assert.DoesNotContain("13,286.49", html);
        Assert.Contains(">13,286<", html); // 13285.49 + 1 = 13286.49 → 13,286
        Assert.Contains(">593.51<", html);  // ค่าแรง/วัน keeps its satang
    }

    [Fact] // the widest report: 14 columns on A4 landscape, long addresses in two of them
    public async Task The_register_keeps_every_cell_on_one_line()
    {
        var lines = Enumerable.Range(1, 40).Select(i => new EmployeeRegisterLine($"P{i:000}", "นางสาวเมตตาลักษณ์ อยู่สมบูรณ์พูนสุขเจริญ", new(1980, 1, 2), "หญิง", "ไทย", Address, Address,
            "ปริญญาตรี", new(2002, 12, 10), "พนักงานขับรถยกสินค้าหนัก", "ฝ่ายผลิตและควบคุมคุณภาพ", 17020.5, null)).ToList();
        var html = await Html<EmployeeRegisterView>("ทะเบียนประวัติพนักงาน", Paper.A4Landscape, new() { [nameof(EmployeeRegisterView.Lines)] = lines });

        Assert.Empty(await chromium.Renderer.WrappedCellsAsync(html, Paper.A4Landscape));
    }

    [Fact] // the measuring itself: a cell allowed to wrap is reported, one with a <br> or nowrap is not
    public async Task The_check_finds_a_wrapped_cell()
    {
        const string body = "<table style=\"table-layout:fixed;width:100%\"><tr><td style=\"width:15mm;white-space:normal\">พนักงานขับรถยกสินค้าหนัก</td><td>สอง<br>บรรทัด</td></tr></table>";
        var html = ReportHtml.Compose(new ReportDocument("ทดสอบ", Paper.A4Portrait), Stamp, body);

        Assert.Equal(["พนักงานขับรถยกสินค้าหนัก"], await chromium.Renderer.WrappedCellsAsync(html, Paper.A4Portrait));
    }

    [Fact] // before / after PDFs of the roster for the ticket: only when LOCKTE_PDF_OUT names a folder
    public async Task Writes_the_roster_before_and_after_when_asked()
    {
        if (Environment.GetEnvironmentVariable("LOCKTE_PDF_OUT") is not { Length: > 0 } folder) return;
        var positions = new[] { "พนักงาน", "หัวหน้า", "พนักงานขับรถ", "ผู้ช่วยหัวหน้าแผนก" };
        var lines = Enumerable.Range(1, 45).Select(i => new DepartmentRosterLine($"K{i:000}", "นางสาวณิชาภา คุ้มด้วงสงค์", new(2018, 5, 21), positions[i % 4], 8, 6, 51,
            i % 3 == 0 ? 22564.32 : 0, 367 + 0.51)).ToList();
        var after = await Html<DepartmentRosterView>("รายชื่อพนักงานทั้งหมด", Paper.A4Portrait, new() { [nameof(DepartmentRosterView.Roster)] = new DepartmentRoster("รายชื่อพนักงานทั้งหมด", lines, 0, 45) });
        var before = after.Replace("white-space:nowrap}", "overflow-wrap:anywhere}").Replace("th{white-space:nowrap;", "th{").Replace("window.lockteFitToPage=", "window.unused=");
        Directory.CreateDirectory(folder);
        await File.WriteAllBytesAsync(Path.Combine(folder, "roster-after.pdf"), await chromium.Renderer.RenderAsync(after, Paper.A4Portrait));
        await File.WriteAllBytesAsync(Path.Combine(folder, "roster-before.pdf"), await chromium.Renderer.RenderAsync(before, Paper.A4Portrait));
    }
}
