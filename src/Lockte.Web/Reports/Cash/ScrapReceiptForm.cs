using System.Globalization;
using Lockte.Cash;
using Lockte.SharedKernel;

namespace Lockte.Web.Reports.Cash;

/// <summary>
/// RPT-06 ใบเสร็จรับเงินขายของเก่า (<c>ขายของเก่า</c>, #238), ระดับ A (#40 ตรงแบบเดิม): Letter portrait, every text and rule where the legacy report printed it — positions in mm (baseline
/// for texts) measured on <c>lot 3\เงินสดย่อย\ขายของเก่า.pdf</c>. No company name, as the legacy had none. The lines are in order of ลำดับที่; a receipt of more than
/// <see cref="RowsPerPage"/> lines goes on to the next page with the head repeated, and the total, its words and the four signatures are on the last. Changes from the legacy: TH Sarabun
/// New; an address too long for its place is carried to a second line instead of running into the phone (D-1), the table moving down for it; a long item is squeezed into its cell.
/// A receipt that is cancelled is not printed (the endpoint refuses, #239).
/// </summary>
public static class ScrapReceiptForm
{
    public const string Title = "ใบเสร็จรับเงินขายของเก่า";
    public const int RowsPerPage = 20;

    /// <summary>About how many characters of the address fit between its label and the "โทรฯ" (80 mm at 15.9 pt); a longer address is carried to the next line at a blank.</summary>
    public const int AddressChars = 48;
    public const int AddressLines = 3;

    private const double W = 0.35;
    private const double RowHeight = 7.14;
    private const double TableTop = 54.87;
    private const double HeaderBottom = 64.85;
    private const double AddressStep = 5.4;
    private static readonly double[] Verticals = [33.87, 46.78, 87.56, 105.46, 121.33, 144.22, 173.04];
    private const double Left = 33.87;
    private const double Right = 173.04;

