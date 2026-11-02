namespace Lockte.Cash;

/// <summary>One line of RPT-09: a line of a receipt with the date, number and buyer of its receipt.</summary>
public sealed record ScrapSaleRow(DateOnly Date, string No, int Seq, string ItemName, decimal Qty, string? Unit, decimal UnitPrice, decimal Amount, string BuyerName);

/// <summary>
/// The rows of the range reports, put in the order of the print (RPT-04, RPT-09) — apart from the pages that print them, so the report and the comparison with Access (#248, a console tool
/// that knows no web page) get the very same rows.
/// </summary>
public static class CashReportRows
{
    /// <summary>
    /// RPT-04 (BR-008): the lines of the range by date, then the number <b>as text</b> (ordinal, as Access compared the characters: <c>05/108</c> before <c>05/99</c>), then the order keyed. A range
    /// the wrong way round has no lines.
    /// </summary>
    public static IReadOnlyList<ExpenseLineDto> Purchases(DateOnly from, DateOnly to, IEnumerable<ExpenseLineDto> lines)
        => from > to ? [] : lines.Where(l => l.Date >= from && l.Date <= to).OrderBy(l => l.Date).ThenBy(l => l.VoucherNo, StringComparer.Ordinal).ThenBy(l => l.Id).ToList();

    /// <summary>
    /// RPT-09 (BR-018): the lines of the receipts dated in the range by date, then number as a number, then ลำดับที่; a cancelled receipt is left out (assumption ง), one already remitted is
    /// in. A range the wrong way round has no lines.
    /// </summary>
    public static IReadOnlyList<ScrapSaleRow> Sales(DateOnly from, DateOnly to, IEnumerable<ScrapReceiptDto> receipts)
        => from > to ? [] : receipts.Where(r => !r.IsCancelled && r.Date >= from && r.Date <= to)
            .OrderBy(r => r.Date).ThenBy(r => ScrapReceiptMath.SortKey(r.No))
            .SelectMany(r => r.Lines.OrderBy(l => l.Seq).Select(l => new ScrapSaleRow(r.Date, r.No, l.Seq, l.ItemName, l.Qty, l.Unit, l.UnitPrice, l.Amount, r.BuyerName))).ToList();
}
