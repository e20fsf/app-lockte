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
