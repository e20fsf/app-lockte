using System.Globalization;
using Lockte.Cash;
using Lockte.SharedKernel;
using Lockte.Web.Accounts;

namespace Lockte.Web.Reports.Cash;

/// <summary>RPT-02: the sums by type of a range of dates and the figure typed for the last period.</summary>
public sealed record ExpenseSummaryReport(DateOnly From, DateOnly To, IReadOnlyList<ExpenseTypeTotal> Rows, decimal LastPay)
{
    /// <summary>รวมค่าใช้จ่ายงวดนี้.</summary>
    public decimal Total => Rows.Sum(r => r.Total);

    /// <summary>รวมค่าใช้จ่ายทั้งสิ้น = this period + the one typed for the last (BR-006); nothing is rounded.</summary>
    public decimal Grand => Total + LastPay;

    /// <summary>"(ห้าหมื่นหนึ่งร้อยหกสิบหกบาทถ้วน)": RND-C of the grand total.</summary>
    public string GrandInWords => $"({ThaiNumber.Baht((double)Grand)})";
}

/// <summary>
/// สรุปค่าใช้จ่ายเงินสดย่อย โรงงาน (RPT-02, SCR-05 "วันที่"; #242), ระดับ B on Letter ตั้ง: the range in long Buddhist dates, ลำดับที่ / รายการ (the type) / จำนวนเงิน by name, then
/// รวมค่าใช้จ่ายงวดนี้, ค่าใช้จ่ายงวดที่แล้ว (typed on the screen, assumption ค), รวมค่าใช้จ่ายทั้งสิ้น in bold and its words, and the four signature boxes with the date to fill in. A range
/// with no lines still prints the totals — 0.00, the last period, the sum — where the legacy printed an empty report (BR-006 #2); the range the wrong way round prints empty. The
/// แบบกลาง prints the date of printing and the page at the foot, which the sample has not.
/// </summary>
public static class ExpenseSummaryReports
{
    public const string Title = "สรุปค่าใช้จ่ายเงินสดย่อย โรงงาน";

    public static async Task<ExpenseSummaryReport> BuildAsync(ExpenseLineDirectory lines, DateOnly from, DateOnly to, decimal lastPay)
        => new(from, to, await lines.SummaryByTypeAsync(from, to), lastPay);

    /// <summary>The date in full, Buddhist year: <c>1 กันยายน 2569</c>.</summary>
    public static string LongDate(DateOnly date) => date.ToString("d MMMM yyyy", ThaiDate.Culture);

    public static RouteGroupBuilder MapCashExpenseSummaryReports(this RouteGroupBuilder reports)
    {
        reports.MapGet("/cash/expense-summary", async (DateOnly from, DateOnly to, string? lastPay, ExpenseLineDirectory lines, HttpContext http, ReportPrinter printer, CancellationToken cancellationToken) =>
        {
            var last = decimal.TryParse(lastPay, NumberStyles.Number, CultureInfo.InvariantCulture, out var typed) ? typed : 0m;
            var report = await BuildAsync(lines, from, to, last);
            var pdf = await printer.PrintAsync<ExpenseSummaryView>(Title, Paper.LetterPortrait, new() { ["Report"] = report }, AccountClaims.DisplayNameOf(http.User), cancellationToken: cancellationToken);
            return ReportsRegistration.Pdf(http, pdf, $"สรุปค่าใช้จ่ายเงินสดย่อย {from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)} ถึง {to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");
        });
        return reports;
    }
}
