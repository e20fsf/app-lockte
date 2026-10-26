using Lockte.Org;
using Lockte.Prod;
using Lockte.SharedKernel;

namespace Lockte.Web.Reports.Order;

public sealed record ProductionSlipLine(
    int LineNo, string? Shift, decimal? Hours, string? MaterialLot1, string? MaterialLot2, string? ProductCode, string ProductName, string? Unit,
    decimal Good, decimal? Reject, decimal Total, string? Mold, string? Lot, string? Employee);

/// <summary>One report as it is printed: the head (number, department, machine, date) and its lines, with the totals and the reject percentage against the department's target.</summary>
public sealed record ProductionSlip(string ReportNo, ProductionDepartment Department, DateOnly? Date, string? Machine, IReadOnlyList<ProductionSlipLine> Lines)
{
    public decimal Good => Lines.Sum(l => l.Good);
    public decimal Reject => Lines.Sum(l => l.Reject ?? 0);
    public decimal Total => Lines.Sum(l => l.Total);

    /// <summary>เปอร์เซ็นต์เสีย = เสีย ÷ จำนวนทั้งหมด × 100, two decimals; null when nothing was made.</summary>
    public decimal? RejectPercent => Total == 0 ? null : Math.Round(Reject * 100m / Total, 2, MidpointRounding.AwayFromZero);

    /// <summary>ผ่าน when the percentage is not over the target of the department; null when the department has no target (บรรจุ) or nothing was made.</summary>
    public bool? Passed => Department.RejectTargetPercent == 0 || RejectPercent is not { } percent ? null : percent <= Department.RejectTargetPercent;
}

public sealed record ProductionSlipsReport(IReadOnlyList<ProductionSlip> Slips);

/// <summary>
/// RPT-10 ใบรายงานผลผลิตประจำวัน, ใบโอนสินค้าระหว่างแผนก และใบแจ้งสุ่มตัวอย่าง (P_REP&lt;แผนก&gt;2, PC-PD003-4/2547 ฉบับที่2, #139), ระดับ B on A4 นอน: one page per
/// report of the department, read from the reports themselves — whether they went into the stock does not matter. The same page for all seven departments, the lot labels from their settings.
/// </summary>
public static class ProductionSlipReports
{
    public const string Title = "ใบรายงานผลผลิตประจำวัน ใบโอนสินค้าระหว่างแผนก และใบแจ้งสุ่มตัวอย่าง";

    public static async Task<ProductionSlipsReport> BuildAsync(ProductionReportDirectory reports, ProductDirectory products, OrgDirectory org, IProductionEmployeeLookup employees,
        DateOnly from, DateOnly to, string? departmentCode, string? reportNo)
    {
        var found = await reports.ListAsync(from, to, departmentCode, reportNo);
        var machines = (await org.GetMachinesAsync()).ToDictionary(m => m.Id, m => m.Code);
        var shifts = (await org.GetShiftsAsync()).ToDictionary(s => s.Id, s => s.Code);
        var productCodes = new Dictionary<int, string>();
        foreach (var id in found.SelectMany(r => r.Lines).Where(l => l.ProductId is not null).Select(l => l.ProductId!.Value).Distinct())
            if (await products.GetAsync(id) is { } product) productCodes[id] = product.Code;
        var employeeCodes = new Dictionary<int, string>();
        foreach (var id in found.SelectMany(r => r.Lines).Where(l => l.EmployeeId is not null).Select(l => l.EmployeeId!.Value).Distinct())
            if (await employees.FindAsync(id) is { } employee) employeeCodes[id] = employee.Code;

        var slips = found.Select(r => new ProductionSlip(r.ReportNo, ProductionDepartments.Find(r.DepartmentCode)!, r.ReportDate, r.MachineId is { } m ? machines.GetValueOrDefault(m) : null,
            r.Lines.OrderBy(l => l.LineNo).Select(l => new ProductionSlipLine(l.LineNo, l.ShiftId is { } s ? shifts.GetValueOrDefault(s) : null, l.Hours, l.MaterialLot1, l.MaterialLot2,
                l.ProductId is { } p ? productCodes.GetValueOrDefault(p) : null, l.ProductName, l.Unit, l.GoodQuantity, l.RejectQuantity, l.TotalQuantity, l.MoldCode, l.Lot,
                l.EmployeeId is { } e ? employeeCodes.GetValueOrDefault(e) : null)).ToList())).ToList();
        return new(slips);
    }

    /// <summary>"วันพฤหัสบดีที่ 15 ตุลาคม 2569": the day of the week, the day, the month and the year in พ.ศ.</summary>
    public static string DateText(DateOnly date)
        => "วัน" + date.ToString("dddd", ThaiDate.Culture) + "ที่ " + date.ToString("d MMMM yyyy", ThaiDate.Culture);

    public static RouteGroupBuilder MapProductionSlipReports(this RouteGroupBuilder reports)
    {
        reports.MapGet("/order/production/slips", async (DateOnly? from, DateOnly? to, string? dept, string? no, HttpContext http, ProductionReportDirectory directory, ProductDirectory products,
            OrgDirectory org, IProductionEmployeeLookup employees, ReportPrinter printer, CancellationToken ct) =>
        {
            if (HrAttendanceReports.Range(from, to) is { } bad) return bad;
            if (!string.IsNullOrWhiteSpace(dept) && ProductionDepartments.Find(dept) is null) return Results.BadRequest("ไม่รู้จักแผนกนี้");
            var report = await BuildAsync(directory, products, org, employees, from!.Value, to!.Value, dept, no);
            var pdf = await printer.PrintAsync<ProductionSlipsView>(Title, Paper.A4Landscape, new() { ["Report"] = report }, Accounts.AccountClaims.DisplayNameOf(http.User), DocumentKeys.ProductionSlip, ct);
            return ReportsRegistration.Pdf(http, pdf, "ใบรายงานผลผลิต");
        });
        return reports;
    }
}
