using Lockte.Prod;
using Lockte.Sales;
using Lockte.SharedKernel;
using Lockte.Web.Accounts;

namespace Lockte.Web.Reports.Order;

/// <summary>What narrows a summary of the deliveries: the range of วันที่ส่ง and, as the report asks, a customer, a เขตการขาย, a code pattern (<c>SB*</c>).</summary>
public sealed record ExportFilter(DateOnly From, DateOnly To, int? CustomerId = null, int? SalesTerritoryId = null, string? CodePattern = null, bool SortByName = false);

/// <summary>
/// สรุปการส่งออก (RPT-08, #114): ตามลูกค้า, ตามสินค้า, ตามรุ่น and the year in 4 forms (แบบ 100/200/300/400), each as a <see cref="SummaryTable"/> printed ระดับ B or
/// downloaded as Excel (INT-01). The month of a delivery is worked out from its date every time — nothing is written into the delivery history (BR-024 not made).
/// </summary>
public static class ExportSummaries
{
    public const string CustomerDetailTitle = "รายงานการส่งออก";
    public const string CustomerSummaryTitle = "สรุปการส่งออกตามลูกค้า";
    public const string ModelSummaryTitle = "สรุปการส่งออกตามรุ่น";
    public const string ProductSummaryPrefix = "สรุปส่งออกเดือน";
    public const string YearTitle = "สรุปส่งออกทั้งปี";

    private static readonly SummaryColumn Number = new("จำนวน", SummaryFormat.Quantity);
    private static readonly SummaryColumn Amount = new("จำนวนเงิน", SummaryFormat.Money);

    /// <summary>"ตั้งแต่วันที่ … ถึงวันที่ …" and what else narrows the report (D-10); dates whole so they never overflow a box (D-2).</summary>
    public static async Task<string> FilterTextAsync(ExportFilter filter, CustomerDirectory customers, SalesTerritoryDirectory territories)
    {
        var parts = new List<string> { $"ตั้งแต่วันที่ {ReportText.Date(filter.From)} ถึงวันที่ {ReportText.Date(filter.To)}" };
        if (filter.CustomerId is { } customerId && await customers.GetAsync(customerId) is { } customer) parts.Add($"รหัสลูกค้า {customer.Code} ชื่อลูกค้า {customer.Name}");
        if (filter.SalesTerritoryId is { } territoryId && (await territories.GetAllAsync()).FirstOrDefault(t => t.Id == territoryId) is { } territory)
            parts.Add($"เขตการขาย {territory.Code} {territory.Name}");
        if (!string.IsNullOrWhiteSpace(filter.CodePattern)) parts.Add($"รหัสสินค้า {filter.CodePattern.Trim()}");
        return string.Join("  ", parts);
    }

    private static async Task<IReadOnlyList<DeliveryReportRow>> RowsAsync(SalesDeliveryReportDirectory deliveries, ExportFilter filter)
        => (await deliveries.GetDeliveriesAsync(filter.From, filter.To))
            .Where(r => filter.CustomerId is null || r.CustomerId == filter.CustomerId)
            .Where(r => filter.SalesTerritoryId is null || r.SalesTerritoryId == filter.SalesTerritoryId)
            .Where(r => string.IsNullOrWhiteSpace(filter.CodePattern) || (r.ProductCode is not null && ProductGroupPattern.Matches(filter.CodePattern.Trim(), r.ProductCode)))
            .ToList();

    private static string? NoUnit(string? unit) => string.IsNullOrWhiteSpace(unit) ? null : unit.Trim();

    // ---------- ตามลูกค้า ----------

    /// <summary>ส่งออกตามลูกค้า (the report of one customer): ที่, รหัส, ชื่อสินค้า, จำนวน + หน่วย, จำนวนเงิน, รวม.</summary>
    public static async Task<SummaryTable> CustomerDetailAsync(SalesDeliveryReportDirectory deliveries, CustomerDirectory customers, SalesTerritoryDirectory territories, ExportFilter filter)
    {
        var rows = (await RowsAsync(deliveries, filter)).GroupBy(r => (r.ProductId, Unit: NoUnit(r.Unit))).OrderBy(g => g.First().ProductCode, StringComparer.Ordinal)
            .Select((g, i) => new object?[] { i + 1, g.First().ProductCode, g.First().ProductName, g.Sum(r => r.Quantity), g.Key.Unit, g.Sum(r => r.Amount ?? 0) }).ToList();
        return new(CustomerDetailTitle, await FilterTextAsync(filter, customers, territories),
            [new("ที่", SummaryFormat.Integer), new("รหัส"), new("ชื่อสินค้า"), Number, new("หน่วย"), Amount], rows,
            rows.Count == 0 ? null : [null, null, "รวม", null, null, rows.Sum(r => (decimal)r[5]!)]);
    }

