using Lockte.SharedKernel.Persistence;

namespace Lockte.Hr.Data;

/// <summary>
/// ผู้ลงนามบนแบบฟอร์มเอกสารบุคคล (UAT-29): one row with the names printed under the signature lines of แบบ คร.2, ประกาศการคำนวณ
/// พักร้อน and ใบลาออก, and (UAT-38) the two addresses with their phones that แบบ คร.2 prints. They used to be set on the server (appsettings); HR now changes them on the settings page and every
/// change is in the history. A blank value falls back to the server's setting, so nothing printed before stops printing.
/// </summary>
internal sealed class FormSigners : IAudited
{
    public int Id { get; set; }
    /// <summary>คำนำหน้า of the ผู้จัดการสถานประกอบกิจการ who signs แบบ คร.2.</summary>
    public string? ManagerTitle { get; set; }
    public string? ManagerFirstName { get; set; }
    public string? ManagerLastName { get; set; }
    /// <summary>ผู้จัดการโรงงาน who signs ประกาศการคำนวณพักร้อน.</summary>
    public string? FactoryManagerName { get; set; }
    /// <summary>เจ้าหน้าที่แผนกบุคคลและธุรการ under ใบลาออก.</summary>
    public string? PersonnelOfficerName { get; set; }

    // UAT-38: ที่ตั้งสถานประกอบกิจการ (สำนักงาน) and สถานที่ทำงาน (โรงงาน) of แบบ คร.2 — an address with nothing saved keeps the server's.
    public string? OfficeHouseNo { get; set; }
    public string? OfficeMoo { get; set; }
    public string? OfficeSoi { get; set; }
    public string? OfficeRoad { get; set; }
    public string? OfficeSubdistrict { get; set; }
    public string? OfficeDistrict { get; set; }
    public string? OfficeProvince { get; set; }
    public string? OfficePhone { get; set; }
    public string? WorkplaceHouseNo { get; set; }
    public string? WorkplaceMoo { get; set; }
    public string? WorkplaceSoi { get; set; }
    public string? WorkplaceRoad { get; set; }
    public string? WorkplaceSubdistrict { get; set; }
    public string? WorkplaceDistrict { get; set; }
    public string? WorkplaceProvince { get; set; }
    public string? WorkplacePhone { get; set; }

    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    string IHasBusinessKey.BusinessKey => "ผู้ลงนามบนแบบฟอร์ม";
}
