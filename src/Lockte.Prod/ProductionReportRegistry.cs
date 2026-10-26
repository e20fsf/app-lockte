using System.Text.RegularExpressions;
using Lockte.Org;
using Lockte.Prod.Data;
using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Prod;

/// <summary>
/// แผนกที่บันทึกผลผลิต — configuration, not code paths (#116): a department added later (#117, #118) only adds a row here.
/// <see cref="Code"/> is the letter its report numbers start with (BR-015; ❓ Q-36: not the prefix of T-แผนก for บรรจุ).
/// </summary>
public sealed record ProductionDepartment(
    string Code,
    string Name,
    string? DefaultMachineCode,
    string MaterialLot1Label,
    string? MaterialLot2Label,
    bool FillsMoldFromProduct);

public static class ProductionDepartments
{
    public static IReadOnlyList<ProductionDepartment> All { get; } =
    [
        new("D", "ไดคาสท์", null, "ล๊อตอลูมิเนียม", "ล๊อตขาเหล็ก", true),
        new("T", "ตกแต่ง", "T01", "ล๊อตวัตถุดิบ1", null, false),
        new("B", "อัดผ้าเบรก", null, "ล๊อตเหล็ก", "ล๊อตเคมีผ้าเบรก", true),
        new("G", "ฝนผ้าเบรก", null, "ล๊อตวัตถุดิบ1", null, false),
        new("K", "ผ้าคลัทช์และดิสเบรก", null, "ล๊อตวัตถุดิบ1", "ล๊อตวัตถุดิบ2", true),
        new("A", "ประกอบ", "A01", "ล๊อตวัตถุดิบ1", "ล๊อตวัตถุดิบ2", false),
        new("P", "บรรจุ", "P01", "ล๊อตวัตถุดิบ1", null, false),
    ];

    public static ProductionDepartment? Find(string? code)
        => All.FirstOrDefault(d => string.Equals(d.Code, code?.Trim(), StringComparison.OrdinalIgnoreCase));
}

/// <summary>พนักงาน from the register of module บุคคล — the interface lives here because that module is not referenced by this one.</summary>
public sealed record ProductionEmployeeRef(int Id, string Code, string Name);

public interface IProductionEmployeeLookup
{
    Task<ProductionEmployeeRef?> FindAsync(int employeeId);
    Task<IReadOnlyList<ProductionEmployeeRef>> SearchAsync(string? text, int take);
}

/// <summary><see cref="Id"/> null = a new line; a line whose <see cref="GoodQuantity"/> is blank is dropped (BR-016).</summary>
public sealed record ProductionReportLineInput(
    int? Id, byte[]? RowVersion, int? ProductId, string? ProductName, string? Unit, int? ShiftId, decimal? Hours,
    string? MaterialLot1, string? MaterialLot2, decimal? GoodQuantity, decimal? RejectQuantity, int? EmployeeId, int? MoldId, string? Lot);

/// <summary><see cref="Id"/> null = a new report.</summary>
public sealed record ProductionReportInput(
    int? Id, byte[]? RowVersion, string DepartmentCode, DateOnly? ReportDate, string ReportNo, int? MachineId,
    IReadOnlyList<ProductionReportLineInput> Lines);

/// <summary>"บันทึก" drops the lines without จำนวนดี; "กลับเมนูเดิม" drops those without จำนวนดี <i>or</i> without a date (BR-016).</summary>
public enum ProductionSaveMode { Save, Leave }

public sealed record ProductionReportSaveResult(bool Succeeded, IReadOnlyList<string> Errors, int? Id, string? NextNo);

public sealed record ProductionReportLineDto(
    int Id, int LineNo, int? ProductId, string ProductName, string? Unit, int? ShiftId, decimal? Hours, string? MaterialLot1, string? MaterialLot2,
    decimal GoodQuantity, decimal? RejectQuantity, decimal TotalQuantity, int? EmployeeId, int? MoldId, string? MoldCode, string? Lot, byte[] RowVersion);

