using System.Globalization;
using Lockte.Purch;
using Lockte.SharedKernel;

namespace Lockte.Web.Reports.Purch;

/// <summary>
/// ข้อความหัวกระดาษบริษัทของใบสั่งซื้อ (section <c>PurchLetterhead</c>): the two lines under the company name (office and factory addresses, Thai and English).
/// They are settings, not code — the repository is public, so the real address and phone numbers live only in the server's settings; blank prints nothing.
/// </summary>
public sealed class PurchLetterheadOptions
{
    public const string Section = "PurchLetterhead";

    public string Line1 { get; set; } = "";
    public string Line2 { get; set; } = "";
}

public sealed record PurchaseOrderPrintLine(int LineNo, string? RequestNo, string Name, decimal Quantity, string? Unit, decimal UnitPrice, decimal Amount);

/// <summary>An order as RPT-02 prints it: the supplier as written on the order, the lines chosen to print and the totals of those lines (BR-022).</summary>
public sealed record PurchaseOrderPrint(
    string OrderNo,
    DateOnly OrderDate,
    string SupplierName,
    string? Address1,
    string? Address2,
    string? Tel,
    string? Fax,
    string? DepartmentName,
    DateOnly? DeliveryDate,
    int? CreditDays,
    IReadOnlyList<PurchaseOrderPrintLine> Lines,
    decimal Subtotal,
    decimal Tax,
    decimal Total,
    string TotalInWords,
    string? Reason,
    string? Note);

/// <summary>
/// RPT-02 ใบสั่งซื้อ (Purchase Order, <c>พิมพ์ใบสั่งซื้อ1</c>…<c>10</c>, PC-PU002-2/2547 ฉบับที่ 1, #224), ระดับ A (#40 ตรงแบบเดิม): every text, rule and box is where the
/// legacy report printed it — positions in mm (baseline for texts) measured on <c>lot 3\วัตถุดิบ\เอกสาร\พิมพ์ใบสั่งซื้อ1.pdf</c>. One layout for any number of lines
/// (the legacy had ten): <see cref="RowsPerPage"/> lines to a sheet, the head repeated, and the totals, remark and sign boxes on the last. Changes from the legacy:
/// A4 as it was; TH Sarabun New; the document number from <see cref="DocumentNumberService"/>; the company address from <see cref="PurchLetterheadOptions"/>; a long
/// text is squeezed into its cell instead of cut (D-1); the amount in words of an empty form is blank instead of <c>#Error</c> (D-3). Everything else, the wording of the
/// notice and "Apporved by" included, is kept.
/// </summary>
public static class PurchaseOrderForm
{
    public const string Title = "ใบสั่งซื้อ (Purchase Order)";

    /// <summary>The table has 11 rows in the legacy: ten lines and the "รวมรายการ" row.</summary>
    public const int RowsPerPage = 10;

    private static readonly double[] RowRules = [94.0, 101.28, 108.51, 115.5, 122.48, 129.47, 136.45, 143.44, 150.47, 157.5, 164.48, 171.47];
    private static readonly double[] Columns = [11.9, 21.96, 42.05, 129.52, 147.31, 159.13, 179.9, 198.54];
    private const double Left = 11.9;
    private const double Right = 198.54;
    private const double W = 0.35;

    public static IReadOnlyList<FormPage> Layout(PurchaseOrderPrint order, string? documentNumber, PurchLetterheadOptions letterhead)
    {
        var chunks = order.Lines.Chunk(RowsPerPage).DefaultIfEmpty([]).ToList();
        var pages = new List<FormPage>();
        for (var i = 0; i < chunks.Count; i++)
        {
            var last = i == chunks.Count - 1;
            var page = Frame(documentNumber, letterhead);
            Head(page, order);
            for (var r = 0; r < chunks[i].Length; r++) Row(page, chunks[i][r], r);
            if (last)
            {
                page.Text(47.75, 169.45, "รวมรายการ", 13.9).Text(63.55, 169.45, "(Total Descriptions)", 13.9);
                page.Text(95.19, 169.45, order.Lines.Count.ToString(CultureInfo.InvariantCulture), 13.9, TextAnchor.Middle).Text(100.11, 169.45, "รายการ", 13.9);
                Lower(page, order);
            }
            pages.Add(page);
        }
        return pages;
    }

    /// <summary>The empty form (the legacy's sample): the frame and the labels, nothing filled in.</summary>
    public static FormPage Blank(string? documentNumber, PurchLetterheadOptions letterhead)
    {
        var page = Frame(documentNumber, letterhead);
        Head(page, null);
        page.Text(47.75, 169.45, "รวมรายการ", 13.9).Text(63.55, 169.45, "(Total Descriptions)", 13.9).Text(95.19, 169.45, "0", 13.9, TextAnchor.Middle).Text(100.11, 169.45, "รายการ", 13.9);
        Lower(page, null);
        return page;
    }

