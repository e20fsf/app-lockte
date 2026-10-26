using Lockte.Prod;
using Lockte.SharedKernel;

namespace Lockte.Web.Reports.Order;

public sealed record WithdrawalSlipGroup(string DocumentNo, DateOnly? Date, string? Employee, IReadOnlyList<WithdrawalReportRow> Lines);

/// <summary>RPT-18 รายงานใบเบิก: the slips of a range, each with its lines.</summary>
public sealed record WithdrawalReport(DateOnly From, DateOnly To, IReadOnlyList<WithdrawalSlipGroup> Slips)
{
    public int Lines => Slips.Sum(s => s.Lines.Count);
}

/// <summary>RPT-18 รายงานใบเบิก (#120), ระดับ B on A4 ตั้ง. The legacy report has no printed sample, so the layout is the common one for a document with groups.</summary>
public static class WithdrawalReports
{
    public const string Title = "รายงานใบเบิก";

    public static async Task<WithdrawalReport> BuildAsync(StockWithdrawalDirectory withdrawals, IProductionEmployeeLookup employees, DateOnly from, DateOnly to)
    {
        var rows = await withdrawals.GetReportRowsAsync(from, to);
        var names = new Dictionary<int, string>();
        foreach (var id in rows.Where(r => r.EmployeeId is not null).Select(r => r.EmployeeId!.Value).Distinct())
            if (await employees.FindAsync(id) is { } employee) names[id] = $"{employee.Code} {employee.Name}";
        var slips = rows.GroupBy(r => (r.DocumentNo, r.Date, r.EmployeeId)).OrderBy(g => g.Key.Date).ThenBy(g => g.Key.DocumentNo, StringComparer.Ordinal)
            .Select(g => new WithdrawalSlipGroup(g.Key.DocumentNo, g.Key.Date, g.Key.EmployeeId is { } id ? names.GetValueOrDefault(id) : null, g.OrderBy(l => l.LineNo).ToList())).ToList();
        return new(from, to, slips);
    }

    public static RouteGroupBuilder MapWithdrawalReports(this RouteGroupBuilder reports)
    {
        reports.MapGet("/order/withdrawals", async (DateOnly? from, DateOnly? to, HttpContext http, StockWithdrawalDirectory withdrawals, IProductionEmployeeLookup employees,
            ReportPrinter printer, CancellationToken ct)
            => HrAttendanceReports.Range(from, to) ?? await HrAttendanceReports.PrintAsync<WithdrawalReportView>(http, printer, Title, Paper.A4Portrait,
                await BuildAsync(withdrawals, employees, from!.Value, to!.Value), Title, ct));
        return reports;
    }
}
