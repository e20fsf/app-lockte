using System.Text.RegularExpressions;
using Lockte.Sales;
using Lockte.Web.Reports;
using Lockte.Web.Reports.Order;

namespace Lockte.Web.Tests;

/// <summary>
/// ใบ SALE ORDER (RPT-01, ระดับ A, #110) as SVG, without Chromium or a database: which values print, where, and in which format.
/// How close the form lies on the legacy print is checked by overlaying the sample (#41: คลาด ≤ ~2 มม. + ผู้ใช้ฝ่ายขายเซ็นรับ), not here.
/// The positions are the ones measured on the legacy sample. Fictional data only.
/// </summary>
public sealed class SaleOrderFormTests
{
    private const string DocumentNumber = "PC-SA001-1/2547 ฉบับที่ 2";

    private static SalesOrderPrint Order(int lines, string? remark = "ร้านสมมุติ 1 หมู่ 2 จังหวัดสมมุติ")
        => new(1, "05-69-0001", new DateOnly(2026, 10, 17), "05-09-001", "ร้านสมมุติ", "30", "05", remark,
            Enumerable.Range(1, lines).Select(i => new SalesOrderPrintLine(i, $"SK{i:000}", $"สินค้าสมมุติ {i}", "คู่", 50, 65m, 3250m)).ToList());