    /// <summary>Company head, title, document number and the rules and headings of the head box and the table.</summary>
    private static FormPage Frame(string? documentNumber, PurchLetterheadOptions letterhead)
    {
        var page = new FormPage(Paper.A4Portrait);
        page.Text(102.54, 17.12, "บริษัท ล็อคเต้ จำกัด (LOCKTE CO.,LTD.)", 19.9, TextAnchor.Middle, bold: true);
        page.Text(102.9, 25.28, letterhead.Line1, 11.9, TextAnchor.Middle, fit: 176);
        page.Text(102.9, 31.28, letterhead.Line2, 11.9, TextAnchor.Middle, fit: 176);
        page.Text(102.4, 45.11, "ใบสั่งซื้อ", 19.9, TextAnchor.Middle, bold: true);
        page.Text(102.4, 53.41, "(Purchase Order)", 17.9, TextAnchor.Middle, bold: true);
        page.Text(13.14, 52.94, "วันที่", 16.9);
        page.Text(197.68, 45.27, documentNumber, 14.9, TextAnchor.End);
        page.Text(162.37, 53.28, "เลขที่", 14.9);
        page.Box(148.03, 46.5, 8.27, 8.34, W);

        // the head box: ถึง / ที่อยู่ on the left, แผนก / วันที่ส่ง / เครดิต on the right
        page.Line(Left, 54.97, Right, 54.97, W).Line(Left, 63.52, Right, 63.52, W).Line(Left, 80.52, Right, 80.52, W).Line(129.52, 72.0, Right, 72.0, W);
        page.Line(Left, 54.97, Left, 94.0, W).Line(129.52, 54.97, 129.52, 94.0, W).Line(Right, 54.97, Right, 94.0, W);
        page.Text(13.12, 60.93, "ถึง(To)", 13.9).Text(13.14, 68.28, "ที่อยู่(Address)", 11.9);
        page.Text(30.94, 79.36, "โทรฯ", 11.9).Text(69.48, 79.29, "FAX:", 11.9);
        page.Text(131.32, 60.93, "แผนกที่ขอซื้อ (Department) :", 13.9).Text(130.75, 69.94, "วันที่ส่งสินค้า (Delivery Date) :", 13.9);
        page.Text(130.75, 77.93, "กำหนดการชำระเงิน (Term) :", 13.9).Text(182.18, 77.97, "วัน", 13.9);

        // the table: headings in two languages, then ruled rows
        for (var c = 1; c < Columns.Length - 1; c++) page.Line(Columns[c], 80.52, Columns[c], RowRules[^1], W);
        page.Line(Left, 94.0, Right, 94.0, W);
        foreach (var rule in RowRules.Skip(1)) page.Line(Left, rule, Right, rule, W);
        page.Line(Left, 94.0, Left, RowRules[^1], W).Line(Right, 94.0, Right, RowRules[^1], W).Line(129.52, 94.0, 129.52, RowRules[^1], W);
        (double X, string Thai, string English)[] headings =
        [
            (17.2, "ลำดับ", "(No.)"), (31.5, "เลขที่ใบขอซื้อ", "(P/R No.)"), (82.5, "รายละเอียด", "(Description)"), (138.4, "จำนวน", "(Quantity)"),
            (153.3, "หน่วย", "(Unit)"), (169.6, "ราคาต่อหน่วย", "(Unit Price)"), (188.9, "จำนวนเงิน", "(Amount)"),
        ];
        foreach (var (x, thai, english) in headings)
            page.Text(x, 86.06, thai, 13.9, TextAnchor.Middle, bold: true).Text(x, 92.06, english, 13.9, TextAnchor.Middle, bold: true);
        return page;
    }

    private static void Head(FormPage page, PurchaseOrderPrint? order)
    {
        if (order is null) return;
        page.Text(22.5, 52.94, ThaiDate.Format(order.OrderDate), 16.9, fit: 36);
        page.Text(171.8, 53.28, order.OrderNo, 14.9, bold: true, fit: 26);
        page.Text(24.0, 60.93, order.SupplierName, 13.9, fit: 103);
        page.Text(30.5, 68.28, order.Address1, 11.9, fit: 97).Text(30.5, 74.2, order.Address2, 11.9, fit: 97);
        page.Text(38.5, 79.36, order.Tel, 11.9, fit: 28).Text(77.0, 79.29, order.Fax, 11.9, fit: 50);
        page.Text(171.5, 60.93, order.DepartmentName, 13.9, fit: 26);
        page.Text(172.5, 69.94, order.DeliveryDate is { } date ? ThaiDate.Format(date) : null, 13.9, fit: 26);
        page.Text(181.0, 77.93, order.CreditDays is { } days and > 0 ? days.ToString(CultureInfo.InvariantCulture) : order.CreditDays == 0 ? "เงินสด" : null, 13.9, TextAnchor.End);
        // the "วัน" after the term is printed whether or not there is one (D-3 noted in the legacy sample) — except for cash, which the legacy left without it
    }

