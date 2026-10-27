using System.Text.RegularExpressions;
using Lockte.Prod.Data;
using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Prod;

/// <summary>
/// กลุ่มใบตรวจ QA ของแต่ละแผนก (SCR-12): a spec belongs to a group by its name — the text before the first model code (<c>ไดคาสท์029</c> → <c>ไดคาสท์</c>), as the reports were named — and a
/// department prints the sheets of its groups. Configuration, not code paths: a group added later (#131–#133) only adds to this list.
/// </summary>
public static class QaSpecGroups
{
    /// <summary>The groups of sheets of each department letter (see <see cref="ProductionDepartments"/>).</summary>
    public static IReadOnlyDictionary<string, string[]> ByDepartment { get; } = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        ["D"] = ["ไดคาสท์"],
        ["T"] = ["ตกแต่ง"],
        ["B"] = ["อัดผ้าเบรค", "อัดดิสเบรค"],
        ["G"] = ["ฝนผ้าเบรค", "ดิสเบรคหลังผ่าร่อง"],
        ["K"] = ["คลัทช์หลังอัดขึ้นรูป", "คลัทช์สำเร็จรูป", "แผ่นผ้าคลัทช์"],
        ["A"] = ["ประกอบ"],
    };

    /// <summary>The sheets of a new mold (ฟอร์ม <c>ตรวจสอบพิมพ์</c>): printed without a production line.</summary>
    public static IReadOnlyList<string> NewMold { get; } = ["พิมพ์"];

    /// <summary>The sheets of the packing department are named by what is packed ("คลัทช์บรรจุกล่อง", "ดรัมสกินแพ็ค", and the same with "ฟอร์มเปล่า"): no model code, so the whole name is the group.</summary>
    public static bool IsPacking(string group) => group.Contains("บรรจุ", StringComparison.Ordinal) || group.Contains("สกินแพ็ค", StringComparison.Ordinal);

    public static string GroupOf(string specName)
    {
        var match = Regex.Match(specName, @"^(.*?)(?=[A-Za-z0-9#]|$)");
        return match.Groups[1].Value.Trim();
    }
}

/// <summary>A production line of a department on a day as SCR-12 shows it.</summary>
public sealed record QaLineRow(
    int LineId, int ReportId, string ReportNo, int LineNo, DateOnly? Date, string? ProductCode, string ProductName, string? Unit, decimal Good, decimal? Reject, decimal Total,
    string? Lot, string? MaterialLot1, string? MaterialLot2, decimal? Hours, bool Selected, byte[] RowVersion);

/// <summary>What the QA changed on a line: the numbers (the total is worked out again), the lots and the mark.</summary>
public sealed record QaLineEdit(int LineId, byte[] RowVersion, decimal Good, decimal? Reject, decimal? Hours, string? Lot, string? MaterialLot1, string? MaterialLot2, bool Selected);

/// <summary>
/// เลือกบรรทัดสุ่มตรวจ (SCR-12, BR-017, #130): the QA picks a day, sees the department's production lines of the day, can correct them, and marks (<c>MARK1 = Y</c>) the ones to be inspected.
/// The sheet of a model is printed for every marked line — the button of a model does not filter by model (⚠ Q-58 kept), and the user clears the marks by hand.
/// </summary>
public sealed class QaSelectionRegistry(ProdDbContext db)
{
    public const int TextLength = ProductionReportRegistry.TextLength;

