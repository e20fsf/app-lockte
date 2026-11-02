using System.Text.Json;
using Lockte.Cash;
using Lockte.Web.Reports;
using Lockte.Web.Reports.Cash;

namespace Lockte.Web.Tests;

/// <summary>ใบเสร็จรับเงินขายของเก่า RPT-06 (#238, ระดับ A) without a database: the layout of the pages, the head, the total and its words.</summary>
public sealed class CashScrapReceiptFormTests
{
    private static ScrapReceiptLineDto Line(int seq, string item, decimal qty, decimal price, decimal amount, string? unit = "กก.") => new(seq, seq, item, null, unit, qty, price, amount);

    internal static ScrapReceiptDto Receipt(IReadOnlyList<ScrapReceiptLineDto> lines, string no = "69029", string buyer = "คุณตัวอย่าง ข", string? address = "99/1 ซอยตัวอย่าง ตำบลตัวอย่าง", string? tel = "000-000-0000", bool cancelled = false)
        => new(1, no, new DateOnly(2026, 9, 8), buyer, address, tel, null, cancelled, null, null, null, lines, [1]);

    /// <summary>The five lines of receipt 69029 (BR-012 #1).</summary>
    internal static ScrapReceiptDto Sample() => Receipt(
    [
        Line(1, "เบ้าเหล็ก", 220, 1, 220), Line(2, "ถังพลาสติก", 1, 120, 120, "ใบ"), Line(3, "น้ำมันเก่า", 1.5m, 800, 1200, "ถัง"), Line(4, "พลาสติกสกินแพ็ค", 110, 3, 330), Line(5, "ถังเหล็ก", 5, 80, 400, "ใบ"),
    ]);

    private static string Svg(FormPage page) => System.Net.WebUtility.HtmlDecode(page.ToSvg());

    [Fact] // AC-10: 2,270.00 and its words
    public void AC10_the_sample_receipt_prints_its_head_lines_total_words_and_signatures()
    {
        var page = Assert.Single(ScrapReceiptForm.Layout(Sample()));
        Assert.Equal(Paper.LetterPortrait, page.Paper);
        var svg = Svg(page);
        foreach (var text in new[] { "ใบเสร็จรับเงินขายของเก่า", "เลขที่", "69029", "ชื่อผู้ซื้อ", "คุณตัวอย่าง ข", "วันที่", ThaiDate_Format(new DateOnly(2026, 9, 8)), "ที่อยู่", "โทรฯ", "000-000-0000",
                     "รายการ", "จำนวน", "หน่วย", "ราคา", "จำนวนเงิน", "เบ้าเหล็ก", "220.00", "1.50", "800.00", "1,200.00", "รวมทั้งหมด", "2,270.00", "(สองพันสองร้อยเจ็ดสิบบาทถ้วน)",
                     "ผู้รับของ", "ผู้ตรวจสอบ", "ผู้รับเงิน", "ผู้อนุมัติ" })
            Assert.Contains(text, svg, StringComparison.Ordinal);
        Assert.DoesNotContain("ล็อคเต้", svg, StringComparison.Ordinal); // the legacy has no company name on it
    }

    private static string ThaiDate_Format(DateOnly date) => Lockte.SharedKernel.ThaiDate.Format(date);

    [Fact] // AC-11: the printed total is the sum cut to two decimals by banker's rounding
    public void AC11_the_total_is_rounded_by_the_rule_of_this_program()
    {
        var svg = Svg(Assert.Single(ScrapReceiptForm.Layout(Receipt([Line(1, "ก", 1, 100.125m, 100.125m)]))));
        Assert.Contains(">100.12<", svg, StringComparison.Ordinal);
        Assert.Contains(">100.13<", svg, StringComparison.Ordinal); // the line itself prints as it is kept, to two decimals
    }

    [Fact] // the lines are in the order of ลำดับที่ and print their own number
    public void The_lines_print_the_sequence_stored_not_their_position()
    {
        var svg = Svg(Assert.Single(ScrapReceiptForm.Layout(Receipt([Line(3, "ก", 1, 1, 1), Line(7, "ข", 1, 1, 1)]))));
        Assert.Contains(">3<", svg, StringComparison.Ordinal);
        Assert.Contains(">7<", svg, StringComparison.Ordinal);
    }

    [Fact] // more lines than a page: the head is repeated, the total and the signatures are on the last page only
    public void A_long_receipt_goes_on_to_the_next_page_and_signs_at_the_end()
    {
        var lines = Enumerable.Range(1, 25).Select(i => Line(i, $"รายการ{i}", 1, 10, 10)).ToList();
        var pages = ScrapReceiptForm.Layout(Receipt(lines));
        Assert.Equal(2, pages.Count);
        var (first, second) = (Svg(pages[0]), Svg(pages[1]));
        Assert.Contains("ใบเสร็จรับเงินขายของเก่า", second, StringComparison.Ordinal);
        Assert.Contains("รายการ20", first, StringComparison.Ordinal);
        Assert.DoesNotContain("รายการ21", first, StringComparison.Ordinal);
        Assert.Contains("รายการ25", second, StringComparison.Ordinal);
        Assert.DoesNotContain("รวมทั้งหมด", first, StringComparison.Ordinal);
        Assert.DoesNotContain("ผู้รับของ", first, StringComparison.Ordinal);
        Assert.Contains("250.00", second, StringComparison.Ordinal);
        Assert.Contains("ผู้อนุมัติ", second, StringComparison.Ordinal);
    }

