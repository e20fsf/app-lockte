using Lockte.SharedKernel.Persistence;

namespace Lockte.Hr.Data;

/// <summary>
/// พนักงาน — one row per person, including those who resigned (T-EMPLO). Internal to the module (ADR 0002):
/// other modules and screens see <see cref="EmployeeDto"/> from <see cref="EmployeeDirectory"/>.
/// Evaluation fields (DATE_1/2, POINT_1/2, GRED_1/2, DATE_3) and the photo come with their own tickets.
/// </summary>
internal sealed class Employee : IAudited
{
    public int Id { get; set; }
    /// <summary>รหัสพนักงาน (ID): upper case, unique, entered by HR, kept when the employee moves department (BR-050, #16).</summary>
    public string Code { get; set; } = "";
    /// <summary>ชื่อ-สกุล (NAME): คำนำหน้า + ชื่อ + นามสกุล in one field, as before.</summary>
    public string Name { get; set; } = "";
    public string? EnglishTitle { get; set; }
    public string? EnglishName { get; set; }
    /// <summary>เพศ: M ชาย / F หญิง (T-เพศ).</summary>
    public string? Sex { get; set; }
    public DateOnly? BirthDate { get; set; }
    public string? NationalId { get; set; }
    public string? SocialSecurityNo { get; set; }
    public string? IdCardPlace { get; set; }
    public DateOnly? IdCardIssued { get; set; }
    public string? RegisteredAddress { get; set; }
    public string? CurrentAddress { get; set; }
    public string? Telephone { get; set; }
    public string Nationality { get; set; } = EmployeeInput.DefaultNationality;
    public DateOnly? StartDate { get; set; }
    /// <summary>ลำดับที่ (NO).</summary>
    public string? ListNo { get; set; }
    /// <summary>วุฒิการศึกษา (POINT).</summary>
    public string? Education { get; set; }
    /// <summary>แผนก in module องค์กร (org.Departments, FK across schemas). ฝ่าย is the department's.</summary>
    public int? DepartmentId { get; set; }
    public string? Position { get; set; }
    /// <summary>หน้าที่ (WORK), e.g. "D11 ฉีดก้ามเบรก".</summary>
    public string? Duty { get; set; }
    /// <summary>ประเภท (CLAS): <see cref="EmployeeTypes"/>.</summary>
    public int? EmployeeType { get; set; }
    /// <summary>เงินเดือน — Double as in Access, so payroll rounds the same (#41).</summary>
    public double Salary { get; set; }
    /// <summary>ค่าแรง/วัน (SA_DAY) — Double as in Access.</summary>
    public double DailyWage { get; set; }
    /// <summary>กะ in module องค์กร (org.Shifts, FK across schemas).</summary>
    public int? ShiftId { get; set; }
    /// <summary>ได้ HOT (HOT1 = "Y").</summary>
    public bool HotAllowance { get; set; }
    /// <summary>บัญชีทหารไทย (ACC) — ค่าแรง.</summary>
    public string? BankAccountTtb { get; set; }
    /// <summary>บัญชีกสิกร (ACC2) — OT / โบนัส.</summary>
    public string? BankAccountKbank { get; set; }
    public string? Hospital { get; set; }
    /// <summary>วันลาออก — blank = still working.</summary>
    public DateOnly? ResignDate { get; set; }
    /// <summary>หมายเหตุ (MEM).</summary>
    public string? Memo { get; set; }

    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    string IHasBusinessKey.BusinessKey => Code;
}
