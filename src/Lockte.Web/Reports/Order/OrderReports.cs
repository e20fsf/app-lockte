using Lockte.Prod;
using Lockte.SharedKernel;
using Lockte.Sales;
using Lockte.Web.Accounts;

namespace Lockte.Web.Reports.Order;

/// <summary>ผลรวมของหน่วยหนึ่ง: quantities of different units are never added together (D-11).</summary>
public sealed record UnitTotal(string Unit, decimal Quantity);

/// <summary>RPT-02 ORDER ค้างส่งทั้งหมด and RPT-04 ของเขตการขาย: the lines grouped by order date, then order number.</summary>
public sealed record BackorderListReport(string Heading, string? Filter, IReadOnlyList<OrderLineReportRow> Lines, bool ShowTerritory);

public sealed record ProductTotalRow(string? Code, string Name, string? Unit, decimal Quantity);

/// <summary>RPT-03 ORDER ค้างส่งรวมตามสินค้า, most first.</summary>
public sealed record BackorderByProductReport(string? Filter, IReadOnlyList<ProductTotalRow> Rows);

public sealed record CustomerGroup(string CustomerCode, string CustomerName, IReadOnlyList<ProductTotalRow> Rows);

/// <summary>RPT-04 ORDER ค้างส่งตามลูกค้า: a group per customer.</summary>
public sealed record BackorderByCustomerReport(string? Filter, IReadOnlyList<CustomerGroup> Groups);

public sealed record ModelTotalRow(string Model, string? Unit, decimal Quantity);

/// <summary>RPT-04 ORDER ค้างส่งตามรุ่น.</summary>
public sealed record BackorderByModelReport(string? Filter, IReadOnlyList<ModelTotalRow> Rows);

public sealed record CustomerQuantityRow(string CustomerCode, string CustomerName, decimal? Quantity);

public sealed record ProductCustomersGroup(string? ProductCode, string ProductName, string? Unit, IReadOnlyList<CustomerQuantityRow> Rows);

/// <summary>
/// RPT-04 ยอดค้างส่งของลูกค้า (with the quantity) and สินค้าที่ลูกค้าสั่งซื้อ (without): the customers of each product. The legacy
/// called the first "ORDER ค้างส่งของลูกค้า" though it is per product (D-8): the head says what it is.
/// </summary>
public sealed record ProductCustomersReport(string Heading, string? Filter, bool ShowQuantity, IReadOnlyList<ProductCustomersGroup> Groups);

public sealed record ReceiptSummaryRow(string Code, string Name, int Orders, decimal Amount);

/// <summary>RPT-06 สรุปการรับ ORDER: every territory, even with no order in the range.</summary>
public sealed record ReceiptSummaryReport(DateOnly? From, DateOnly? To, IReadOnlyList<ReceiptSummaryRow> Rows)
{
    public int TotalOrders => Rows.Sum(r => r.Orders);
    public decimal TotalAmount => Rows.Sum(r => r.Amount);
}

public sealed record ReceiptCustomerRow(string CustomerCode, string CustomerName, decimal Quantity, decimal Amount);

/// <summary>RPT-06 สรุป ORDER มากกว่า 3000: customers whose Σ <b>จำนวน</b> (not money) is at least the limit.</summary>
public sealed record ReceiptOver3000Report(DateOnly? From, DateOnly? To, decimal Limit, IReadOnlyList<ReceiptCustomerRow> Rows);

public sealed record ReceiptOrderGroup(string OrderNo, DateOnly OrderDate, string CustomerCode, string CustomerName, IReadOnlyList<OrderLineReportRow> Lines)
{
    public decimal Amount => Lines.Sum(l => l.Amount ?? 0);
}

/// <summary>RPT-06 รายงานการรับ ORDER ของเขตการขาย / ตามชื่อลูกค้า: the orders in the history, each with its lines and total.</summary>
public sealed record ReceiptOrdersReport(string Heading, DateOnly? From, DateOnly? To, IReadOnlyList<ReceiptOrderGroup> Orders)
{
    public decimal Amount => Orders.Sum(o => o.Amount);
}