    /// <summary>The address as lines of the print: split at blanks, a word longer than a line cut; at most <see cref="AddressLines"/> (the last may run long and is squeezed).</summary>
    public static IReadOnlyList<string> AddressLinesOf(string? address)
    {
        var lines = new List<string>();
        var current = "";
        foreach (var word in (address ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var rest = word;
            while (rest.Length > AddressChars)
            {
                if (current.Length > 0) { lines.Add(current); current = ""; }
                lines.Add(rest[..AddressChars]);
                rest = rest[AddressChars..];
            }
            if (current.Length == 0) current = rest;
            else if (current.Length + 1 + rest.Length <= AddressChars) current += " " + rest;
            else { lines.Add(current); current = rest; }
        }
        if (current.Length > 0) lines.Add(current);
        if (lines.Count > AddressLines) lines = [.. lines.Take(AddressLines - 1), string.Join(" ", lines.Skip(AddressLines - 1))];
        return lines;
    }

    public static IReadOnlyList<FormPage> Layout(ScrapReceiptDto receipt)
    {
        var address = AddressLinesOf(receipt.BuyerAddress);
        var extra = Math.Max(0, address.Count - 1) * AddressStep;
        var chunks = receipt.Lines.Chunk(RowsPerPage).DefaultIfEmpty([]).ToList();
        var pages = new List<FormPage>();
        for (var p = 0; p < chunks.Count; p++)
        {
            var last = p == chunks.Count - 1;
            var page = new FormPage(Paper.LetterPortrait);
            Head(page, receipt, address);
            Table(page, extra, chunks[p].Length);
            for (var r = 0; r < chunks[p].Length; r++) Row(page, chunks[p][r], extra, r);
            var bottom = HeaderBottom + extra + RowHeight * Math.Max(1, chunks[p].Length);
            if (last) Total(page, receipt, bottom);
            if (last) Signatures(page);
            pages.Add(page);
        }
        return pages;
    }

    private static void Head(FormPage page, ScrapReceiptDto receipt, IReadOnlyList<string> address)
    {
        page.Text(108.7, 32.71, Title, 21.9, TextAnchor.Middle, bold: true);
        page.Text(171.31, 30.68, "เลขที่", 15.9).Text(181.29, 30.58, receipt.No, 15.9, fit: 26);
        page.Text(36.1, 41.66, "ชื่อผู้ซื้อ", 15.9).Text(50.11, 41.55, receipt.BuyerName, 15.9, fit: 72);
        page.Text(125.57, 41.66, "วันที่", 15.9).Text(135.53, 41.55, ThaiDate.Format(receipt.Date), 15.9, fit: 38);
        page.Text(36.1, 50.64, "ที่อยู่", 15.9).Text(126.56, 50.64, "โทรฯ", 15.9).Text(135.59, 50.53, receipt.BuyerTel, 15.9, fit: 38);
        for (var i = 0; i < Math.Max(1, address.Count); i++)
            page.Text(45.08, 50.53 + i * AddressStep, i < address.Count ? address[i] : null, 15.9, fit: 80);

        // the lines to write on
        page.Line(50.11, 41.83, 122.5, 41.83, 0.38).Line(135.53, 41.83, 173.04, 41.83, 0.38);
        for (var i = 0; i < Math.Max(1, address.Count); i++) page.Line(45.08, 50.81 + i * AddressStep, 124.0, 50.81 + i * AddressStep, 0.38);
        page.Line(135.59, 50.81, 173.04, 50.81, 0.38);
    }

    /// <summary>The table's frame and headings; <paramref name="rows"/> is how many lines it holds (at least one, as the sample has).</summary>
    private static void Table(FormPage page, double extra, int rows)
    {
        var top = TableTop + extra;
        var headerBottom = HeaderBottom + extra;
        var bottom = headerBottom + RowHeight * Math.Max(1, rows);
        page.Line(Left, top, Right, top, W).Line(Left, headerBottom, Right, headerBottom, W);
        for (var r = 1; r <= Math.Max(1, rows); r++) page.Line(Left, headerBottom + RowHeight * r, Right, headerBottom + RowHeight * r, W);
        foreach (var x in Verticals) page.Line(x, top, x, bottom, W);
        var baseline = 61.62 + extra;
        page.Text(67.7, baseline, "รายการ", 15.9, TextAnchor.Middle);
        page.Text(104.22, baseline, "จำนวน", 15.9, TextAnchor.End).Text(113.43, baseline, "หน่วย", 15.9, TextAnchor.Middle);
        page.Text(142.88, baseline, "ราคา", 15.9, TextAnchor.End).Text(171.6, baseline, "จำนวนเงิน", 15.9, TextAnchor.End);
    }

    private static void Row(FormPage page, ScrapReceiptLineDto line, double extra, int row)
    {
        var baseline = HeaderBottom + extra + RowHeight * row + 5.17;
        page.Text(40.3, baseline, line.Seq.ToString(CultureInfo.InvariantCulture), 14.0, TextAnchor.Middle);
        page.Text(48.06, baseline, line.ItemName, 14.0, fit: 37.9);
        page.Text(104.14, baseline, line.Qty.ToString("#,##0.00#", CultureInfo.InvariantCulture), 14.0, TextAnchor.End);
        page.Text(113.4, baseline, line.Unit, 14.0, TextAnchor.Middle, fit: 14.5);
        page.Text(143.09, baseline, line.UnitPrice.ToString("#,##0.00##", CultureInfo.InvariantCulture), 14.0, TextAnchor.End);
        page.Text(171.59, baseline, line.Amount.ToString("#,##0.00", CultureInfo.InvariantCulture), 14.0, TextAnchor.End);
    }

    /// <summary>รวมทั้งหมด in bold, the sum in the double-ruled box under the amount column, and the words in brackets.</summary>
    private static void Total(FormPage page, ScrapReceiptDto receipt, double tableBottom)
    {
        page.Line(Verticals[5], tableBottom, Verticals[5], tableBottom + 8.18, W).Line(Right, tableBottom, Right, tableBottom + 8.18, W);
        page.Line(Verticals[5], tableBottom + 7.16, Right, tableBottom + 7.16, W).Line(Verticals[5], tableBottom + 8.18, Right, tableBottom + 8.18, W);
        page.Text(47.04, tableBottom + 7.93, "รวมทั้งหมด", 15.9, bold: true);
        page.Text(171.7, tableBottom + 5.16, receipt.Total.ToString("#,##0.00", CultureInfo.InvariantCulture), 14.0, TextAnchor.End);
        page.Text(47.07, tableBottom + 16.8, receipt.TotalInWords, 15.9, fit: 126);
    }

    /// <summary>Two by two at the foot of the page, where the legacy fixed them.</summary>
    private static void Signatures(FormPage page)
    {
        page.Text(32.11, 239.65, "ผู้รับของ", 15.9).Line(46.77, 240.85, 87.55, 240.85, FormPage.Rule);
        page.Text(118.63, 239.65, "ผู้ตรวจสอบ", 15.9).Line(140.49, 240.9, 172.03, 240.9, FormPage.Rule);
        page.Text(32.11, 250.74, "ผู้รับเงิน", 15.9).Line(46.77, 251.85, 87.55, 251.85, FormPage.Rule);
        page.Text(118.63, 250.62, "ผู้อนุมัติ", 15.9).Line(131.29, 251.85, 172.03, 251.85, FormPage.Rule);
    }

    public static RouteGroupBuilder MapCashReceiptReports(this RouteGroupBuilder reports)
    {
        reports.MapGet("/cash/receipt", async (int? id, string? no, ScrapReceiptDirectory receipts, ReportPrinter printer, HttpContext http, CancellationToken cancellationToken) =>
        {
            var receipt = id is { } i ? await receipts.GetAsync(i) : no is { Length: > 0 } n ? await receipts.GetByNoAsync(n) : null;
            if (receipt is null) return Results.NotFound("ไม่พบใบเสร็จ");
            if (receipt.IsCancelled) return Results.BadRequest($"ใบเสร็จเลขที่ {receipt.No} ถูกยกเลิกแล้ว พิมพ์ไม่ได้");
            var pdf = await printer.PrintFormAsync(Title, Layout(receipt), cancellationToken);
            return ReportsRegistration.Pdf(http, pdf, "ใบเสร็จ " + receipt.No);
        });
        return reports;
    }
}
