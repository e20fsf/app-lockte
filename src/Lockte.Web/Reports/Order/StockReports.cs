using Lockte.Prod;
using Lockte.SharedKernel;

namespace Lockte.Web.Reports.Order;

/// <summary>RPT-11 รายการSTOCKคงเหลือ of a กลุ่มสินค้า: the balances (open entries) of the products whose code matches the group, only those that are not 0.</summary>
public sealed record StockBalanceReport(string Pattern, string? GroupName, DateOnly AsOf, IReadOnlyList<StockBalance> Lines);

public sealed record StockCardLine(DateOnly? Date, string? DocumentNo, int? LineNo, decimal In, decimal Out, decimal Balance, string? Employee, string? Remark);

/// <summary>RPT-12 STOCK CARD of a product: its entries, in and out apart, with the running balance.</summary>
public sealed record StockCardReport(string ProductCode, string ProductName, string? Unit, IReadOnlyList<StockCardLine> Lines)
{
    public decimal TotalIn => Lines.Sum(l => l.In);
    public decimal TotalOut => Lines.Sum(l => l.Out);
    public decimal Balance => TotalIn + TotalOut;
}

/// <summary>RPT-11 and RPT-12 (#124), ระดับ B: the stock from the ledger of module ผลิต — the balance is the sum of the entries not closed (BR-006).</summary>
public static class StockReports
{
    public const string BalanceTitle = "รายการสต็อกคงเหลือ";
    public const string CardTitle = "STOCK CARD";

    /// <summary>The products matching <paramref name="pattern"/> (such as <c>SB*</c>, <c>G1*</c>; a pattern that several groups share counts for each — Q-26) with a balance that is not 0.</summary>
    public static async Task<StockBalanceReport> BalanceAsync(StockDirectory stock, ProductDirectory products, string pattern, DateOnly asOf)
    {
        pattern = pattern.Trim();
        var group = (await products.GetGroupsAsync()).FirstOrDefault(g => string.Equals(g.Pattern, pattern, StringComparison.OrdinalIgnoreCase));
        var lines = (await stock.GetBalancesAsync(nonZeroOnly: true)).Where(b => ProductGroupPattern.Matches(pattern, b.ProductCode)).ToList();
        return new(pattern, group?.Name, asOf, lines);
    }

    /// <summary>One line per entry of the current period, by date, then number, then line; <c>In</c> is the entries ≥ 0, <c>Out</c> the negative ones (shown negative).</summary>
    public static async Task<StockCardReport?> CardAsync(StockDirectory stock, ProductDirectory products, IProductionEmployeeLookup employees, int productId)
    {
        if (await products.GetAsync(productId) is not { } product) return null;
        var entries = (await stock.GetEntriesAsync(productId)).OrderBy(e => e.Date).ThenBy(e => e.DocumentNo, StringComparer.Ordinal).ThenBy(e => e.LineNo).ThenBy(e => e.Id).ToList();
        var names = new Dictionary<int, string>();
        foreach (var id in entries.Where(e => e.EmployeeId is not null).Select(e => e.EmployeeId!.Value).Distinct())
            if (await employees.FindAsync(id) is { } employee) names[id] = employee.Code;
        decimal running = 0;
        var lines = new List<StockCardLine>();
        foreach (var e in entries)
        {
            running += e.Quantity;
            lines.Add(new(e.Date, e.DocumentNo, e.LineNo, e.Quantity >= 0 ? e.Quantity : 0, e.Quantity < 0 ? e.Quantity : 0, running, e.EmployeeId is { } id ? names.GetValueOrDefault(id) : null, e.Remark));
        }
        return new(product.Code, product.Name, product.Unit, lines);
    }

    public static RouteGroupBuilder MapStockReports(this RouteGroupBuilder reports)
    {
        var group = reports.MapGroup("/order/stock");

        group.MapGet("/balance", async (string? pattern, HttpContext http, StockDirectory stock, ProductDirectory products, ReportPrinter printer, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(pattern)) return Results.BadRequest("กรุณาเลือกกลุ่มสินค้า");
            var report = await BalanceAsync(stock, products, pattern, DateOnly.FromDateTime(DateTime.Today));
            return await HrAttendanceReports.PrintAsync<StockBalanceView>(http, printer, BalanceTitle, Paper.A4Portrait, report, $"{BalanceTitle} {pattern.Trim()}", ct);
        });

        group.MapGet("/card", async (int? product, HttpContext http, StockDirectory stock, ProductDirectory products, IProductionEmployeeLookup employees, ReportPrinter printer, CancellationToken ct) =>
        {
            if (product is null) return Results.BadRequest("กรุณาเลือกสินค้า");
            if (await CardAsync(stock, products, employees, product.Value) is not { } report) return Results.NotFound("ไม่พบสินค้า");
            return await HrAttendanceReports.PrintAsync<StockCardView>(http, printer, CardTitle, Paper.A4Portrait, report, $"{CardTitle} {report.ProductCode}", ct);
        });

        return reports;
    }
}
