using Lockte.Cash;
using Lockte.Web.Accounts;

namespace Lockte.Web.Reports.Cash;

/// <summary>RPT-10: the buyers in the order the list prints.</summary>
public sealed record ScrapBuyerListReport(IReadOnlyList<ScrapBuyerDto> Rows);

/// <summary>RPT-11: the scrap kinds in the order the list prints.</summary>
public sealed record ScrapItemListReport(IReadOnlyList<ScrapItemDto> Rows);

/// <summary>
/// รายชื่อผู้ซื้อของเก่า (RPT-10, ปุ่ม "พิมพ์" ของ SCR-07) และรายชื่อของเก่า (RPT-11, SCR-08; #230), ระดับ B on A4 ตั้ง: ลำดับ + ชื่อ + ที่อยู่ + โทร / ลำดับ + ชื่อ + หน่วย.
/// The legacy printed them in the order of the table; here they are by name in the Thai dictionary order (the directories give that order), as the RPT-11 sample already was.
/// </summary>
public static class ScrapListReports
{
    public const string BuyerTitle = "รายชื่อผู้ซื้อของเก่า";
    public const string ItemTitle = "รายชื่อของเก่า";

    public static RouteGroupBuilder MapCashScrapListReports(this RouteGroupBuilder reports)
    {
        reports.MapGet("/cash/scrap-buyers", async (ScrapBuyerDirectory buyers, HttpContext http, ReportPrinter printer, CancellationToken cancellationToken) =>
        {
            var pdf = await printer.PrintAsync<ScrapBuyerListView>(BuyerTitle, Paper.A4Portrait, new() { ["Report"] = new ScrapBuyerListReport(await buyers.ListAsync()) },
                AccountClaims.DisplayNameOf(http.User), cancellationToken: cancellationToken);
            return ReportsRegistration.Pdf(http, pdf, BuyerTitle);
        });

        reports.MapGet("/cash/scrap-items", async (ScrapItemDirectory items, HttpContext http, ReportPrinter printer, CancellationToken cancellationToken) =>
        {
            var pdf = await printer.PrintAsync<ScrapItemListView>(ItemTitle, Paper.A4Portrait, new() { ["Report"] = new ScrapItemListReport(await items.ListAsync()) },
                AccountClaims.DisplayNameOf(http.User), cancellationToken: cancellationToken);
            return ReportsRegistration.Pdf(http, pdf, ItemTitle);
        });
        return reports;
    }
}
