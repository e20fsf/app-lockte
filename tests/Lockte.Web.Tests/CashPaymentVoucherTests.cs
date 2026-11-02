using Lockte.Cash;
using Lockte.SharedKernel;
using Lockte.Web.Reports;
using Lockte.Web.Reports.Cash;

namespace Lockte.Web.Tests;

/// <summary>ใบสำคัญจ่ายเงิน RPT-01 (#234, ระดับ A) without a database: the layout of the sheets, the head, the total and its words.</summary>
public sealed class CashPaymentVoucherTests
{
    private static ExpenseLineDto Line(int id, string name, decimal qty, decimal price, decimal amount, string voucher = "09/30", int day = 9, string? unit = "ตัว")
        => new(id, new DateOnly(2026, 9, day), voucher, name, qty, unit, price, amount, 1, "ประเภทตัวอย่าง", [1]);

    private static string Svg(FormPage page) => System.Net.WebUtility.HtmlDecode(page.ToSvg());

    [Fact] // BR-005 #1, AC-06: three lines of 09/30 (30 + 50 + 60)
    public void AC06_three_lines_make_one_voucher_with_the_total_and_its_words()
    {
        var pages = PaymentVoucherForm.Layout([Line(1, "โอริง 3.5X27", 2, 15, 30), Line(2, "โอริง 3X56", 2, 25, 50), Line(3, "โอริง 3.5X62", 2, 30, 60)]);

        var page = Assert.Single(pages);
        Assert.Equal(Paper.A5Landscape, page.Paper);
        var svg = Svg(page);
        foreach (var text in new[] { "บริษัท ล็อคเต้ จำกัด", "ใบสำคัญจ่ายเงิน", "PAYMENT VOUCHER", "จ่ายโดย", "เงินสด", "09/30", "โอริง 3.5X27", "30.00", "50.00", "60.00", "140.00",
                     "(หนึ่งร้อยสี่สิบบาทถ้วน)", "รวมเงิน", "ผู้อนุมัติ", "ผู้จ่ายเงิน", "สมุห์บัญชี", "ผู้รับเงิน" })
            Assert.Contains(text, svg, StringComparison.Ordinal);
        Assert.DoesNotContain("แผ่นที่", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void The_head_is_the_first_lines_number_and_date_and_the_amounts_carry_two_decimals()
    {
        var svg = Svg(Assert.Single(PaymentVoucherForm.Layout([Line(1, "ก", 1.5m, 1234.5m, 1851.75m, "09/31", 11)])));
        Assert.Contains("09/31", svg, StringComparison.Ordinal);
        Assert.Contains(ThaiDate.Format(new DateOnly(2026, 9, 11)), svg, StringComparison.Ordinal);
        Assert.Contains("1.5", svg, StringComparison.Ordinal);
        Assert.Contains("1,234.50", svg, StringComparison.Ordinal);
        Assert.Contains("1,851.75", svg, StringComparison.Ordinal);
    }

    [Fact] // assumption ข, Q-31: nine lines are two sheets of seven rows; the total and its words only on the last
    public void More_than_seven_lines_print_as_sheets_in_turn_with_the_total_on_the_last()
    {
        var lines = Enumerable.Range(1, 9).Select(i => Line(i, $"รายการ{i}", 1, 10, 10)).ToList();
        var pages = PaymentVoucherForm.Layout(lines);

        Assert.Equal(2, pages.Count);
        var (first, second) = (Svg(pages[0]), Svg(pages[1]));
        Assert.Contains("แผ่นที่ 1/2", first, StringComparison.Ordinal);
        Assert.Contains("แผ่นที่ 2/2", second, StringComparison.Ordinal);
        Assert.Contains("รายการ7", first, StringComparison.Ordinal);
        Assert.DoesNotContain("รายการ8", first, StringComparison.Ordinal);
        Assert.Contains("รายการ9", second, StringComparison.Ordinal);
        Assert.DoesNotContain("รวมเงิน", first, StringComparison.Ordinal);
        Assert.DoesNotContain("บาทถ้วน", first, StringComparison.Ordinal);
        Assert.Contains("90.00", second, StringComparison.Ordinal);
        Assert.Contains("(เก้าสิบบาทถ้วน)", second, StringComparison.Ordinal);
        Assert.Contains(">8<", second, StringComparison.Ordinal); // the numbering goes on across the sheets
    }

    [Fact] // AC-21: lines of two numbers (or two days) → one voucher headed by the first, with a warning on the screen
    public void Lines_of_more_than_one_number_or_day_are_found_so_the_screen_can_warn()
    {
        Assert.False(PaymentVoucherForm.MixesVouchers([Line(1, "ก", 1, 1, 1), Line(2, "ข", 1, 1, 1)]));
        Assert.True(PaymentVoucherForm.MixesVouchers([Line(1, "ก", 1, 1, 1), Line(2, "ข", 1, 1, 1, voucher: "09/31")]));
        Assert.True(PaymentVoucherForm.MixesVouchers([Line(1, "ก", 1, 1, 1), Line(2, "ข", 1, 1, 1, day: 10)]));
        var svg = Svg(Assert.Single(PaymentVoucherForm.Layout([Line(1, "ก", 1, 1, 1), Line(2, "ข", 1, 1, 1, voucher: "09/31", day: 10)])));
        Assert.Contains("09/30", svg, StringComparison.Ordinal);
        Assert.DoesNotContain("09/31", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void A_voucher_of_no_lines_is_refused()
        => Assert.Throws<ArgumentException>(() => PaymentVoucherForm.Layout([]));

    [Theory] // AC-12 (RND-C): the words of an amount
    [InlineData("0.50", "(ห้าสิบสตางค์)")]
    [InlineData("21", "(ยี่สิบเอ็ดบาทถ้วน)")]
    [InlineData("101", "(หนึ่งร้อยหนึ่งบาทถ้วน)")]
    [InlineData("1000001", "(หนึ่งล้านหนึ่งบาทถ้วน)")]
    [InlineData("140", "(หนึ่งร้อยสี่สิบบาทถ้วน)")]
    public void The_total_is_written_in_thai_words(string amount, string words)
        => Assert.Equal(words, PaymentVoucherForm.TotalInWords(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture)));

    [Fact] // AC-12: 1,234.567 is cut, not rounded, to 56 satang
    public void An_amount_with_more_than_two_decimals_is_cut_to_satang_by_the_legacy_rule()
        => Assert.Equal("หนึ่งพันสองร้อยสามสิบสี่บาทห้าสิบหกสตางค์", ThaiNumber.Baht(1234.567));
}

/// <summary>ใบสำคัญจ่ายเงิน through Chromium (#234): A5 landscape, one page a sheet, TH Sarabun New embedded.</summary>
[Collection(ChromiumCollection.Name)]
public sealed class CashPaymentVoucherPdfTests(ChromiumFixture chromium)
{
    private static string Text(byte[] pdf) => System.Text.Encoding.Latin1.GetString(pdf);
    private static int Pages(byte[] pdf) => System.Text.RegularExpressions.Regex.Matches(Text(pdf), @"/Type /Page\b").Count;

    private static ExpenseLineDto Line(int id, string name, decimal amount)
        => new(id, new DateOnly(2026, 9, 9), "09/30", name, 2, "ตัว", amount / 2, amount, 1, "ประเภทตัวอย่าง", [1]);

    private Task<byte[]> PrintAsync(IReadOnlyList<ExpenseLineDto> lines)
    {
        var pages = PaymentVoucherForm.Layout(lines);
        return chromium.Renderer.RenderAsync(FormPageHtml.Compose(PaymentVoucherForm.Title, pages), pages[0].Paper);
    }

    [Fact]
    public async Task A_voucher_is_one_A5_landscape_page_and_nine_lines_are_two()
    {
        var one = await PrintAsync([Line(1, "โอริง 3.5X27", 30), Line(2, "โอริง 3X56", 50), Line(3, "โอริง 3.5X62", 60)]);
        Assert.Equal(1, Pages(one));
        var box = System.Text.RegularExpressions.Regex.Match(Text(one), @"/MediaBox \[0 0 ([\d.]+) ([\d.]+)\]");
        Assert.Equal((595, 420), (Math.Round(double.Parse(box.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)), Math.Round(double.Parse(box.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture))));
        Assert.Contains("THSarabunNew", Text(one));
        Assert.Contains("/FontFile2", Text(one));

        Assert.Equal(2, Pages(await PrintAsync(Enumerable.Range(1, 9).Select(i => Line(i, $"รายการ{i}", 10)).ToList())));
    }

    /// <summary>
    /// ตรวจรับระดับ A (#40): with env <c>LOCKTE_FORM_OVERLAY_DIR</c> set, prints the voucher of the sample's AC-06 data (fictional) to <c>voucher.pdf</c> there, for
    /// <c>tools/form-overlay/compare.py</c> against <c>พิมพ์ใบสำคัญจ่าย1.pdf</c>. Does nothing otherwise.
    /// </summary>
    [Fact]
    public async Task Prints_the_overlay_check_pdf_when_asked()
    {
        var dir = Environment.GetEnvironmentVariable("LOCKTE_FORM_OVERLAY_DIR");
        if (string.IsNullOrEmpty(dir)) return;
        await File.WriteAllBytesAsync(Path.Combine(dir, "voucher.pdf"), await PrintAsync([Line(1, "โอริง 3.5X27", 30), Line(2, "โอริง 3X56", 50), Line(3, "โอริง 3.5X62", 60)]));
        await File.WriteAllBytesAsync(Path.Combine(dir, "voucher-empty-row.pdf"), await PrintAsync(Enumerable.Range(1, 7).Select(i => Line(i, $"รายการ{i}", 10)).ToList()));
    }
}
