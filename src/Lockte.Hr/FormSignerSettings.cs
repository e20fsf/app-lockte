using Lockte.Hr.Data;
using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Hr;

/// <summary>An address as แบบ คร.2 prints it (UAT-38): เลขที่, หมู่ที่, ตรอก/ซอย, ถนน, แขวง/ตำบล, เขต/อำเภอ, จังหวัด, โทรศัพท์.</summary>
public sealed record FormAddress(string? HouseNo, string? Moo, string? Soi, string? Road, string? Subdistrict, string? District, string? Province,
    string? Phone)
{
    public static readonly FormAddress Blank = new(null, null, null, null, null, null, null, null);

    /// <summary>Nothing saved in any field: the form prints the server's address instead.</summary>
    public bool IsBlank => new[] { HouseNo, Moo, Soi, Road, Subdistrict, District, Province, Phone }.All(string.IsNullOrWhiteSpace);
}

/// <summary>The saved names and addresses; a never-saved setting is all blank and <see cref="RowVersion"/> null.</summary>
public sealed record FormSignersDto(string? ManagerTitle, string? ManagerFirstName, string? ManagerLastName, string? FactoryManagerName,
    string? PersonnelOfficerName, byte[]? RowVersion)
{
    /// <summary>ที่ตั้งสถานประกอบกิจการ (สำนักงาน) of แบบ คร.2.</summary>
    public FormAddress Office { get; init; } = FormAddress.Blank;

    /// <summary>สถานที่ทำงาน (โรงงาน) of แบบ คร.2.</summary>
    public FormAddress Workplace { get; init; } = FormAddress.Blank;
}

/// <summary>What HR saves; <see cref="RowVersion"/> is the one the page loaded (null while nothing has been saved).</summary>
public sealed record FormSignersInput(string? ManagerTitle, string? ManagerFirstName, string? ManagerLastName, string? FactoryManagerName,
    string? PersonnelOfficerName, byte[]? RowVersion)
{
    public FormAddress Office { get; init; } = FormAddress.Blank;
    public FormAddress Workplace { get; init; } = FormAddress.Blank;
}

/// <summary>
/// ผู้ลงนามบนแบบฟอร์ม (UAT-29) and the addresses of แบบ คร.2 (UAT-38): read for printing and saved from the settings page. A newer save
/// by someone else throws <see cref="ConcurrencyConflictException"/>.
/// </summary>
public sealed class FormSignerSettings(HrDbContext db)
{
    public const int NameLength = 100;
    public const int TitleLength = 30;
    public const int AddressLength = 100;

    public async Task<FormSignersDto> GetAsync()
    {
        var row = await db.FormSigners.AsNoTracking().FirstOrDefaultAsync();
        return row is null
            ? new FormSignersDto(null, null, null, null, null, null)
            : new FormSignersDto(row.ManagerTitle, row.ManagerFirstName, row.ManagerLastName, row.FactoryManagerName, row.PersonnelOfficerName, row.RowVersion)
            {
                Office = new(row.OfficeHouseNo, row.OfficeMoo, row.OfficeSoi, row.OfficeRoad, row.OfficeSubdistrict, row.OfficeDistrict,
                    row.OfficeProvince, row.OfficePhone),
                Workplace = new(row.WorkplaceHouseNo, row.WorkplaceMoo, row.WorkplaceSoi, row.WorkplaceRoad, row.WorkplaceSubdistrict,
                    row.WorkplaceDistrict, row.WorkplaceProvince, row.WorkplacePhone),
            };
    }

    public async Task<ServiceResult> SaveAsync(FormSignersInput input)
    {
        var title = Clean(input.ManagerTitle);
        var first = Clean(input.ManagerFirstName);
        var last = Clean(input.ManagerLastName);
        var factory = Clean(input.FactoryManagerName);
        var officer = Clean(input.PersonnelOfficerName);
        var office = Clean(input.Office);
        var workplace = Clean(input.Workplace);
        var errors = new List<string>();
        if (title?.Length > TitleLength) errors.Add($"คำนำหน้ายาวได้ไม่เกิน {TitleLength} ตัว");
        foreach (var (label, value) in new[] { ("ชื่อผู้จัดการ", first), ("นามสกุลผู้จัดการ", last), ("ชื่อผู้จัดการโรงงาน", factory), ("ชื่อเจ้าหน้าที่บุคคล", officer) })
            if (value?.Length > NameLength) errors.Add($"{label}ยาวได้ไม่เกิน {NameLength} ตัว");
        foreach (var (where, address) in new[] { ("ที่ตั้งสถานประกอบกิจการ", office), ("สถานที่ทำงาน", workplace) })
            if (new[] { address.HouseNo, address.Moo, address.Soi, address.Road, address.Subdistrict, address.District, address.Province, address.Phone }
                .Any(v => v?.Length > AddressLength))
                errors.Add($"ที่อยู่{where}: แต่ละช่องยาวได้ไม่เกิน {AddressLength} ตัว");
        if (errors.Count > 0) return new ServiceResult(false, errors);

        var row = await db.FormSigners.FirstOrDefaultAsync();
        if (row is null)
        {
            if (input.RowVersion is not null) throw new ConcurrencyConflictException();
            row = db.FormSigners.Add(new FormSigners()).Entity;
        }
        else row.ExpectRowVersion(input.RowVersion ?? throw new ConcurrencyConflictException());
        row.ManagerTitle = title;
        row.ManagerFirstName = first;
        row.ManagerLastName = last;
        row.FactoryManagerName = factory;
        row.PersonnelOfficerName = officer;
        (row.OfficeHouseNo, row.OfficeMoo, row.OfficeSoi, row.OfficeRoad, row.OfficeSubdistrict, row.OfficeDistrict, row.OfficeProvince, row.OfficePhone)
            = (office.HouseNo, office.Moo, office.Soi, office.Road, office.Subdistrict, office.District, office.Province, office.Phone);
        (row.WorkplaceHouseNo, row.WorkplaceMoo, row.WorkplaceSoi, row.WorkplaceRoad, row.WorkplaceSubdistrict, row.WorkplaceDistrict,
                row.WorkplaceProvince, row.WorkplacePhone)
            = (workplace.HouseNo, workplace.Moo, workplace.Soi, workplace.Road, workplace.Subdistrict, workplace.District, workplace.Province,
                workplace.Phone);
        await db.SaveChangesAsync();
        return ServiceResult.Ok(row.Id);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static FormAddress Clean(FormAddress? a) => a is null
        ? FormAddress.Blank
        : new(Clean(a.HouseNo), Clean(a.Moo), Clean(a.Soi), Clean(a.Road), Clean(a.Subdistrict), Clean(a.District), Clean(a.Province), Clean(a.Phone));
}
