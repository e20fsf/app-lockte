using Lockte.Org;
using Lockte.Purch;
using Lockte.Web.Reports;
using Lockte.Web.Reports.Purch;

namespace Lockte.Web.Tests;

/// <summary>RPT-01 ใบขออนุมัติสั่งซื้อ (#220): the data handed to the report, the number printed top right, and the page body.</summary>
[Collection(LocalDbCollection.Name)]
public sealed class PurchRequestReportTests(LocalDbFixture db)
{
    private static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..6];

    private async Task<T> AsBuyerAsync<T>(Func<TestScope, Task<T>> action)
    {
        await using var scope = db.Scope("buyer1");
        return await action(scope);
    }

    private async Task<(int Id, PurchaseRequestDto Request)> NewRequestAsync(int items)
    {
        int department;
        await using (var scope = db.Scope("hr1"))
        {
            var org = scope.Get<OrgRegistry>();
            var section = (await org.SaveSectionAsync(new SectionInput(null, Unique("ฝ่าย"), 1, null))).Id!.Value;
            department = (await org.SaveDepartmentAsync(new DepartmentInput(null, Unique("D"), Unique("แผนกทดสอบ"), "d", 3, section, null, [], null))).Id!.Value;
        }
        var lines = new List<PurchaseRequestLineInput>();
        for (var i = 1; i <= items; i++)
        {
            var material = (await AsBuyerAsync(s => s.Get<MaterialRegistry>().SaveAsync(new MaterialInput(null, Unique("rp").ToUpperInvariant(), $"ชื่อทะเบียน {i}", null, "ตัว", Cost: 10)))).Id!.Value;
            lines.Add(new PurchaseRequestLineInput(material, i, MaterialName: $"ชื่อในใบ {i}"));
        }
        var no = Unique("R") + "69-0001";
        var saved = await AsBuyerAsync(s => s.Get<PurchaseRequestRegistry>().SaveAsync(
            new PurchaseRequestInput(no, new DateOnly(2026, 10, 29), department, new DateOnly(2026, 11, 15), "F", "เหตุผลทดสอบ", "หมายเหตุทดสอบ", lines)));
        return (saved.Id!.Value, (await AsBuyerAsync(s => s.Get<PurchaseRequestDirectory>().GetAsync(saved.Id!.Value)))!);
    }

    private Task<PurchRequestPrint?> BuildAsync(int id, IReadOnlyCollection<int>? lineIds = null)
        => AsBuyerAsync(s => PurchRequestReports.BuildAsync(s.Get<PurchaseRequestDirectory>(), s.Get<OrgDirectory>(), s.Get<RequestSetupDirectory>(), id, lineIds));

    [Fact]
    public async Task The_print_has_the_head_and_every_item_with_the_names_typed_on_the_request()
    {
        var (id, request) = await NewRequestAsync(3);
        var print = (await BuildAsync(id))!;
        Assert.Equal((request.RequestNo, "โรงงาน", "เหตุผลทดสอบ", new DateOnly(2026, 11, 15)), (print.RequestNo, print.PurchasingLocation, print.Reason, print.DesiredDate));
        Assert.StartsWith("แผนกทดสอบ", print.DepartmentName);
        Assert.Equal(["ชื่อในใบ 1", "ชื่อในใบ 2", "ชื่อในใบ 3"], print.Lines.Select(l => l.Name)); // the document's text, not the register's (DQ-04)
        Assert.Equal([1m, 2m, 3m], print.Lines.Select(l => l.Quantity));
    }

    [Fact]
    public async Task Printing_again_only_the_picked_items_and_an_unknown_request_has_no_data()
    {
        var (id, request) = await NewRequestAsync(4);
        var picked = new[] { request.Lines[1].Id, request.Lines[3].Id };
        var print = (await BuildAsync(id, picked))!;
        Assert.Equal([2, 4], print.Lines.Select(l => l.LineNo));
        Assert.Empty((await BuildAsync(id, [int.MaxValue]))!.Lines); // nothing picked that exists → "ไม่มีรายการ" at the route
        Assert.Null(await BuildAsync(int.MaxValue));
    }

    [Fact]
    public async Task The_document_number_is_the_legacy_one_and_the_body_lists_the_items_and_the_count()
    {
        await using (var scope = db.Scope())
            Assert.Equal("PC-PU002-1/2547 ฉบับที่ 1", await scope.Get<DocumentNumberService>().PrintedTextAsync(DocumentKeys.PurchRequest));

        var (id, _) = await NewRequestAsync(15); // more rows than one sheet holds: a second sheet
        var pages = PurchRequestForm.Layout((await BuildAsync(id))!, "PC-PU002-1/2547 ฉบับที่ 1", new PurchLetterheadOptions());
        var svg = System.Net.WebUtility.HtmlDecode(string.Concat(pages.Select(p => p.ToSvg())));
        Assert.Equal(2, pages.Count);
        Assert.Contains("รวมรายการ", pages[1].ToSvg());
        Assert.Contains(">15<", pages[1].ToSvg());
        Assert.Contains("เหตุผลทดสอบ", svg);
        Assert.Contains("ชื่อในใบ 15", svg);
        Assert.Contains("ผู้จัดการโรงงาน", svg);
    }
}

/// <summary>RPT-01 form (#420) without a database.</summary>
public sealed class PurchRequestFormTests
{
    private static readonly PurchLetterheadOptions Letterhead = new() { Line1 = "ที่อยู่สำนักงาน (ข้อมูลทดสอบ)", Line2 = "Test Office Address" };

    private static PurchRequestPrint Print(string? reason = "เหตุผลทดสอบ", string? note = "หมายเหตุทดสอบ", params PurchRequestPrintLine[] lines)
        => new("O69-0040", new DateOnly(2026, 10, 29), "แผนกทดสอบ", "โรงงาน", "F", new DateOnly(2026, 11, 15), reason, note, lines);

