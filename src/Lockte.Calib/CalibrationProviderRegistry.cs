using Lockte.Calib.Data;
using Lockte.Purch;
using Lockte.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Calib;

/// <summary>A calibration company as the screens and the instrument register read it: the supplier of purch it is.</summary>
public sealed record CalibrationProviderDto(int SupplierId, string SupplierCode, string Name);

/// <summary>The public, read-only face of บริษัทที่สอบเทียบ (ADR 0002). Names come from the supplier register of purch.</summary>
public sealed class CalibrationProviderDirectory(CalibDbContext db, SupplierDirectory suppliers)
{
    /// <summary>All of them, by name.</summary>
    public async Task<IReadOnlyList<CalibrationProviderDto>> ListAsync()
    {
        var ids = await db.CalibrationProviders.AsNoTracking().Select(p => p.SupplierId).ToListAsync();
        var result = new List<CalibrationProviderDto>();
        foreach (var id in ids)
            if (await suppliers.GetAsync(id) is { } s) result.Add(new CalibrationProviderDto(s.Id, s.Code, s.Name));
        return result.OrderBy(p => p.Name, StringComparer.CurrentCulture).ToList();
    }

    public async Task<CalibrationProviderDto?> GetAsync(int supplierId)
        => await db.CalibrationProviders.AsNoTracking().AnyAsync(p => p.SupplierId == supplierId) && await suppliers.GetAsync(supplierId) is { } s
            ? new CalibrationProviderDto(s.Id, s.Code, s.Name)
            : null;
}

/// <summary>
/// เพิ่มรายชื่อบริษัทที่สอบเทียบ (SCR-06, BR-007). A name that is already a supplier is marked as it is; otherwise a supplier is added to the
/// register of purch through its public service (code <c>CAL-nnn</c>). Unlike the legacy a blank name is refused and a repeated one too (Q-10).
/// </summary>
public sealed class CalibrationProviderRegistry(CalibDbContext db, SupplierDirectory suppliers, SupplierRegistry supplierRegistry)
{
    public const string CodePrefix = "CAL-";

    public async Task<ServiceResult> AddAsync(string? name)
    {
        var clean = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        if (clean is null) return ServiceResult.Fail("กรุณากรอกชื่อบริษัทที่สอบเทียบ");
        if (clean.Length > SupplierRegistry.TextLength) return ServiceResult.Fail($"ชื่อบริษัทยาวได้ไม่เกิน {SupplierRegistry.TextLength} ตัว");

        var supplier = (await suppliers.SearchAsync(clean, 50)).FirstOrDefault(s => string.Equals(s.Name.Trim(), clean, StringComparison.OrdinalIgnoreCase));
        if (supplier is not null && await db.CalibrationProviders.AnyAsync(p => p.SupplierId == supplier.Id))
            return ServiceResult.Fail($"มีบริษัทที่สอบเทียบ '{clean}' อยู่แล้ว");

        int supplierId;
        if (supplier is not null) supplierId = supplier.Id;
        else
        {
            var next = (await suppliers.GetAllAsync()).Select(s => s.Code)
                .Where(c => c.StartsWith(CodePrefix, StringComparison.OrdinalIgnoreCase) && int.TryParse(c[CodePrefix.Length..], out _))
                .Select(c => int.Parse(c[CodePrefix.Length..], System.Globalization.CultureInfo.InvariantCulture)).DefaultIfEmpty(0).Max() + 1;
            var added = await supplierRegistry.SaveAsync(new SupplierInput(null, $"{CodePrefix}{next.ToString("000", System.Globalization.CultureInfo.InvariantCulture)}", clean, null));
            if (!added.Succeeded) return added;
            supplierId = added.Id!.Value;
        }
        var provider = db.CalibrationProviders.Add(new CalibrationProvider { SupplierId = supplierId, Name = clean }).Entity;
        await db.SaveChangesAsync();
        return ServiceResult.Ok(provider.SupplierId);
    }
}
