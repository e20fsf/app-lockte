using Lockte.Cash;
using Lockte.Web.Accounts;

namespace Lockte.Web.Reports.Cash;

/// <summary>RPT-05: the active expense types, in the order the list prints.</summary>
public sealed record ExpenseTypeListReport(IReadOnlyList<string> Names);

/// <summary>
/// รายการค่าใช้จ่ายต่างๆ (RPT-05, ปุ่ม "พิมพ์ค่าใช้จ่ายต่างๆ" ของ SCR-02; #229), ระดับ B on A4 ตั้ง: ลำดับ + รายการ, only the types in use, by name in the Thai
/// dictionary order (the leading vowels do not count) as the legacy sorted — the directory already gives that order.
/// </summary>
public static class ExpenseTypeListReports
{
    public const string Title = "รายการค่าใช้จ่ายต่างๆ";

    public static ExpenseTypeListReport Build(IReadOnlyList<ExpenseTypeDto> active) => new(active.Select(t => t.Name).ToList());

    public static RouteGroupBuilder MapCashExpenseTypeReports(this RouteGroupBuilder reports)
    {
        reports.MapGet("/cash/expense-types", async (ExpenseTypeDirectory types, HttpContext http, ReportPrinter printer, CancellationToken cancellationToken) =>
        {
            var pdf = await printer.PrintAsync<ExpenseTypeListView>(Title, Paper.A4Portrait, new() { ["Report"] = Build(await types.ListActiveAsync()) },
                AccountClaims.DisplayNameOf(http.User), cancellationToken: cancellationToken);
            return ReportsRegistration.Pdf(http, pdf, Title);
        });
        return reports;
    }
}
