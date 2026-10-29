using Lockte.Org;
using Lockte.Purch;
using Lockte.Web.Accounts;

namespace Lockte.Web.Reports.Purch;

/// <summary>A material row of RPT-12 / RPT-15 / RPT-18: quantity, amount and the average price (amount ÷ quantity).</summary>
public sealed record ReceiptMaterialRow(string Code, string Name, string? Unit, decimal Quantity, decimal Amount)
{
    public decimal? AveragePrice => Quantity == 0 ? null : Amount / Quantity;
}

/// <summary>RPT-12 รายงานการรับวัตถุดิบแผนก: the materials one แผนก received, by amount ↓, and how many purchase requests they came from.</summary>
public sealed record DepartmentReceiptReport(string DepartmentName, DateOnly From, DateOnly To, IReadOnlyList<ReceiptMaterialRow> Rows, int RequestCount)
{
    public decimal Total => Rows.Sum(r => r.Amount);
}

/// <summary>RPT-13 สรุปรายงานรับวัตถุดิบตามแผนก: the amount of every แผนก for the month of the end date.</summary>
public sealed record DepartmentSummaryReport(DateOnly From, DateOnly To, IReadOnlyList<(string Department, decimal Amount)> Rows)
{
    public decimal Total => Rows.Sum(r => r.Amount);
}

/// <summary>RPT-15 รายงานการรับวัตถุดิบ ตามชื่อวัตถุดิบ: each delivery of one material by date.</summary>
public sealed record MaterialReceiptReport(string MaterialName, DateOnly From, DateOnly To, IReadOnlyList<ReceivedLine> Rows)
{
    public decimal TotalAmount => Rows.Sum(r => r.Amount);
    public decimal TotalQuantity => Rows.Sum(r => r.Quantity);
    public decimal? AveragePrice => TotalQuantity == 0 ? null : TotalAmount / TotalQuantity;
}

/// <summary>One supplier of RPT-18: its materials, each with its deliveries by date.</summary>
public sealed record SupplierReceiptGroup(string Code, string Name, IReadOnlyList<(string MaterialCode, string MaterialName, IReadOnlyList<ReceivedLine> Rows)> Materials)
{
    public decimal Amount => Materials.Sum(m => m.Rows.Sum(r => r.Amount));
    public decimal Quantity => Materials.Sum(m => m.Rows.Sum(r => r.Quantity));
}

public sealed record SupplierReceiptReport(DateOnly From, DateOnly To, IReadOnlyList<SupplierReceiptGroup> Suppliers)
{
    public decimal TotalAmount => Suppliers.Sum(s => s.Amount);
    public decimal TotalQuantity => Suppliers.Sum(s => s.Quantity);
}

/// <summary>
/// รายงานการรับวัตถุดิบ (RPT-12, 13, 15, 18, 19; #258, BR-061), ระดับ B: grouped by the **id** of the material, the supplier and the แผนก, not by the names they were typed with
/// (DQ-02, DQ-04). The reports of the purchases leave out the deliveries that failed the inspection (RPT-12, 13, 15); the ones by supplier (RPT-18, 19) do not filter by the result.
/// </summary>
public static class ReceiptReports
{
    public const string DepartmentTitle = "รายงานการรับวัตถุดิบแผนก";
    public const string DepartmentSummaryTitle = "สรุปรายงานรับวัตถุดิบตามแผนก";
    public const string MaterialTitle = "รายงานการรับวัตถุดิบ";
    public const string SupplierTitle = "รายงานการรับวัตถุดิบ (รายละเอียด)";
    public const string SupplierSummaryTitle = "รายงานการรับวัตถุดิบ (สรุป)";

    public static DepartmentReceiptReport Department(IReadOnlyList<ReceivedLine> lines, string departmentName, int departmentId, DateOnly from, DateOnly to)
    {
        var mine = lines.Where(l => l.DepartmentId == departmentId && !l.Failed).ToList();
        var rows = mine.GroupBy(l => l.MaterialId)
            .Select(g => new ReceiptMaterialRow(g.First().MaterialCode, g.First().MaterialName, g.First().Unit, g.Sum(l => l.Quantity), g.Sum(l => l.Amount)))
            .OrderByDescending(r => r.Amount).ThenBy(r => r.Code, StringComparer.Ordinal).ToList();
        return new DepartmentReceiptReport(departmentName, from, to, rows, mine.Select(l => l.RequestNo).Distinct().Count());
    }

    public static DepartmentSummaryReport DepartmentSummary(IReadOnlyList<ReceivedLine> lines, IReadOnlyDictionary<int, string> departments, DateOnly from, DateOnly to)
        => new(from, to, lines.Where(l => !l.Failed).GroupBy(l => l.DepartmentId)
            .Select(g => (Department: departments.GetValueOrDefault(g.Key) ?? "", Amount: g.Sum(l => l.Amount))).OrderBy(r => r.Department, StringComparer.Ordinal).ToList());

