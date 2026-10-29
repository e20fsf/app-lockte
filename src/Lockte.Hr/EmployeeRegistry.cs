using Lockte.Hr.Data;
using Lockte.Org;
using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Hr;

/// <summary>
/// เพิ่ม/แก้ ทะเบียนพนักงาน (SCR-02 / SCR-03). แผนก and กะ must be in the registers of module องค์กร, read through
/// <see cref="OrgDirectory"/> (ADR 0002). Each save carries the rowversion the screen loaded; if the row changed
/// since, it throws <see cref="ConcurrencyConflictException"/>.
/// </summary>
public sealed class EmployeeRegistry(HrDbContext db, OrgDirectory org, IPayAccess pay)
{
    /// <summary>T-EMPLO.ID is Text(4): a letter and three digits, e.g. D385.</summary>
    public const int CodeLength = 4;

    public async Task<ServiceResult> SaveAsync(EmployeeInput input)
    {
        var code = EmployeeFormRules.NormalizeCode(input.Code);
        var name = Clean(input.Name);
        var errors = new List<string>();
        if (code is null) errors.Add("กรุณากรอกรหัสพนักงาน");
        else if (code.Length > CodeLength) errors.Add($"รหัสพนักงานยาวได้ไม่เกิน {CodeLength} ตัว");
        else if (await db.Employees.AnyAsync(e => e.Code == code && e.Id != input.Id)) errors.Add($"มีรหัสพนักงาน '{code}' อยู่แล้ว");
        if (name is null) errors.Add("กรุณากรอกชื่อ-สกุล");
        if (input.DepartmentId is { } departmentId && await org.GetDepartmentAsync(departmentId) is null)
            errors.Add("ไม่พบแผนกที่เลือก");
        if (input.SectionId is { } sectionId && (await org.GetSectionsAsync()).All(s => s.Id != sectionId))
            errors.Add("ไม่พบฝ่ายที่เลือก");
        if (input.ShiftId is { } shiftId && (await org.GetShiftsAsync()).All(s => s.Id != shiftId))
            errors.Add("ไม่พบกะที่เลือก");
        if (input.Sex is { } sex && !EmployeeLookups.Sexes.ContainsKey(sex)) errors.Add("เพศต้องเป็น M หรือ F");
        if (input.EmployeeType is { } type && !EmployeeTypes.Names.ContainsKey(type)) errors.Add("ประเภทพนักงานต้องเป็นรายเดือนหรือรายวัน");
        if (errors.Count > 0) return new ServiceResult(false, errors);

        Employee? employee;
        if (input.Id is { } id)
        {
            employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == id && !e.IsMigrationStub);
            if (employee is null) return ServiceResult.Fail("ไม่พบพนักงานคนนี้");
            employee.ExpectRowVersion(input.RowVersion
                ?? throw new ArgumentException("Editing needs the rowversion the screen loaded.", nameof(input)));
        }
        else
        {
            employee = db.Employees.Add(new Employee()).Entity;
        }

        employee.Code = code!;
        employee.Name = name!;
        employee.EnglishTitle = Clean(input.EnglishTitle);
        employee.EnglishName = Clean(input.EnglishName);
        employee.Sex = input.Sex;
        employee.BirthDate = input.BirthDate;
        employee.NationalId = Clean(input.NationalId);
        employee.SocialSecurityNo = Clean(input.SocialSecurityNo);
        employee.IdCardPlace = Clean(input.IdCardPlace);
        employee.IdCardIssued = input.IdCardIssued;
        employee.RegisteredAddress = Clean(input.RegisteredAddress);
        employee.CurrentAddress = Clean(input.CurrentAddress);
        employee.Telephone = Clean(input.Telephone);
        employee.Nationality = Clean(input.Nationality) ?? EmployeeInput.DefaultNationality;
        employee.StartDate = input.StartDate;
        employee.ListNo = Clean(input.ListNo);
        employee.Education = Clean(input.Education);
        employee.DepartmentId = input.DepartmentId;
        employee.SectionId = input.SectionId;
        employee.Position = Clean(input.Position);
        employee.Duty = Clean(input.Duty);
        employee.EmployeeType = input.EmployeeType;
        if (pay.CanViewPay)
        {
            employee.Salary = input.Salary;
            employee.DailyWage = input.DailyWage;
        }
        employee.ShiftId = input.ShiftId;
        employee.HotAllowance = input.HotAllowance;
        if (pay.CanViewPay) // #277: a user who cannot see ข้อมูลค่าจ้าง never changes it; a new employee starts with none
        {
            employee.BankAccountTtb = Clean(input.BankAccountTtb);
            employee.BankAccountKbank = Clean(input.BankAccountKbank);
        }
        employee.Hospital = Clean(input.Hospital);
        employee.ResignDate = input.ResignDate;
        employee.Memo = Clean(input.Memo);
        employee.Evaluation1Date = input.Evaluation1Date;
        employee.Evaluation1Score = input.Evaluation1Score;
        employee.Evaluation1Grade = Clean(input.Evaluation1Grade);
        employee.Evaluation2Date = input.Evaluation2Date;
        employee.Evaluation2Score = input.Evaluation2Score;
        employee.Evaluation2Grade = Clean(input.Evaluation2Grade);
        employee.ProbationPassedDate = input.ProbationPassedDate;
        await db.SaveChangesAsync();
        return ServiceResult.Ok(employee.Id);
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
