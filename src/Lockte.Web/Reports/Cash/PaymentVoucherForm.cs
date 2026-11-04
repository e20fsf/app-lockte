using System.Globalization;
using Lockte.Cash;
using Lockte.SharedKernel;

namespace Lockte.Web.Reports.Cash;

/// <summary>
/// RPT-01 ใบสำคัญจ่ายเงิน (PAYMENT VOUCHER, <c>พิมพ์ใบสำคัญจ่าย1</c>…<c>7</c>, #234), ระดับ A (#40 ตรงแบบเดิม): A5 landscape, every text and rule where the legacy report printed it —
/// positions in mm (baseline for texts) measured on <c>lot 3\เงินสดย่อย\พิมพ์ใบสำคัญจ่าย1.pdf</c>. The table has <see cref="RowsPerSheet"/> fixed rows (the legacy had seven reports
/// that differed only in where the total and the signatures sat, Q-11; one layout does for any number of lines). A voucher of more lines prints as sheets in turn — "แผ่นที่ x/y" —
/// with the total and its words on the last (assumption ข, Q-31). The number and the date are those of the first line (BR-005 #2); the legacy printed a blank voucher when nothing was
/// chosen, here the endpoint refuses. Changes from the legacy: TH Sarabun New; the sheet mark; a long text is squeezed into its cell (D-1). The positions of the values that the blank
/// sample does not show (the number, the date, the text of a row) are placed by the labels and the cells around them.
/// </summary>
public static class PaymentVoucherForm
{
    public const string Title = "ใบสำคัญจ่ายเงิน (PAYMENT VOUCHER)";
    public const int RowsPerSheet = 7;

    private const double W = 0.35;
    private const double Left = 7.96;
    private const double Right = 194.82;
    private const double HeaderTop = 40.63;
    private const double FirstRule = 49.5;
    private const double RowPitch = 6.9486;
    private const double TotalTop = FirstRule + RowsPerSheet * RowPitch; // 98.14
    private const double TotalBottom = 106.05;
    private static readonly double[] Verticals = [Left, 24.99, 113.97, 133.97, 148.86, 170.12, Right];

    /// <summary>Whether the lines are not all of one (date, number): the voucher then takes its head from the first and the screen says so before printing (AC-21).</summary>
    public static bool MixesVouchers(IEnumerable<ExpenseLineDto> lines) => lines.Select(l => (l.Date, l.VoucherNo)).Distinct().Count() > 1;

    public static decimal Total(IEnumerable<ExpenseLineDto> lines) => ExpenseLineMath.Total(lines);

    /// <summary>"(หนึ่งร้อยสี่สิบบาทถ้วน)": RND-C of the legacy <c>GetThaiUnit</c>, in brackets.</summary>
    public static string TotalInWords(decimal total) => $"({ThaiNumber.Baht((double)total)})";

    /// <summary>The sheets of one voucher; the lines are in the order of a voucher (date, number, order keyed) and the head is the first line's.</summary>
    public static IReadOnlyList<FormPage> Layout(IReadOnlyList<ExpenseLineDto> lines)
    {
        if (lines.Count == 0) throw new ArgumentException("A voucher needs at least one line.", nameof(lines));
        var sheets = lines.Chunk(RowsPerSheet).ToList();
        var total = Total(lines);
        var pages = new List<FormPage>();
        for (var s = 0; s < sheets.Count; s++)
        {
            var last = s == sheets.Count - 1;
            var page = Frame(lines[0]);
            if (sheets.Count > 1) page.Text(8.23, 20.5, $"แผ่นที่ {(s + 1).ToString(CultureInfo.InvariantCulture)}/{sheets.Count.ToString(CultureInfo.InvariantCulture)}", 15.9);
            for (var r = 0; r < sheets[s].Length; r++) Row(page, sheets[s][r], s * RowsPerSheet + r + 1, r);
            Total(page, last ? total : null);
            pages.Add(page);
        }
        return pages;
    }

