using Lockte.Hr;
using Microsoft.Extensions.Options;

namespace Lockte.Web.Reports.Hr;

/// <summary>
/// ข้อมูลนายจ้างที่แบบฟอร์มราชการพิมพ์ (section <c>Employer</c>, #64): the legacy typed them into the labels of
/// แบบ คร.2 and ทะเบียนผู้ประกันตน. They are settings, not code, so they change without a new version; the
/// repository is public, so the real address, manager and สปส. account live only in the server's settings
/// (appsettings.Production.json / user-secrets) — blank here prints a blank line, as on a form filled by hand.
/// </summary>
public sealed class EmployerOptions
{
    public const string Section = "Employer";

    /// <summary>ชื่อสถานประกอบการ / สถานประกอบกิจการ.</summary>
    public string Name { get; set; } = ReportHtml.Company;

    /// <summary>ผู้จัดการสถานประกอบกิจการ who signs แบบ คร.2: คำนำหน้า, ชื่อ, นามสกุล.</summary>
    public string ManagerTitle { get; set; } = "นาย";
    public string ManagerFirstName { get; set; } = "";
    public string ManagerLastName { get; set; } = "";

    /// <summary>ผู้จัดการโรงงาน who signs ประกาศการคำนวณพักร้อน (RPT-52), as printed in the brackets.</summary>
    public string FactoryManagerName { get; set; } = "";

    /// <summary>ชื่อเจ้าหน้าที่บุคคล printed in the brackets of ใบลาออก (UAT-25); blank prints no name.</summary>
    public string PersonnelOfficerName { get; set; } = "";

    /// <summary>ประกอบกิจการ.</summary>
    public string Business { get; set; } = "";

    /// <summary>ที่ตั้งสถานประกอบกิจการ (สำนักงาน) in แบบ คร.2.</summary>
    public EmployerAddress Office { get; set; } = new();

    /// <summary>สถานที่ทำงาน (โรงงาน) in แบบ คร.2.</summary>
    public EmployerAddress Workplace { get; set; } = new();

    /// <summary>ชื่อสาขา in ทะเบียนผู้ประกันตน.</summary>
    public string BranchName { get; set; } = ReportHtml.Company;

    /// <summary>เลขที่บัญชีนายจ้าง สปส. 10 หลัก, printed one digit per box.</summary>
    public string SocialSecurityAccountNo { get; set; } = "";

    /// <summary>ลำดับที่สาขา สปส. 6 หลัก.</summary>
    public string SocialSecurityBranchNo { get; set; } = "";

    /// <summary>
    /// A copy with the signers HR saved on the settings page (UAT-29) laid over these settings: a blank saved name keeps the server's,
    /// so a form never loses a name it printed before.
    /// </summary>
    public EmployerOptions WithSigners(FormSignersDto saved)
    {
        var copy = (EmployerOptions)MemberwiseClone();
        static string Pick(string? saved, string fallback) => string.IsNullOrWhiteSpace(saved) ? fallback : saved;
        copy.ManagerTitle = Pick(saved.ManagerTitle, ManagerTitle);
        copy.ManagerFirstName = Pick(saved.ManagerFirstName, ManagerFirstName);
        copy.ManagerLastName = Pick(saved.ManagerLastName, ManagerLastName);
        copy.FactoryManagerName = Pick(saved.FactoryManagerName, FactoryManagerName);
        copy.PersonnelOfficerName = Pick(saved.PersonnelOfficerName, PersonnelOfficerName);
        return copy;
    }
}

/// <summary>The employer's settings as the forms print them: the server's <see cref="EmployerOptions"/> with the signers HR saved in the app.</summary>
public sealed class EmployerSettings(IOptionsSnapshot<EmployerOptions> options, FormSignerSettings signers)
{
    public async Task<EmployerOptions> GetAsync() => options.Value.WithSigners(await signers.GetAsync());
}

public sealed class EmployerAddress
{
    /// <summary>เลขที่.</summary>
    public string HouseNo { get; set; } = "";
    /// <summary>หมู่ที่.</summary>
    public string Moo { get; set; } = "";
    /// <summary>ตรอก/ซอย.</summary>
    public string Soi { get; set; } = "";
    /// <summary>ถนน.</summary>
    public string Road { get; set; } = "";
    /// <summary>แขวง/ตำบล.</summary>
    public string Subdistrict { get; set; } = "";
    /// <summary>เขต/อำเภอ.</summary>
    public string District { get; set; } = "";
    public string Province { get; set; } = "";
    public string Phone { get; set; } = "";
}