    /// <summary>สรุปส่งออก1: per customer Σ จำนวน and Σ จำนวนเงิน, the most money first. The quantity adds different units (as the legacy) so it has no total.</summary>
    public static async Task<SummaryTable> CustomerSummaryAsync(SalesDeliveryReportDirectory deliveries, CustomerDirectory customers, SalesTerritoryDirectory territories, ExportFilter filter)
    {
        var rows = (await RowsAsync(deliveries, filter)).GroupBy(r => (r.CustomerCode, r.CustomerName))
            .Select(g => (g.Key, Quantity: g.Sum(r => r.Quantity), Money: g.Sum(r => r.Amount ?? 0)))
            .OrderByDescending(c => c.Money).ThenBy(c => c.Key.CustomerCode, StringComparer.Ordinal)
            .Select((c, i) => new object?[] { i + 1, c.Key.CustomerCode, c.Key.CustomerName, c.Quantity, c.Money }).ToList();
        return new(CustomerSummaryTitle, await FilterTextAsync(filter, customers, territories),
            [new("ที่", SummaryFormat.Integer), new("รหัส"), new("ชื่อลูกค้า"), Number, Amount], rows,
            rows.Count == 0 ? null : [null, null, "รวมทั้งหมด", null, rows.Sum(r => (decimal)r[4]!)]);
    }

    // ---------- ตามสินค้า ----------

    /// <summary>สรุปส่งออก22 / ตามชื่อสินค้า / ตามรหัสสินค้า: per product Σ จำนวน + หน่วย and Σ จำนวนเงิน, by code or by name; the month in the head comes from the dates (D-5).</summary>
    public static async Task<SummaryTable> ProductSummaryAsync(SalesDeliveryReportDirectory deliveries, CustomerDirectory customers, SalesTerritoryDirectory territories, ExportFilter filter)
    {
        var grouped = (await RowsAsync(deliveries, filter)).GroupBy(r => (r.ProductId, Unit: NoUnit(r.Unit)))
            .Select(g => (Code: g.First().ProductCode ?? "", Name: g.First().ProductName, g.Key.Unit, Quantity: g.Sum(r => r.Quantity), Money: g.Sum(r => r.Amount ?? 0)));
        var ordered = filter.SortByName ? grouped.OrderBy(p => p.Name, StringComparer.Ordinal).ThenBy(p => p.Code, StringComparer.Ordinal)
            : grouped.OrderBy(p => p.Code, StringComparer.Ordinal).ThenBy(p => p.Unit, StringComparer.Ordinal);
        var rows = ordered.Select((p, i) => new object?[] { i + 1, p.Code, p.Name, p.Quantity, p.Unit, p.Money }).ToList();
        return new($"{ProductSummaryPrefix} {ReportText.MonthTitle(filter.From, filter.To)}", await FilterTextAsync(filter, customers, territories),
            [new("ที่", SummaryFormat.Integer), new("รหัส"), new("ชื่อสินค้า"), Number, new("หน่วย"), Amount], rows,
            rows.Count == 0 ? null : [null, null, "รวมทั้งหมด", null, null, rows.Sum(r => (decimal)r[5]!)]);
    }