    public static MaterialReceiptReport Material(IReadOnlyList<ReceivedLine> lines, int materialId, string materialName, DateOnly from, DateOnly to)
        => new(materialName, from, to, lines.Where(l => l.MaterialId == materialId && !l.Failed).OrderBy(l => l.ReceiveDate).ToList());

    public static SupplierReceiptReport Supplier(IReadOnlyList<ReceivedLine> lines, int? supplierId, DateOnly from, DateOnly to)
        => new(from, to, lines.Where(l => supplierId is null || l.SupplierId == supplierId).GroupBy(l => l.SupplierId).OrderBy(g => g.First().SupplierCode, StringComparer.Ordinal)
            .Select(g => new SupplierReceiptGroup(g.First().SupplierCode, g.First().SupplierName, g.GroupBy(l => l.MaterialId).OrderBy(m => m.First().MaterialCode, StringComparer.Ordinal)
                .Select(m => (m.First().MaterialCode, m.First().MaterialName, (IReadOnlyList<ReceivedLine>)m.OrderBy(l => l.ReceiveDate).ToList())).ToList())).ToList());

    public static RouteGroupBuilder MapReceiptReports(this RouteGroupBuilder reports)
    {
        var group = reports.MapGroup("/purch/receipts");

        group.MapGet("/department", async (DateOnly? from, DateOnly? to, int? department, bool? summary, GoodsReceiptDirectory receipts, OrgDirectory org, HttpContext http, ReportPrinter printer,
            CancellationToken cancellationToken) =>
        {
            if (Range(from, to) is { } bad) return bad;
            var lines = await receipts.GetReceivedLinesAsync(from!.Value, to!.Value);
            var user = AccountClaims.DisplayNameOf(http.User);
            if (summary == true)
            {
                var names = (await org.GetDepartmentsAsync()).ToDictionary(d => d.Id, d => d.Name);
                var pdf = await printer.PrintAsync<DepartmentReceiptSummaryView>(DepartmentSummaryTitle, Paper.A4Portrait, new() { ["Report"] = DepartmentSummary(lines, names, from.Value, to.Value) }, user, cancellationToken: cancellationToken);
                return ReportsRegistration.Pdf(http, pdf, "สรุปรับวัตถุดิบตามแผนก");
            }
            if (department is not { } id || await org.GetDepartmentAsync(id) is not { } dept) return Results.BadRequest("กรุณาเลือกแผนก");
            var print = await printer.PrintAsync<DepartmentReceiptView>(DepartmentTitle + dept.Name, Paper.A4Portrait, new() { ["Report"] = Department(lines, dept.Name, id, from.Value, to.Value) }, user, cancellationToken: cancellationToken);
            return ReportsRegistration.Pdf(http, print, "รับวัตถุดิบแผนก " + dept.Name);
        });

        group.MapGet("/material", async (DateOnly? from, DateOnly? to, int? material, GoodsReceiptDirectory receipts, MaterialDirectory materials, HttpContext http, ReportPrinter printer,
            CancellationToken cancellationToken) =>
        {
            if (Range(from, to) is { } bad) return bad;
            if (material is not { } id || await materials.GetAsync(id) is not { } found) return Results.BadRequest("กรุณาเลือกวัตถุดิบ");
            var lines = await receipts.GetReceivedLinesAsync(from!.Value, to!.Value);
            var pdf = await printer.PrintAsync<MaterialReceiptView>(MaterialTitle + " " + found.Name, Paper.A4Portrait, new() { ["Report"] = Material(lines, id, found.Name, from.Value, to.Value) },
                AccountClaims.DisplayNameOf(http.User), cancellationToken: cancellationToken);
            return ReportsRegistration.Pdf(http, pdf, "รับวัตถุดิบ " + found.Code);
        });

        group.MapGet("/supplier", async (DateOnly? from, DateOnly? to, int? supplier, bool? summary, GoodsReceiptDirectory receipts, HttpContext http, ReportPrinter printer, CancellationToken cancellationToken) =>
        {
            if (Range(from, to) is { } bad) return bad;
            var lines = await receipts.GetReceivedLinesAsync(from!.Value, to!.Value);
            var report = Supplier(lines, supplier, from.Value, to.Value);
            var user = AccountClaims.DisplayNameOf(http.User);
            var pdf = summary == true
                ? await printer.PrintAsync<SupplierReceiptSummaryView>(SupplierSummaryTitle, Paper.A4Portrait, new() { ["Report"] = report }, user, cancellationToken: cancellationToken)
                : await printer.PrintAsync<SupplierReceiptView>(SupplierTitle, Paper.A4Portrait, new() { ["Report"] = report }, user, cancellationToken: cancellationToken);
            return ReportsRegistration.Pdf(http, pdf, summary == true ? "รับวัตถุดิบตามผู้ขาย (สรุป)" : "รับวัตถุดิบตามผู้ขาย");
        });
        return reports;
    }

    private static IResult? Range(DateOnly? from, DateOnly? to)
        => from is null || to is null ? Results.BadRequest("กรุณากรอกช่วงวันที่") : from > to ? Results.BadRequest("ตั้งแต่วันที่ต้องไม่หลังถึงวันที่") : null;
}
