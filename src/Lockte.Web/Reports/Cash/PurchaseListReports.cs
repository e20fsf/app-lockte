using System.Globalization;
using Lockte.Cash;
using Lockte.Web.Accounts;

namespace Lockte.Web.Reports.Cash;

/// <summary>RPT-04: the lines of a range of dates in the order of the print.</summary>
public sealed record PurchaseListReport(DateOnly From, DateOnly To, IReadOnlyList<ExpenseLineDto> Lines)
{
    public decimal Total => Lines.Sum(l => l.Amount);
}

/// <summary>
/// รายการซื้อของเงินสดย่อย (RPT-04, SCR-05 "วันที่ซื้อของ"; #243, BR-008), ระดับ B on A4 ตั้ง: วันที่, ลำดับที่ (the voucher number, repeated for each line of the voucher), รายการ, จำนวน with
/// หน่วย, จำนวนเงิน, ประเภท, and รวมทั้งหมด. The order is date, then the number <b>as text</b>, then the order keyed — as Access sorted, so a print can be put against the old one line by
/// line (<c>05/108</c> comes before <c>05/99</c>). A range the wrong way round prints empty, with no message.
/// </summary>
public static class PurchaseListReports
{
    public const string Title = "รายการซื้อของเงินสดย่อย";

    /// <summary>The lines of the range in the order of the print; text order is ordinal (the legacy compared the characters).</summary>
    public static PurchaseListReport Build(DateOnly from, DateOnly to, IEnumerable<ExpenseLineDto> lines)
        => new(from, to, CashReportRows.Purchases(from, to, lines));

    public static RouteGroupBuilder MapCashPurchaseListReports(this RouteGroupBuilder reports)
    {
        reports.MapGet("/cash/purchases", async (DateOnly from, DateOnly to, ExpenseLineDirectory lines, HttpContext http, ReportPrinter printer, CancellationToken cancellationToken) =>
        {
            var report = Build(from, to, from > to ? [] : await lines.ListAsync(from, to));
            var pdf = await printer.PrintAsync<PurchaseListView>(Title, Paper.A4Portrait, new() { ["Report"] = report }, AccountClaims.DisplayNameOf(http.User), cancellationToken: cancellationToken);
            return ReportsRegistration.Pdf(http, pdf, $"รายการซื้อของ {from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)} ถึง {to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");
        });
        return reports;
    }
}