    private static void Row(FormPage page, PurchaseOrderPrintLine line, int index)
    {
        var baseline = RowRules[index + 1] - 2.0;
        page.Text(20.3, baseline, line.LineNo.ToString(CultureInfo.InvariantCulture), 13.9, TextAnchor.End);
        page.Text(23.0, baseline, line.RequestNo, 12.9, fit: 42.05 - 23.0 - 0.6);
        page.Text(43.5, baseline, line.Name, 13.9, fit: 129.52 - 43.5 - 1.0);
        page.Text(146.2, baseline, FormatQuantity(line.Quantity), 13.9, TextAnchor.End);
        page.Text(153.2, baseline, line.Unit, 13.9, TextAnchor.Middle, fit: 159.13 - 147.31 - 0.8);
        page.Text(178.4, baseline, ReportText.Decimal2(line.UnitPrice), 13.9, TextAnchor.End);
        page.Text(197.2, baseline, ReportText.Decimal2(line.Amount), 13.9, TextAnchor.End);
    }

    /// <summary>Under the table: เหตุผล, หมายเหตุ, the amounts and the amount in words, the notice and the sign boxes. <paramref name="order"/> null = labels only.</summary>
    private static void Lower(FormPage page, PurchaseOrderPrint? order)
    {
        // boxes
        page.Line(Left, 191.63, Right, 191.63, W).Line(Left, 211.74, Right, 211.74, W).Line(Left, 227.52, Right, 227.52, W);
        page.Line(Left, 171.47, Left, 227.52, W).Line(129.52, 171.47, 129.52, 227.52, W).Line(Right, 171.47, Right, 227.52, W);
        page.Text(14.13, 177.45, "เหตุผลในการสั่งซื้อ", 13.9).Text(14.13, 196.46, "หมายเหตุ (Remark)", 13.9);
        page.Text(14.13, 217.45, "รวมจำนวนเงิน(ตัวอักษร)(Total Amount in words)", 13.9);
        page.Text(130.75, 177.46, "รวมราคาทั้งสิ้น", 13.9).Text(130.75, 183.46, "(Sub Total)", 13.9);
        page.Text(130.75, 197.5, "ภาษีมูลค่าเพิ่ม 7%", 13.9).Text(130.75, 204.51, "(Value Added Tax 7%)", 13.9);
        page.Text(130.75, 217.48, "จำนวนเงินทั้งสิ้น", 13.9).Text(130.75, 224.49, "(Grand Total)", 13.9);
        if (order is not null)
        {
            page.Text(40.0, 177.45, order.Reason, 13.9, fit: 88);
            page.Text(42.0, 196.46, order.Note, 13.9, fit: 86);
            page.Text(14.17, 225.4, "(" + order.TotalInWords + ")", 13.9, fit: 113);
            page.Text(197.2, 181.5, ReportText.Decimal2(order.Subtotal), 13.9, TextAnchor.End);
            page.Text(197.2, 201.5, ReportText.Decimal2(order.Tax), 13.9, TextAnchor.End);
            page.Text(197.2, 221.5, ReportText.Decimal2(order.Total), 13.9, TextAnchor.End);
        }

        page.Text(13.14, 237.28, "โปรดระบุเลขที่ใบสั่งซื้อลงในใบส่งของ/ใบกำกับภาษีทุกครั้ง", 15.9, bold: true, fit: 87);
        page.Text(13.14, 244.28, "(Please sign Purchase Order Number in Invoice/Tax Invoice.)", 15.9, bold: true, fit: 96.4);

        // sign boxes
        foreach (var y in new[] { 247.5, 263.52, 280.49, 289.46 }) page.Line(Left, y, Right, y, W);
        foreach (var x in new[] { Left, 74.15, 136.45, Right }) page.Line(x, 247.5, x, 289.46, W);
        (double X, string Thai, string English, double Date)[] signers =
        [
            (40.3, "ผู้จัดทำใบสั่งซื้อ", "(Prepared by)", 18.10), (105.1, "ผู้ตรวจสอบใบสั่งซื้อ", "(Reviewed by)", 81.34), (168.3, "ผู้อนุมัติสั่งซื้อ", "(Apporved by)", 146.57),
        ];
        foreach (var (x, thai, english, date) in signers)
        {
            page.Text(x, 254.46, thai, 13.9, TextAnchor.Middle).Text(x, 260.45, english, 13.9, TextAnchor.Middle);
            page.Text(date, 286.79, "วันที่ (Date)", 11.9).Line(date + 14.57, 287.47, date + 42.21, 287.47, W);
        }
    }

    /// <summary>จำนวน as typed (General Number): 50, 12.5 — no thousands separator, no trailing zeros.</summary>
    public static string FormatQuantity(decimal value) => value.ToString("0.####", CultureInfo.InvariantCulture);
}