public sealed record ProductionReportDto(
    int Id, string DepartmentCode, DateOnly? ReportDate, string ReportNo, int? MachineId, IReadOnlyList<ProductionReportLineDto> Lines, byte[] RowVersion,
    DateTimeOffset? StockedAt = null)
{
    /// <summary>เข้าสต็อกแล้ว: the production was transferred to the stock (BR-005); changing the report does not change the stock until the range is transferred again (⚠ BR-019).</summary>
    public bool InStock => StockedAt is not null;
}

/// <summary>A report in the list of แก้ไขผลผลิต (SCR-11): found by its number or by its date.</summary>
public sealed record ProductionReportSummary(int Id, string DepartmentCode, string DepartmentName, DateOnly? ReportDate, string ReportNo, int Lines, DateTimeOffset? StockedAt);

/// <summary>A production line as the Lot trace (RPT-17) reads it: where it was made (department, machine), what, how many, and its Lots — the one it made and the two it received.</summary>
public sealed record LotLine(
    DateOnly? Date, string DepartmentCode, int? MachineId, string? ProductCode, string ProductName, decimal Good, decimal Total, string? Lot, string? MaterialLot1, string? MaterialLot2);

public sealed record ProductionLineDefaults(string ProductName, string? Unit, int? MoldId, string? MoldCode);

public sealed class ProductionReportRegistry(ProdDbContext db, OrgDirectory org, IProductionEmployeeLookup? employees = null)
{
    /// <summary>REF_NO is Text(8).</summary>
    public const int ReportNoLength = 8;
    /// <summary>PD_N, LOT, LOT_MTR1/2 and the other text fields are Text(50).</summary>
    public const int TextLength = 50;

    /// <summary>จำนวนรวม = ดี + เสีย (BR-016, AC-21): worked out on every save, whichever of the two was keyed last.</summary>
    public static decimal Total(decimal? good, decimal? reject) => (good ?? 0) + (reject ?? 0);

    public async Task<ProductionReportSaveResult> SaveAsync(ProductionReportInput input, ProductionSaveMode mode)
    {
        var errors = new List<string>();
        var department = ProductionDepartments.Find(input.DepartmentCode);
        if (department is null) return Failed("ไม่รู้จักแผนกนี้");
        var reportNo = input.ReportNo?.Trim().ToUpperInvariant() ?? "";
        if (reportNo.Length == 0) errors.Add("กรุณาใส่เลขที่ใบรายงาน");
        else if (reportNo.Length > ReportNoLength) errors.Add($"เลขที่ใบรายงานยาวได้ไม่เกิน {ReportNoLength} ตัว");
        else if (!reportNo.StartsWith(department.Code, StringComparison.Ordinal)) errors.Add($"เลขที่ใบรายงานของแผนก{department.Name}ต้องขึ้นต้นด้วย {department.Code}");

        ProductionReport? report = null;
        if (input.Id is { } id)
        {
            report = await db.ProductionReports.Include(r => r.Lines).FirstOrDefaultAsync(r => r.Id == id);
            if (report is null) return Failed("ไม่พบใบรายงานนี้ อาจถูกลบไปแล้ว");
            report.ExpectRowVersion(input.RowVersion ?? []);
        }
        if (errors.Count == 0 && await db.ProductionReports.AnyAsync(r => r.ReportNo == reportNo && r.Id != (input.Id ?? 0)))
        {
            var proposed = await ProposeAsync(department.Code);
            return new ProductionReportSaveResult(false, [$"เลขที่ใบรายงาน '{reportNo}' มีอยู่แล้ว" + (proposed is null ? "" : $" — เลขถัดไปที่ว่างคือ {proposed}")], null, proposed);
        }

        if (input.MachineId is { } machineId && (await org.GetMachinesAsync()).All(m => m.Id != machineId)) errors.Add("ไม่พบเครื่องจักรที่เลือก");

        var shifts = (await org.GetShiftsAsync()).Select(s => s.Id).ToHashSet();
        var kept = new List<(ProductionReportLine? Existing, ProductionReportLineInput Edit, Product? Product)>();
        foreach (var edit in input.Lines)
        {
            // BR-016: a line without จำนวนดี is dropped; leaving the screen drops those without a date as well.
            if (edit.GoodQuantity is null || (mode == ProductionSaveMode.Leave && input.ReportDate is null)) continue;
            if (edit.GoodQuantity < 0 || edit.RejectQuantity < 0 || edit.Hours < 0) { errors.Add("จำนวนดี เสีย และชั่วโมงต้องไม่ติดลบ"); continue; }
            ProductionReportLine? existing = null;
            if (edit.Id is { } lineId)
            {
                existing = report?.Lines.FirstOrDefault(l => l.Id == lineId);
                if (existing is null) { errors.Add("ไม่พบบรรทัดที่แก้ อาจถูกลบไปแล้ว"); continue; }
                if (edit.RowVersion is not null) existing.ExpectRowVersion(edit.RowVersion);
            }
            Product? product = null;
            if (edit.ProductId is { } productId)
            {
                product = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == productId);
                if (product is null) { errors.Add("ไม่พบสินค้าที่เลือกในทะเบียนสินค้า"); continue; }
            }
            if (edit.ShiftId is { } shiftId && !shifts.Contains(shiftId)) { errors.Add("ไม่พบกะที่เลือก"); continue; }
            if (edit.EmployeeId is { } employeeId && (employees is null || await employees.FindAsync(employeeId) is null)) { errors.Add("เลือกพนักงานจากทะเบียนพนักงานเท่านั้น"); continue; }
            if (edit.MoldId is { } moldId && !await db.Molds.AnyAsync(m => m.Id == moldId)) { errors.Add("ไม่พบแม่พิมพ์ที่เลือก"); continue; }
            if (new[] { edit.ProductName, edit.Unit, edit.MaterialLot1, edit.MaterialLot2, edit.Lot }.Any(t => t?.Trim().Length > TextLength))
            {
                errors.Add($"ข้อความยาวได้ไม่เกิน {TextLength} ตัว");
                continue;
            }
            kept.Add((existing, edit, product));
        }
        if (errors.Count > 0) return Failed(errors.Distinct().ToArray());

