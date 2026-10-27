using Lockte.Org;
using Lockte.Prod;
using Lockte.SharedKernel;
using Lockte.Web.Accounts;

namespace Lockte.Web.Reports.Order;

/// <summary>
/// A group of the monthly output report (RPT-16, BR-020): the lines it takes are told by product-code patterns and/or the machine, exactly the conditions written in the legacy queries
/// <c>ผลผลิตประจำเดือน*</c>. <see cref="Target"/> is the objective the % of rejects is judged against (null = none); <see cref="ByProductName"/> makes a row of each product (die-cast D0… parts).
/// </summary>
public sealed record YieldGroup(string Department, string Name, string[] Patterns, string? Machine = null, string? MachinePrefix = null, decimal? Target = null, bool ByProductName = false)
{
    public bool Takes(YieldLine line, string? machine)
        => (Patterns.Length == 0 || Patterns.Any(p => ProductGroupPattern.Matches(p, line.ProductCode)))
            && (Machine is null || string.Equals(machine, Machine, StringComparison.OrdinalIgnoreCase))
            && (MachinePrefix is null || machine?.StartsWith(MachinePrefix, StringComparison.OrdinalIgnoreCase) == true);
}

public sealed record YieldRow(string Department, string Name, decimal Good, decimal Reject, decimal? Target)
{
    public decimal Total => Good + Reject;

    /// <summary>% ของเสีย = เสีย ÷ (ดี + เสีย) × 100; null when nothing was made (the legacy divided by zero and showed an error).</summary>
    public decimal? Percent => Total == 0 ? null : Reject * 100m / Total;

    /// <summary>ผ่าน when the % is not over the target; null when the group has no target or no output.</summary>
    public bool? Passed => Target is { } target && Percent is { } percent ? percent <= target : null;
}

public sealed record YieldReport(DateOnly From, DateOnly To, IReadOnlyList<YieldRow> Rows)
{
    public decimal Good => Rows.Sum(r => r.Good);
    public decimal Reject => Rows.Sum(r => r.Reject);
    public decimal Total => Good + Reject;
    public decimal? Percent => Total == 0 ? null : Reject * 100m / Total;

    /// <summary>The overall objective written in the legacy report (0.80).</summary>
    public const decimal OverallTarget = 0.8m;

    public bool? Passed => Percent is { } percent ? percent <= OverallTarget : null;
}

/// <summary>A row of "รายงานผลผลิตรวมทุกแผนก": a product of a department with its type (or model) and the quantities.</summary>
public sealed record AllDepartmentsRow(string Department, string Group, string? Code, string Name, string? Unit, decimal Good, decimal Reject)
{
    public decimal Total => Good + Reject;
}

public sealed record AllDepartmentsReport(DateOnly From, DateOnly To, bool ByModel, IReadOnlyList<AllDepartmentsRow> Rows);

public sealed record DieCastRow(string Code, string Name, string? Unit, decimal Quantity, decimal? Weight)
{
    /// <summary>ใช้อลูมิเนียม = จำนวน × น้ำหนักต่อชิ้น; blank when the product has no weight (its quantity is still counted).</summary>
    public decimal? Aluminum => Weight is { } w ? Math.Round(Quantity * w, 2, MidpointRounding.AwayFromZero) : null;
}

public sealed record DieCastReport(DateOnly From, DateOnly To, IReadOnlyList<DieCastRow> Rows)
{
    public decimal TotalAluminum => Rows.Sum(r => r.Aluminum ?? 0);
}

/// <summary>ผลผลิตเดือน: a product by the twelve months of a year, then the total. A month with no output is 0 (the legacy table was set to 0 before the months were filled).</summary>
public sealed record MonthlyYieldRow(string Code, string Name, decimal[] Months)
{
    public decimal Total => Months.Sum();
}

public sealed record MonthlyYieldReport(int Year, IReadOnlyList<MonthlyYieldRow> Rows);

/// <summary>
/// The output reports of the month (#138): RPT-16 ผลผลิตประจำเดือน with the % of rejects against the objective of each group (BR-020), RPT-10 รายงานผลผลิตรวมทุกแผนก (ตามรหัส /
/// ตามรุ่น) and รวมแผนกไดคาสท์ with the aluminium used, and ผลผลิตเดือน. All read the reports that went into the stock (เข้าสต็อกแล้ว) — transfer the range first.
/// </summary>
public static class YieldReports
{
    public const string MonthlyTitle = "รายงานผลผลิตประจำเดือน";
    public const string AllDepartmentsTitle = "รายงานผลผลิตรวมทุกแผนก";
    public const string AllDepartmentsByModelTitle = "รายงานผลผลิตรวมทุกแผนก (ตามรุ่น)";
    public const string DieCastTitle = "รายงานผลผลิตรวมแผนกไดคาสท์";
    public const string YearTitle = "ผลผลิตเดือน";

