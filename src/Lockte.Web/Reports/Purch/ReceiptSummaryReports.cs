using Lockte.Org;
using Lockte.Purch;
using Lockte.Web.Accounts;

namespace Lockte.Web.Reports.Purch;

/// <summary>RPT-20 / RPT-23: one row per material of the period; the head of RPT-20 says how many purchase requests and orders the receipts came from.</summary>
public sealed record MonthlyReceiptSummary(bool MainOnly, DateOnly From, DateOnly To, IReadOnlyList<ReceiptMaterialRow> Rows, int RequestCount, int OrderCount)
{
    public decimal TotalAmount => Rows.Sum(r => r.Amount);
}

/// <summary>One month of RPT-21 / RPT-24.</summary>
public sealed record MonthRow(int Month, int RequestCount, int OrderCount, decimal Quantity, decimal Amount);

/// <summary>RPT-21 / RPT-24: the twelve months of one year, computed from the receipts every time (BR-061: no accumulation table to clear or replace).</summary>
public sealed record YearlyReceiptSummary(bool MainOnly, int Year, IReadOnlyList<MonthRow> Months)
{
    public decimal TotalAmount => Months.Sum(m => m.Amount);
    public decimal TotalQuantity => Months.Sum(m => m.Quantity);
    public int TotalRequests => Months.Sum(m => m.RequestCount);
    public int TotalOrders => Months.Sum(m => m.OrderCount);
}

/// <summary>
/// สรุปการรับวัตถุดิบ (RPT-20 … 24; #259, BR-061 ก), ระดับ B. The purchase totals (RPT-20, 21, 22) leave out the deliveries that failed the inspection; the main-material ones (RPT-23, 24) count
/// only the main materials and do **not** cut the failed. Grouped by the id of the material and the แผนก, not by their names.
/// </summary>
public static class ReceiptSummaryReports
{
    public const string MonthlyTitle = "สรุปการรับวัตถุดิบเดือน";
    public const string MonthlyMainTitle = "สรุปการรับวัตถุดิบหลักเดือน";
    public const string YearlyTitle = "สรุปการรับวัตถุดิบ";
    public const string YearlyMainTitle = "สรุปการรับวัตถุดิบหลัก";
    public const string DepartmentPurchasesTitle = "การซื้อวัตถุดิบแยกแผนก";

    private static IEnumerable<ReceivedLine> Counted(IEnumerable<ReceivedLine> lines, bool mainOnly)
        => mainOnly ? lines.Where(l => l.MaterialIsMain) : lines.Where(l => !l.Failed);

    public static MonthlyReceiptSummary Monthly(IReadOnlyList<ReceivedLine> lines, bool mainOnly, DateOnly from, DateOnly to)
    {
        var counted = Counted(lines.Where(l => l.ReceiveDate >= from && l.ReceiveDate <= to), mainOnly).ToList();
        var rows = counted.GroupBy(l => l.MaterialId)
            .Select(g => new ReceiptMaterialRow(g.First().MaterialCode, g.First().MaterialName, g.First().Unit, g.Sum(l => l.Quantity), g.Sum(l => l.Amount)))
            .OrderByDescending(r => r.Amount).ThenBy(r => r.Code, StringComparer.Ordinal).ToList();
        return new MonthlyReceiptSummary(mainOnly, from, to, rows, counted.Select(l => l.RequestNo).Distinct().Count(), counted.Select(l => l.OrderNo).Distinct().Count());
    }

    public static YearlyReceiptSummary Yearly(IReadOnlyList<ReceivedLine> lines, bool mainOnly, int year)
    {
        var counted = Counted(lines.Where(l => l.ReceiveDate.Year == year), mainOnly).ToList();
        var months = Enumerable.Range(1, 12).Select(m =>
        {
            var inMonth = counted.Where(l => l.ReceiveDate.Month == m).ToList();
            return new MonthRow(m, inMonth.Select(l => l.RequestNo).Distinct().Count(), inMonth.Select(l => l.OrderNo).Distinct().Count(), inMonth.Sum(l => l.Quantity), inMonth.Sum(l => l.Amount));
        }).Where(m => m.Amount != 0 || m.Quantity != 0 || m.OrderCount != 0).ToList();
        return new YearlyReceiptSummary(mainOnly, year, months);
    }

