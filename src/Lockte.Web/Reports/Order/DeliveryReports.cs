using Lockte.Sales;
using Lockte.SharedKernel;
using Lockte.Web.Accounts;

namespace Lockte.Web.Reports.Order;

public sealed record DeliveryCustomerGroup(string CustomerCode, string CustomerName, IReadOnlyList<DeliveryReportRow> Rows)
{
    public decimal Amount => Rows.Sum(r => r.Amount ?? 0);

    /// <summary>Σ กล่อง (DISC_PS) — the pieces shown in place of boxes for a customer of code <c>40…</c> are not boxes and are not added.</summary>
    public decimal Boxes => Rows.Sum(r => r.Boxes ?? 0);
}

/// <summary>RPT-07 ใบรายงานการส่งสินค้า: the deliveries of a range, a group per customer.</summary>
public sealed record DeliveryReport(DateOnly From, DateOnly To, IReadOnlyList<DeliveryCustomerGroup> Customers)
{
    public int Rows => Customers.Sum(c => c.Rows.Count);
    public decimal Amount => Customers.Sum(c => c.Amount);
    public decimal Boxes => Customers.Sum(c => c.Boxes);
    public int Invoices => Customers.SelectMany(c => c.Rows).Where(r => r.InvoiceNo is not null).Select(r => r.InvoiceNo).Distinct().Count();
    public int Orders => Customers.SelectMany(c => c.Rows).Select(r => (r.OrderNo, r.CustomerId)).Distinct().Count();
}

/// <summary>One strip of the quality check before delivery (WI-QA033): a customer delivered to in the range.</summary>
public sealed record ShippingInspectionStrip(string CustomerCode, string CustomerName, DateOnly DeliveryDate, int Boxes, int Sample);

public sealed record ShippingInspectionReport(DateOnly From, DateOnly To, IReadOnlyList<ShippingInspectionStrip> Strips);

/// <summary>One line of รายงานส่งผู้จัดการโรงงาน: a delivery date of an order of a customer, with the due date and whether it was in time.</summary>
public sealed record ManagerDeliveryRow(DateOnly DeliveryDate, string OrderNo, string CustomerCode, string CustomerName, DateOnly? DueDate, bool? OnTime, decimal Amount);

public sealed record ManagerDeliveryReport(DateOnly From, DateOnly To, IReadOnlyList<ManagerDeliveryRow> Rows)
{
    public int OnTime => Rows.Count(r => r.OnTime == true);
    public int Late => Rows.Count(r => r.OnTime == false);
    public decimal Amount => Rows.Sum(r => r.Amount);

    /// <summary>Of the orders that have a due date; null when none has.</summary>
    public decimal? PercentOnTime => OnTime + Late == 0 ? null : Math.Round(100m * OnTime / (OnTime + Late), 2, MidpointRounding.AwayFromZero);
}

/// <summary>
/// The ISO delivery documents of ฝ่ายขายและส่งของ (#136), all ระดับ B with their เลขเอกสารควบคุม as master data (#52):
/// RPT-05 DELIVERY SCHEDULE, RPT-07 ใบรายงานการส่งสินค้า, รายงานการตรวจสอบสินค้าที่จัดส่งให้ลูกค้า (WI-QA033) and RPT-08 รายงานส่งผู้จัดการโรงงาน.
/// </summary>
public static class DeliveryReports
{
    public const string ScheduleTitle = "DELIVERY SCHEDULE";
    public const string DeliveryTitle = "ใบรายงานการส่งสินค้า";
    public const string InspectionTitle = "รายงานการตรวจสอบสินค้าที่จัดส่งให้ลูกค้า";
    public const string ManagerTitlePrefix = "รายงานการส่งสินค้า";

    /// <summary>A quantity as the reports print it: thousands separators, no needless decimals.</summary>
    public static string Qty(decimal value) => value.ToString("#,##0.####", ThaiDate.Culture);

    /// <summary>A customer of code <c>40…</c> (งานรับจ้างผลิต) is shown the pieces and "ชิ้น" in the box column.</summary>
    public static bool ShowsPieces(string customerCode) => customerCode.StartsWith("40", StringComparison.Ordinal);

    /// <summary>A sample of the boxes of a customer, at most 5 (🔍 min(กล่อง, 5) from the samples; the legacy formula is not recovered).</summary>
    public const int MaxSample = 5;