    /// <summary>สรุปส่งออก32: per model (the model of the product, "ไม่ระบุรุ่น" when it has none) and unit.</summary>
    public static async Task<SummaryTable> ModelSummaryAsync(SalesDeliveryReportDirectory deliveries, ProductDirectory products, CustomerDirectory customers, SalesTerritoryDirectory territories,
        ExportFilter filter)
    {
        var lines = await RowsAsync(deliveries, filter);
        var models = new Dictionary<int, string>();
        foreach (var id in lines.Select(l => l.ProductId).Distinct()) models[id] = (await products.GetAsync(id))?.ModelName ?? "ไม่ระบุรุ่น";
        var rows = lines.GroupBy(l => (Model: models[l.ProductId], Unit: NoUnit(l.Unit))).OrderBy(g => g.Key.Model, StringComparer.Ordinal).ThenBy(g => g.Key.Unit, StringComparer.Ordinal)
            .Select((g, i) => new object?[] { i + 1, g.Key.Model, g.Sum(l => l.Quantity), g.Key.Unit, g.Sum(l => l.Amount ?? 0) }).ToList();
        return new(ModelSummaryTitle, await FilterTextAsync(filter, customers, territories),
            [new("ที่", SummaryFormat.Integer), new("รุ่น"), Number, new("หน่วย"), Amount], rows,
            rows.Count == 0 ? null : [null, "รวมทั้งหมด", null, null, rows.Sum(r => (decimal)r[4]!)]);
    }

    // ---------- สรุปทั้งปี ----------

    /// <summary>
    /// The four forms of the year summary (macro สรุปส่งออกเดือน100/200/300/400): month columns by the month of วันที่ส่ง, then the total. <b>The total is blank when any month
    /// is blank</b> (Access added a Null and got Null; ⚠ Q-56 kept, AC-23): a month with no delivery is blank, not 0.
    /// แบบ 100 — Σ มูลค่า of every customer of the เขตการขาย (also those with none) + the territory; 200 — Σ มูลค่า of every customer; 300 — Σ จำนวน of every customer;
    /// 400 — Σ จำนวน by month of every product delivered, with the total จำนวน and the total จำนวนเงิน.
    /// </summary>
    public static async Task<SummaryTable> YearAsync(int form, SalesDeliveryReportDirectory deliveries, CustomerDirectory customers, SalesTerritoryDirectory territories, ExportFilter filter)
    {
        var month = ThaiDate.Culture.DateTimeFormat.MonthNames;
        var monthColumns = Enumerable.Range(0, 12).Select(m => new SummaryColumn(month[m], form == 300 || form == 400 ? SummaryFormat.Quantity : SummaryFormat.Money)).ToList();
        var delivered = (await deliveries.GetDeliveriesAsync(filter.From, filter.To)).Where(r => filter.SalesTerritoryId is null || form != 100 || r.SalesTerritoryId == filter.SalesTerritoryId).ToList();
        var rows = new List<(string Code, decimal?[] Months, decimal? Total, decimal? Total2, object?[] Head, object?[] Tail)>();

        decimal?[] Months(IEnumerable<DeliveryReportRow> source, Func<DeliveryReportRow, decimal> value)
        {
            var result = new decimal?[12];
            foreach (var row in source) result[row.DeliveryDate.Month - 1] = (result[row.DeliveryDate.Month - 1] ?? 0) + value(row);
            return result;
        }
        static decimal? Total(decimal?[] months) => months.Any(m => m is null) ? null : months.Sum(m => m!.Value);

        if (form == 400)
        {
            foreach (var g in delivered.GroupBy(r => (r.ProductId, Unit: NoUnit(r.Unit))))
            {
                var quantity = Months(g, r => r.Quantity);
                var money = Months(g, r => r.Amount ?? 0);
                rows.Add((g.First().ProductCode ?? "", quantity, Total(quantity), Total(money), [g.First().ProductCode, g.First().ProductName], []));
            }
        }
        else
        {
            var byCustomer = delivered.GroupBy(r => r.CustomerId).ToDictionary(g => g.Key);
            var territoryName = filter.SalesTerritoryId is { } tid ? (await territories.GetAllAsync()).FirstOrDefault(t => t.Id == tid) : null;
            foreach (var customer in await customers.GetAllAsync())
            {
                if (form == 100 && customer.SalesTerritoryId != filter.SalesTerritoryId) continue; // every customer of the เขตการขาย chosen, even with nothing delivered
                var source = byCustomer.TryGetValue(customer.Id, out var g) ? g.AsEnumerable() : [];
                var months = Months(source, form == 300 ? r => r.Quantity : r => r.Amount ?? 0);
                rows.Add((customer.Code, months, Total(months), null, [customer.Code, customer.Name], form == 100 ? [customer.SalesTerritoryCode, territoryName?.Name] : []));
            }
        }

        var ordered = rows.OrderBy(r => r.Code, StringComparer.Ordinal).ThenByDescending(r => r.Total ?? decimal.MinValue).ToList();
        var columns = new List<SummaryColumn>();
        if (form == 400) columns.AddRange([new("รหัสสินค้า"), new("ชื่อสินค้า")]);
        else columns.AddRange([new("รหัสลูกค้า"), new("ชื่อลูกค้า")]);
        columns.AddRange(monthColumns);
        columns.Add(new SummaryColumn(form == 300 || form == 400 ? "จำนวนรวม" : "จำนวนเงินรวม", form == 300 || form == 400 ? SummaryFormat.Quantity : SummaryFormat.Money));
        if (form == 400) columns.Add(new SummaryColumn("จำนวนเงินรวม", SummaryFormat.Money));
        if (form == 100) columns.AddRange([new SummaryColumn("รหัสเซล"), new SummaryColumn("ชื่อเซล")]);

        var table = ordered.Select(r =>
        {
            var cells = new List<object?>(r.Head);
            cells.AddRange(r.Months.Cast<object?>());
            cells.Add(r.Total);
            if (form == 400) cells.Add(r.Total2);
            cells.AddRange(r.Tail);
            return cells.ToArray();
        }).ToList();
        var title = form switch
        {
            100 => $"{YearTitle} (แบบ 100 จำนวนเงินตามเขตการขาย)",
            200 => $"{YearTitle} (แบบ 200 จำนวนเงิน)",
            300 => $"{YearTitle} (แบบ 300 จำนวน)",
            _ => $"{YearTitle} (แบบ 400 ตามสินค้า)",
        };
        return new(title, await FilterTextAsync(filter with { CustomerId = null, CodePattern = null, SalesTerritoryId = form == 100 ? filter.SalesTerritoryId : null }, customers, territories), columns, table);
    }

