using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Lockte.Migration;
using Lockte.Prod;
using Lockte.Prod.Data;
using Lockte.SharedKernel;
using Lockte.Web.Reports;
using Lockte.Web.Reports.Order.Qa;

namespace Lockte.Web.Tests;

/// <summary>
/// ใบตรวจคุณภาพ QA ระดับ A (#130): the sheet of a family with a made-up spec in it. What the pages look like against the sample prints is checked by eye and by
/// <c>tools/qa-form-templates/compare.py</c> (the sample prints and the recovered reports are the company's own, outside the repo).
/// </summary>
[Collection(ChromiumCollection.Name)]
public sealed class QaSheetTests(ChromiumFixture chromium)
{
    private static QaSheetContent Content(string models = "FAKE-1, FAKE-2") => new("รายงานตรวจชิ้นงานสมมุติ", QaSheetForm.DocumentNumber("WI-QA999/2570", 7, "ฉบับที่ 2"), models,
        ["จุดที่1     51.0          mm.  /  50.50-51.30  mm.", "จุดที่2    น้อยกว่า 15.30  mm."], ["แตก", "รอยบิ่น"], null, null);

    private static string Svg(FormPage page) => page.ToSvg();

    private static string Text(byte[] pdf) => Encoding.Latin1.GetString(pdf);

    private static int Pages(byte[] pdf) => Regex.Matches(Text(pdf), @"/Type /Page\b").Count;

    [Theory] // the number is the code with the sequence of the model before the year, then the edition
    [InlineData("WI-QA055/2551", 21, "ฉบับที่ 2", "WI-QA055-21/2551 ฉบับที่ 2")]
    [InlineData("WI-QA055/2551", null, "ฉบับที่ 2", "WI-QA055/2551 ฉบับที่ 2")]
    [InlineData("WI-QA022/2547", 3, null, "WI-QA022-3/2547")]
    [InlineData("WI-QA999", 4, " ฉบับที่ 1 ", "WI-QA999-4 ฉบับที่ 1")]
    public void The_document_number_has_the_sequence_before_the_year(string code, int? sequence, string? revision, string expected)
        => Assert.Equal(expected, QaSheetForm.DocumentNumber(code, sequence, revision));

    [Fact] // the numbered point keeps the spaces that line its value up; a point with words gets one space
    public void A_point_prints_as_typed()
    {
        Assert.Equal("จุดที่1     51.0 mm.", QaSheetForm.PointText(new QaMeasurePointDto(1, "จุดที่1", null, "     51.0 mm.")));
        Assert.Equal("P.2 51.0", QaSheetForm.PointText(new QaMeasurePointDto(2, "P.2", null, "51.0")));
        Assert.Equal("ความหนา 3.50 mm.", QaSheetForm.PointText(new QaMeasurePointDto(3, null, "ความหนา", "3.50 mm.")));
        Assert.Equal("5.0 ± 0.2", QaSheetForm.PointText(new QaMeasurePointDto(4, null, null, " 5.0 ± 0.2")));
    }

    [Theory] // every family has its template; a group with none yet has no sheet
    [InlineData("ไดคาสท์029", "D")]
    [InlineData("ตกแต่ง029", "T")]
    [InlineData("พิมพ์WING", "M")]
    [InlineData("อัดผ้าเบรค068", "B")]
    [InlineData("ฝนผ้าเบรคTS100", "G")]
    [InlineData("ประกอบ052", "A")]
    public void The_template_is_by_the_group_of_the_spec(string specName, string key)
        => Assert.Equal(key, QaFormTemplates.For(specName)?.Key);

    [Theory] // a group with two shapes of sheet (with measure points or without) takes the template whose rows fit the spec
    [InlineData("อัดดิสเบรคAR125", 0, 7, "H")]
    [InlineData("อัดดิสเบรคSPARK135", 2, 5, "I")]
    [InlineData("ดิสเบรคหลังผ่าร่อง4POT", 0, 7, "J")]
    [InlineData("ดิสเบรคหลังผ่าร่องNINJA 650", 2, 5, "K")]
    [InlineData("ดิสเบรคหลังผ่าร่องNINJA 650", null, null, "J")]
    public void A_group_with_two_shapes_takes_the_one_that_fits(string specName, int? points, int? defects, string key)
        => Assert.Equal(key, QaFormTemplates.For(specName, points, defects)?.Key);

    [Fact]
    public void A_group_without_a_template_has_none()
        => Assert.Null(QaFormTemplates.For("สินค้าก่อนส่งให้ลูกค้า"));