    public static async Task<DeliveryReport> DeliveryAsync(SalesDeliveryReportDirectory deliveries, DateOnly from, DateOnly to)
    {
        var groups = (await deliveries.GetDeliveriesAsync(from, to)).GroupBy(r => (r.CustomerCode, r.CustomerName))
            .Select(g => new DeliveryCustomerGroup(g.Key.CustomerCode, g.Key.CustomerName, g.ToList())).ToList();
        return new(from, to, groups);
    }

    public static async Task<ShippingInspectionReport> InspectionAsync(SalesDeliveryReportDirectory deliveries, DateOnly from, DateOnly to)
    {
        var strips = (await deliveries.GetDeliveriesAsync(from, to)).GroupBy(r => (r.CustomerCode, r.CustomerName)).OrderBy(g => g.Key.CustomerCode, StringComparer.Ordinal)
            .Select(g =>
            {
                var boxes = (int)Math.Ceiling(g.Sum(r => r.Boxes ?? 0));
                return new ShippingInspectionStrip(g.Key.CustomerCode, g.Key.CustomerName, g.Max(r => r.DeliveryDate), boxes, Math.Min(boxes, MaxSample));
            }).ToList();
        return new(from, to, strips);
    }

    /// <summary>
    /// One row per delivery date, order and customer: Σ มูลค่าส่ง; ทัน when delivered on or before the due date written on the order
    /// (the earliest of its lines'), ล่าช้า after it, blank when the order has no due date.
    /// </summary>
    public static async Task<ManagerDeliveryReport> ManagerAsync(SalesDeliveryReportDirectory deliveries, DateOnly from, DateOnly to)
    {
        var rows = (await deliveries.GetDeliveriesAsync(from, to)).GroupBy(r => (r.DeliveryDate, r.OrderNo, r.CustomerId))
            .OrderBy(g => g.Key.DeliveryDate).ThenBy(g => g.Key.OrderNo, StringComparer.Ordinal)
            .Select(g =>
            {
                var due = g.Where(r => r.DueDate is not null).Select(r => r.DueDate).Min();
                return new ManagerDeliveryRow(g.Key.DeliveryDate, g.Key.OrderNo, g.First().CustomerCode, g.First().CustomerName, due,
                    due is { } d ? g.Key.DeliveryDate <= d : null, g.Sum(r => r.Amount ?? 0));
            }).ToList();
        return new(from, to, rows);
    }

    public static string ManagerTitle(DateOnly from, DateOnly to) => $"{ManagerTitlePrefix}เดือน {ReportText.MonthTitle(from, to)}";

    public static RouteGroupBuilder MapDeliveryReports(this RouteGroupBuilder reports)
    {
        var group = reports.MapGroup("/order/deliveries");

        group.MapGet("/", async (DateOnly? from, DateOnly? to, HttpContext http, SalesDeliveryReportDirectory deliveries, ReportPrinter printer, CancellationToken ct)
            => HrAttendanceReports.Range(from, to) ?? Pdf(http, await printer.PrintAsync<DeliveryReportView>(DeliveryTitle, Paper.A4Landscape,
                new() { ["Report"] = await DeliveryAsync(deliveries, from!.Value, to!.Value) }, AccountClaims.DisplayNameOf(http.User), DocumentKeys.DeliveryReport, ct), DeliveryTitle));

        group.MapGet("/inspection", async (DateOnly? from, DateOnly? to, HttpContext http, SalesDeliveryReportDirectory deliveries, ReportPrinter printer, CancellationToken ct)
            => HrAttendanceReports.Range(from, to) ?? Pdf(http, await printer.PrintAsync<ShippingInspectionView>(InspectionTitle, Paper.A4Landscape,
                new() { ["Report"] = await InspectionAsync(deliveries, from!.Value, to!.Value) }, AccountClaims.DisplayNameOf(http.User), DocumentKeys.ShippingInspection, ct), InspectionTitle));

        group.MapGet("/manager", async (DateOnly? from, DateOnly? to, HttpContext http, SalesDeliveryReportDirectory deliveries, ReportPrinter printer, CancellationToken ct) =>
        {
            if (HrAttendanceReports.Range(from, to) is { } bad) return bad;
            var title = ManagerTitle(from!.Value, to!.Value);
            return Pdf(http, await printer.PrintAsync<ManagerDeliveryView>(title, Paper.A4Portrait,
                new() { ["Report"] = await ManagerAsync(deliveries, from.Value, to.Value) }, AccountClaims.DisplayNameOf(http.User), DocumentKeys.ManagerDelivery, ct), title);
        });

        return reports;
    }

    private static IResult Pdf(HttpContext http, byte[] pdf, string name) => ReportsRegistration.Pdf(http, pdf, name);
}