    /// <summary>RPT-22: the amount of every แผนก, the biggest first.</summary>
    public static DepartmentSummaryReport DepartmentPurchases(IReadOnlyList<ReceivedLine> lines, IReadOnlyDictionary<int, string> departments, DateOnly from, DateOnly to)
        => new(from, to, lines.Where(l => !l.Failed && l.ReceiveDate >= from && l.ReceiveDate <= to).GroupBy(l => l.DepartmentId)
            .Select(g => (Department: departments.GetValueOrDefault(g.Key) ?? "", Amount: g.Sum(l => l.Amount)))
            .OrderByDescending(r => r.Amount).ThenBy(r => r.Department, StringComparer.Ordinal).ToList());

    public static RouteGroupBuilder MapReceiptSummaryReports(this RouteGroupBuilder reports)
    {
        var group = reports.MapGroup("/purch/receipts");

        group.MapGet("/summary", async (DateOnly? from, DateOnly? to, bool? main, GoodsReceiptDirectory receipts, HttpContext http, ReportPrinter printer, CancellationToken cancellationToken) =>
        {
            if (from is null || to is null) return Results.BadRequest("กรุณากรอกช่วงวันที่");
            if (from > to) return Results.BadRequest("ตั้งแต่วันที่ต้องไม่หลังถึงวันที่");
            var mainOnly = main == true;
            var report = Monthly(await receipts.GetReceivedLinesAsync(from.Value, to.Value), mainOnly, from.Value, to.Value);
            var pdf = await printer.PrintAsync<MonthlyReceiptSummaryView>(mainOnly ? MonthlyMainTitle : MonthlyTitle, Paper.A4Portrait, new() { ["Report"] = report }, AccountClaims.DisplayNameOf(http.User),
                cancellationToken: cancellationToken);
            return ReportsRegistration.Pdf(http, pdf, mainOnly ? "สรุปรับวัตถุดิบหลัก" : "สรุปรับวัตถุดิบ");
        });

        group.MapGet("/yearly", async (int? year, bool? main, GoodsReceiptDirectory receipts, HttpContext http, ReportPrinter printer, CancellationToken cancellationToken) =>
        {
            if (year is not { } y || y is < 1900 or > 2999) return Results.BadRequest("กรุณาเลือกปี");
            var mainOnly = main == true;
            var report = Yearly(await receipts.GetReceivedLinesAsync(new DateOnly(y, 1, 1), new DateOnly(y, 12, 31)), mainOnly, y);
            var pdf = await printer.PrintAsync<YearlyReceiptSummaryView>(mainOnly ? YearlyMainTitle : YearlyTitle, Paper.A4Portrait, new() { ["Report"] = report }, AccountClaims.DisplayNameOf(http.User),
                cancellationToken: cancellationToken);
            return ReportsRegistration.Pdf(http, pdf, mainOnly ? "สรุปรับวัตถุดิบหลักรายเดือน" : "สรุปรับวัตถุดิบรายเดือน");
        });

        group.MapGet("/department-purchases", async (DateOnly? from, DateOnly? to, GoodsReceiptDirectory receipts, OrgDirectory org, HttpContext http, ReportPrinter printer, CancellationToken cancellationToken) =>
        {
            if (from is null || to is null) return Results.BadRequest("กรุณากรอกช่วงวันที่");
            if (from > to) return Results.BadRequest("ตั้งแต่วันที่ต้องไม่หลังถึงวันที่");
            var names = (await org.GetDepartmentsAsync()).ToDictionary(d => d.Id, d => d.Name);
            var report = DepartmentPurchases(await receipts.GetReceivedLinesAsync(from.Value, to.Value), names, from.Value, to.Value);
            var pdf = await printer.PrintAsync<DepartmentReceiptSummaryView>(DepartmentPurchasesTitle, Paper.A4Portrait, new() { ["Report"] = report }, AccountClaims.DisplayNameOf(http.User),
                cancellationToken: cancellationToken);
            return ReportsRegistration.Pdf(http, pdf, "ซื้อวัตถุดิบแยกแผนก");
        });
        return reports;
    }
}
