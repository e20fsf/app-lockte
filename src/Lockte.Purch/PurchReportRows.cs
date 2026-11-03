namespace Lockte.Purch;

/// <summary>
/// The rows the receipt and stock reports are built from, shared by the reports and by the comparison with Access (#271) so both count the same thing: the purchase totals (RPT-20, 21, 22)
/// count the deliveries of the period that did not fail the inspection (BR-061), RPT-26 lists the balances of a cut that are not zero (BR-042).
/// </summary>
public static class PurchReportRows
{
    /// <summary>The deliveries of <paramref name="from"/>…<paramref name="to"/> (both ends) that count as bought: the failed ones are left out (BR-032, BR-061).</summary>
    public static IReadOnlyList<ReceivedLine> Purchases(IEnumerable<ReceivedLine> lines, DateOnly from, DateOnly to)
        => lines.Where(l => !l.Failed && l.ReceiveDate >= from && l.ReceiveDate <= to).ToList();

    /// <summary>RPT-22: the amount bought for every แผนก (by id), the biggest first.</summary>
    public static IReadOnlyList<(int DepartmentId, decimal Amount)> ByDepartment(IEnumerable<ReceivedLine> lines, DateOnly from, DateOnly to)
        => Purchases(lines, from, to).GroupBy(l => l.DepartmentId).Select(g => (g.Key, g.Sum(l => l.Amount))).OrderByDescending(r => r.Item2).ToList();

    /// <summary>RPT-26: the balances of a cut that are not zero, by code.</summary>
    public static IReadOnlyList<StockPeriodBalanceDto> StockCut(IEnumerable<StockPeriodBalanceDto> balances)
        => balances.Where(b => b.Quantity != 0).OrderBy(b => b.MaterialCode, StringComparer.Ordinal).ToList();
}
