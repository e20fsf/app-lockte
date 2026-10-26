using System.Globalization;
using Lockte.Sales;
using Lockte.SharedKernel;

namespace Lockte.Web.Reports.Order;

/// <summary>
/// RPT-01 ใบ SALE ORDER (report <c>ใบ sale order</c>, #110), ระดับ A (#40 ตรงแบบเดิม): every text, rule and box is where the
/// legacy report printed it — positions in mm (baseline for texts) measured on <c>lot 2\ใบ sale order.pdf</c>. Changes from
/// the legacy, all decided (#28, #40): A4 (the legacy was Letter, D-7 — the form uses 11–192 mm of the width, so nothing
/// moves); TH Sarabun New at Angsana New's point sizes; เลขเอกสารควบคุม from <see cref="DocumentNumberService"/>; a long
/// name or remark is squeezed into its line instead of cut (D-1). The legacy sample has one line, so the pitch of further
/// lines (6.47 mm: header rule to first row rule) is taken from the rules themselves; more than <see cref="RowsPerPage"/>
/// lines go on further sheets with the head repeated and the total, ลงนาม and หมายเหตุ on the last.
/// </summary>
public static class SaleOrderForm
{
    public const string Title = "SALE ORDER";

    /// <summary>The form is designed for half a sheet: the signatures sit at 134 mm, so 11 lines fit above the total.</summary>
    public const int RowsPerPage = 11;

    /// <summary>Distance from one row rule to the next.</summary>
    public const double RowPitch = 6.47;

    // The table: x of the column rules, y of the rule under the headings and the baseline of the first line.
    private static readonly double[] Columns = [11.44, 20.40, 38.285, 107.86, 128.755, 140.67, 160.54, 191.405];
    private const double TableTop = 37.275;
    private const double HeaderBottom = 45.265;
    private const double FirstBaseline = 51.2;
    private const double Left = 11.44;
    private const double Right = 191.405;

    /// <summary>The printed sheets of one order: its ค้างส่ง lines in order, <see cref="SalesOrderPrint.Total"/> under the last.</summary>
    public static IReadOnlyList<FormPage> Layout(SalesOrderPrint order, string? documentNumber)
    {
        var chunks = order.Lines.Chunk(RowsPerPage).ToList();
        var pages = new List<FormPage>();
        for (var i = 0; i < chunks.Count; i++)
        {
            var last = i == chunks.Count - 1;
            var page = Frame(documentNumber, chunks[i].Length);
            Head(page, order);
            for (var r = 0; r < chunks[i].Length; r++) Row(page, chunks[i][r], r);
            if (last)
            {
                Total(page, chunks[i].Length, order.Total);
                Signatures(page, order.SalesTerritoryCode);
                Remark(page, order.Remark);
            }
            pages.Add(page);
        }
        return pages;
    }

    /// <summary>พิมพ์ฟอร์มเปล่า (report <c>ใบ sale order1</c>): the frame with the empty rows ruled, nothing filled in.</summary>
    public static FormPage Blank(string? documentNumber)
    {
        var page = Frame(documentNumber, RowsPerPage);
        page.Text(10.7, 24.9, "เลขที่", 16);
        page.Text(165.1, 24.9, "วันที่", 16);
        page.Text(10.8, 32.9, "รหัสลูกค้า", 16).Text(47.3, 32.9, "ชื่อลูกค้า", 16).Text(143.1, 32.9, "เงื่อนไขการชำระเงิน", 16);
        page.Line(172.78, 25.24, 189.61, 25.24, 0.41);
        page.Line(27.31, 34.265, 46.27, 34.265, 0.35).Line(60.96, 34.265, 139.65, 34.265, 0.35).Line(174.69, 34.265, 190.39, 34.265, 0.35);
        Total(page, RowsPerPage, null);
        Signatures(page, null);
        Remark(page, null);
        return page;
    }

    /// <summary>Title, เลขเอกสารควบคุม, the table's rules and headings, with room for <paramref name="rows"/> lines.</summary>
    private static FormPage Frame(string? documentNumber, int rows)
    {
        var page = new FormPage(Paper.A4Portrait);
        page.Text(97.9, 15.5, "LOCKTE CO.,LTD", 25.9, TextAnchor.Middle, bold: true);
        page.Text(98.0, 23.8, Title, 23.9, TextAnchor.Middle, bold: true);
        page.Text(191.1, 11.9, documentNumber, 16, TextAnchor.End);

        var bottom = HeaderBottom + rows * RowPitch;
        page.Line(Left, TableTop, Right, TableTop).Line(Left, HeaderBottom, Right, HeaderBottom);
        for (var r = 1; r <= rows; r++) page.Line(Left, HeaderBottom + r * RowPitch + 0.0, Right, HeaderBottom + r * RowPitch);
        foreach (var x in Columns)
        {
            page.Line(x, TableTop, x, HeaderBottom);
            page.Line(x, HeaderBottom, x, bottom);
        }
        page.Text(21.5, 42.9, "รหัสสินค้า", 16).Text(39.6, 42.9, "ชื่อสินค้า", 16);
        page.Text(117.2, 42.9, "จำนวน", 16).Text(130.3, 42.9, "หน่วย", 16).Text(142.3, 42.9, "ราคา/หน่วย", 16).Text(173.5, 42.9, "จำนวนเงิน", 16);
        return page;
    }

