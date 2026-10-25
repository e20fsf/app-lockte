using Lockte.SharedKernel.Persistence;

namespace Lockte.Hr.Data;

/// <summary>
/// พนักงาน — one row per person, including those who resigned (T-EMPLO). Internal to the module (ADR 0002):
/// other modules and screens see <see cref="EmployeeDto"/> from <see cref="EmployeeDirectory"/>.
/// The evaluation fields are stored now (the data migration and payroll need them) and get their screen with
/// SCR-53/54 (#67); the photo is not kept (empty for everyone working).
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
    public string Nationality { get; set; } = "";
    public DateOnly? StartDate { get; set; }
    /// <summary>ลำดับที่ (NO).</summary>
    public string? ListNo { get; set; }
    /// <summary>วุฒิการศึกษา (POINT).</summary>
    public string? Education { get; set; }
    /// <summary>แผนก in module องค์กร (org.Departments, FK across schemas).</summary>
    public int? DepartmentId { get; set; }
    /// <summary>
    /// ฝ่าย (SECTION) in module องค์กร (org.Sections): copied from the department when it is chosen (BR-052) and
    /// editable, so it is the employee's own value — a later change of the department's ฝ่าย does not rewrite it.
    /// </summary>
    public int? SectionId { get; set; }
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
    /// <summary>
    /// พนักงานตัวแทนที่เครื่องมือย้ายข้อมูลสร้างให้รหัสกำพร้าที่มีผลค่าแรง (#51, DQ-6): the legacy had no EMPLO row for it, so it is
    /// kept out of every list, search, form and government register and cannot be opened or changed on a screen; only the
    /// pay results point to it.
    /// </summary>
    public bool IsMigrationStub { get; set; }

    /// <summary>วันประเมินครั้งที่ 1 (DATE_1, BR-055).</summary>
    public DateOnly? Evaluation1Date { get; set; }
    /// <summary>คะแนนประเมินครั้งที่ 1 (POINT_1, BR-055) — also read by payroll as a set wage per period (BR-003 ⚠ Q-09).</summary>
    public int? Evaluation1Score { get; set; }
    /// <summary>เกรดประเมินครั้งที่ 1 (GRED_1, BR-055) — set = not in ค่าแรงสิ้นเดือน (BR-063).</summary>
    public string? Evaluation1Grade { get; set; }
    /// <summary>วันประเมินครั้งที่ 2 (DATE_2).</summary>
    public DateOnly? Evaluation2Date { get; set; }
    /// <summary>คะแนนประเมินครั้งที่ 2 (POINT_2).</summary>
    public int? Evaluation2Score { get; set; }
    /// <summary>เกรดประเมินครั้งที่ 2 (GRED_2).</summary>
    public string? Evaluation2Grade { get; set; }
    /// <summary>วันผ่านทดลองงาน (DATE_3, BR-055).</summary>
    public DateOnly? ProbationPassedDate { get; set; }

    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    string IHasBusinessKey.BusinessKey => Code;
}
