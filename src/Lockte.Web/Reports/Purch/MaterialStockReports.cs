using Lockte.Purch;
using Lockte.Web.Accounts;

namespace Lockte.Web.Reports.Purch;

/// <summary>A line of RPT-14: เข้า when the quantity is ≥ 0, เบิก (kept negative) when it is below 0, and the running balance after it.</summary>
public sealed record StockCardRow(DateOnly Date, string? DocumentNo, string? LineNo, decimal? In, decimal? Out, decimal Balance, string? EmployeeCode, string? Note);

/// <summary>RPT-14 STOCK CARD of one material: the current period only (since the last ตัดสต็อก), the carried-forward balance first.</summary>
public sealed record StockCardReport(string Code, string Name, string? Unit, IReadOnlyList<StockCardRow> Rows)
{
    public decimal TotalIn => Rows.Sum(r => r.In ?? 0);
    public decimal TotalOut => Rows.Sum(r => r.Out ?? 0);
    public decimal Balance => TotalIn + TotalOut;
}

/// <summary>A line of RPT-26: the balance carried forward at the cut date × the register's price on the day of printing.</summary>
public sealed record StockCutRow(string Code, string Name, string? Unit, decimal Quantity, decimal Price)
{
    public decimal Amount => Quantity * Price;
}

/// <summary>RPT-26 รายงานการตัด STOCK วันที่ …: every material with a balance (≠ 0) at the cut, valued at the latest price — so printing it later gives another value (Q-54, kept).</summary>
public sealed record StockCutReport(DateOnly CutDate, IReadOnlyList<StockCutRow> Rows)
{
    public decimal Total => Rows.Sum(r => r.Amount);
}

/// <summary>
/// STOCK CARD (RPT-14, BR-041) and รายงานการตัด STOCK (RPT-26, BR-042; #262), ระดับ B. The card is computed from the open rows of the ledger each time — no work table — and the balance is the
/// running sum of เข้า + เบิก; an overdrawn carried-forward balance sits in the เบิก column.
/// </summary>
public static class MaterialStockReports
{
    public const string CardTitle = "STOCK CARD";
    public const string CutTitle = "รายงานการตัด STOCK";

    public static StockCardReport Card(MaterialDto material, IReadOnlyList<StockEntryDto> entries, IReadOnlyDictionary<int, string> employeeCodes)
    {
        var balance = 0m;
        var rows = new List<StockCardRow>();
        foreach (var e in entries.OrderBy(e => e.EntryDate).ThenBy(e => e.Kind == StockEntryKind.OpeningBalance ? 0 : 1).ThenBy(e => e.DocumentNo, StringComparer.Ordinal)
                     .ThenBy(e => e.DocumentLineNo, StringComparer.Ordinal).ThenBy(e => e.Id))
        {
            balance += e.Quantity;
            rows.Add(new StockCardRow(e.EntryDate, e.DocumentNo, e.DocumentLineNo, e.Quantity >= 0 ? e.Quantity : null, e.Quantity < 0 ? e.Quantity : null, balance,
                e.EmployeeId is { } id ? employeeCodes.GetValueOrDefault(id) : null, e.Note));
        }
        return new StockCardReport(material.Code, material.Name, material.Unit, rows);
    }

    public static StockCutReport Cut(DateOnly cutDate, IReadOnlyList<StockPeriodBalanceDto> balances)
        => new(cutDate, balances.Where(b => b.Quantity != 0).OrderBy(b => b.MaterialCode, StringComparer.Ordinal).Select(b => new StockCutRow(b.MaterialCode, b.MaterialName, b.Unit, b.Quantity, b.Cost)).ToList());

    public static RouteGroupBuilder MapMaterialStockReports(this RouteGroupBuilder reports)
    {
        reports.MapGet("/purch/stock-card", async (int? material, MaterialDirectory materials, StockDirectory stock, IPurchEmployeeLookup employees, HttpContext http, ReportPrinter printer,
            CancellationToken cancellationToken) =>
        {
            if (material is not { } id || await materials.GetAsync(id) is not { } found) return Results.BadRequest("กรุณาเลือกรหัสวัตถุดิบ");
            var entries = await stock.GetEntriesAsync(id);
            var codes = new Dictionary<int, string>();
            foreach (var employeeId in entries.Where(e => e.EmployeeId is not null).Select(e => e.EmployeeId!.Value).Distinct())
                if (await employees.FindAsync(employeeId) is { } employee) codes[employeeId] = employee.Code;
            var pdf = await printer.PrintAsync<StockCardView>(CardTitle, Paper.A4Portrait, new() { ["Report"] = Card(found, entries, codes) }, AccountClaims.DisplayNameOf(http.User), cancellationToken: cancellationToken);
            return ReportsRegistration.Pdf(http, pdf, "STOCK CARD " + found.Code);
        });

        reports.MapGet("/purch/stock-cut", async (DateOnly? date, StockClosingDirectory closing, HttpContext http, ReportPrinter printer, CancellationToken cancellationToken) =>
        {
            if (date is not { } day) return Results.BadRequest("กรุณาเลือกวันที่ตัด STOCK");
            var report = Cut(day, await closing.GetBalancesAsync(day));
            var pdf = await printer.PrintAsync<StockCutView>(CutTitle, Paper.A4Portrait, new() { ["Report"] = report }, AccountClaims.DisplayNameOf(http.User), cancellationToken: cancellationToken);
            return ReportsRegistration.Pdf(http, pdf, "ตัด STOCK " + day.ToString("yyyy-MM-dd"));
        });
        return reports;
    }
}
