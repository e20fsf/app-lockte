using System.Text;
using System.Text.RegularExpressions;
using Lockte.Calib;
using Lockte.Web.Reports;
using Lockte.Web.Reports.Calib;

namespace Lockte.Web.Tests;

/// <summary>
/// RPT-05 รายงานการตรวจสอบ JIG วัดศูนย์ (#207, ระดับ A), AC-14: the head, the picture, the points of the model with their specification, the result and the three signatures. The
/// position of every element is checked by overlaying the PDF on the sample (set env <c>LOCKTE_PDF_OUT</c> to a folder to keep the PDFs the tests print).
/// </summary>
[Collection(ChromiumCollection.Name)]
public sealed class JigInspectionFormTests(ChromiumFixture chromium)
{
    private static readonly byte[] FakePng = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3];

    /// <summary>A sheet as the service returns it, made from the starting data (so the test follows reports.md).</summary>
    internal static JigSheetDto Sheet(JigSheetType type, string model)
    {
        var seed = JigInspectionSeed.Sheets.Single(s => s.Type == type && s.ModelKey == model);
        var points = seed.Points.Select((p, i) => new JigPointDto(i + 1, p.Label, p.Kind, p.Value, p.Tolerance, p.ToleranceValue, [1])).ToList();
        return new JigSheetDto(1, seed.Type, seed.ModelKey, seed.Title, seed.DocumentKey, seed.DocumentNo, false, points, [1]);
    }

    private static string Svg(JigSheetDto sheet, JigImageData? image = null, string? number = null)
        => System.Net.WebUtility.HtmlDecode(JigInspectionForm.Page(sheet, image, number).ToSvg());

    private static int Pages(byte[] pdf) => Regex.Matches(Encoding.Latin1.GetString(pdf), @"/Type /Page\b").Count;

    private static void Keep(string name, byte[] pdf)
    {
        if (Environment.GetEnvironmentVariable("LOCKTE_PDF_OUT") is { Length: > 0 } folder)
        {
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, name + ".pdf"), pdf);
        }
    }

    [Fact] // AC-14: CLICK → WI-QA049-4/2550 and P1 20.00 / P2 9.00 / P3 20.00 / P4 Wide 8.00 / P5 72.00 / P6 66.50
    public void The_click_sheet_has_the_document_number_the_name_and_the_six_points()
    {
        var svg = Svg(Sheet(JigSheetType.ZeroCheck, "CLICK"), null, "WI-QA049-4/2550 ฉบับที่ 2");
        foreach (var text in new[] { ">LOCKTE CO.,LTD<", ">รายงานการตรวจสอบ JIG วัดศูนย์ก้ามเบรก<", ">WI-QA049-4/2550 ฉบับที่ 2<", ">ชื่อ JIG<", ">CLICK<", ">WI-QA049<", ">± 0.20 mm.<", ">สอบเทียบภายใน<",
                     ">Diameter 20.00 ± 0.20 mm.<", ">Diameter 9.00 ± 0.20 mm.<", ">Wide 8.00 ± 0.20 mm.<", ">Dimension 72.00 ± 0.20 mm.<", ">Dimension 66.50 ± 0.20 mm.<",
                     ">P1<", ">P6<", ">ค่าที่วัดได้<", ">ค่าเฉลี่ย<", ">ค่าผิดพลาด<", ">ผลการตรวจสอบ<", ">ผ่าน<", ">ไม่ผ่าน<",
                     ">(ผู้ตรวจสอบ)<", ">(ผู้จัดการฝ่ายประกันคุณภาพ)<", ">(ผู้จัดการฝ่ายวางแผนและควบคุมการผลิต)<" })
            Assert.Contains(text, svg);
        Assert.DoesNotContain(">P7<", svg, StringComparison.Ordinal);
    }

    [Fact] // the name of the JIG prints on every sheet, 233, 068, 168, 061, 143 included
    public void The_name_of_the_jig_is_printed_on_every_sheet()
        => Assert.All(JigInspectionSeed.Sheets.Where(s => s.Type == JigSheetType.ZeroCheck),
            s => Assert.Contains($">{System.Net.WebUtility.HtmlDecode(System.Net.WebUtility.HtmlEncode(s.Title))}<", Svg(Sheet(JigSheetType.ZeroCheck, s.ModelKey))));

    [Fact] // print-samples: PIN and TAPER have their own titles and one or two points
    public void Pin_and_taper_gauge_sheets_have_their_titles_and_points()
    {
        var pin = Svg(Sheet(JigSheetType.ZeroCheck, "PIN GAUGE"));
        Assert.Contains(">รายงานการตรวจสอบ PIN GAUGE<", pin);
        Assert.Contains(">Diameter 11.50 ± 0.20 mm.<", pin);
        var taper = Svg(Sheet(JigSheetType.ZeroCheck, "TAPER GAUGE"));
        Assert.Contains(">รายงานการตรวจสอบ TAPER GAUGE<", taper);
        Assert.Contains(">Diameter 47.00 ± 0.20 mm.<", taper);
        Assert.Contains(">Diameter 50.00 ± 0.20 mm.<", taper);
    }

    [Fact] // RX100 has nine points, WING other kinds
    public void The_nine_point_and_wing_sheets_print_their_own_points()
    {
        var rx = Svg(Sheet(JigSheetType.ZeroCheck, "RX100"));
        foreach (var label in new[] { ">P2.1<", ">P2.2<", ">P2.3<" }) Assert.Contains(label, rx);
        Assert.Contains(">Wide 4.00 ± 0.20 mm.<", rx);
        var wing = Svg(Sheet(JigSheetType.ZeroCheck, "WING"));
        Assert.Contains(">Length 21.00 ± 0.20 mm.<", wing);
        Assert.Contains(">Length 73.90 ± 0.20 mm.<", wing);
    }

    [Fact] // a sheet with a picture embeds it, one without prints an empty frame (and still prints)
    public void The_picture_is_embedded_when_there_is_one()
    {
        var sheet = Sheet(JigSheetType.ZeroCheck, "JOG");
        Assert.Contains("<image ", Svg(sheet, new JigImageData(FakePng, "image/png")), StringComparison.Ordinal);
        Assert.DoesNotContain("<image ", Svg(sheet), StringComparison.Ordinal);
    }

    [Fact] // every one of the 23 sheets fits a page: Chromium prints one page each
    public async Task Each_sheet_is_one_page()
    {
        foreach (var model in new[] { "CLICK", "RX100", "WING", "PIN GAUGE", "TAPER GAUGE" })
        {
            var page = JigInspectionForm.Page(Sheet(JigSheetType.ZeroCheck, model), new JigImageData(FakePng, "image/png"), "WI-QA049-1/2550 ฉบับที่ 2");
            var pdf = await chromium.Renderer.RenderAsync(FormPageHtml.Compose(JigInspectionForm.TitleOf(Sheet(JigSheetType.ZeroCheck, model)), [page]), page.Paper);
            Keep("calib-jig-" + model.Replace(' ', '_'), pdf);
            Assert.Equal(1, Pages(pdf));
        }
    }

    [Fact]
    public void The_file_name_has_underscores_for_commas()
        => Assert.Equal("JIG วัดศูนย์ 052_063", JigInspectionForm.FileName(Sheet(JigSheetType.ZeroCheck, "052,063")));

    [Fact] // AC-14 (RPT-06): K125 → WI-QA049-35/2560, P1 Diameter 12.00, P2 Length 9.00 (not `>`), P3 Wide 9.00
    public void The_k125_grinding_sheet_has_its_number_and_three_points()
    {
        var svg = Svg(Sheet(JigSheetType.Grinding, "K125"), null, "WI-QA049-35/2560 ฉบับที่ 2");
        foreach (var text in new[] { ">รายงานการตรวจสอบ JIG กรอก้ามเบรก<", ">WI-QA049-35/2560 ฉบับที่ 2<", ">K125<", ">Diameter 12.00 ± 0.20 mm.<", ">Length 9.00 ± 0.20 mm.<", ">Wide 9.00 ± 0.20 mm.<", ">P3<" })
            Assert.Contains(text, svg);
        Assert.DoesNotContain(">P4<", svg, StringComparison.Ordinal);
        Assert.DoesNotContain("รายงานการตรวจสอบ JIG วัดศูนย์", svg, StringComparison.Ordinal);
    }

    [Fact] // `>15.00` (143: `>13.00`) has no ±
    public void A_greater_than_length_prints_without_a_tolerance()
    {
        var click = Svg(Sheet(JigSheetType.Grinding, "CLICK"));
        Assert.Contains(">Length >15.00<", click);
        Assert.Contains(">Diameter 9.00 ± 0.20 mm.<", click);
        Assert.Contains(">Length >13.00<", Svg(Sheet(JigSheetType.Grinding, "143")));
    }

    [Fact]
    public void The_name_of_the_jig_is_printed_on_every_grinding_sheet_233_061_and_143_included()
        => Assert.All(JigInspectionSeed.Sheets.Where(s => s.Type == JigSheetType.Grinding), s => Assert.Contains($">{s.Title}<", Svg(Sheet(JigSheetType.Grinding, s.ModelKey))));

    [Fact] // 19 sheets, none for 068, 168, PIN GAUGE, TAPER GAUGE
    public void There_is_no_grinding_sheet_for_the_four_models_without_one()
    {
        var models = JigInspectionSeed.Sheets.Where(s => s.Type == JigSheetType.Grinding).Select(s => s.ModelKey).ToList();
        Assert.Equal(19, models.Count);
        foreach (var none in new[] { "068", "168", "PIN GAUGE", "TAPER GAUGE" }) Assert.DoesNotContain(none, models);
    }

    [Fact]
    public async Task A_grinding_sheet_is_one_page_and_its_file_name_says_grinding()
    {
        var sheet = Sheet(JigSheetType.Grinding, "K125");
        var page = JigInspectionForm.Page(sheet, new JigImageData(FakePng, "image/png"), "WI-QA049-35/2560 ฉบับที่ 2");
        var pdf = await chromium.Renderer.RenderAsync(FormPageHtml.Compose(JigInspectionForm.TitleOf(sheet), [page]), page.Paper);
        Keep("calib-jig-grinding-K125", pdf);
        Assert.Equal(1, Pages(pdf));
        Assert.Equal("JIG กรอ K125", JigInspectionForm.FileName(sheet));
    }
}
