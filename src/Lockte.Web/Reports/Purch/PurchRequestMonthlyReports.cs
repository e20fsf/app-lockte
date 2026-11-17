using Lockte.Org;
using Lockte.Purch;
using Lockte.SharedKernel;
using Lockte.Web.Accounts;
using Lockte.Web.Reports.Order;

namespace Lockte.Web.Reports.Purch;

/// <summary>
/// สรุปจำนวนใบขออนุมัติสั่งซื้อรายเดือนแยกตามแผนก (#420): one row per แผนก that has requests in the chosen months of a year, one column per month and the total of the
/// row, with the total of every month at the foot. Counts requests (heads), not items. PDF (A4 นอน) or Excel, the same <see cref="SummaryTable"/>.
/// </summary>
public static class PurchRequestMonthlyReports
{
    public const string Title = "สรุปใบขออนุมัติสั่งซื้อรายเดือนแยกตามแผนก";

    private static readonly string[] MonthNames = ["ม.ค.", "ก.พ.", "มี.ค.", "เม.ย.", "พ.ค.", "มิ.ย.", "ก.ค.", "ส.ค.", "ก.ย.", "ต.ค.", "พ.ย.", "ธ.ค."];

    public static string? Problem(int? year, int? fromMonth, int? toMonth)
        => year is not { } y || y is < 1900 or > 2999 ? "กรุณาเลือกปี"
            : fromMonth is not { } f || toMonth is not { } t || f is < 1 or > 12 || t is < 1 or > 12 ? "กรุณาเลือกเดือน"
            : f > t ? "ตั้งแต่เดือนต้องไม่หลังถึงเดือน" : null;

    /// <summary>The table of the months <paramref name="fromMonth"/>…<paramref name="toMonth"/> of <paramref name="year"/> (ค.ศ.); แผนกs with no request in them are left out.</summary>
    public static SummaryTable Build(IReadOnlyList<(int DepartmentId, DateOnly RequestDate)> requests, IReadOnlyDictionary<int, string> departments, int year, int fromMonth, int toMonth)
    {
        var months = Enumerable.Range(fromMonth, toMonth - fromMonth + 1).ToList();
        var inRange = requests.Where(r => r.RequestDate.Year == year && r.RequestDate.Month >= fromMonth && r.RequestDate.Month <= toMonth).ToList();
        var columns = new List<SummaryColumn> { new("แผนก") };
        columns.AddRange(months.Select(m => new SummaryColumn(MonthNames[m - 1], SummaryFormat.Integer)));
        columns.Add(new("รวม", SummaryFormat.Integer));

        var rows = inRange.GroupBy(r => r.DepartmentId)
            .Select(g => (Name: departments.GetValueOrDefault(g.Key) ?? "", Counts: months.Select(m => g.Count(r => r.RequestDate.Month == m)).ToList()))
            .OrderBy(r => r.Name, StringComparer.Ordinal)
            .Select(r =>
            {
                var cells = new List<object?> { r.Name };
                cells.AddRange(r.Counts.Select(c => (object?)c));
                cells.Add(r.Counts.Sum());
                return cells.ToArray();
            }).ToList();

        var total = new List<object?> { "รวมทั้งหมด" };
        total.AddRange(months.Select(m => (object?)inRange.Count(r => r.RequestDate.Month == m)));
        total.Add(inRange.Count);

        var range = fromMonth == toMonth ? MonthNames[fromMonth - 1] : $"{MonthNames[fromMonth - 1]} – {MonthNames[toMonth - 1]}";
        return new(Title, $"ปี พ.ศ. {year + 543}  เดือน {range}", columns, rows, rows.Count == 0 ? null : total.ToArray());
    }

    public static RouteGroupBuilder MapPurchRequestMonthlyReports(this RouteGroupBuilder reports)
    {
        reports.MapGet("/purch/requests/monthly", async (int? year, int? fromMonth, int? toMonth, string? format, HttpContext http, PurchaseRequestDirectory requests, OrgDirectory org,
            ReportPrinter printer, CancellationToken cancellationToken) =>
        {
            if (Problem(year, fromMonth, toMonth) is { } problem) return Results.BadRequest(problem);
            var from = new DateOnly(year!.Value, fromMonth!.Value, 1);
            var to = new DateOnly(year.Value, toMonth!.Value, 1).AddMonths(1).AddDays(-1);
            var names = (await org.GetDepartmentsAsync()).ToDictionary(d => d.Id, d => d.Name);
            var table = Build(await requests.GetRequestDatesAsync(from, to), names, year.Value, fromMonth.Value, toMonth.Value);
            if (string.Equals(format, "xlsx", StringComparison.OrdinalIgnoreCase)) return Results.File(XlsxWriter.Write(table), XlsxWriter.ContentType, Title + ".xlsx");
            var pdf = await printer.PrintAsync<SummaryTableView>(Title, Paper.A4Landscape, new() { ["Table"] = table }, AccountClaims.DisplayNameOf(http.User),
                cancellationToken: cancellationToken, program: ReportProgram.Purch);
            return ReportsRegistration.Pdf(http, pdf, Title);
        });
        return reports;
    }
}
