using Lockte.Org;
using Lockte.SharedKernel;

namespace Lockte.Hr;

/// <summary>
/// ค่าที่คำนวณ/คัดลอกในฟอร์มพนักงาน (BR-052) — what SCR-02/03 fill in as HR types, as the legacy VBA did.
/// The form calls these; saving stores whatever HR left in the fields.
/// </summary>
public static class EmployeeFormRules
{
    /// <summary>SCR-02 (ฟอร์ม EMPLO): SA_DAY = Round(SALARY / 30, 2), RND-B. รายวัน enter SA_DAY directly (SALARY = 0).</summary>
    public static double DailyWageOnAdd(double salary) => Rounding.RndB(salary / 30, 2);

    /// <summary>SCR-03 (ฟอร์ม EMPLO2): SA_DAY = SALARY / 30, not rounded ⚠ — kept as in the legacy edit form.</summary>
    public static double DailyWageOnEdit(double salary) => salary / 30;

    /// <summary>เลือกแผนก → SECTION = แผนก.SECTION; HR may still choose another ฝ่าย.</summary>
    public static int SectionOnDepartmentChosen(DepartmentDto department) => department.SectionId;

    /// <summary>เปลี่ยนเลขบัตรประชาชน → ID_S = ID_CODE.</summary>
    public static string? SocialSecurityNoFor(string? nationalId) => string.IsNullOrWhiteSpace(nationalId) ? null : nationalId.Trim();

    /// <summary>รหัสพนักงานเป็นตัวพิมพ์ใหญ่ (BR-050).</summary>
    public static string? NormalizeCode(string? code) => string.IsNullOrWhiteSpace(code) ? null : code.Trim().ToUpperInvariant();
}

/// <summary>ทดลองงาน / ผ่านทดลองงาน (BR-053); the legacy day counts are kept as they are (Q-35).</summary>
public static class Probation
{
    /// <summary>Days after START still counted as probation by RPT-20 and the hiring form (วันครบ 90 วัน = START + 89).</summary>
    public const int LastProbationDay = 89;

    /// <summary>Days after START to วันครบทดลองงาน on the employment contract.</summary>
    public const int ContractProbationDays = 118;

    /// <summary>"พนักงานยังไม่ผ่านทดลองงาน" (RPT-20): ยังไม่ลาออก และ วันนี้ − START ≤ 89 วัน.</summary>
    public static bool IsOnProbation(DateOnly? startDate, DateOnly? resignDate, DateOnly today)
        => resignDate is null && startDate is { } start && start >= EarliestStartOnProbation(today);

    /// <summary>Employees who started on or after this date are still on probation today.</summary>
    public static DateOnly EarliestStartOnProbation(DateOnly today) => today.AddDays(-LastProbationDay);

    /// <summary>วันครบ 90 วัน on the hiring form = START + 89.</summary>
    public static DateOnly NinetyDayDate(DateOnly startDate) => startDate.AddDays(LastProbationDay);

    /// <summary>วันครบทดลองงาน on the employment contract = START + 118.</summary>
    public static DateOnly EndDate(DateOnly startDate) => startDate.AddDays(ContractProbationDays);
}
