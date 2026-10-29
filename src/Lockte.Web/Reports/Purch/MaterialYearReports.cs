using Lockte.Purch;
using Lockte.Web.Accounts;

namespace Lockte.Web.Reports.Purch;

/// <summary>One material of RPT-16 / RPT-17: its twelve months (whole numbers, RND-D) and the total of the year.</summary>
public sealed record MaterialYearRow(string Code, string Name, string? Unit, IReadOnlyList<decimal> Months)
{
    public decimal Total => Months.Sum();
}

/// <summary>RPT-16 การซื้อวัตถุดิบปี (จำนวนเงิน) / RPT-17 (จำนวนหน่วย): material × twelve months, the biggest total first.</summary>
public sealed record MaterialYearReport(int Year, bool Units, IReadOnlyList<MaterialYearRow> Rows)
{
    public decimal MonthTotal(int index) => Rows.Sum(r => r.Months[index]);
    public decimal Total => Rows.Sum(r => r.Total);
}

/// <summary>
/// การซื้อวัตถุดิบรายปีตามชื่อ (RPT-16, 17; #260, BR-061 ข), ระดับ B on A4 นอน: every month of the year is computed at once from the receipts — no button per month, so no month is counted twice or
/// skipped (AC-46). **Every delivery counts, the failed ones too.** Each month's figure is rounded to a whole number (RND-D, half to even: 1,234.56 → 1,235) and the year is the sum of those.
/// </summary>
public static class MaterialYearReports
{
    public static string Title(int year, bool units) => $"การซื้อวัตถุดิบปี {year + 543} ({(units ? "จำนวนหน่วย" : "จำนวนเงิน")})";

    public static MaterialYearReport Build(IReadOnlyList<ReceivedLine> lines, int year, bool units)
    {
        var rows = lines.Where(l => l.ReceiveDate.Year == year).GroupBy(l => l.MaterialId)
            .Select(g => new MaterialYearRow(g.First().MaterialCode, g.First().MaterialName, g.First().Unit,
                Enumerable.Range(1, 12).Select(m => decimal.Round(g.Where(l => l.ReceiveDate.Month == m).Sum(l => units ? l.Quantity : l.Amount), 0, MidpointRounding.ToEven)).ToList()))
            .OrderByDescending(r => r.Total).ThenBy(r => r.Code, StringComparer.Ordinal).ToList();
        return new MaterialYearReport(year, units, rows);
    }

    public static RouteGroupBuilder MapMaterialYearReports(this RouteGroupBuilder reports)
    {
        reports.MapGet("/purch/receipts/yearly-materials", async (int? year, bool? units, GoodsReceiptDirectory receipts, HttpContext http, ReportPrinter printer, CancellationToken cancellationToken) =>
        {
            if (year is not { } y || y is < 1900 or > 2999) return Results.BadRequest("กรุณาเลือกปี");
            var report = Build(await receipts.GetReceivedLinesAsync(new DateOnly(y, 1, 1), new DateOnly(y, 12, 31)), y, units == true);
            var pdf = await printer.PrintAsync<MaterialYearView>(Title(y, report.Units), Paper.A4Landscape, new() { ["Report"] = report }, AccountClaims.DisplayNameOf(http.User), cancellationToken: cancellationToken);
            return ReportsRegistration.Pdf(http, pdf, report.Units ? "ซื้อวัตถุดิบรายปี (จำนวนหน่วย)" : "ซื้อวัตถุดิบรายปี (จำนวนเงิน)");
        });
        return reports;
    }
}