/// <summary>
/// The ORDER reports (#113), all ระดับ B: ORDER ค้างส่ง (RPT-02, RPT-03, RPT-04 ×5) and สรุปการรับ ORDER (RPT-06 ×4). The rows come from
/// <see cref="SalesOrderReportDirectory"/>; what is grouped, totalled and said in the head (D-10) is done here, and the views only lay it out.
/// RPT-05 กำหนดส่ง (an ISO document) is #136.
/// </summary>
public static class OrderReports
{
    public const string BackorderTitle = "ORDER ค้างส่งทั้งหมด";
    public const string BackorderByProductTitle = "ORDER ค้างส่งรวมตามสินค้า";
    public const string BackorderByCustomerTitle = "ORDER ค้างส่งตามลูกค้า";
    public const string BackorderByModelTitle = "ORDER ค้างส่งตามรุ่น";
    public const string BackorderOfProductTitle = "ลูกค้าที่ค้างส่งแยกตามสินค้า";
    public const string OrderedProductsTitle = "สินค้าที่ลูกค้าสั่งซื้อ";
    public const string ReceiptSummaryTitle = "สรุปการรับ ORDER";
    public const string ReceiptOver3000Title = "สรุปการรับ ORDER มากกว่า 3000";
    public const string ReceiptOfTerritoryTitle = "รายงานการรับ ORDER ของเขตการขาย";
    public const string ReceiptOfCustomerTitle = "รายงานการรับ ORDER ตามชื่อลูกค้า";

    /// <summary>"มากกว่า 3000": the limit of Σ จำนวน (BR-006 of the reports, not money).</summary>
    public const decimal Over3000Limit = 3000;

    /// <summary>The same number as the report prints it: thousands separators, no needless decimals.</summary>
    public static string Qty(decimal value) => value.ToString("#,##0.####", ThaiDate.Culture);

