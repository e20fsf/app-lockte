using Lockte.Org;
using Lockte.Purch.Data;
using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Purch;

/// <summary>A แผนก of module องค์กร with the prefix of its purchase request numbers (null = none yet) and its head (องค์กร's).</summary>
public sealed record DepartmentPrefixDto(int DepartmentId, string DepartmentName, string? Prefix, int? HeadEmployeeId, byte[]? RowVersion);

public sealed record PurchasingLocationDto(string Code, string Name);

/// <summary>
/// ข้อมูลที่ใบขอซื้อต้องเลือก (#216, SCR-10): prefix เลขที่ใบขอซื้อต่อแผนก, ผู้จัดซื้อ, เหตุผลในการขอซื้อ, หมายเหตุ. There is no
/// register of departments in purch (D-04, ADR 0002): the แผนก belong to module องค์กร.
/// </summary>
public sealed class RequestSetupRegistry(PurchDbContext db, OrgDirectory org)
{
    public const int PrefixLength = 10;
    public const int NameLength = 100;
    public const int TextLength = 255;

    /// <summary>
    /// ตั้ง prefix เลขที่ใบขอซื้อของแผนก (blank removes it). A prefix is upper case and belongs to one department. Numbers already issued
    /// keep their text: they are stored on the request, not derived from the prefix.
    /// </summary>
    public async Task<ServiceResult> SetPrefixAsync(int departmentId, string? prefix, byte[]? loadedRowVersion)
    {
        if (await org.GetDepartmentAsync(departmentId) is null) return ServiceResult.Fail("ไม่พบแผนกที่เลือก");
        var value = Clean(prefix)?.ToUpperInvariant();
        if (value?.Length > PrefixLength) return ServiceResult.Fail($"prefix ยาวได้ไม่เกิน {PrefixLength} ตัว");
        if (value is not null && value.Contains('*')) return ServiceResult.Fail("prefix เป็นตัวอักษรของเลขที่ ไม่ใช่รูปแบบค้น (ไม่ต้องใส่ *)");
        if (value is not null && await db.DepartmentPrefixes.AnyAsync(p => p.Prefix == value && p.DepartmentId != departmentId))
            return ServiceResult.Fail($"prefix '{value}' เป็นของแผนกอื่นแล้ว");

        var row = await db.DepartmentPrefixes.FirstOrDefaultAsync(p => p.DepartmentId == departmentId);
        if (row is not null) row.ExpectRowVersion(loadedRowVersion ?? throw new ArgumentException("Editing needs the rowversion the screen loaded.", nameof(loadedRowVersion)));
        if (value is null)
        {
            // The แผนก's requests were numbered with it: it can be changed to another prefix, not removed (#216).
            if (row is not null && await db.PurchaseRequests.AnyAsync(r => r.DepartmentId == departmentId))
                return ServiceResult.Fail("แผนกนี้มีใบขอซื้อแล้ว ลบ prefix ไม่ได้ (เปลี่ยนเป็น prefix อื่นได้)");
            if (row is not null) db.DepartmentPrefixes.Remove(row);
        }
        else
        {
            row ??= db.DepartmentPrefixes.Add(new DepartmentPrefix { DepartmentId = departmentId }).Entity;
            row.Prefix = value;
        }
        await db.SaveChangesAsync();
        return ServiceResult.Ok(row?.Id);
    }