    /// <summary>The groups of BR-020 in the order of the legacy queries. The two contractors are named by their machine only (D00, T03) — no names in the program.</summary>
    public static IReadOnlyList<YieldGroup> Groups { get; } =
    [
        new("ไดคาสท์", "ก้ามเบรก", ["G1*"], Target: 1.2m),
        new("ไดคาสท์", "", ["D0*"], MachinePrefix: "D", Target: 5m, ByProductName: true),
        new("ตกแต่ง", "ก้ามเบรก", ["G3*"], Machine: "T01", Target: 0.16m),
        new("อัดผ้าเบรก", "ผ้าเบรก", ["G5*"], Target: 1.2m),
        new("อัดผ้าเบรก", "ดิสเบรก", ["GD5*", "GD6*"], Target: 1.2m),
        new("ฝนผ้าเบรก", "ผ้าเบรก", ["G6*"], Target: 0.3m),
        new("ประกอบ", "ก้ามเบรก", [], Machine: "A01", Target: 0.35m),
        new("ผ้าคลัทช์และดิสเบรก", "คลัทช์", ["G9*"], Target: 1.2m),
        new("ผ้าคลัทช์และดิสเบรก", "ดิสเบรก", ["GD7*", "GD8*"], Target: 0.6m),
        new("บรรจุ", "ก้ามเปล่า", ["G3*"], Machine: "X01", Target: 0.3m),
        new("บรรจุ", "ก้ามเบรกกล่อง", ["SB*"]),
        new("บรรจุ", "ดิสเบรกสกินแพ็ค", ["SD*"]),
        new("บรรจุ", "ก้ามเบรกสกินแพ็ค", ["SK*"]),
        new("ผู้รับจ้างฉีด (เครื่อง D00)", "ฉีดก้ามเบรก", ["G1*"], Machine: "D00"),
        new("ผู้รับจ้างแต่ง (เครื่อง T03)", "แต่งก้ามเบรก", ["G3*"], Machine: "T03"),
        new("บรรจุ", "ดิสเบรกกล่อง", ["DB*"]),
        new("ไดคาสท์", "คลัทช์ฉีด", ["G0*"]),
        new("บรรจุ", "คลัทช์ตกแต่ง", ["G2*"]),
        new("ผ้าคลัทช์และดิสเบรก", "ไม้ก๊อก", ["G8*"]),
        new("บรรจุ", "คลัทช์กล่อง", ["SX*"]),
        new("บรรจุ", "คลัทช์สกินแพค", ["SG*"]),
    ];

    private static async Task<(IReadOnlyList<YieldLine> Lines, Func<YieldLine, string?> Machine)> LoadAsync(ProductionReportDirectory reports, OrgDirectory org, DateOnly from, DateOnly to)
    {
        var machines = (await org.GetMachinesAsync()).ToDictionary(m => m.Id, m => m.Code);
        return (await reports.GetYieldLinesAsync(from, to), l => l.MachineId is { } id ? machines.GetValueOrDefault(id) : null);
    }

