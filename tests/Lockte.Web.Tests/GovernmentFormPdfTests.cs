using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Lockte.Hr;
using Lockte.Web.Reports;
using Lockte.Web.Reports.Hr;

namespace Lockte.Web.Tests;

/// <summary>แบบ คร.2 and ทะเบียนผู้ประกันตน through Chromium (#64): page size, page count, TH Sarabun New embedded.</summary>
[Collection(ChromiumCollection.Name)]
public sealed class GovernmentFormPdfTests(ChromiumFixture chromium)
{
    private static string Text(byte[] pdf) => Encoding.Latin1.GetString(pdf);
    private static int Pages(byte[] pdf) => Regex.Matches(Text(pdf), @"/Type /Page\b").Count;

    private static (double Width, double Height) MediaBox(byte[] pdf)
    {
        var m = Regex.Match(Text(pdf), @"/MediaBox \[0 0 ([\d.]+) ([\d.]+)\]");
        return (double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture));
    }

    private Task<byte[]> PrintAsync(IReadOnlyList<FormPage> pages)
        => chromium.Renderer.RenderAsync(SvgFormHtml.Compose("test", pages), pages[0].Paper);

    private static readonly Kor2Employee Child = new(1, "A001", "นายเด็ก ทดสอบ", new(2009, 3, 31), "0000000000001", "10 หมู่ที่ 1",
        new(2026, 1, 16), "พนักงาน", "ประกอบ", 363, 187);

    [Fact]
    public async Task Kor2_is_one_A4_portrait_page_with_TH_Sarabun_New_embedded()
    {
        var pdf = await PrintAsync([Kor2Form.Layout(Child, new(null, "งานยาวมาก ".PadRight(400, 'ก'), null, null), new EmployerOptions())]);

        Assert.Equal(1, Pages(pdf));
        Assert.Equal((595, 842), (Math.Round(MediaBox(pdf).Width), Math.Round(MediaBox(pdf).Height)));
        Assert.Contains("THSarabunNew", Text(pdf));
        Assert.Contains("/FontFile2", Text(pdf));
    }

    [Fact] // 450 แถว = 25 หน้า A4 นอน
    public async Task The_register_prints_18_rows_a_page_on_A4_landscape()
    {
        var lines = Enumerable.Range(1, 450).Select(i => new InsuredPersonLine($"{i:D13}", $"นายคนที่ {i}", "ไทย", new(1990, 2, 16),
            new(2024, 10, 18), "พนักงาน", 363, null)).ToList();

        var pdf = await PrintAsync(InsuredPersonsForm.Layout(lines, new EmployerOptions()));

        Assert.Equal(25, Pages(pdf));
        Assert.Equal((842, 595), (Math.Round(MediaBox(pdf).Width), Math.Round(MediaBox(pdf).Height)));
    }

    /// <summary>
    /// ตรวจรับระดับ A (#40): with env <c>LOCKTE_FORM_OVERLAY_DIR</c> set, prints both forms from the data in that folder
    /// (<c>kor2.json</c>, <c>register.json</c> — the data of the legacy sample, kept outside git) to <c>kor2.pdf</c> and
    /// <c>register.pdf</c> there, for <c>tools/form-overlay/compare.py</c>. Does nothing otherwise.
    /// </summary>
    [Fact]
    public async Task Prints_the_overlay_check_pdfs_when_asked()
    {
        var dir = Environment.GetEnvironmentVariable("LOCKTE_FORM_OVERLAY_DIR");
        if (string.IsNullOrEmpty(dir)) return;
        var json = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        var kor2 = JsonSerializer.Deserialize<OverlayKor2>(await File.ReadAllTextAsync(Path.Combine(dir, "kor2.json")), json)!;
        await File.WriteAllBytesAsync(Path.Combine(dir, "kor2.pdf"), await PrintAsync([Kor2Form.Layout(kor2.Employee, kor2.Input, kor2.Employer)]));

        var register = JsonSerializer.Deserialize<OverlayRegister>(await File.ReadAllTextAsync(Path.Combine(dir, "register.json")), json)!;
        await File.WriteAllBytesAsync(Path.Combine(dir, "register.pdf"), await PrintAsync(InsuredPersonsForm.Layout(register.Lines, register.Employer)));
    }

    private sealed record OverlayKor2(Kor2Employee Employee, Kor2Input Input, EmployerOptions Employer);
    private sealed record OverlayRegister(List<InsuredPersonLine> Lines, EmployerOptions Employer);
}