        // The legacy file has no head apart from its lines: a report with no line left does not exist.
        if (kept.Count == 0)
        {
            if (report is not null) { db.ProductionReports.Remove(report); await db.SaveChangesAsync(); }
            return new ProductionReportSaveResult(true, [], null, await ProposeAsync(department.Code));
        }

        if (report is null)
        {
            report = new ProductionReport { DepartmentCode = department.Code };
            db.ProductionReports.Add(report);
        }
        report.ReportDate = input.ReportDate;
        report.ReportNo = reportNo;
        report.MachineId = input.MachineId;

        var keepIds = kept.Where(k => k.Existing is not null).Select(k => k.Existing!.Id).ToHashSet();
        foreach (var gone in report.Lines.Where(l => !keepIds.Contains(l.Id)).ToList()) db.ProductionReportLines.Remove(gone);
        var nextLineNo = report.Lines.Select(l => l.LineNo).DefaultIfEmpty(0).Max();
        foreach (var (existing, edit, product) in kept)
        {
            var line = existing;
            if (line is null)
            {
                line = new ProductionReportLine { ProductionReport = report, LineNo = ++nextLineNo };
                report.Lines.Add(line);
            }
            line.ProductId = product?.Id;
            line.ProductName = Clean(edit.ProductName) ?? product?.Name ?? "";
            line.Unit = Clean(edit.Unit) ?? product?.Unit;
            line.ShiftId = edit.ShiftId;
            line.Hours = edit.Hours;
            line.MaterialLot1 = Clean(edit.MaterialLot1);
            line.MaterialLot2 = Clean(edit.MaterialLot2);
            line.GoodQuantity = edit.GoodQuantity!.Value;
            line.RejectQuantity = edit.RejectQuantity;
            line.TotalQuantity = Total(edit.GoodQuantity, edit.RejectQuantity);
            line.EmployeeId = edit.EmployeeId;
            line.MoldId = edit.MoldId ?? (department.FillsMoldFromProduct ? product?.MoldId : null);
            line.Lot = Clean(edit.Lot);
        }
        await db.SaveChangesAsync();
        return new ProductionReportSaveResult(true, [], report.Id, await ProposeAsync(department.Code));
    }

    /// <summary>Deletes the whole report (the lines go with it).</summary>
    public async Task<ServiceResult> DeleteAsync(int id, byte[] loadedRowVersion)
    {
        var report = await db.ProductionReports.Include(r => r.Lines).FirstOrDefaultAsync(r => r.Id == id);
        if (report is null) return ServiceResult.Ok();
        report.ExpectRowVersion(loadedRowVersion);
        db.ProductionReports.Remove(report);
        await db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    /// <summary>เลขที่ใบถัดไปของแผนก (BR-015).</summary>
    public Task<string?> ProposeAsync(string departmentCode) => ProductionReportNumbering.ProposeAsync(db, departmentCode);

    private static ProductionReportSaveResult Failed(params string[] errors) => new(false, errors, null, null);

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}