    [Fact] // a family: a page for each line marked, the spec in its places, the fields filled; the blank one when nothing is marked
    public void A_sheet_has_a_page_for_each_marked_line_and_one_blank_when_none()
    {
        var template = QaFormTemplates.For("ไดคาสท์029")!;
        var lines = new List<QaSheetLine> { new("SB1001", "ก้ามสมมุติ", "LOT-9", new DateOnly(2026, 5, 4), 1234, 80), new("SB1002", "ก้ามสมมุติสอง", null, null, 10, 5) };

        var pages = QaSheetForm.Layout(template, Content(), lines);
        var blank = QaSheetForm.Layout(template, Content(), []);

        Assert.Equal(2, pages.Count);
        Assert.Single(blank);
        var first = Svg(pages[0]);
        Assert.Contains("WI-QA999-7/2570 ฉบับที่ 2", first);
        Assert.Contains("รายงานตรวจชิ้นงานสมมุติ", first);
        Assert.Contains("FAKE-1, FAKE-2", first);
        Assert.Contains("จุดที่1     51.0", first);
        Assert.Contains("รอยบิ่น", first);
        Assert.Contains("SB1001", first);
        Assert.Contains("ก้ามสมมุติ", first);
        Assert.Contains("LOT-9", first);
        Assert.Contains(ThaiDate.Format(new DateOnly(2026, 5, 4)), first);
        Assert.DoesNotContain("SB1001", Svg(blank[0]));
        Assert.DoesNotContain("SB1001", Svg(pages[1]));
    }

    [Fact] // the trimming sheet marks the columns of the attribute table up to the sample size ("/"), and shows the quantity and the size
    public void The_trimming_sheet_marks_the_columns_the_sample_reaches()
    {
        var template = QaFormTemplates.For("ตกแต่ง029")!;

        var small = Svg(QaSheetForm.Layout(template, Content(), [new("SB1001", "ก้าม", null, null, 100, 20)])[0]);
        var large = Svg(QaSheetForm.Layout(template, Content(), [new("SB1001", "ก้าม", null, null, 100, 125)])[0]);

        Assert.Contains("Veriable Data", small); // the legacy spelling is kept
        // the mark is "/" in the columns the sample reaches: more of them for a sample of 125 than for 20
        var marks = (string svg) => Regex.Matches(svg, @">/</text>").Count;
        Assert.True(marks(large) > marks(small));
        Assert.True(marks(small) > 0);
    }

    [Theory] // the groups of pairs: every family prints a page for the line, with its rules and its sample-size marks
    [InlineData("อัดผ้าเบรค068")]
    [InlineData("ฝนผ้าเบรค068")]
    [InlineData("ประกอบ052")]
    public void The_brake_groups_print_a_page_for_each_line(string specName)
    {
        var template = QaFormTemplates.For(specName)!;

        var pages = QaSheetForm.Layout(template, Content(), [new("SB1001", "ก้าม", "L1", new DateOnly(2026, 5, 4), 100, 125), new("SB1002", "ก้ามสอง", null, null, 10, 5)]);

        Assert.Equal(2, pages.Count);
        Assert.Contains("WI-QA999-7/2570 ฉบับที่ 2", Svg(pages[0]));
        Assert.Contains("SB1001", Svg(pages[0]));
        Assert.True(template.Rules.Count > 40);
        Assert.True(Regex.Matches(Svg(pages[0]), @">/</text>").Count > Regex.Matches(Svg(pages[1]), @">/</text>").Count); // 125 reaches more columns than 5
    }

    [Theory] // every packing sheet has the one frame; the blank form is its own
    [InlineData("คลัทช์บรรจุกล่อง", "S")]
    [InlineData("ดรัมสกินแพ็ค", "S")]
    [InlineData("ดิสบรรจุกล่องฟอร์มเปล่า", "Z")]
    [InlineData("ดรัมสกินแพ็คฟอร์มเปล่า", "Z")]
    public void The_packing_sheets_share_two_frames(string specName, string key)
        => Assert.Equal(key, QaFormTemplates.For(specName)?.Key);

