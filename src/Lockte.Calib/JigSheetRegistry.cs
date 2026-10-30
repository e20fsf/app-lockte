using Lockte.Calib.Data;
using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Calib;

public sealed record JigPointDto(int Id, string Label, JigPointKind Kind, decimal Value, JigToleranceKind Tolerance, decimal? ToleranceValue, byte[] RowVersion)
{
    /// <summary>The specification as printed: <c>20.00 ± 0.20 mm.</c> or <c>&gt;15.00</c>.</summary>
    public string SpecificationText => Tolerance == JigToleranceKind.GreaterThan
        ? $">{Value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}"
        : $"{Value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)} ± {(ToleranceValue ?? 0m).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)} mm.";
}

/// <summary>แบบตรวจ JIG as the screens and the reports (#207 #208) read it: the points in print order.</summary>
public sealed record JigSheetDto(int Id, JigSheetType Type, string ModelKey, string Title, string DocumentKey, int DocumentNo, IReadOnlyList<JigPointDto> Points, byte[] RowVersion);

/// <summary>One point on the edit screen; <see cref="Id"/> null = a new point. Existing points carry their rowversion as loaded.</summary>
public sealed record JigPointInput(int? Id, string Label, JigPointKind Kind, decimal Value, JigToleranceKind Tolerance, decimal? ToleranceValue, byte[]? RowVersion);

/// <summary>The edit of a sheet: its name on the sheet and its points, in the order given. Points of the sheet that are not listed are deleted.</summary>
public sealed record JigSheetInput(int Id, string Title, IReadOnlyList<JigPointInput> Points, byte[] RowVersion);

/// <summary>The public, read-only face of แบบตรวจ JIG (ADR 0002). Edits go through <see cref="JigSheetRegistry"/>.</summary>
public sealed class JigSheetDirectory(CalibDbContext db)
{
    /// <summary>All sheets of a type (or both) in the order of their เลขที่ในเอกสาร, วัดศูนย์ first.</summary>
    public async Task<IReadOnlyList<JigSheetDto>> ListAsync(JigSheetType? type = null)
        => (await db.JigInspectionSheets.AsNoTracking().Include(s => s.Points).Where(s => type == null || s.Type == type)
            .OrderBy(s => s.Type).ThenBy(s => s.DocumentNo).ToListAsync()).Select(ToDto).ToList();

    public async Task<JigSheetDto?> GetAsync(int id)
        => await db.JigInspectionSheets.AsNoTracking().Include(s => s.Points).FirstOrDefaultAsync(s => s.Id == id) is { } s ? ToDto(s) : null;

    /// <summary>The sheet of a model as SCR-05's button names it (not case sensitive).</summary>
    public async Task<JigSheetDto?> FindAsync(JigSheetType type, string modelKey)
    {
        var key = modelKey.Trim().ToUpper();
        return await db.JigInspectionSheets.AsNoTracking().Include(s => s.Points).FirstOrDefaultAsync(s => s.Type == type && s.ModelKey.ToUpper() == key) is { } s ? ToDto(s) : null;
    }

    private static JigSheetDto ToDto(JigInspectionSheet s) => new(s.Id, s.Type, s.ModelKey, s.Title, s.DocumentKey, s.DocumentNo,
        s.Points.OrderBy(p => p.SortOrder).Select(p => new JigPointDto(p.Id, p.Label, p.Kind, p.Value, p.Tolerance, p.ToleranceValue, p.RowVersion)).ToList(), s.RowVersion);
}

/// <summary>
/// แก้แบบตรวจ JIG (#199, Q-15): the name on the sheet and the points — add, delete, reorder, change a value — saved with ประวัติการแก้ไข. The sheet itself and
/// its เลขเอกสารควบคุม are not added or deleted here. A newer save by someone else throws <see cref="ConcurrencyConflictException"/>.
/// </summary>
public sealed class JigSheetRegistry(CalibDbContext db)
{
    public const int TitleLength = 255;
    public const int LabelLength = 20;
    public const decimal MaxValue = 9999.99m;

    public async Task<ServiceResult> SaveAsync(JigSheetInput input)
    {
        var title = string.IsNullOrWhiteSpace(input.Title) ? null : input.Title.Trim();
        var errors = new List<string>();
        if (title is null) errors.Add("กรุณากรอกชื่อ JIG บนแบบ");
        else if (title.Length > TitleLength) errors.Add($"ชื่อ JIG ยาวได้ไม่เกิน {TitleLength} ตัว");
        if (input.Points.Count == 0) errors.Add("แบบตรวจต้องมีจุดวัดอย่างน้อย 1 จุด");

        var labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in input.Points)
        {
            var label = p.Label?.Trim();
            if (string.IsNullOrEmpty(label)) errors.Add("กรุณากรอกป้ายจุด (Point)");
            else if (label.Length > LabelLength) errors.Add($"ป้ายจุด '{label}' ยาวได้ไม่เกิน {LabelLength} ตัว");
            else if (!labels.Add(label)) errors.Add($"ป้ายจุด '{label}' ซ้ำ");
            if (!Enum.IsDefined(p.Kind) || !Enum.IsDefined(p.Tolerance)) errors.Add($"จุด {label}: ชนิดหรือพิกัดไม่ถูกต้อง");
            if (p.Value < 0 || p.Value > MaxValue) errors.Add($"จุด {label}: ค่าที่กำหนดต้องอยู่ระหว่าง 0 ถึง 9999.99");
            if (p.Tolerance == JigToleranceKind.PlusMinus && (p.ToleranceValue is null || p.ToleranceValue < 0 || p.ToleranceValue > MaxValue))
                errors.Add($"จุด {label}: กรุณากรอกค่าพิกัด ± (ไม่ติดลบ)");
        }
        if (errors.Count > 0) return new ServiceResult(false, errors.Distinct().ToList());

        var sheet = await db.JigInspectionSheets.Include(s => s.Points).FirstOrDefaultAsync(s => s.Id == input.Id);
        if (sheet is null) return ServiceResult.Fail("ไม่พบแบบตรวจนี้");
        sheet.ExpectRowVersion(input.RowVersion);
        sheet.Title = title!;

        var keep = input.Points.Where(p => p.Id is not null).Select(p => p.Id!.Value).ToHashSet();
        foreach (var gone in sheet.Points.Where(p => !keep.Contains(p.Id)).ToList()) db.JigInspectionPoints.Remove(gone);
        for (var i = 0; i < input.Points.Count; i++)
        {
            var p = input.Points[i];
            JigInspectionPoint point;
            if (p.Id is { } id)
            {
                point = sheet.Points.FirstOrDefault(x => x.Id == id) ?? throw new ArgumentException("A point that is not on the sheet.", nameof(input));
                point.ExpectRowVersion(p.RowVersion ?? throw new ArgumentException("Editing needs the rowversion the screen loaded.", nameof(input)));
            }
            else
            {
                point = new JigInspectionPoint();
                sheet.Points.Add(point);
            }
            point.SortOrder = i + 1;
            point.Label = p.Label.Trim();
            point.Kind = p.Kind;
            point.Value = p.Value;
            point.Tolerance = p.Tolerance;
            point.ToleranceValue = p.Tolerance == JigToleranceKind.PlusMinus ? p.ToleranceValue : null;
        }
        await db.SaveChangesAsync();
        return ServiceResult.Ok(sheet.Id);
    }
}
