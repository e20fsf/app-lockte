using Lockte.Org;
using Lockte.Prod;
using Lockte.Sales;
using Lockte.Web.Accounts;

namespace Lockte.Web.Reports.Order;

/// <summary>
/// The rows of one RPT-21 list (#137) and what narrows them, as the page head tells it (D-10): the lines are what the module
/// services returned, in the register's order.
/// </summary>
public sealed record MasterList<T>(string? Filter, IReadOnlyList<T> Lines);

/// <summary>
/// RPT-21 รายการข้อมูลหลัก of ผลผลิตและORDER (#137), all ระดับ B: รายชื่อลูกค้า, รายชื่อสินค้า (ทั้งหมด / ตามรหัส / ตามลูกค้า),
/// ทะเบียนแม่พิมพ์ with its เลขเอกสารควบคุม and รายชื่อแม่พิมพ์ที่ยังใช้งานอยู่, รายชื่อเครื่องจักร, รายชื่อเขตการขาย/พนักงานขาย.
/// The BARCODE report is not built until the users say they use it (#102 คำถามที่เหลือ 2).
/// </summary>
public static class MasterListReports
{
    public const string CustomersTitle = "รายชื่อลูกค้า";
    public const string ProductsTitle = "รายชื่อสินค้า";
    public const string MoldRegisterTitle = "ทะเบียนแม่พิมพ์";
    public const string ActiveMoldsTitle = "รายชื่อแม่พิมพ์ที่ยังใช้งานอยู่";
    public const string MachinesTitle = "รายชื่อเครื่องจักร";
    public const string TerritoriesTitle = "รายชื่อเขตการขายและพนักงานขาย";

    public static async Task<MasterList<CustomerDto>> CustomersAsync(CustomerDirectory customers)
        => new(null, await customers.GetAllAsync());

    /// <summary>พิมพ์รายชื่อสินค้า (all), พิมพ์ตามรหัส (<paramref name="code"/>, a pattern such as <c>G1*</c>), พิมพ์ตามลูกค้า (<paramref name="customerId"/>).</summary>
    public static async Task<MasterList<ProductListLine>> ProductsAsync(ProductDirectory products, CustomerDirectory customers, string? code, int? customerId)
    {
        var filters = new List<string>();
        if (!string.IsNullOrWhiteSpace(code)) filters.Add($"รหัสสินค้า {code.Trim()}");
        if (customerId is { } id && await customers.GetAsync(id) is { } customer) filters.Add($"ลูกค้า {customer.Code} {customer.Name}");
        return new(filters.Count == 0 ? null : string.Join(" · ", filters), await products.GetListAsync(code, customerId));
    }

    public static async Task<MasterList<MoldRegisterLine>> MoldsAsync(MoldDirectory molds, bool inUseOnly)
        => new(null, await molds.GetRegisterAsync(inUseOnly));

    public static async Task<MasterList<MachineDto>> MachinesAsync(OrgDirectory org) => new(null, await org.GetMachinesAsync());

    public static async Task<MasterList<SalesTerritoryDto>> TerritoriesAsync(SalesTerritoryDirectory territories) => new(null, await territories.GetAllAsync());

    public static RouteGroupBuilder MapOrderMasterListReports(this RouteGroupBuilder reports)
    {
        var order = reports.MapGroup("/order");

        // เมนู 7 "พิมพ์รายชื่อทั้งหมด" (P_CUS1): A4 นอน — the legacy printed Letter portrait; the columns need the width
        order.MapGet("/customers", async (HttpContext http, CustomerDirectory customers, ReportPrinter printer, CancellationToken cancellationToken)
            => await HrAttendanceReports.PrintAsync<CustomerListView>(http, printer, CustomersTitle, Paper.A4Landscape,
                await CustomersAsync(customers), CustomersTitle, cancellationToken));

        // เมนู 8 (P_NAME2 "พิมพ์รายชื่อผลผลิต", "พิมพ์ตามรหัส", "พิมพ์ตามลูกค้า"): ?code= รหัสสินค้า (มี * ได้), ?customer= ลูกค้า
        order.MapGet("/products", async (string? code, int? customer, HttpContext http, ProductDirectory products, CustomerDirectory customers,
            ReportPrinter printer, CancellationToken cancellationToken)
            => await HrAttendanceReports.PrintAsync<ProductListView>(http, printer, ProductsTitle, Paper.A4Landscape,
                await ProductsAsync(products, customers, code, customer), ProductsTitle, cancellationToken));

        // เมนู 6: ทะเบียนแม่พิมพ์ (every mold, with the เลขเอกสารควบคุม) and รายชื่อที่ยังใช้งานอยู่ (A4, no number)
        order.MapGet("/molds", async (HttpContext http, MoldDirectory molds, ReportPrinter printer, CancellationToken cancellationToken) =>
        {
            var pdf = await printer.PrintAsync<MoldRegisterView>(MoldRegisterTitle, Paper.A4Portrait,
                new() { ["Report"] = await MoldsAsync(molds, inUseOnly: false), ["InUseOnly"] = false }, AccountClaims.DisplayNameOf(http.User),
                DocumentKeys.MoldRegister, cancellationToken);
            return ReportsRegistration.Pdf(http, pdf, MoldRegisterTitle);
        });
        order.MapGet("/molds/active", async (HttpContext http, MoldDirectory molds, ReportPrinter printer, CancellationToken cancellationToken) =>
        {
            var pdf = await printer.PrintAsync<MoldRegisterView>(ActiveMoldsTitle, Paper.A4Portrait,
                new() { ["Report"] = await MoldsAsync(molds, inUseOnly: true), ["InUseOnly"] = true }, AccountClaims.DisplayNameOf(http.User),
                cancellationToken: cancellationToken);
            return ReportsRegistration.Pdf(http, pdf, ActiveMoldsTitle);
        });

        // เมนู 6 "พิมพ์รายชื่อเครื่องจักร" (MC_N)
        order.MapGet("/machines", async (HttpContext http, OrgDirectory org, ReportPrinter printer, CancellationToken cancellationToken)
            => await HrAttendanceReports.PrintAsync<MachineListView>(http, printer, MachinesTitle, Paper.A4Portrait,
                await MachinesAsync(org), MachinesTitle, cancellationToken));

        // เมนู 9 "พิมพ์รายชื่อ" (EMPLO1)
        order.MapGet("/territories", async (HttpContext http, SalesTerritoryDirectory territories, ReportPrinter printer, CancellationToken cancellationToken)
            => await HrAttendanceReports.PrintAsync<TerritoryListView>(http, printer, TerritoriesTitle, Paper.A4Portrait,
                await TerritoriesAsync(territories), TerritoriesTitle, cancellationToken));

        return reports;
    }
}
