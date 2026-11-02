using System.Globalization;
using Lockte.Cash;
using Lockte.SharedKernel;
using Lockte.Web.Accounts;

namespace Lockte.Web.Reports.Cash;

/// <summary>RPT-09: the lines sold in a range of dates, with the sum and the number of lines.</summary>
public sealed record ScrapSalesReport(DateOnly From, DateOnly To, IReadOnlyList<ScrapSaleRow> Rows)
{
    public decimal Total => Rows.Sum(r => r.Amount);

    public int Count => Rows.Count;
}

/// <summary>
/// รายงานยอดขายของเก่าและอื่นๆ (RPT-09, SCR-05 "วันที่2"; #244, BR-018), ระดับ B on A4 ตั้ง: ตั้งแต่วันที่ … ถึงวันที่ … เดือน <the month of ถึงวันที่>; (ลำดับวิ่ง), วันที่, เลขที่, ลำดับ, รายการ,
/// จำนวน, หน่วย, ราคา, จำนวนเงิน, ผู้ซื้อ; no heading per date — the date and the number are printed on every row, as the sample does, in columns of their own so they do not run together
/// (D-1) — then รวมทั้งหมด and the number of lines. Date, then number as a number, then ลำดับ. A cancelled receipt is left out (assumption ง); one already remitted is in.
/// </summary>
public static class ScrapSalesReports
{
    public const string Title = "รายงานยอดขายของเก่าและอื่นๆ";

    /// <summary>The lines of the receipts dated in the range, cancelled ones not counted, in the order of the print.</summary>
    public static ScrapSalesReport Build(DateOnly from, DateOnly to, IEnumerable<ScrapReceiptDto> receipts)
        => new(from, to, CashReportRows.Sales(from, to, receipts));

    /// <summary>The month of ถึงวันที่ with its Buddhist year (<c>กันยายน 2569</c>).</summary>
    public static string MonthOf(DateOnly to) => to.ToString("MMMM yyyy", ThaiDate.Culture);

    public static RouteGroupBuilder MapCashScrapSalesReports(this RouteGroupBuilder reports)
    {
        reports.MapGet("/cash/scrap-sales", async (DateOnly from, DateOnly to, ScrapReceiptDirectory receipts, HttpContext http, ReportPrinter printer, CancellationToken cancellationToken) =>
        {
            var report = Build(from, to, from > to ? [] : await receipts.ListAsync(from, to));
            var pdf = await printer.PrintAsync<ScrapSalesView>(Title, Paper.A4Portrait, new() { ["Report"] = report }, AccountClaims.DisplayNameOf(http.User), cancellationToken: cancellationToken);
            return ReportsRegistration.Pdf(http, pdf, $"ยอดขายของเก่า {from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)} ถึง {to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");
        });
        return reports;
    }
}
