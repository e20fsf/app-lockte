using System.Globalization;
using Lockte.SharedKernel;

namespace Lockte.Web.Reports.Purch;

/// <summary>
/// RPT-01 ใบขออนุมัติสั่งซื้อ (PC-PU002-1/2547 ฉบับที่ 1, #420), as the form the factory fills in by hand: company head from <see cref="PurchLetterheadOptions"/>, the
/// letter of the ผู้จัดซื้อ in a box beside เอกสารเลขที่, a fully ruled table with blank rows to write more items, รวมรายการ, เหตุผล and หมายเหตุ boxes and, at the foot, วันที่ต้องการของ
/// with the three signature boxes. ราคาต่อหน่วย and จำนวนเงิน are left empty on purpose. <see cref="RowsPerPage"/> rows to a sheet (blank ones fill the rest); the boxes below the
/// table are on the last sheet. Positions in mm on A4 portrait.
/// </summary>
public static class PurchRequestForm
{
    public const string Title = "ใบขออนุมัติสั่งซื้อ";

    public const int RowsPerPage = 13;

    private const double Left = 11.0;
    private const double Right = 199.0;
    private const double W = 0.35;
    private const double TableTop = 80.0;
    private const double HeaderHeight = 8.0;
    private const double RowHeight = 7.1;
    private static readonly double[] Columns = [Left, 22.0, 38.0, 118.0, 141.0, 154.0, 176.0, Right];

    private const int ReasonChars = 92;
    private const int NoteChars = 100;

    public static IReadOnlyList<FormPage> Layout(PurchRequestPrint request, string? documentNumber, PurchLetterheadOptions letterhead)
    {
        var chunks = request.Lines.Chunk(RowsPerPage).DefaultIfEmpty([]).ToList();
        var pages = new List<FormPage>();
        for (var i = 0; i < chunks.Count; i++)
        {
            var page = Frame(request, documentNumber, letterhead);
            for (var r = 0; r < chunks[i].Length; r++) Row(page, chunks[i][r], r);
            if (i == chunks.Count - 1) Lower(page, request);
            pages.Add(page);
        }
        return pages;
    }

    private static double TableBottom => TableTop + HeaderHeight + RowsPerPage * RowHeight;

    private static FormPage Frame(PurchRequestPrint request, string? documentNumber, PurchLetterheadOptions letterhead)
    {
        var page = new FormPage(Paper.A4Portrait);
        page.Text(105, 21, "บริษัท ล็อคเต้ จำกัด (LOCKTE CO.,LTD.)", 19.9, TextAnchor.Middle, bold: true);
        page.Text(105, 28, letterhead.Line1, 11.9, TextAnchor.Middle, fit: 188);
        page.Text(105, 34, letterhead.Line2, 11.9, TextAnchor.Middle, fit: 188);

        page.Text(105, 50, Title, 19.9, TextAnchor.Middle, bold: true);
        page.Text(Right, 50, documentNumber, 12.9, TextAnchor.End);

        // เลขที่ใบสั่งซื้อ is filled in when the purchase order is made: a line to write on
        page.Text(Left, 63, "เลขที่ใบสั่งซื้อ", 15.9).Line(41, 64.2, 75, 64.2, W);
        page.Box(118, 57, 9, 9, W).Text(122.5, 63.6, request.LocationCode, 15.9, TextAnchor.Middle);
        page.Text(130, 63, "เอกสารเลขที่", 15.9).Text(Right - 1, 63, request.RequestNo, 15.9, TextAnchor.End, bold: true, fit: 36).Line(161, 64.2, Right, 64.2, W);

        page.Text(Left, 73, "วันที่สั่งซื้อ", 15.9).Text(38, 73, ThaiDate.Format(request.RequestDate), 15.9, fit: 36).Line(36, 74.2, 75, 74.2, W);
        page.Text(130, 73, "แผนกที่ขอซื้อ", 15.9).Text(163, 73, request.DepartmentName, 15.9, fit: 35).Line(160, 74.2, Right, 74.2, W);

        // the table: ruled all round, with the blank rows
        var bottom = TableBottom;
        page.Line(Left, TableTop, Right, TableTop, W).Line(Left, TableTop + HeaderHeight, Right, TableTop + HeaderHeight, W);
        for (var r = 1; r <= RowsPerPage; r++) page.Line(Left, TableTop + HeaderHeight + r * RowHeight, Right, TableTop + HeaderHeight + r * RowHeight, W);
        foreach (var x in Columns) page.Line(x, TableTop, x, bottom, W);
        string[] headings = ["ลำดับ", "รหัส", "รายการ", "จำนวน", "หน่วย", "ราคาต่อหน่วย", "จำนวนเงิน"];
        for (var c = 0; c < headings.Length; c++)
            page.Text((Columns[c] + Columns[c + 1]) / 2, TableTop + 5.8, headings[c], 13.9, TextAnchor.Middle, bold: true, fit: Columns[c + 1] - Columns[c] - 1.5);
        return page;
    }

