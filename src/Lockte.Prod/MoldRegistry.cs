using Lockte.Org;
using Lockte.Prod.Data;
using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Prod;

public sealed record MoldDto(
    int Id,
    string Code,
    string Name,
    int? PiecesPerShot,
    int? MaintenanceShots,
    int? MaintenancePieces,
    int? MaintenanceYears,
    int? DepartmentId,
    string? Maker,
    DateOnly? StartDate,
    DateOnly? EndDate,
    decimal Cost,
    string? Owner,
    decimal? AluminumWeight,
    byte[] RowVersion)
{
    /// <summary>วันที่เลิกใช้ ว่าง = ยังใช้อยู่ (รหัสพิมพ์ที่ยังใช้อยู่).</summary>
    public bool InUse => EndDate is null;
}

/// <summary>
/// The fields of SCR-17. <see cref="Id"/> null = new mold; editing needs <see cref="RowVersion"/> as loaded.
/// เกณฑ์บำรุงรักษา (<see cref="MaintenanceShots"/>, <see cref="MaintenancePieces"/>, <see cref="MaintenanceYears"/>):
/// the form says to fill only one, shown as a hint — not enforced, as before.
/// </summary>
public sealed record MoldInput(
    int? Id,
    string Code,
    string Name,
    byte[]? RowVersion,
    int? PiecesPerShot = null,
    int? MaintenanceShots = null,
    int? MaintenancePieces = null,
    int? MaintenanceYears = null,
    int? DepartmentId = null,
    string? Maker = null,
    DateOnly? StartDate = null,
    DateOnly? EndDate = null,
    decimal Cost = 0,
    string? Owner = null,
    decimal? AluminumWeight = null);

/// <summary>
/// เพิ่ม/แก้/ลบ ทะเบียนแม่พิมพ์ (SCR-17). แผนก is read from module องค์กร through <see cref="OrgDirectory"/> (ADR 0002).
/// A newer save by someone else throws <see cref="ConcurrencyConflictException"/>.
/// </summary>
public sealed class MoldRegistry(ProdDbContext db, OrgDirectory org)
{
    /// <summary>T-รหัสพิมพ์ ID is Text(50).</summary>
    public const int CodeLength = 50;
    /// <summary>T-รหัสพิมพ์ NAME is Text(100).</summary>
    public const int NameLength = 100;
    /// <summary>MARKER and OWNER are Text(100).</summary>
    public const int PersonLength = 100;

    /// <summary>Default of MARKER and OWNER in the legacy table (the company).</summary>
    public const string DefaultPerson = "บจก.ล็อคเต้";

    public async Task<ServiceResult> SaveAsync(MoldInput input)
    {
        var code = Code(input.Code);
        var name = Clean(input.Name);
        var maker = Clean(input.Maker);
        var owner = Clean(input.Owner);
        var errors = new List<string>();
        if (code is null) errors.Add("กรุณากรอกรหัสแม่พิมพ์");
        else if (code.Length > CodeLength) errors.Add($"รหัสแม่พิมพ์ยาวได้ไม่เกิน {CodeLength} ตัว");
        else if (await db.Molds.AnyAsync(m => m.Code == code && m.Id != input.Id)) errors.Add($"มีรหัสแม่พิมพ์ '{code}' อยู่แล้ว");
        if (name is null) errors.Add("กรุณากรอกชื่อแม่พิมพ์");
        else if (name.Length > NameLength) errors.Add($"ชื่อแม่พิมพ์ยาวได้ไม่เกิน {NameLength} ตัว");
        if (maker?.Length > PersonLength) errors.Add($"ผู้ทำแม่พิมพ์ยาวได้ไม่เกิน {PersonLength} ตัว");
        if (owner?.Length > PersonLength) errors.Add($"เจ้าของแม่พิมพ์ยาวได้ไม่เกิน {PersonLength} ตัว");
        if (input.PiecesPerShot < 0 || input.MaintenanceShots < 0 || input.MaintenancePieces < 0 || input.MaintenanceYears < 0)
            errors.Add("ชิ้น/พิมพ์และเกณฑ์บำรุงรักษาต้องไม่ติดลบ");
        if (input.Cost < 0) errors.Add("ราคาต้องไม่ติดลบ");
        if (input.AluminumWeight < 0) errors.Add("น้ำหนักอลูมิเนียมต้องไม่ติดลบ");
        if (input.StartDate is { } start && input.EndDate is { } end && end < start) errors.Add("วันที่เลิกใช้ต้องไม่ก่อนวันที่เริ่มใช้");
        if (input.DepartmentId is { } departmentId && await org.GetDepartmentAsync(departmentId) is null)
            errors.Add("ไม่พบแผนกที่เลือก");
        if (errors.Count > 0) return new ServiceResult(false, errors);

        var mold = await LoadAsync(input.Id, input.RowVersion);
        if (input.Id is not null && mold is null) return ServiceResult.Fail("ไม่พบแม่พิมพ์นี้");
        mold ??= db.Molds.Add(new Mold()).Entity;
        mold.Code = code!;
        mold.Name = name!;
        mold.PiecesPerShot = input.PiecesPerShot;
        mold.MaintenanceShots = input.MaintenanceShots;
        mold.MaintenancePieces = input.MaintenancePieces;
        mold.MaintenanceYears = input.MaintenanceYears;
        mold.DepartmentId = input.DepartmentId;
        mold.Maker = maker;
        mold.StartDate = input.StartDate;
        mold.EndDate = input.EndDate;
        mold.Cost = input.Cost;
        mold.Owner = owner;
        mold.AluminumWeight = input.AluminumWeight;
        await db.SaveChangesAsync();
        return ServiceResult.Ok(mold.Id);
    }

