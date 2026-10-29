using Lockte.Purch;
using Lockte.SharedKernel;
using Lockte.Web.Accounts;

namespace Lockte.Web.Reports.Purch;

/// <summary>RPT-28 (by รหัส, one กลุ่มรหัส or all) / RPT-29 (by ชื่อ): the register of materials.</summary>
public sealed record MaterialListReport(string? Group, bool ByName, IReadOnlyList<MaterialDto> Rows);

/// <summary>RPT-30 (by รหัส) / RPT-31 (by ชื่อ): the register of suppliers.</summary>
public sealed record SupplierListReport(bool ByName, IReadOnlyList<SupplierDto> Rows);

/// <summary>
/// รายชื่อวัตถุดิบ (RPT-28, 29) และรายชื่อผู้ขาย (RPT-30, 31; #263), ระดับ B. By รหัส the codes run in plain order; by ชื่อ the Thai collation puts the names in dictionary order, the
/// leading vowels not counting, as the legacy did.
/// </summary>
public static class ListReports
{
    public const string MaterialByCodeTitle = "รายชื่อวัตถุดิบเรียงตามรหัส";
    public const string MaterialByNameTitle = "รายชื่อวัตถุดิบเรียงตามชื่อ";
    public const string SupplierByCodeTitle = "รายชื่อผู้ขายเรียงตามรหัส";
    public const string SupplierByNameTitle = "รายชื่อผู้ขายเรียงตามชื่อ";

    private static readonly StringComparer Thai = StringComparer.Create(ThaiDate.Culture, ignoreCase: true);

    public static MaterialListReport Materials(IReadOnlyList<MaterialDto> all, string? group, bool byName)
    {
        var prefix = string.IsNullOrWhiteSpace(group) ? null : group.Trim().TrimEnd('*');
        var rows = all.Where(m => prefix is null || m.Code.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        return new MaterialListReport(prefix, byName, (byName ? rows.OrderBy(m => m.Name, Thai).ThenBy(m => m.Code, StringComparer.Ordinal) : rows.OrderBy(m => m.Code, StringComparer.Ordinal)).ToList());
    }

    public static SupplierListReport Suppliers(IReadOnlyList<SupplierDto> all, bool byName)
        => new(byName, (byName ? all.OrderBy(s => s.Name, Thai).ThenBy(s => s.Code, StringComparer.Ordinal) : all.OrderBy(s => s.Code, StringComparer.Ordinal)).ToList());

    public static RouteGroupBuilder MapListReports(this RouteGroupBuilder reports)
    {
        var group = reports.MapGroup("/purch/lists");

        group.MapGet("/materials", async (bool? byName, string? group, MaterialDirectory materials, HttpContext http, ReportPrinter printer, CancellationToken cancellationToken) =>
        {
            var report = Materials(await materials.GetAllAsync(), group, byName == true);
            var pdf = await printer.PrintAsync<MaterialListView>(report.ByName ? MaterialByNameTitle : MaterialByCodeTitle, Paper.A4Portrait, new() { ["Report"] = report },
                AccountClaims.DisplayNameOf(http.User), cancellationToken: cancellationToken);
            return ReportsRegistration.Pdf(http, pdf, report.ByName ? "รายชื่อวัตถุดิบตามชื่อ" : "รายชื่อวัตถุดิบตามรหัส");
        });

        group.MapGet("/suppliers", async (bool? byName, SupplierDirectory suppliers, HttpContext http, ReportPrinter printer, CancellationToken cancellationToken) =>
        {
            var report = Suppliers(await suppliers.GetAllAsync(), byName == true);
            var pdf = await printer.PrintAsync<SupplierListView>(report.ByName ? SupplierByNameTitle : SupplierByCodeTitle, Paper.A4Landscape, new() { ["Report"] = report },
                AccountClaims.DisplayNameOf(http.User), cancellationToken: cancellationToken);
            return ReportsRegistration.Pdf(http, pdf, report.ByName ? "รายชื่อผู้ขายตามชื่อ" : "รายชื่อผู้ขายตามรหัส");
        });
        return reports;
    }
}