/// <summary>เลขที่ใบรายงาน <c>LYYNNN</c> (BR-015).</summary>
internal static class ProductionReportNumbering
{
    private static readonly Regex Shape = new(@"^[A-Z]\d{5,}$", RegexOptions.Compiled);

    /// <summary>
    /// 3 ตัวแรกของเลขมากสุดของแผนก + (3 ตัวท้าย + 1) จากใบจริงของแผนก (ข้อสมมติ ฉ) — the year is not changed by itself: the user
    /// keys the first number of the year and the next follows it. <c>D69205</c> → <c>D69206</c>; <c>D69999</c> → <c>D691000</c>.
    /// Null when the department has no report yet (the user types the first).
    /// </summary>
    public static async Task<string?> ProposeAsync(ProdDbContext db, string departmentCode)
    {
        var code = departmentCode.Trim().ToUpperInvariant();
        var numbers = await db.ProductionReports.AsNoTracking().Where(r => r.DepartmentCode == code).Select(r => r.ReportNo).ToListAsync();
        var max = numbers.Where(n => Shape.IsMatch(n)).OrderBy(n => n, StringComparer.Ordinal).LastOrDefault();
        if (max is null) return null;
        var next = Next(max);
        var used = numbers.ToHashSet();
        while (used.Contains(next)) next = Next(next); // never offers a number that is taken
        return next;
    }

    public static string Next(string number)
        => number[..3] + (int.Parse(number[3..]) + 1).ToString("000");
}

/// <summary>Reads for the screens of ใบรายงานผลผลิต.</summary>
public sealed class ProductionReportDirectory(ProdDbContext db, OrgDirectory org)
{
    public async Task<ProductionReportDto?> GetAsync(int id)
        => await db.ProductionReports.AsNoTracking().Include(r => r.Lines).ThenInclude(l => l.Mold).FirstOrDefaultAsync(r => r.Id == id) is { } r ? ToDto(r) : null;

    public async Task<ProductionReportDto?> FindByNumberAsync(string reportNo)
    {
        var no = reportNo.Trim().ToUpperInvariant();
        return await db.ProductionReports.AsNoTracking().Include(r => r.Lines).ThenInclude(l => l.Mold).FirstOrDefaultAsync(r => r.ReportNo == no) is { } r ? ToDto(r) : null;
    }

    /// <summary>เลขที่ถัดไปของแผนก; null when it has no report yet.</summary>
    public Task<string?> ProposeNumberAsync(string departmentCode) => ProductionReportNumbering.ProposeAsync(db, departmentCode);

    /// <summary>เครื่องเริ่มต้นของแผนก (ตกแต่ง <c>T01</c>, ประกอบ <c>A01</c>, บรรจุ <c>P01</c>); null for the others or when the machine is not in the register.</summary>
    public async Task<int?> DefaultMachineIdAsync(string departmentCode)
        => ProductionDepartments.Find(departmentCode)?.DefaultMachineCode is { } machine ? (await org.FindMachineByCodeAsync(machine))?.Id : null;

    /// <summary>ชื่อ หน่วย และแม่พิมพ์ที่เติมเมื่อเลือกสินค้า (แม่พิมพ์เฉพาะแผนกที่เติมจากสินค้า).</summary>
    public async Task<ProductionLineDefaults?> DefaultsForProductAsync(string departmentCode, int productId)
    {
        var product = await db.Products.AsNoTracking().Include(p => p.Mold).FirstOrDefaultAsync(p => p.Id == productId);
        if (product is null) return null;
        var mold = ProductionDepartments.Find(departmentCode)?.FillsMoldFromProduct == true ? product.Mold : null;
        return new ProductionLineDefaults(product.Name, product.Unit, mold?.Id, mold?.Code);
    }