    private static FormPage Frame(ExpenseLineDto first)
    {
        var page = new FormPage(Paper.A5Landscape);
        page.Text(100.75, 16.12, "บริษัท ล็อคเต้ จำกัด", 35.8, TextAnchor.Middle, bold: true);
        page.Text(100.9, 24.03, "ใบสำคัญจ่ายเงิน", 23.9, TextAnchor.Middle, bold: true);
        page.Text(100.75, 30.61, "PAYMENT VOUCHER", 19.9, TextAnchor.Middle, bold: true);
        page.Text(166.77, 18.77, "เลขที่", 19.9).Text(178.5, 18.77, first.VoucherNo, 19.9, fit: 16.5);
        page.Text(161.84, 38.5, "วันที่", 19.9).Text(172.5, 38.5, ThaiDate.Format(first.Date), 19.9, fit: 22.3);
        page.Text(8.23, 38.5, "จ่ายโดย", 19.9).Text(31.21, 38.5, "เงินสด", 19.9);

        // the table: a header row, seven fixed rows, the total row
        page.Line(Left, HeaderTop, Right, HeaderTop, W);
        for (var i = 0; i <= RowsPerSheet; i++) page.Line(Left, FirstRule + i * RowPitch, Right, FirstRule + i * RowPitch, W);
        page.Line(Left, TotalBottom, Right, TotalBottom, W);
        foreach (var x in Verticals)
        {
            var inRows = x is not (133.97 or 148.86); // the unit and the price columns end with the rows; the total row is merged
            page.Line(x, HeaderTop, x, inRows ? TotalBottom : TotalTop, W);
        }
        page.Text(69.4, 46.67, "รายการ", 17.9, TextAnchor.Middle);
        page.Text(121.38, 46.67, "จำนวน", 17.9).Text(133.9, 46.67, "หน่วย", 17.9);
        page.Text(165.5, 46.67, "ราคา", 17.9, TextAnchor.Middle).Text(185.3, 46.67, "จำนวนเงิน", 17.9, TextAnchor.Middle);

        // the signatures: four lines with the title under each
        (double X0, double X1, double Centre, string Label)[] signatures =
        [
            (7.95, 46.95, 27.9, "ผู้อนุมัติ"), (57.28, 96.28, 77.9, "ผู้จ่ายเงิน"), (106.65, 145.65, 127.9, "สมุห์บัญชี"), (155.96, 194.97, 175.95, "ผู้รับเงิน"),
        ];
        foreach (var (x0, x1, centre, label) in signatures)
        {
            page.Line(x0, 122.66, x1, 122.66, FormPage.Rule);
            page.Text(centre, 128.55, label, 14.0, TextAnchor.Middle); // Tahoma 10.9 pt is wider than TH Sarabun New at the same size
        }
        return page;
    }

    private static void Row(FormPage page, ExpenseLineDto line, int number, int row)
    {
        var top = FirstRule - 0.18 + row * RowPitch;
        var baseline = top + 5.68;
        page.Text(16.4, baseline, number.ToString(CultureInfo.InvariantCulture), 15.9, TextAnchor.Middle);
        page.Text(26.2, baseline, line.Name, 15.9, fit: 86);
        page.Text(132.4, baseline, line.Qty.ToString("0.###", CultureInfo.InvariantCulture), 15.9, TextAnchor.End);
        page.Text(135.2, baseline, line.Unit, 15.9, fit: 12.5);
        page.Text(169.0, baseline, line.UnitPrice.ToString("#,##0.00", CultureInfo.InvariantCulture), 15.9, TextAnchor.End);
        page.Text(194.34, baseline, line.Amount.ToString("#,##0.00", CultureInfo.InvariantCulture), 15.9, TextAnchor.End);
    }

    /// <summary>The total row: the words in brackets on the left, "รวมเงิน", and the sum; empty on every sheet but the last.</summary>
    private static void Total(FormPage page, decimal? total)
    {
        if (total is not { } sum) return;
        var baseline = TotalTop - 0.18 + 5.61;
        page.Text(25.41, baseline, TotalInWords(sum), 15.9, fit: 86);
        page.Text(136.23, baseline, "รวมเงิน", 15.9);
        page.Text(194.34, baseline, sum.ToString("#,##0.00", CultureInfo.InvariantCulture), 15.9, TextAnchor.End);
    }

    public static RouteGroupBuilder MapCashVoucherReports(this RouteGroupBuilder reports)
    {
        // ?ids=1,2,3 — the lines ticked on SCR-04; nothing chosen prints nothing (the legacy printed a blank voucher, BR-005 #3)
        reports.MapGet("/cash/voucher", async (string? ids, ExpenseLineDirectory lines, ReportPrinter printer, HttpContext http, CancellationToken cancellationToken) =>
        {
            var wanted = (ids ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(t => int.TryParse(t, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : (int?)null).OfType<int>().Distinct().ToList();
            if (wanted.Count == 0) return Results.BadRequest("กรุณาเลือกบรรทัดที่จะพิมพ์ใบสำคัญจ่ายก่อน");
            var chosen = await lines.GetManyAsync(wanted);
            if (chosen.Count == 0) return Results.NotFound("ไม่พบบรรทัดที่เลือก (อาจถูกลบไปแล้ว)");
            var pdf = await printer.PrintFormAsync(ReportProgram.Cash, Title, Layout(chosen), cancellationToken);
            return ReportsRegistration.Pdf(http, pdf, "ใบสำคัญจ่าย " + chosen[0].VoucherNo.Replace('/', '-'));
        });
        return reports;
    }
}