    [Theory] // D-1: a long address is carried to the next line at a blank instead of running into "โทรฯ"
    [InlineData("39/50 หมู่ 4 ซอยตัวอย่าง แขวงตัวอย่าง", 1)]
    [InlineData("39/50 หมู่ 4 ซอยตัวอย่างหนึ่ง แขวงตัวอย่างสอง เขตตัวอย่างสาม จังหวัดตัวอย่างสี่ 00000", 2)]
    [InlineData(null, 0)]
    public void A_long_address_is_carried_to_a_second_line(string? address, int lines)
        => Assert.Equal(lines, ScrapReceiptForm.AddressLinesOf(address).Count);

    [Fact]
    public void An_address_is_cut_only_at_blanks_unless_one_word_is_longer_than_a_line()
    {
        var wrapped = ScrapReceiptForm.AddressLinesOf("ที่อยู่ตัวอย่าง " + string.Join(" ", Enumerable.Repeat("หมู่ที่หนึ่ง", 8)));
        Assert.All(wrapped, l => Assert.True(l.Length <= ScrapReceiptForm.AddressChars));
        Assert.Equal(3, wrapped.Count);

        var oneWord = ScrapReceiptForm.AddressLinesOf(new string('ก', ScrapReceiptForm.AddressChars * 2 + 5));
        Assert.Equal(3, oneWord.Count);
        Assert.Equal(new string('ก', ScrapReceiptForm.AddressChars), oneWord[0]);
    }

    [Fact]
    public void A_wrapped_address_moves_the_table_down_and_the_second_line_prints()
    {
        var address = "39/50 หมู่ 4 ซอยตัวอย่างหนึ่ง แขวงตัวอย่างสอง เขตตัวอย่างสาม จังหวัดตัวอย่างสี่ 00000";
        var svg = Svg(Assert.Single(ScrapReceiptForm.Layout(Receipt([Line(1, "ก", 1, 1, 1)], address: address))));
        foreach (var piece in ScrapReceiptForm.AddressLinesOf(address)) Assert.Contains(piece, svg, StringComparison.Ordinal);
    }
}

/// <summary>ใบเสร็จรับเงินขายของเก่า through Chromium (#238): Letter portrait, one page a sheet, TH Sarabun New embedded.</summary>
[Collection(ChromiumCollection.Name)]
public sealed class CashScrapReceiptPdfTests(ChromiumFixture chromium)
{
    private static string Text(byte[] pdf) => System.Text.Encoding.Latin1.GetString(pdf);
    private static int Pages(byte[] pdf) => System.Text.RegularExpressions.Regex.Matches(Text(pdf), @"/Type /Page\b").Count;

    private Task<byte[]> PrintAsync(ScrapReceiptDto receipt)
    {
        var pages = ScrapReceiptForm.Layout(receipt);
        return chromium.Renderer.RenderAsync(FormPageHtml.Compose(ScrapReceiptForm.Title, pages), pages[0].Paper);
    }

    [Fact]
    public async Task A_receipt_is_one_Letter_page_and_twenty_five_lines_are_two()
    {
        var pdf = await PrintAsync(CashScrapReceiptFormTests.Sample());
        Assert.Equal(1, Pages(pdf));
        var box = System.Text.RegularExpressions.Regex.Match(Text(pdf), @"/MediaBox \[0 0 ([\d.]+) ([\d.]+)\]");
        Assert.Equal((612, 792), (Math.Round(double.Parse(box.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)), Math.Round(double.Parse(box.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture))));
        Assert.Contains("THSarabunNew", Text(pdf));
        Assert.Contains("/FontFile2", Text(pdf));

        var lines = Enumerable.Range(1, 25).Select(i => new ScrapReceiptLineDto(i, i, $"รายการ{i}", null, "กก.", 1, 10, 10)).ToList();
        Assert.Equal(2, Pages(await PrintAsync(CashScrapReceiptFormTests.Receipt(lines))));
    }

    /// <summary>
    /// ตรวจรับระดับ A (#40): with env <c>LOCKTE_FORM_OVERLAY_DIR</c> set, prints the receipt to <c>receipt.pdf</c> there for <c>tools/form-overlay/compare.py</c> against
    /// <c>ขายของเก่า.pdf</c>: from <c>receipt.json</c> in the folder when it is there (the data of the sample, real, kept outside git), else the fictional sample receipt. Does nothing otherwise.
    /// </summary>
    [Fact]
    public async Task Prints_the_overlay_check_pdf_when_asked()
    {
        var dir = Environment.GetEnvironmentVariable("LOCKTE_FORM_OVERLAY_DIR");
        if (string.IsNullOrEmpty(dir)) return;
        var file = Path.Combine(dir, "receipt.json");
        var receipt = File.Exists(file)
            ? JsonSerializer.Deserialize<ScrapReceiptDto>(await File.ReadAllTextAsync(file), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!
            : CashScrapReceiptFormTests.Sample();
        await File.WriteAllBytesAsync(Path.Combine(dir, "receipt.pdf"), await PrintAsync(receipt));
    }
}