    public async Task<ServiceResult> SaveAsync(IReadOnlyList<QaLineEdit> edits)
    {
        var errors = new List<string>();
        foreach (var edit in edits)
        {
            if (edit.Good < 0) errors.Add("จำนวนดีต้องไม่ติดลบ");
            if (edit.Reject < 0) errors.Add("จำนวนเสียต้องไม่ติดลบ");
            if (edit.Hours < 0) errors.Add("ชั่วโมงต้องไม่ติดลบ");
            if (edit.Lot?.Trim().Length > TextLength || edit.MaterialLot1?.Trim().Length > TextLength || edit.MaterialLot2?.Trim().Length > TextLength) errors.Add($"Lot ยาวได้ไม่เกิน {TextLength} ตัว");
        }
        if (errors.Count > 0) return new ServiceResult(false, errors.Distinct().ToList());

        var ids = edits.Select(e => e.LineId).ToList();
        var lines = await db.ProductionReportLines.Where(l => ids.Contains(l.Id)).ToDictionaryAsync(l => l.Id);
        foreach (var edit in edits)
        {
            if (!lines.TryGetValue(edit.LineId, out var line)) return ServiceResult.Fail("ไม่พบบรรทัดผลผลิตที่แก้ (อาจถูกลบแล้ว)");
            line.ExpectRowVersion(edit.RowVersion);
            line.GoodQuantity = Math.Round(edit.Good, 4);
            line.RejectQuantity = edit.Reject is { } reject ? Math.Round(reject, 4) : null;
            line.TotalQuantity = line.GoodQuantity + (line.RejectQuantity ?? 0); // AC-21: the total is worked out on every save
            line.Hours = edit.Hours;
            line.Lot = Clean(edit.Lot);
            line.MaterialLot1 = Clean(edit.MaterialLot1);
            line.MaterialLot2 = Clean(edit.MaterialLot2);
            line.QaSelected = edit.Selected;
        }
        await db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    /// <summary>Clears the marks of a department on a day — what the user did line by line.</summary>
    public async Task<int> ClearMarksAsync(string departmentCode, DateOnly date)
    {
        var code = departmentCode.Trim().ToUpperInvariant();
        var lines = await db.ProductionReportLines.Where(l => l.QaSelected && l.ProductionReport.DepartmentCode == code && l.ProductionReport.ReportDate == date).ToListAsync();
        foreach (var line in lines) line.QaSelected = false;
        await db.SaveChangesAsync();
        return lines.Count;
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}

/// <summary>Reads for SCR-12 and for the sheets.</summary>
public sealed class QaSelectionDirectory(ProdDbContext db)
{
    /// <summary>The lines of a department on a day (the date of the report), by report number and line.</summary>
    public async Task<IReadOnlyList<QaLineRow>> GetLinesAsync(string departmentCode, DateOnly date, bool onlySelected = false)
    {
        var code = departmentCode.Trim().ToUpperInvariant();
        var rows = await db.ProductionReportLines.AsNoTracking()
            .Where(l => l.ProductionReport.DepartmentCode == code && l.ProductionReport.ReportDate == date && (!onlySelected || l.QaSelected))
            .Select(l => new
            {
                l.Id, ReportId = l.ProductionReportId, l.ProductionReport.ReportNo, l.LineNo, l.ProductionReport.ReportDate, ProductCode = l.Product == null ? null : l.Product.Code,
                l.ProductName, l.Unit, l.GoodQuantity, l.RejectQuantity, l.TotalQuantity, l.Lot, l.MaterialLot1, l.MaterialLot2, l.Hours, l.QaSelected, l.RowVersion,
            }).ToListAsync();
        return rows.OrderBy(r => r.ReportNo, StringComparer.Ordinal).ThenBy(r => r.LineNo)
            .Select(r => new QaLineRow(r.Id, r.ReportId, r.ReportNo, r.LineNo, r.ReportDate, r.ProductCode, r.ProductName, r.Unit, r.GoodQuantity, r.RejectQuantity, r.TotalQuantity,
                r.Lot, r.MaterialLot1, r.MaterialLot2, r.Hours, r.QaSelected, r.RowVersion)).ToList();
    }

    /// <summary>The specs a department prints: those of its groups (see <see cref="QaSpecGroups"/>), by name.</summary>
    public async Task<IReadOnlyList<QaSpecSummary>> GetSpecsAsync(string departmentCode)
        => departmentCode.Trim().Equals("P", StringComparison.OrdinalIgnoreCase) ? await SpecsWhereAsync(QaSpecGroups.IsPacking)
            : QaSpecGroups.ByDepartment.TryGetValue(departmentCode.Trim(), out var groups) ? await SpecsWhereAsync(groups.Contains) : [];

    /// <summary>The specs of a new mold's inspection (ตรวจสอบพิมพ์), by name.</summary>
    public Task<IReadOnlyList<QaSpecSummary>> GetMoldSpecsAsync() => SpecsWhereAsync(QaSpecGroups.NewMold.Contains);

    private async Task<IReadOnlyList<QaSpecSummary>> SpecsWhereAsync(Func<string, bool> groupFits)
    {
        var all = await db.QaModelSpecs.AsNoTracking().Select(s => new { s.Id, s.DocumentTypeId, TypeCode = s.DocumentType.Code, s.Name, s.Models, s.DocumentSequence, Points = s.Points.Count, Defects = s.Defects.Count }).ToListAsync();
        return all.Where(s => groupFits(QaSpecGroups.GroupOf(s.Name))).OrderBy(s => s.Name, StringComparer.Ordinal)
            .Select(s => new QaSpecSummary(s.Id, s.DocumentTypeId, s.TypeCode, s.Name, s.Models, s.DocumentSequence, s.Points, s.Defects)).ToList();
    }
}