    public static string Title(SummaryTable table) => table.Title;

    // ---------- PDF and Excel ----------

    public static RouteGroupBuilder MapExportSummaries(this RouteGroupBuilder reports)
    {
        reports.MapGet("/order/exports/{kind}", async (string kind, DateOnly? from, DateOnly? to, int? customer, int? territory, string? code, bool? byName, string? format,
            HttpContext http, SalesDeliveryReportDirectory deliveries, CustomerDirectory customers, SalesTerritoryDirectory territories, ProductDirectory products,
            ReportPrinter printer, CancellationToken ct) =>
        {
            if (HrAttendanceReports.Range(from, to) is { } bad) return bad;
            var filter = new ExportFilter(from!.Value, to!.Value, customer, territory, code, byName == true);
            SummaryTable table;
            var wide = false;
            switch (kind)
            {
                case "customer-detail":
                    if (customer is null) return Results.BadRequest("กรุณาเลือกลูกค้า");
                    table = await CustomerDetailAsync(deliveries, customers, territories, filter);
                    break;
                case "customer-summary":
                    table = await CustomerSummaryAsync(deliveries, customers, territories, filter);
                    break;
                case "product-summary":
                    table = await ProductSummaryAsync(deliveries, customers, territories, filter);
                    break;
                case "model-summary":
                    table = await ModelSummaryAsync(deliveries, products, customers, territories, filter);
                    break;
                case "year-100" or "year-200" or "year-300" or "year-400":
                    var form = int.Parse(kind[5..]);
                    if (form == 100 && territory is null) return Results.BadRequest("กรุณาเลือกเขตการขาย");
                    table = await YearAsync(form, deliveries, customers, territories, filter);
                    wide = true;
                    break;
                default:
                    return Results.NotFound();
            }
            if (string.Equals(format, "xlsx", StringComparison.OrdinalIgnoreCase))
                return Results.File(XlsxWriter.Write(table), XlsxWriter.ContentType, table.Title + ".xlsx");
            var pdf = await printer.PrintAsync<SummaryTableView>(table.Title, wide ? Paper.A4Landscape : Paper.A4Portrait, new() { ["Table"] = table }, AccountClaims.DisplayNameOf(http.User),
                cancellationToken: ct);
            return ReportsRegistration.Pdf(http, pdf, table.Title);
        });
        return reports;
    }
}