    private static void Head(FormPage page, SalesOrderPrint order)
    {
        page.Text(10.7, 24.9, "เลขที่", 16);
        page.Text(21.7, 24.5, order.OrderNo, 19.9, bold: true, fit: 40);
        page.Text(165.1, 24.9, "วันที่", 16);
        page.Text(172.8, 24.9, ThaiDate.Format(order.OrderDate), 16, fit: 17);
        page.Line(172.78, 25.24, 189.61, 25.24, 0.41);

        page.Text(10.8, 32.9, "รหัสลูกค้า", 16).Text(27.6, 32.9, order.CustomerCode, 16, fit: 46.27 - 27.6);
        page.Text(47.3, 32.9, "ชื่อลูกค้า", 16).Text(62.4, 32.9, order.CustomerName, 16, fit: 139.65 - 62.4);
        page.Text(143.1, 32.9, "เงื่อนไขการชำระเงิน", 16).Text(174.6, 32.9, order.PaymentTerms, 16, fit: 190.39 - 174.6);
        page.Line(27.31, 34.265, 46.27, 34.265, 0.35).Line(60.96, 34.265, 139.65, 34.265, 0.35).Line(174.69, 34.265, 190.39, 34.265, 0.35);
    }

    private static void Row(FormPage page, SalesOrderPrintLine line, int index)
    {
        var baseline = FirstBaseline + index * RowPitch;
        page.Text(19.1, baseline, line.LineNo.ToString(CultureInfo.InvariantCulture), 16, TextAnchor.End);
        page.Text(21.7, baseline, line.ProductCode, 16, fit: 38.285 - 21.7 - 0.6);
        page.Text(39.7, baseline, line.ProductName, 16, fit: 107.86 - 39.7 - 1.2);
        page.Text(126.8, baseline, Quantity(line.Quantity), 16, TextAnchor.End);
        page.Text(138.4, baseline - 0.7, line.Unit, 14, TextAnchor.Middle, fit: 140.67 - 128.755 - 1);
        page.Text(159.4, baseline, line.UnitPrice is { } price ? ReportText.Decimal2(price) : null, 16, TextAnchor.End);
        page.Text(189.3, baseline, line.Amount is { } amount ? ReportText.Decimal2(amount) : null, 16, TextAnchor.End);
    }

    /// <summary>"รวมทั้งหมด" and Σ จำนวนเงิน in the box under the amount column, with the double rule below (blank form: the empty box).</summary>
    private static void Total(FormPage page, int rows, decimal? total)
    {
        var rowsBottom = HeaderBottom + rows * RowPitch;
        page.Line(Columns[6], rowsBottom, Columns[6], rowsBottom + 7.235).Line(Columns[7], rowsBottom, Columns[7], rowsBottom + 7.235);
        page.Line(Columns[6], rowsBottom + 6.425, Columns[7], rowsBottom + 6.425).Line(Columns[6], rowsBottom + 7.23, Columns[7], rowsBottom + 7.23);
        if (total is null) return;
        page.Text(141.9, rowsBottom + 5.165, "รวมทั้งหมด", 14);
        page.Text(189.3, rowsBottom + 5.865, ReportText.Decimal2(total.Value), 16, TextAnchor.End);
    }

    /// <summary>ผู้เสนอ (the เขตการขาย code) / ผู้ตรวจสอบ / ผู้อนุมัติ, signed on the lines.</summary>
    private static void Signatures(FormPage page, string? territoryCode)
    {
        const double y = 134.2;
        page.Text(10.7, y, "ผู้เสนอ", 16).Text(23.7, y, territoryCode, 16, fit: 71.05 - 23.7);
        page.Text(73.2, y, "ผู้ตรวจสอบ", 16);
        page.Text(143.0, y, "ผู้อนุมัติ", 16);
        page.Line(21.33, 134.58, 71.05, 134.58, 0.35).Line(90.64, 134.58, 141.45, 134.58, 0.35).Line(154.77, 134.58, 191.32, 134.58, 0.35);
    }

    /// <summary>"*หมายเหตุ" (bold, underlined) and the order's หมายเหตุ — ชื่อ + ที่อยู่ส่ง — on its line.</summary>
    private static void Remark(FormPage page, string? remark)
    {
        page.Text(10.8, 143.0, "*หมายเหตุ", 17.9, bold: true);
        page.Line(10.82, 143.35, 29.34, 143.35, 0.45);
        page.Text(31.7, 143.3, remark, 16, fit: 191.37 - 31.7);
        page.Line(30.61, 143.595, 191.37, 143.595, 0.35);
    }

    /// <summary>จำนวน as typed (General Number): 50, 12.5 — no thousands separator, no trailing zeros.</summary>
    public static string Quantity(decimal value) => (value / 1.0000000000000000000000000000m).ToString("0.####", CultureInfo.InvariantCulture);
}