    /// <summary>RPT-16: for each group, Σ ดี and Σ เสีย of the lines it takes; a group with no output is not shown (BR-020).</summary>
    public static async Task<YieldReport> MonthlyAsync(ProductionReportDirectory reports, OrgDirectory org, DateOnly from, DateOnly to)
    {
        var (lines, machineOf) = await LoadAsync(reports, org, from, to);
        var rows = new List<YieldRow>();
        foreach (var group in Groups)
        {
            var taken = lines.Where(l => group.Takes(l, machineOf(l))).ToList();
            if (taken.Count == 0) continue;
            if (group.ByProductName)
                rows.AddRange(taken.GroupBy(l => l.ProductName).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => new YieldRow(group.Department, g.Key, g.Sum(l => l.Good), g.Sum(l => l.Reject), group.Target)));
            else
                rows.Add(new YieldRow(group.Department, group.Name, taken.Sum(l => l.Good), taken.Sum(l => l.Reject), group.Target));
        }
        return new(from, to, rows);
    }

    /// <summary>RPT-10 รวมทุกแผนก: department → type (or model) → product, with the quantities (one row per product and unit).</summary>
    public static async Task<AllDepartmentsReport> AllDepartmentsAsync(ProductionReportDirectory reports, OrgDirectory org, DateOnly from, DateOnly to, bool byModel)
    {
        var (lines, _) = await LoadAsync(reports, org, from, to);
        var order = ProductionDepartments.All.Select((d, i) => (d.Code, i)).ToDictionary(x => x.Code, x => x.i);
        var rows = lines.GroupBy(l => (l.DepartmentCode, Group: (byModel ? l.Model : l.ProductType) ?? "ไม่ระบุ", Code: byModel ? null : l.ProductCode, Name: byModel ? (l.Model ?? "ไม่ระบุรุ่น") : l.ProductName, Unit: l.Unit?.Trim()))
            .OrderBy(g => order.GetValueOrDefault(g.Key.DepartmentCode, 99)).ThenBy(g => g.Key.Group, StringComparer.Ordinal).ThenBy(g => g.Key.Code, StringComparer.Ordinal).ThenBy(g => g.Key.Name, StringComparer.Ordinal)
            .Select(g => new AllDepartmentsRow(ProductionDepartments.Find(g.Key.DepartmentCode)?.Name ?? g.Key.DepartmentCode, g.Key.Group, g.Key.Code, g.Key.Name, g.Key.Unit,
                g.Sum(l => l.Good), g.Sum(l => l.Reject))).ToList();
        return new(from, to, byModel, rows);
    }

    /// <summary>RPT-10 รวมแผนกไดคาสท์: per product Σ จำนวน (ดี + เสีย, the metal went into the rejects too), the weight per piece and the aluminium used.</summary>
    public static async Task<DieCastReport> DieCastAsync(ProductionReportDirectory reports, OrgDirectory org, DateOnly from, DateOnly to)
    {
        var (lines, _) = await LoadAsync(reports, org, from, to);
        var rows = lines.Where(l => l.DepartmentCode == "D").GroupBy(l => (l.ProductCode, Unit: l.Unit?.Trim()))
            .OrderBy(g => g.Key.ProductCode, StringComparer.Ordinal)
            .Select(g => new DieCastRow(g.Key.ProductCode, g.First().ProductName, g.Key.Unit, g.Sum(l => l.Total), g.First().AluminumWeight)).ToList();
        return new(from, to, rows);
    }

    /// <summary>ผลผลิตเดือน: the good quantity of each product in the twelve months of <paramref name="year"/> (month of the report date), calculated all at once.</summary>
    public static async Task<MonthlyYieldReport> YearAsync(ProductionReportDirectory reports, OrgDirectory org, int year)
    {
        var (lines, _) = await LoadAsync(reports, org, new DateOnly(year, 1, 1), new DateOnly(year, 12, 31));
        var rows = lines.GroupBy(l => l.ProductCode).OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new MonthlyYieldRow(g.Key, g.First().ProductName, Enumerable.Range(1, 12).Select(m => g.Where(l => l.Date.Month == m).Sum(l => l.Good)).ToArray())).ToList();
        return new(year, rows);
    }

    public static string MonthlyHeading(DateOnly from, DateOnly to) => $"{MonthlyTitle} {ReportText.MonthTitle(from, to)}";

    public static RouteGroupBuilder MapYieldReports(this RouteGroupBuilder reports)
    {
        var group = reports.MapGroup("/order/yield");

        group.MapGet("/monthly", async (DateOnly? from, DateOnly? to, HttpContext http, ProductionReportDirectory directory, OrgDirectory org, ReportPrinter printer, CancellationToken ct) =>
        {
            if (HrAttendanceReports.Range(from, to) is { } bad) return bad;
            var title = MonthlyHeading(from!.Value, to!.Value);
            var pdf = await printer.PrintAsync<YieldMonthlyView>(title, Paper.A4Portrait, new() { ["Report"] = await MonthlyAsync(directory, org, from.Value, to.Value) },
                AccountClaims.DisplayNameOf(http.User), DocumentKeys.MonthlyYield, ct);
            return ReportsRegistration.Pdf(http, pdf, title);
        });

        group.MapGet("/all/{by}", async (string by, DateOnly? from, DateOnly? to, HttpContext http, ProductionReportDirectory directory, OrgDirectory org, ReportPrinter printer, CancellationToken ct) =>
        {
            if (HrAttendanceReports.Range(from, to) is { } bad) return bad;
            if (by is not ("code" or "model")) return Results.NotFound();
            var report = await AllDepartmentsAsync(directory, org, from!.Value, to!.Value, by == "model");
            var title = by == "model" ? AllDepartmentsByModelTitle : AllDepartmentsTitle;
            return await HrAttendanceReports.PrintAsync<YieldAllDepartmentsView>(http, printer, title, Paper.A4Landscape, report, title, ct);
        });

        group.MapGet("/die-cast", async (DateOnly? from, DateOnly? to, HttpContext http, ProductionReportDirectory directory, OrgDirectory org, ReportPrinter printer, CancellationToken ct) =>
        {
            if (HrAttendanceReports.Range(from, to) is { } bad) return bad;
            return await HrAttendanceReports.PrintAsync<YieldDieCastView>(http, printer, DieCastTitle, Paper.A4Portrait, await DieCastAsync(directory, org, from!.Value, to!.Value), DieCastTitle, ct);
        });

        group.MapGet("/year", async (int? year, HttpContext http, ProductionReportDirectory directory, OrgDirectory org, ReportPrinter printer, CancellationToken ct) =>
        {
            if (year is null or < 1990 or > 2200) return Results.BadRequest("กรุณาเลือกปี");
            return await HrAttendanceReports.PrintAsync<YieldYearView>(http, printer, YearTitle, Paper.A4Landscape, await YearAsync(directory, org, year.Value), $"{YearTitle} {year}", ct);
        });

        return reports;
    }
}