    private IQueryable<LotLine> LotLines(IQueryable<ProductionReportLine> lines)
        => lines.OrderBy(l => l.ProductionReport.ReportDate).ThenBy(l => l.Lot).ThenBy(l => l.Id)
            .Select(l => new LotLine(l.ProductionReport.ReportDate, l.ProductionReport.DepartmentCode, l.ProductionReport.MachineId, l.Product == null ? null : l.Product.Code, l.ProductName,
                l.GoodQuantity, l.TotalQuantity, l.Lot, l.MaterialLot1, l.MaterialLot2));

    /// <summary>The production lines of reports dated in the range — the first stage of a Lot trace.</summary>
    public async Task<IReadOnlyList<LotLine>> GetLotLinesAsync(DateOnly from, DateOnly to)
        => await LotLines(db.ProductionReportLines.AsNoTracking().Where(l => l.ProductionReport.ReportDate >= from && l.ProductionReport.ReportDate <= to)).ToListAsync();

    /// <summary>The lines whose own Lot is one of <paramref name="lots"/>, of any date (the stage before in a trace).</summary>
    public async Task<IReadOnlyList<LotLine>> GetLotLinesByLotAsync(IReadOnlyCollection<string> lots)
        => lots.Count == 0 ? [] : await LotLines(db.ProductionReportLines.AsNoTracking().Where(l => l.Lot != null && lots.Contains(l.Lot))).ToListAsync();

    /// <summary>The lines that received one of <paramref name="lots"/> as raw material 1 (<paramref name="second"/> false) or 2, of any date (the stage after in a trace).</summary>
    public async Task<IReadOnlyList<LotLine>> GetLotLinesByMaterialLotAsync(IReadOnlyCollection<string> lots, bool second = false)
        => lots.Count == 0 ? [] : await LotLines(db.ProductionReportLines.AsNoTracking()
            .Where(l => second ? l.MaterialLot2 != null && lots.Contains(l.MaterialLot2) : l.MaterialLot1 != null && lots.Contains(l.MaterialLot1))).ToListAsync();

    private static ProductionReportDto ToDto(ProductionReport r) => new(
        r.Id, r.DepartmentCode, r.ReportDate, r.ReportNo, r.MachineId,
        r.Lines.OrderBy(l => l.LineNo).Select(l => new ProductionReportLineDto(l.Id, l.LineNo, l.ProductId, l.ProductName, l.Unit, l.ShiftId, l.Hours,
            l.MaterialLot1, l.MaterialLot2, l.GoodQuantity, l.RejectQuantity, l.TotalQuantity, l.EmployeeId, l.MoldId, l.Mold?.Code, l.Lot, l.RowVersion)).ToList(),
        r.RowVersion, r.StockedAt);

    /// <summary>
    /// Reports found for แก้ไขผลผลิต (SCR-11): by <paramref name="reportNo"/> (the whole number or the start of it) or by <paramref name="date"/>, newest first; both given = both must match.
    /// </summary>
    public async Task<IReadOnlyList<ProductionReportSummary>> SearchAsync(string? reportNo, DateOnly? date, int max = 200)
    {
        var query = db.ProductionReports.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(reportNo)) { var no = reportNo.Trim().ToUpperInvariant(); query = query.Where(r => r.ReportNo.StartsWith(no)); }
        if (date is { } d) query = query.Where(r => r.ReportDate == d);
        var rows = await query.OrderByDescending(r => r.ReportDate).ThenBy(r => r.ReportNo).Take(max)
            .Select(r => new { r.Id, r.DepartmentCode, r.ReportDate, r.ReportNo, Lines = r.Lines.Count, r.StockedAt }).ToListAsync();
        return rows.Select(r => new ProductionReportSummary(r.Id, r.DepartmentCode, ProductionDepartments.Find(r.DepartmentCode)?.Name ?? r.DepartmentCode, r.ReportDate, r.ReportNo, r.Lines, r.StockedAt)).ToList();
    }
}