    /// <summary>แก้ผู้จัดซื้อ: the list is replaced by the one given (codes upper case, no repeats; a blank name is refused).</summary>
    public async Task<ServiceResult> SaveLocationsAsync(IReadOnlyList<PurchasingLocationDto> locations)
    {
        var wanted = new List<PurchasingLocationDto>();
        foreach (var l in locations)
        {
            var code = Clean(l.Code)?.ToUpperInvariant();
            var name = Clean(l.Name);
            if (code is null && name is null) continue;
            if (code is null || name is null) return ServiceResult.Fail("ผู้จัดซื้อต้องกรอกทั้งรหัสและชื่อ");
            if (code.Length > PrefixLength || name.Length > NameLength) return ServiceResult.Fail("ผู้จัดซื้อยาวเกินกำหนด");
            if (wanted.Any(w => w.Code == code)) return ServiceResult.Fail($"รหัสผู้จัดซื้อ '{code}' ซ้ำ");
            wanted.Add(new PurchasingLocationDto(code, name));
        }
        var old = await db.PurchasingLocations.ToListAsync();
        db.PurchasingLocations.RemoveRange(old.Where(o => wanted.All(w => w.Code != o.Code)));
        foreach (var w in wanted)
            (old.FirstOrDefault(o => o.Code == w.Code) ?? db.PurchasingLocations.Add(new PurchasingLocation { Code = w.Code }).Entity).Name = w.Name;
        await db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    /// <summary>แก้รายการเหตุผลในการขอซื้อ: replaced by the list given (no blanks or repeats).</summary>
    public Task<ServiceResult> SaveReasonsAsync(IReadOnlyList<string> texts)
        => SaveTextsAsync(db.RequestReasons, texts, t => new RequestReason { Text = t }, r => r.Text);

    /// <summary>แก้รายการหมายเหตุ: replaced by the list given (no blanks or repeats).</summary>
    public Task<ServiceResult> SaveNotesAsync(IReadOnlyList<string> texts)
        => SaveTextsAsync(db.RequestNotes, texts, t => new RequestNote { Text = t }, r => r.Text);

    private async Task<ServiceResult> SaveTextsAsync<T>(DbSet<T> set, IReadOnlyList<string> texts, Func<string, T> create, Func<T, string> text) where T : class
    {
        var wanted = texts.Select(Clean).OfType<string>().Distinct().ToList();
        if (wanted.Any(t => t.Length > TextLength)) return ServiceResult.Fail($"ข้อความยาวได้ไม่เกิน {TextLength} ตัว");
        var old = await set.ToListAsync();
        set.RemoveRange(old.Where(o => !wanted.Contains(text(o))));
        foreach (var t in wanted.Where(t => old.All(o => text(o) != t))) set.Add(create(t));
        await db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}

/// <summary>The public, read-only face of the request set-up (ADR 0002). Writes go through <see cref="RequestSetupRegistry"/>.</summary>
public sealed class RequestSetupDirectory(PurchDbContext db, OrgDirectory org)
{
    /// <summary>T-PURC as the legacy had it.</summary>
    public static IReadOnlyList<PurchasingLocationDto> DefaultLocations { get; } = [new("F", "โรงงาน"), new("O", "สำนักงานสีลม")];

    /// <summary>Every แผนก of องค์กร, in its order, with its prefix and its head — what the department choice of SCR-03 lists.</summary>
    public async Task<IReadOnlyList<DepartmentPrefixDto>> GetDepartmentsAsync()
    {
        var prefixes = await db.DepartmentPrefixes.AsNoTracking().ToDictionaryAsync(p => p.DepartmentId);
        return (await org.GetDepartmentsAsync()).OrderBy(d => d.SortOrder)
            .Select(d => prefixes.TryGetValue(d.Id, out var p)
                ? new DepartmentPrefixDto(d.Id, d.Name, p.Prefix, d.HeadEmployeeId, p.RowVersion)
                : new DepartmentPrefixDto(d.Id, d.Name, null, d.HeadEmployeeId, null)).ToList();
    }

    public async Task<string?> GetPrefixAsync(int departmentId)
        => await db.DepartmentPrefixes.AsNoTracking().Where(p => p.DepartmentId == departmentId).Select(p => p.Prefix).FirstOrDefaultAsync();

    /// <summary>ผู้จัดซื้อ; the legacy two until the list is edited.</summary>
    public async Task<IReadOnlyList<PurchasingLocationDto>> GetLocationsAsync()
    {
        var saved = await db.PurchasingLocations.AsNoTracking().OrderBy(l => l.Code).Select(l => new PurchasingLocationDto(l.Code, l.Name)).ToListAsync();
        return saved.Count == 0 ? DefaultLocations : saved;
    }

    public async Task<IReadOnlyList<string>> GetReasonsAsync()
        => await db.RequestReasons.AsNoTracking().OrderBy(r => r.Text).Select(r => r.Text).ToListAsync();

    public async Task<IReadOnlyList<string>> GetNotesAsync()
        => await db.RequestNotes.AsNoTracking().OrderBy(r => r.Text).Select(r => r.Text).ToListAsync();
}