    [Fact] // packing: a strip for each product, four to a page; the signatures follow the last strip on its page, or have a page of their own when four strips fill it
    public void The_packing_sheet_has_a_strip_for_each_line_and_the_signatures_after_the_last()
    {
        var template = QaFormTemplates.For("คลัทช์บรรจุกล่อง")!;
        QaSheetLine Line(int i) => new($"SB{i:00}", $"สินค้า{i}", $"L{i}", new DateOnly(2026, 5, 4), 100 + i, 20);
        string Signatures(FormPage page) => Svg(page);

        var one = QaSheetForm.Layout(template, Content(), [Line(1)]);
        var four = QaSheetForm.Layout(template, Content(), Enumerable.Range(1, 4).Select(Line).ToList());
        var five = QaSheetForm.Layout(template, Content(), Enumerable.Range(1, 5).Select(Line).ToList());

        Assert.Single(one);
        Assert.Contains("ผู้ตรวจสอบ", Signatures(one[0]));
        Assert.Contains("SB01", Svg(one[0]));
        Assert.Equal(2, four.Count); // four strips fill the page: the signatures are on the next
        Assert.DoesNotContain("ผู้ตรวจสอบ", Svg(four[0]));
        Assert.Contains("ผู้ตรวจสอบ", Svg(four[1]));
        Assert.Contains("SB04", Svg(four[0]));
        Assert.Equal(2, five.Count); // the fifth is on the second page, with the signatures
        Assert.Contains("SB05", Svg(five[1]));
        Assert.Contains("ผู้ตรวจสอบ", Svg(five[1]));
        Assert.DoesNotContain("#Name?", Svg(four[1]));
        Assert.Contains(ThaiDate.Format(new DateOnly(2026, 5, 4)), Svg(four[1])); // the date boxes under the signatures show the date
    }

    [Fact] // the blank form: four empty strips, then the signatures on the second page, and no #Name? (D-3)
    public void The_blank_packing_form_has_four_empty_strips_and_clean_signatures()
    {
        var template = QaFormTemplates.For("คลัทช์บรรจุกล่องฟอร์มเปล่า")!;

        var pages = QaSheetForm.Layout(template, Content(), [new("SB01", "สินค้า", "L1", new DateOnly(2026, 5, 4), 100, 20)]);

        Assert.Equal(2, pages.Count);
        Assert.Equal(4, Regex.Matches(Svg(pages[0]), "รหัสสินค้า").Count); // four strips
        Assert.DoesNotContain("SB01", Svg(pages[0]));                      // blank: whatever is marked
        Assert.Contains("ผู้ตรวจสอบ", Svg(pages[1]));
        Assert.DoesNotContain("#Name?", Svg(pages[1]));
    }

    [Fact] // a page is A4 landscape, and the PDF has TH Sarabun New in it
    public async Task The_sheet_is_a_PDF_of_A4_landscape_pages()
    {
        var template = QaFormTemplates.For("ไดคาสท์029")!;
        var pages = QaSheetForm.Layout(template, Content(), [new("SB1001", "ก้าม", "L1", null, 10, 5), new("SB1002", "ก้ามสอง", "L2", null, 20, 5)]);

        var pdf = await chromium.Renderer.RenderAsync(FormPageHtml.Compose("test", pages), pages[0].Paper);

        Assert.Equal(2, Pages(pdf));
        var box = Regex.Match(Text(pdf), @"/MediaBox \[0 0 ([\d.]+) ([\d.]+)\]");
        Assert.Equal((842, 595), (Math.Round(double.Parse(box.Groups[1].Value, CultureInfo.InvariantCulture)), Math.Round(double.Parse(box.Groups[2].Value, CultureInfo.InvariantCulture))));
        Assert.Contains("THSarabunNew", Text(pdf));
    }

    /// <summary>
    /// Run by hand to compare with the sample prints: <c>LOCKTE_QA_COMPARE=&lt;folder of reports_decompiled&gt;|&lt;output folder&gt;</c> prints the blank sheet of every report of the three families
    /// from its recovered definition into the output folder, for <c>tools/qa-form-templates/compare.py</c>.
    /// </summary>
    [Fact]
    public async Task Prints_the_sheets_of_the_recovered_reports_for_comparison()
    {
        if (Environment.GetEnvironmentVariable("LOCKTE_QA_COMPARE")?.Split('|') is not [var definitions, var output]) return;
        Directory.CreateDirectory(output);
        foreach (var file in Directory.GetFiles(definitions, "*.txt"))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            if (QaSheetReader.Read(name, ReportDefinition.Read(file)) is not { } sheet) continue;
            if (QaFormTemplates.For(name, sheet.Points.Count, sheet.Defects.Count) is not { } template) continue;
            var content = new QaSheetContent(sheet.Title, QaSheetForm.DocumentNumber(sheet.DocumentCode, sheet.Sequence, sheet.Revision), sheet.Models,
                sheet.Points.Select(p => QaSheetForm.PointText(new QaMeasurePointDto(0, p.PointNo, p.Description, p.Standard))).ToList(), sheet.Defects, null, null);
            var pages = QaSheetForm.Layout(template, content, []);
            await File.WriteAllBytesAsync(Path.Combine(output, name + ".pdf"), await chromium.Renderer.RenderAsync(FormPageHtml.Compose(name, pages), pages[0].Paper));
        }
    }
}