    /// <summary>
    /// ลบแม่พิมพ์ (ปุ่มลบพร้อมหน้ายืนยันบนหน้าจอ — instead of clearing NAME, BR-021). A mold that other tables refer to
    /// (ใบรายงานผลผลิต, การใช้งานแม่พิมพ์) is refused (ข้อสมมติ ง). Deleting a row that is already gone succeeds.
    /// </summary>
    public async Task<ServiceResult> DeleteAsync(int id, byte[] loadedRowVersion)
    {
        if (await LoadAsync(id, loadedRowVersion) is not { } mold) return ServiceResult.Ok();
        db.Molds.Remove(mold);
        return await db.TrySaveDeleteAsync()
            ? ServiceResult.Ok()
            : ServiceResult.Fail($"ลบแม่พิมพ์ '{mold.Code}' ไม่ได้ เพราะมีรายการอื่นอ้างถึงอยู่");
    }

    private async Task<Mold?> LoadAsync(int? id, byte[]? loadedRowVersion)
    {
        if (id is not { } moldId) return null;
        var mold = await db.Molds.FirstOrDefaultAsync(m => m.Id == moldId);
        mold?.ExpectRowVersion(loadedRowVersion
            ?? throw new ArgumentException("Editing needs the rowversion the screen loaded.", nameof(loadedRowVersion)));
        return mold;
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    /// <summary>Codes are compared and stored upper-case.</summary>
    internal static string? Code(string? text) => Clean(text)?.ToUpperInvariant();
}

/// <summary>A row of ทะเบียนแม่พิมพ์ as RPT-21 prints it: the แผนก by name; <see cref="MaintenanceShots"/> is the SHOT criterion.</summary>
public sealed record MoldRegisterLine(
    string Code,
    string Name,
    int? PiecesPerShot,
    string? Maker,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string? Owner,
    string? DepartmentName,
    int? MaintenanceShots);

/// <summary>The public, read-only face of ทะเบียนแม่พิมพ์ (ADR 0002). Writes go through <see cref="MoldRegistry"/>.</summary>
public sealed class MoldDirectory(ProdDbContext db, OrgDirectory org)
{
    /// <summary>
    /// The lines of RPT-21 ทะเบียนแม่พิมพ์ by รหัส — every mold, the ones no longer used too — or, with
    /// <paramref name="inUseOnly"/>, รายชื่อแม่พิมพ์ที่ยังใช้งานอยู่ (วันที่เลิกใช้ ว่าง).
    /// </summary>
    public async Task<IReadOnlyList<MoldRegisterLine>> GetRegisterAsync(bool inUseOnly = false)
    {
        var departments = (await org.GetDepartmentsAsync()).ToDictionary(d => d.Id, d => d.Name);
        return (await GetAllAsync(inUseOnly)).Select(m => new MoldRegisterLine(m.Code, m.Name, m.PiecesPerShot, m.Maker, m.StartDate, m.EndDate,
            m.Owner, m.DepartmentId is { } id ? departments.GetValueOrDefault(id) : null, m.MaintenanceShots)).ToList();
    }

    /// <summary>By รหัส; <paramref name="inUseOnly"/> = รหัสพิมพ์ที่ยังใช้อยู่ (วันที่เลิกใช้ ว่าง).</summary>
    public async Task<IReadOnlyList<MoldDto>> GetAllAsync(bool inUseOnly = false)
        => (await db.Molds.AsNoTracking().Where(m => !inUseOnly || m.EndDate == null).OrderBy(m => m.Code).ToListAsync())
            .Select(ToDto).ToList();

    public async Task<MoldDto?> GetAsync(int id)
        => await db.Molds.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id) is { } m ? ToDto(m) : null;

    public async Task<MoldDto?> FindByCodeAsync(string code)
    {
        var normalized = MoldRegistry.Code(code);
        return await db.Molds.AsNoTracking().FirstOrDefaultAsync(m => m.Code == normalized) is { } m ? ToDto(m) : null;
    }

    private static MoldDto ToDto(Mold m) => new(
        m.Id, m.Code, m.Name, m.PiecesPerShot, m.MaintenanceShots, m.MaintenancePieces, m.MaintenanceYears, m.DepartmentId,
        m.Maker, m.StartDate, m.EndDate, m.Cost, m.Owner, m.AluminumWeight, m.RowVersion);
}