    private static List<(double X, double Y, string Text)> Texts(FormPage page)
        => Regex.Matches(page.ToSvg(), "<text x=\"([\\d.]+)\" y=\"([\\d.]+)\"[^>]*>([^<]*)</text>")
            .Select(m => (double.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture),
                double.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture), System.Net.WebUtility.HtmlDecode(m.Groups[3].Value)))
            .ToList();

    /// <summary>The rules (SVG lines) of a page as x1, y1, x2, y2.</summary>
    private static List<(double X1, double Y1, double X2, double Y2)> Rules(FormPage page)
        => Regex.Matches(page.ToSvg(), "<line x1=\"([\\d.]+)\" y1=\"([\\d.]+)\" x2=\"([\\d.]+)\" y2=\"([\\d.]+)\"")
            .Select(m => (D(m.Groups[1].Value), D(m.Groups[2].Value), D(m.Groups[3].Value), D(m.Groups[4].Value))).ToList();

    private static double D(string text) => double.Parse(text, System.Globalization.CultureInfo.InvariantCulture);

    private static bool Horizontal(FormPage page, double y) => Rules(page).Any(r => Math.Abs(r.Y1 - y) < 0.02 && Math.Abs(r.Y2 - y) < 0.02 && r.X1 < 12 && r.X2 > 191);

    private static bool Vertical(FormPage page, double x) => Rules(page).Any(r => Math.Abs(r.X1 - x) < 0.02 && Math.Abs(r.X2 - x) < 0.02);

    private static string TextAt(FormPage page, double x, double y)
        => Assert.Single(Texts(page), t => Math.Abs(t.X - x) < 0.01 && Math.Abs(t.Y - y) < 0.01).Text;

    [Fact] // the sample's head, one line and the total, each where the legacy printed it
    public void One_line_order_prints_where_the_legacy_sample_has_everything()
    {
        var page = Assert.Single(SaleOrderForm.Layout(Order(1), DocumentNumber));

        Assert.Equal(Paper.A4Portrait, page.Paper);
        Assert.Equal("LOCKTE CO.,LTD", TextAt(page, 97.9, 15.5));
        Assert.Equal("SALE ORDER", TextAt(page, 98.0, 23.8));
        Assert.Equal(DocumentNumber, TextAt(page, 191.1, 11.9));
        Assert.Equal(("เลขที่", "05-69-0001", "วันที่", "17/10/2569"), (TextAt(page, 10.7, 24.9), TextAt(page, 21.7, 24.5), TextAt(page, 165.1, 24.9), TextAt(page, 172.8, 24.9)));
        Assert.Equal(("รหัสลูกค้า", "05-09-001", "ชื่อลูกค้า", "ร้านสมมุติ", "เงื่อนไขการชำระเงิน", "30"),
            (TextAt(page, 10.8, 32.9), TextAt(page, 27.6, 32.9), TextAt(page, 47.3, 32.9), TextAt(page, 62.4, 32.9), TextAt(page, 143.1, 32.9), TextAt(page, 174.6, 32.9)));
        Assert.Equal(("รหัสสินค้า", "ชื่อสินค้า", "จำนวน", "หน่วย", "ราคา/หน่วย", "จำนวนเงิน"),
            (TextAt(page, 21.5, 42.9), TextAt(page, 39.6, 42.9), TextAt(page, 117.2, 42.9), TextAt(page, 130.3, 42.9), TextAt(page, 142.3, 42.9), TextAt(page, 173.5, 42.9)));
        Assert.Equal(("1", "SK001", "สินค้าสมมุติ 1", "50", "คู่", "65.00", "3,250.00"),
            (TextAt(page, 19.1, 51.2), TextAt(page, 21.7, 51.2), TextAt(page, 39.7, 51.2), TextAt(page, 126.8, 51.2), TextAt(page, 138.4, 50.5), TextAt(page, 159.4, 51.2), TextAt(page, 189.3, 51.2)));
        Assert.Equal(("รวมทั้งหมด", "3,250.00"), (TextAt(page, 141.9, 56.9), TextAt(page, 189.3, 57.6))); // 51.735 + 5.165 / 5.865
        Assert.Equal(("ผู้เสนอ", "05", "ผู้ตรวจสอบ", "ผู้อนุมัติ"), (TextAt(page, 10.7, 134.2), TextAt(page, 23.7, 134.2), TextAt(page, 73.2, 134.2), TextAt(page, 143.0, 134.2)));
        Assert.Equal(("*หมายเหตุ", "ร้านสมมุติ 1 หมู่ 2 จังหวัดสมมุติ"), (TextAt(page, 10.8, 143.0), TextAt(page, 31.7, 143.3)));
    }

    [Fact] // the rules: the table's frame and the total box with its double rule, as measured
    public void The_table_frame_and_the_total_box_are_where_the_legacy_rules_are()
    {
        var page = SaleOrderForm.Layout(Order(1), DocumentNumber)[0];

        foreach (var y in new[] { 37.275, 45.265, 51.735 }) Assert.True(Horizontal(page, y), $"rule at y={y}");
        foreach (var x in new[] { 11.44, 20.40, 38.285, 107.86, 128.755, 140.67, 160.54, 191.405 }) Assert.True(Vertical(page, x), $"rule at x={x}");
        // the double rule under the total, over the amount column only
        Assert.Contains(Rules(page), r => Math.Abs(r.Y1 - 58.16) < 0.02 && Math.Abs(r.X1 - 160.54) < 0.02 && Math.Abs(r.X2 - 191.405) < 0.02);
        Assert.Contains(Rules(page), r => Math.Abs(r.Y1 - 58.965) < 0.02 && Math.Abs(r.X1 - 160.54) < 0.02 && Math.Abs(r.X2 - 191.405) < 0.02);
    }

    [Fact] // lines of a longer order follow at the row pitch, and the total sits under the last of them
    public void More_lines_follow_at_the_row_pitch_and_the_total_moves_down()
    {
        var page = Assert.Single(SaleOrderForm.Layout(Order(4), DocumentNumber));

        Assert.Equal(["SK001", "SK002", "SK003", "SK004"], Texts(page).Where(t => t.Text.StartsWith("SK")).OrderBy(t => t.Y).Select(t => t.Text));
        Assert.Equal(51.2 + 3 * SaleOrderForm.RowPitch, Texts(page).Single(t => t.Text == "SK004").Y, 2);
        Assert.Equal("13,000.00", TextAt(page, 189.3, 51.735 + 3 * SaleOrderForm.RowPitch + 5.865 + 0.0)); // Σ 4 × 3,250
    }

    [Fact] // more than a sheet's lines: the head repeats, the total and the signatures are on the last sheet only
    public void Lines_beyond_a_sheet_go_on_the_next_with_the_total_on_the_last()
    {
        var pages = SaleOrderForm.Layout(Order(SaleOrderForm.RowsPerPage + 3), DocumentNumber);

        Assert.Equal(2, pages.Count);
        Assert.All(pages, p => Assert.Contains(Texts(p), t => t.Text == "05-69-0001"));
        Assert.DoesNotContain(Texts(pages[0]), t => t.Text == "รวมทั้งหมด");
        Assert.DoesNotContain(Texts(pages[0]), t => t.Text == "ผู้เสนอ");
        Assert.Contains(Texts(pages[1]), t => t.Text == "รวมทั้งหมด");
        Assert.Contains(Texts(pages[1]), t => t.Text == "ผู้เสนอ");
        Assert.Equal((SaleOrderForm.RowsPerPage + 3) * 3250m, Order(SaleOrderForm.RowsPerPage + 3).Total);
        Assert.Equal("45,500.00", Texts(pages[1]).Where(t => t.X == 189.3 && t.Text.EndsWith(".00")).MaxBy(t => t.Y).Text); // the total is the lowest amount
    }

    [Fact] // D-1: a long name or remark is squeezed into its line, not cut — and the page text keeps the whole of it
    public void Long_text_is_kept_whole_and_fitted_to_its_line()
    {
        var longName = string.Concat(Enumerable.Repeat("ผ้าเบรกสกินแพ็คสมมุติ ", 6)).Trim();
        var order = Order(1, remark: string.Concat(Enumerable.Repeat("ร้านสมมุติ ที่อยู่สมมุติ ", 12)).Trim()) with
        {
            Lines = [new SalesOrderPrintLine(1, "SK001", longName, "คู่", 12.5m, 65m, 812.5m)],
        };

        var svg = SaleOrderForm.Layout(order, DocumentNumber)[0].ToSvg();
        var decoded = System.Net.WebUtility.HtmlDecode(svg);

        Assert.Contains(longName, decoded);
        Assert.Contains("data-fit=\"66.96\"", svg); // ชื่อสินค้า column, squeezed to it at print time
        Assert.Contains("data-fit=\"159.67\"", svg); // หมายเหตุ
        Assert.Contains("12.5", decoded);
    }

    [Fact] // พิมพ์ฟอร์มเปล่า: the frame, the labels and the rules, nothing filled in
    public void The_blank_form_has_the_frame_and_no_data()
    {
        var page = SaleOrderForm.Blank(DocumentNumber);
        var texts = Texts(page).Select(t => t.Text).ToList();

        Assert.Contains("SALE ORDER", texts);
        Assert.Contains(DocumentNumber, texts);
        Assert.Contains("รหัสลูกค้า", texts);
        Assert.Contains("ผู้อนุมัติ", texts);
        Assert.Contains("*หมายเหตุ", texts);
        Assert.DoesNotContain("รวมทั้งหมด", texts);
        // the rules under the 11 rows, and the table's top and heading rule
        Assert.Equal(11 + 2, Rules(page).Count(r => Math.Abs(r.Y1 - r.Y2) < 0.001 && Math.Abs(r.X1 - 11.44) < 0.02 && r.X2 > 191));
    }

    [Fact]
    public void Quantity_prints_as_typed()
    {
        Assert.Equal("50", SaleOrderForm.Quantity(50.0000m));
        Assert.Equal("12.5", SaleOrderForm.Quantity(12.5000m));
        Assert.Equal("1000", SaleOrderForm.Quantity(1000m));
        Assert.Equal("0.25", SaleOrderForm.Quantity(0.25m));
    }
}