    private static PurchRequestPrintLine[] Lines(int count)
        => Enumerable.Range(1, count).Select(i => new PurchRequestPrintLine(i, $"E{i:0000}", $"รายการ {i}", i, "ตัว")).ToArray();

    [Fact]
    public void The_form_has_the_head_the_box_of_the_letter_the_blank_rows_and_the_three_signatures()
    {
        var svg = System.Net.WebUtility.HtmlDecode(PurchRequestForm.Layout(Print(lines: Lines(2)), "PC-PU002-1/2547 ฉบับที่ 1", Letterhead).Single().ToSvg());
        foreach (var text in new[] { "ที่อยู่สำนักงาน (ข้อมูลทดสอบ)", "Test Office Address", "PC-PU002-1/2547 ฉบับที่ 1", "เลขที่ใบสั่งซื้อ", "เอกสารเลขที่", "O69-0040", ">F<", "แผนกที่ขอซื้อ", "แผนกทดสอบ",
                     "วันที่สั่งซื้อ", "ราคาต่อหน่วย", "จำนวนเงิน", "รวมรายการ", ">2<", "เหตุผลในการซื้อ", "เหตุผลทดสอบ", "หมายเหตุ", "วันที่ต้องการของ", "ผู้สั่งซื้อ", "ผู้จัดการฝ่าย", "ผู้จัดการโรงงาน" })
            Assert.Contains(text, svg);
        // ruled all round: 14 horizontal rules in the table (top, below the heading, one under each of the 13 rows) → blank rows are ruled too
        Assert.True(System.Text.RegularExpressions.Regex.Matches(svg, "<line ").Count >= 14 + 8 + 3);
    }

    [Fact]
    public void Prices_are_left_blank_to_write_by_hand()
    {
        var svg = PurchRequestForm.Layout(Print(lines: Lines(1)), null, Letterhead).Single().ToSvg();
        Assert.Equal(1, System.Text.RegularExpressions.Regex.Matches(svg, @">1\.00<").Count); // the quantity only: no unit price, no amount
    }

    [Fact]
    public void More_items_than_a_sheet_holds_go_on_a_second_sheet_with_the_boxes_on_the_last()
    {
        Assert.Single(PurchRequestForm.Layout(Print(lines: Lines(PurchRequestForm.RowsPerPage)), null, Letterhead));
        var pages = PurchRequestForm.Layout(Print(lines: Lines(PurchRequestForm.RowsPerPage + 1)), null, Letterhead);
        Assert.Equal(2, pages.Count);
        Assert.DoesNotContain("ผู้จัดการโรงงาน", pages[0].ToSvg());
        Assert.Contains("ผู้จัดการโรงงาน", pages[1].ToSvg());
        Assert.Contains(">14<", pages[1].ToSvg());
    }

    [Fact]
    public void A_long_reason_or_note_is_wrapped_into_lines_without_losing_text()
    {
        var text = string.Concat(Enumerable.Repeat("ก", 150));
        var lines = PurchRequestForm.Wrap(text, 92).ToList();
        Assert.Equal([92, 58], lines.Select(l => l.Length));
        Assert.Empty(PurchRequestForm.Wrap(null, 92));
        Assert.Equal(["a b", "c"], PurchRequestForm.Wrap("a b\nc", 92));
    }
}

/// <summary>RPT-01 form (#420) printed by Chromium (set env <c>LOCKTE_PDF_OUT</c> to a folder to keep the PDF/HTML for a look).</summary>
[Collection(ChromiumCollection.Name)]
public sealed class PurchRequestFormPdfTests(ChromiumFixture chromium)
{
    [Fact]
    public async Task The_form_prints_as_an_A4_pdf_one_sheet_for_few_items_and_two_for_many()
    {
        var letterhead = new PurchLetterheadOptions { Line1 = "97 ซอยทดสอบ 9 ถนนทดสอบ เขตทดสอบ กรุงเทพฯ 10500 โทร. (02)000-0000 (โรงงาน) โทร. (02)000-0001", Line2 = "97 Test Soi 9, Test Road, Bangkok 10500 Tel. (02)000-0000 (Factory) Tel. (02)000-0001" };
        PurchRequestPrintLine[] Lines(int n) => Enumerable.Range(1, n).Select(i => new PurchRequestPrintLine(i, $"X{i:000}", $"วัตถุดิบทดสอบ {i} รุ่นทดสอบ", i * 1000m, "ตัว")).ToArray();
        PurchRequestPrint Print(int n) => new("P69-0086", new DateOnly(2026, 10, 31), "แผนกทดสอบ", "โรงงาน", "F", new DateOnly(2026, 11, 10), "1. เหตุผลทดสอบข้อหนึ่ง 2-5 ข้อสอง", null, Lines(n));
        foreach (var (n, pagesExpected) in new[] { (6, 1), (15, 2) })
        {
            var pages = PurchRequestForm.Layout(Print(n), "PC-PU002-1/2547 ฉบับที่ 1", letterhead);
            var html = FormPageHtml.Compose(PurchRequestForm.Title, pages);
            var pdf = await chromium.Renderer.RenderAsync(html, pages[0].Paper);
            Assert.Equal(pagesExpected, System.Text.RegularExpressions.Regex.Matches(System.Text.Encoding.Latin1.GetString(pdf), @"/Type /Page\b").Count);
            if (Environment.GetEnvironmentVariable("LOCKTE_PDF_OUT") is { Length: > 0 } folder)
            {
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder, $"purch-request-{n}.pdf"), pdf);
                File.WriteAllText(Path.Combine(folder, $"purch-request-{n}.html"), html);
            }
        }
    }
}