    private static void Row(FormPage page, PurchRequestPrintLine line, int index)
    {
        var baseline = TableTop + HeaderHeight + (index + 1) * RowHeight - 2.1;
        page.Text(Columns[1] - 1.5, baseline, line.LineNo.ToString(CultureInfo.InvariantCulture), 13.9, TextAnchor.End);
        page.Text(Columns[1] + 1.2, baseline, line.Code, 13.9, fit: Columns[2] - Columns[1] - 2.0);
        page.Text(Columns[2] + 1.2, baseline, line.Name, 13.9, fit: Columns[3] - Columns[2] - 2.0);
        page.Text(Columns[4] - 1.5, baseline, ReportText.Decimal2(line.Quantity), 13.9, TextAnchor.End, fit: Columns[4] - Columns[3] - 3.0);
        page.Text((Columns[4] + Columns[5]) / 2, baseline, line.Unit, 13.9, TextAnchor.Middle, fit: Columns[5] - Columns[4] - 1.0);
    }

    /// <summary>Under the table: รวมรายการ, เหตุผลในการซื้อ, หมายเหตุ, วันที่ต้องการของ and the sign boxes.</summary>
    private static void Lower(FormPage page, PurchRequestPrint request)
    {
        var bottom = TableBottom;
        double[] rules = [bottom + 14.5, bottom + 31.5, bottom + 52.5];
        page.Line(Left, bottom, Left, rules[^1], W).Line(Right, bottom, Right, rules[^1], W);
        foreach (var y in rules) page.Line(Left, y, Right, y, W);

        page.Text(14, bottom + 6.5, "รวมรายการ", 13.9).Text(48, bottom + 6.5, request.Lines.Count.ToString(CultureInfo.InvariantCulture), 13.9, TextAnchor.Middle).Text(55, bottom + 6.5, "รายการ", 13.9);

        page.Text(14, bottom + 20.5, "เหตุผลในการซื้อ", 13.9);
        var y1 = bottom + 20.5;
        foreach (var line in Wrap(request.Reason, ReasonChars).Take(2)) { page.Text(50, y1, line, 13.9, fit: 146); y1 += 6.2; }

        page.Text(14, bottom + 37.5, "หมายเหตุ", 13.9);
        var y2 = bottom + 37.5;
        foreach (var line in Wrap(request.Note, NoteChars).Take(3)) { page.Text(36, y2, line, 13.9, fit: 161); y2 += 6.2; }

        // วันที่ต้องการของ and the three signatures
        var top = rules[^1] + 5.5;
        var floor = top + 44;
        double[] x = [Left, 75.0, 137.0, Right];
        foreach (var xx in x) page.Line(xx, top, xx, floor, W);
        page.Line(Left, top, Right, top, W).Line(Left, floor, Right, floor, W);

        page.Text(13, top + 8.5, "วันที่ต้องการของ", 14.9).Text(49, top + 8.5, request.DesiredDate is { } d ? ThaiDate.Format(d) : null, 14.9, fit: 24).Line(46, top + 9.7, 73, top + 9.7, W);
        page.Line(18, top + 29, 69, top + 29, W).Text(43, top + 37, "ผู้สั่งซื้อ", 14.9, TextAnchor.Middle);
        page.Text(78, top + 29, "ผู้อนุมัติ", 14.9).Line(94, top + 29.5, 133, top + 29.5, W).Text(106, top + 37, "ผู้จัดการฝ่าย", 14.9, TextAnchor.Middle);
        page.Text(140, top + 29, "ผู้อนุมัติ", 14.9).Line(156, top + 29.5, 195, top + 29.5, W).Text(168, top + 37, "ผู้จัดการโรงงาน", 14.9, TextAnchor.Middle);
    }

    /// <summary>The text cut into lines of at most <paramref name="chars"/> characters, at a space when there is one near the end (Thai has none: it is cut).</summary>
    public static IEnumerable<string> Wrap(string? text, int chars)
    {
        if (string.IsNullOrWhiteSpace(text)) yield break;
        foreach (var paragraph in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var rest = paragraph.Trim();
            while (rest.Length > chars)
            {
                var cut = rest.LastIndexOf(' ', chars);
                if (cut < chars / 2) cut = chars;
                yield return rest[..cut].TrimEnd();
                rest = rest[cut..].TrimStart();
            }
            if (rest.Length > 0) yield return rest;
        }
    }
}