    /// <summary>D-11: the quantities added up per unit, units in order.</summary>
    public static IReadOnlyList<UnitTotal> UnitTotals(IEnumerable<(string? Unit, decimal Quantity)> rows)
        => rows.GroupBy(r => r.Unit?.Trim() ?? "").OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => new UnitTotal(g.Key, g.Sum(r => r.Quantity))).ToList();

    // ---------- the head: what narrows the report (D-10) ----------

    public static async Task<string?> FilterTextAsync(OrderReportFilter filter, CustomerDirectory customers, SalesTerritoryDirectory territories, string? product = null)
    {
        var parts = new List<string>();
        if (filter.From is { } from || filter.To is not null)
            parts.Add($"วันที่ ORDER {(filter.From is { } f ? "ตั้งแต่ " + ReportText.Date(f) : "")}{(filter.From is not null && filter.To is not null ? " " : "")}{(filter.To is { } t ? "ถึง " + ReportText.Date(t) : "")}".Replace("  ", " "));
        if (filter.CustomerId is { } customerId && await customers.GetAsync(customerId) is { } customer) parts.Add($"ลูกค้า {customer.Code} {customer.Name}");
        if (filter.SalesTerritoryId is { } territoryId && (await territories.GetAllAsync()).FirstOrDefault(t => t.Id == territoryId) is { } territory)
            parts.Add($"เขตการขาย {territory.Code} {territory.Name}");
        if (product is not null) parts.Add($"สินค้า {product}");
        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }

    // ---------- RPT-02, RPT-03, RPT-04: ORDER ค้างส่ง ----------

    public static async Task<BackorderListReport> BackorderAsync(SalesOrderReportDirectory reports, CustomerDirectory customers, SalesTerritoryDirectory territories, OrderReportFilter filter)
        => new(BackorderTitle, await FilterTextAsync(filter, customers, territories), await reports.GetBackorderAsync(filter), ShowTerritory: true);

    /// <summary>ORDER ค้างส่งของเขตการขาย <c>รหัส ชื่อ</c> (ODD ด้างส่งตามเซล): the territory is the head, not a column.</summary>
    public static async Task<BackorderListReport> BackorderOfTerritoryAsync(SalesOrderReportDirectory reports, CustomerDirectory customers, SalesTerritoryDirectory territories,
        OrderReportFilter filter)
    {
        var territory = (await territories.GetAllAsync()).FirstOrDefault(t => t.Id == filter.SalesTerritoryId);
        var heading = territory is null ? BackorderTitle : $"ORDER ค้างส่งของเขตการขาย {territory.Code} {territory.Name}";
        var others = filter with { SalesTerritoryId = null };
        return new(heading, await FilterTextAsync(others, customers, territories), await reports.GetBackorderAsync(filter), ShowTerritory: false);
    }

    public static async Task<BackorderByProductReport> BackorderByProductAsync(SalesOrderReportDirectory reports, CustomerDirectory customers, SalesTerritoryDirectory territories,
        OrderReportFilter filter)
    {
        var rows = (await reports.GetBackorderAsync(filter))
            .GroupBy(l => (l.ProductId, Unit: l.Unit?.Trim() ?? ""))
            .Select(g => new ProductTotalRow(g.First().ProductCode, g.First().ProductName, g.Key.Unit.Length == 0 ? null : g.Key.Unit, g.Sum(l => l.Quantity)))
            .OrderByDescending(r => r.Quantity).ThenBy(r => r.Code, StringComparer.Ordinal).ToList();
        return new(await FilterTextAsync(filter, customers, territories), rows);
    }

    public static async Task<BackorderByCustomerReport> BackorderByCustomerAsync(SalesOrderReportDirectory reports, CustomerDirectory customers, SalesTerritoryDirectory territories,
        OrderReportFilter filter)
    {
        var groups = (await reports.GetBackorderAsync(filter)).GroupBy(l => (l.CustomerCode, l.CustomerName)).OrderBy(g => g.Key.CustomerCode, StringComparer.Ordinal)
            .Select(g => new CustomerGroup(g.Key.CustomerCode, g.Key.CustomerName, g
                .GroupBy(l => (l.ProductId, Unit: l.Unit?.Trim() ?? ""))
                .Select(p => new ProductTotalRow(p.First().ProductCode, p.First().ProductName, p.Key.Unit.Length == 0 ? null : p.Key.Unit, p.Sum(l => l.Quantity)))
                .OrderBy(r => r.Code, StringComparer.Ordinal).ToList()))
            .ToList();
        return new(await FilterTextAsync(filter, customers, territories), groups);
    }

    /// <summary>Σ ค้างส่งตามรุ่น (<c>P_NAME.MODEL</c>): the model comes from module ผลิต, a product with none is "ไม่ระบุรุ่น".</summary>
    public static async Task<BackorderByModelReport> BackorderByModelAsync(SalesOrderReportDirectory reports, ProductDirectory products, CustomerDirectory customers,
        SalesTerritoryDirectory territories, OrderReportFilter filter)
    {
        var lines = await reports.GetBackorderAsync(filter);
        var models = new Dictionary<int, string>();
        foreach (var id in lines.Select(l => l.ProductId).Distinct())
            models[id] = (await products.GetAsync(id))?.ModelName ?? "ไม่ระบุรุ่น";
        var rows = lines.GroupBy(l => (Model: models[l.ProductId], Unit: l.Unit?.Trim() ?? ""))
            .OrderBy(g => g.Key.Model, StringComparer.Ordinal).ThenBy(g => g.Key.Unit, StringComparer.Ordinal)
            .Select(g => new ModelTotalRow(g.Key.Model, g.Key.Unit.Length == 0 ? null : g.Key.Unit, g.Sum(l => l.Quantity))).ToList();
        return new(await FilterTextAsync(filter, customers, territories), rows);
    }

    /// <summary>ยอดค้างส่งของลูกค้า (RPT-04): per product, the customers that still wait for it and how many.</summary>
    public static async Task<ProductCustomersReport> BackorderOfProductsAsync(SalesOrderReportDirectory reports, CustomerDirectory customers, SalesTerritoryDirectory territories,
        OrderReportFilter filter, int? productId)
    {
        var lines = (await reports.GetBackorderAsync(filter)).Where(l => productId is null || l.ProductId == productId).ToList();
        var groups = lines.GroupBy(l => (l.ProductId, Unit: l.Unit?.Trim() ?? "")).OrderBy(g => g.First().ProductCode, StringComparer.Ordinal)
            .Select(g => new ProductCustomersGroup(g.First().ProductCode, g.First().ProductName, g.Key.Unit.Length == 0 ? null : g.Key.Unit,
                g.GroupBy(l => (l.CustomerCode, l.CustomerName)).OrderBy(c => c.Key.CustomerCode, StringComparer.Ordinal)
                    .Select(c => new CustomerQuantityRow(c.Key.CustomerCode, c.Key.CustomerName, c.Sum(l => l.Quantity))).ToList()))
            .ToList();
        var product = productId is { } id ? lines.FirstOrDefault()?.ProductCode : null;
        return new(BackorderOfProductTitle, await FilterTextAsync(filter, customers, territories, product), ShowQuantity: true, groups);
    }

    /// <summary>สินค้าที่ลูกค้าสั่งซื้อ: the products each customer has received, from the delivery history (no quantities).</summary>
    public static async Task<ProductCustomersReport> OrderedProductsAsync(SalesOrderReportDirectory reports, CustomerDirectory customers, int? productId, int? customerId)
    {
        var rows = await reports.GetDeliveredProductsAsync(productId, customerId);
        var groups = rows.GroupBy(r => r.ProductId).Select(g => new ProductCustomersGroup(g.First().ProductCode, g.First().ProductName, g.First().Unit,
            g.OrderBy(r => r.CustomerCode, StringComparer.Ordinal).Select(r => new CustomerQuantityRow(r.CustomerCode, r.CustomerName, null)).ToList())).ToList();
        string? filter = null;
        if (customerId is { } id && await customers.GetAsync(id) is { } customer) filter = $"ลูกค้า {customer.Code} {customer.Name}";
        if (productId is not null && groups.Count > 0) filter = string.Join(" · ", new[] { filter, $"สินค้า {groups[0].ProductCode}" }.Where(p => p is not null));
        return new(OrderedProductsTitle, filter, ShowQuantity: false, groups);
    }

    // ---------- RPT-06: สรุปการรับ ORDER ----------

    /// <summary>
    /// Every territory in the register with its orders (<b>distinct</b> numbers) and Σ มูลค่า of every line, from the orders in the
    /// history in the range. A territory with none is listed with 0; lines of an order with no territory are not in any row.
    /// </summary>
    public static async Task<ReceiptSummaryReport> ReceiptSummaryAsync(SalesOrderReportDirectory reports, SalesTerritoryDirectory territories, DateOnly? from, DateOnly? to)
    {
        var lines = await reports.GetReceivedAsync(new OrderReportFilter(from, to));
        var byTerritory = lines.Where(l => l.SalesTerritoryId is not null).GroupBy(l => l.SalesTerritoryId!.Value).ToDictionary(g => g.Key);
        var rows = (await territories.GetAllAsync()).OrderBy(t => t.Code, StringComparer.Ordinal)
            .Select(t => byTerritory.TryGetValue(t.Id, out var g)
                ? new ReceiptSummaryRow(t.Code, t.Name, g.Select(l => (l.OrderNo, l.CustomerId)).Distinct().Count(), g.Sum(l => l.Amount ?? 0))
                : new ReceiptSummaryRow(t.Code, t.Name, 0, 0)).ToList();
        return new(from, to, rows);
    }

    /// <summary>"มากกว่า 3000": customers with Σ จำนวน ≥ 3,000 in the range, with Σ จำนวน and Σ มูลค่า.</summary>
    public static async Task<ReceiptOver3000Report> ReceiptOver3000Async(SalesOrderReportDirectory reports, DateOnly? from, DateOnly? to)
    {
        var rows = (await reports.GetReceivedAsync(new OrderReportFilter(from, to)))
            .GroupBy(l => (l.CustomerCode, l.CustomerName))
            .Select(g => new ReceiptCustomerRow(g.Key.CustomerCode, g.Key.CustomerName, g.Sum(l => l.Quantity), g.Sum(l => l.Amount ?? 0)))
            .Where(r => r.Quantity >= Over3000Limit).OrderBy(r => r.CustomerCode, StringComparer.Ordinal).ToList();
        return new(from, to, Over3000Limit, rows);
    }

    /// <summary>รายงานการรับ ORDER ของเขตการขาย (<paramref name="territoryId"/>) or ตามชื่อลูกค้า (<paramref name="customerId"/>).</summary>
    public static async Task<ReceiptOrdersReport> ReceiptOrdersAsync(SalesOrderReportDirectory reports, CustomerDirectory customers, SalesTerritoryDirectory territories,
        DateOnly? from, DateOnly? to, int? territoryId, int? customerId)
    {
        var heading = customerId is not null ? ReceiptOfCustomerTitle : ReceiptOfTerritoryTitle;
        if (territoryId is { } tid && (await territories.GetAllAsync()).FirstOrDefault(t => t.Id == tid) is { } territory) heading += $" {territory.Code} {territory.Name}";
        if (customerId is { } cid && await customers.GetAsync(cid) is { } customer) heading += $" {customer.Code} {customer.Name}";
        var orders = (await reports.GetReceivedAsync(new OrderReportFilter(from, to, customerId, territoryId)))
            .GroupBy(l => (l.OrderNo, l.CustomerId)).OrderBy(g => g.First().OrderDate).ThenBy(g => g.Key.OrderNo, StringComparer.Ordinal)
            .Select(g => new ReceiptOrderGroup(g.Key.OrderNo, g.First().OrderDate, g.First().CustomerCode, g.First().CustomerName, g.OrderBy(l => l.LineNo).ToList()))
            .ToList();
        return new(heading, from, to, orders);
    }

    // ---------- the PDFs ----------

    public static RouteGroupBuilder MapOrderReports(this RouteGroupBuilder reports)
    {
        var group = reports.MapGroup("/order");

        OrderReportFilter Filter(DateOnly? from, DateOnly? to, int? customer, int? territory) => new(from, to, customer, territory);

        // เมนู 13 ยอดค้างส่ง
        group.MapGet("/backorder", async (DateOnly? from, DateOnly? to, int? customer, int? territory, HttpContext http, SalesOrderReportDirectory reports,
            CustomerDirectory customers, SalesTerritoryDirectory territories, ReportPrinter printer, CancellationToken ct)
            => await HrAttendanceReports.PrintAsync<BackorderListView>(http, printer, BackorderTitle, Paper.A4Landscape,
                await BackorderAsync(reports, customers, territories, Filter(from, to, customer, territory)), BackorderTitle, ct));

        group.MapGet("/backorder/territory", async (DateOnly? from, DateOnly? to, int? customer, int? territory, HttpContext http, SalesOrderReportDirectory reports,
            CustomerDirectory customers, SalesTerritoryDirectory territories, ReportPrinter printer, CancellationToken ct) =>
        {
            if (territory is null) return Results.BadRequest("กรุณาเลือกเขตการขาย");
            var report = await BackorderOfTerritoryAsync(reports, customers, territories, Filter(from, to, customer, territory));
            return await HrAttendanceReports.PrintAsync<BackorderListView>(http, printer, report.Heading, Paper.A4Landscape, report, report.Heading, ct);
        });

        group.MapGet("/backorder/products", async (DateOnly? from, DateOnly? to, int? customer, int? territory, HttpContext http, SalesOrderReportDirectory reports,
            CustomerDirectory customers, SalesTerritoryDirectory territories, ReportPrinter printer, CancellationToken ct)
            => await HrAttendanceReports.PrintAsync<BackorderByProductView>(http, printer, BackorderByProductTitle, Paper.A4Portrait,
                await BackorderByProductAsync(reports, customers, territories, Filter(from, to, customer, territory)), BackorderByProductTitle, ct));

        group.MapGet("/backorder/customers", async (DateOnly? from, DateOnly? to, int? customer, int? territory, HttpContext http, SalesOrderReportDirectory reports,
            CustomerDirectory customers, SalesTerritoryDirectory territories, ReportPrinter printer, CancellationToken ct)
            => await HrAttendanceReports.PrintAsync<BackorderByCustomerView>(http, printer, BackorderByCustomerTitle, Paper.A4Portrait,
                await BackorderByCustomerAsync(reports, customers, territories, Filter(from, to, customer, territory)), BackorderByCustomerTitle, ct));

        group.MapGet("/backorder/models", async (DateOnly? from, DateOnly? to, int? customer, int? territory, HttpContext http, SalesOrderReportDirectory reports,
            ProductDirectory products, CustomerDirectory customers, SalesTerritoryDirectory territories, ReportPrinter printer, CancellationToken ct)
            => await HrAttendanceReports.PrintAsync<BackorderByModelView>(http, printer, BackorderByModelTitle, Paper.A4Portrait,
                await BackorderByModelAsync(reports, products, customers, territories, Filter(from, to, customer, territory)), BackorderByModelTitle, ct));

        group.MapGet("/backorder/product-customers", async (DateOnly? from, DateOnly? to, int? customer, int? territory, int? product, HttpContext http,
            SalesOrderReportDirectory reports, CustomerDirectory customers, SalesTerritoryDirectory territories, ReportPrinter printer, CancellationToken ct)
            => await HrAttendanceReports.PrintAsync<ProductCustomersView>(http, printer, BackorderOfProductTitle, Paper.A4Portrait,
                await BackorderOfProductsAsync(reports, customers, territories, Filter(from, to, customer, territory), product), BackorderOfProductTitle, ct));

        group.MapGet("/ordered-products", async (int? customer, int? product, HttpContext http, SalesOrderReportDirectory reports, CustomerDirectory customers,
            ReportPrinter printer, CancellationToken ct)
            => await HrAttendanceReports.PrintAsync<ProductCustomersView>(http, printer, OrderedProductsTitle, Paper.A4Portrait,
                await OrderedProductsAsync(reports, customers, product, customer), OrderedProductsTitle, ct));

        // เมนู 17 รับ ORDER
        group.MapGet("/receipts/summary", async (DateOnly? from, DateOnly? to, HttpContext http, SalesOrderReportDirectory reports, SalesTerritoryDirectory territories,
            ReportPrinter printer, CancellationToken ct)
            => HrAttendanceReports.Range(from, to) ?? await HrAttendanceReports.PrintAsync<ReceiptSummaryView>(http, printer, ReceiptSummaryTitle, Paper.A4Portrait,
                await ReceiptSummaryAsync(reports, territories, from, to), ReceiptSummaryTitle, ct));

        group.MapGet("/receipts/over-3000", async (DateOnly? from, DateOnly? to, HttpContext http, SalesOrderReportDirectory reports, ReportPrinter printer, CancellationToken ct)
            => HrAttendanceReports.Range(from, to) ?? await HrAttendanceReports.PrintAsync<ReceiptOver3000View>(http, printer, ReceiptOver3000Title, Paper.A4Portrait,
                await ReceiptOver3000Async(reports, from, to), ReceiptOver3000Title, ct));

        group.MapGet("/receipts/orders", async (DateOnly? from, DateOnly? to, int? territory, int? customer, HttpContext http, SalesOrderReportDirectory reports,
            CustomerDirectory customers, SalesTerritoryDirectory territories, ReportPrinter printer, CancellationToken ct) =>
        {
            if (HrAttendanceReports.Range(from, to) is { } bad) return bad;
            if (territory is null && customer is null) return Results.BadRequest("กรุณาเลือกเขตการขายหรือลูกค้า");
            var report = await ReceiptOrdersAsync(reports, customers, territories, from, to, territory, customer);
            return await HrAttendanceReports.PrintAsync<ReceiptOrdersView>(http, printer, report.Heading, Paper.A4Landscape, report, report.Heading, ct);
        });

        return reports;
    }
}
