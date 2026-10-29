using Lockte.Purch;
using Lockte.Web.Accounts;

namespace Lockte.Web.Reports.Purch;

/// <summary>One group of RPT-25; the figures are null while the group has no receipt — printed blank, not 0 (BR-062 #2).</summary>
public sealed record ResaleRow(string Name, decimal? Quantity, decimal? Amount);

/// <summary>RPT-25 รายงานการรับสินค้าซื้อมาขายไป: the fixed groups in the order of the settings.</summary>
public sealed record ResaleReport(DateOnly From, DateOnly To, IReadOnlyList<ResaleRow> Rows)
{
    public decimal TotalQuantity => Rows.Sum(r => r.Quantity ?? 0);
    public decimal TotalAmount => Rows.Sum(r => r.Amount ?? 0);
}

/// <summary>RPT-11 สรุปใบสั่งซื้อยอดต่ำกว่า 5,000 บาท: the lines of the small orders and who approves them.</summary>
public sealed record SmallOrderReport(DateOnly From, DateOnly To, string? Approver, IReadOnlyList<SmallOrderLine> Lines)
{
    public decimal Total => Lines.Sum(l => l.AmountWithTax);
}

/// <summary>
/// รายงานการรับสินค้าซื้อมาขายไป (RPT-25, BR-062) and สรุปใบสั่งซื้อต่ำกว่า 5,000 บาท (RPT-11, BR-063; #261), ระดับ B. RPT-25 adds up the receipts of the period by the prefix of the code
/// (settings, case ignored) and **counts the failed ones too**; each figure is a whole number (RND-D, half to even) and the total adds the rounded groups.
/// </summary>
public static class ResaleAndSmallOrderReports
{
    public const string ResaleTitle = "รายงานการรับสินค้าซื้อมาขายไป";
    public const string SmallOrderTitle = "สรุปใบสั่งซื้อยอดเงินต่ำกว่า 5,000 บาท";

    public static ResaleReport Resale(IReadOnlyList<ReceivedLine> lines, IReadOnlyList<ResaleGroupDto> groups, DateOnly from, DateOnly to)
    {
        var inRange = lines.Where(l => l.ReceiveDate >= from && l.ReceiveDate <= to).ToList();
        // a code belongs to the first group whose prefix it starts with (case ignored)
        var byGroup = inRange.ToLookup(l => groups.FirstOrDefault(g => l.MaterialCode.StartsWith(g.Prefix, StringComparison.OrdinalIgnoreCase)));
        var rows = groups.Select(g =>
        {
            var mine = byGroup[g].ToList();
            return mine.Count == 0 ? new ResaleRow(g.Name, null, null)
                : new ResaleRow(g.Name, decimal.Round(mine.Sum(l => l.Quantity), 0, MidpointRounding.ToEven), decimal.Round(mine.Sum(l => l.Amount), 0, MidpointRounding.ToEven));
        }).ToList();
        return new ResaleReport(from, to, rows);
    }

    public static SmallOrderReport SmallOrders(IReadOnlyList<SmallOrderLine> lines, string? approver, DateOnly from, DateOnly to)
        => new(from, to, string.IsNullOrWhiteSpace(approver) ? null : approver.Trim(), lines);

    public static RouteGroupBuilder MapResaleAndSmallOrderReports(this RouteGroupBuilder reports)
    {
        reports.MapGet("/purch/receipts/resale", async (DateOnly? from, DateOnly? to, GoodsReceiptDirectory receipts, PurchSettingsDirectory settings, HttpContext http, ReportPrinter printer,
            CancellationToken cancellationToken) =>
        {
            if (from is null || to is null) return Results.BadRequest("กรุณากรอกช่วงวันที่");
            if (from > to) return Results.BadRequest("ตั้งแต่วันที่ต้องไม่หลังถึงวันที่");
            var report = Resale(await receipts.GetReceivedLinesAsync(from.Value, to.Value), (await settings.GetAsync()).ResaleGroups, from.Value, to.Value);
            var pdf = await printer.PrintAsync<ResaleView>(ResaleTitle, Paper.A4Portrait, new() { ["Report"] = report }, AccountClaims.DisplayNameOf(http.User), cancellationToken: cancellationToken);
            return ReportsRegistration.Pdf(http, pdf, "รับสินค้าซื้อมาขายไป");
        });

        reports.MapGet("/purch/small-orders", async (DateOnly? from, DateOnly? to, string? approver, PurchaseOrderDirectory orders, PurchSettingsDirectory settings, HttpContext http, ReportPrinter printer,
            CancellationToken cancellationToken) =>
        {
            if (from is null || to is null) return Results.BadRequest("กรุณากรอกช่วงวันที่");
            if (from > to) return Results.BadRequest("ตั้งแต่วันที่ต้องไม่หลังถึงวันที่");
            var limit = (await settings.GetAsync()).SmallOrderLimit;
            var report = SmallOrders(await orders.GetSmallOrderLinesAsync(from.Value, to.Value, limit), approver, from.Value, to.Value);
            var pdf = await printer.PrintAsync<SmallOrderView>(SmallOrderTitle, Paper.A4Landscape, new() { ["Report"] = report }, AccountClaims.DisplayNameOf(http.User), cancellationToken: cancellationToken);
            return ReportsRegistration.Pdf(http, pdf, "ใบสั่งซื้อต่ำกว่า 5000 บาท");
        });
        return reports;
    }
}
