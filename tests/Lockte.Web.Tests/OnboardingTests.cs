using Lockte.Hr;
using Lockte.Org;

namespace Lockte.Web.Tests;

/// <summary>
/// What the onboarding / probation documents and the employee lists read (#66, BR-053, BR-071..073) through
/// <see cref="OnboardingDirectory"/>.
/// </summary>
[Collection(LocalDbCollection.Name)]
public sealed class OnboardingTests(LocalDbFixture db)
{
    private static int lastCode;

    private static string NewCode() => $"G{Interlocked.Increment(ref lastCode):D3}";

    private static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..6];

    private static DateOnly Today => DateOnly.FromDateTime(TimeProvider.System.GetLocalNow().DateTime);

    private async Task<T> AsHrAsync<T>(Func<TestScope, Task<T>> action)
    {
        await using var scope = db.Scope("hr1");
        return await action(scope);
    }

    private async Task<DepartmentDto> NewDepartmentAsync(params DepartmentShiftInput[] shifts)
    {
        var sectionId = (await AsHrAsync(s => s.Get<OrgRegistry>().SaveSectionAsync(new SectionInput(null, Unique("ฝ่าย"), 1, null)))).Id!.Value;
        var result = await AsHrAsync(s => s.Get<OrgRegistry>().SaveDepartmentAsync(new DepartmentInput(
            null, Unique("R"), Unique("แผนก"), "R", 9, sectionId, null, shifts, null)));
        Assert.True(result.Succeeded, string.Join(", ", result.Errors));
        return (await AsHrAsync(s => s.Get<OrgDirectory>().GetDepartmentAsync(result.Id!.Value)))!;
    }

    private Task<ShiftDto> ShiftAsync(string code) => AsHrAsync(async s => (await s.Get<OrgDirectory>().FindShiftByCodeAsync(code))!);

    private async Task<EmployeeDto> AddAsync(EmployeeInput input)
    {
        var result = await AsHrAsync(s => s.Get<EmployeeRegistry>().SaveAsync(input));
        Assert.True(result.Succeeded, string.Join(", ", result.Errors));
        return (await AsHrAsync(s => s.Get<EmployeeDirectory>().GetAsync(result.Id!.Value)))!;
    }

    [Fact] // BR-072: แผนก JOIN on กะ gives the names printed as ผู้สัมภาษณ์ / ผู้จัดการฝ่าย / ผู้อนุมัติ
    public async Task An_employee_comes_with_the_signers_of_their_department_in_their_shift()
    {
        var shiftA = await ShiftAsync("A");
        var shiftB = await ShiftAsync("B");
        var department = await NewDepartmentAsync(
            new DepartmentShiftInput(shiftA.Id, "หัวหน้า เอ", "ผจก. ฝ่าย เอ", "ผจก. โรงงาน"),
            new DepartmentShiftInput(shiftB.Id, "หัวหน้า บี", "ผจก. ฝ่าย บี", "ผจก. โรงงาน"));
        var employee = await AddAsync(new EmployeeInput
        {
            Code = NewCode(), Name = "นายรับ เข้างาน", DepartmentId = department.Id, ShiftId = shiftB.Id, StartDate = new(2026, 8, 13),
            BirthDate = new(1982, 10, 20), EmployeeType = EmployeeTypes.Monthly, Salary = 12340, Position = "พนักงาน",
        });

        var e = (await AsHrAsync(s => s.Get<OnboardingDirectory>().GetEmployeeAsync(employee.Id)))!;

        Assert.Equal(("หัวหน้า บี", "ผจก. ฝ่าย บี", "ผจก. โรงงาน"), (e.SupervisorName, e.ManagerName, e.FactoryManagerName));
        Assert.Equal((department.Name, department.SectionName, "B"), (e.DepartmentName, e.SectionName, e.ShiftCode));
        Assert.Equal(new DateOnly(2026, 11, 10), e.NinetyDayDate); // START + 89
        Assert.True(e.IsMonthly);
        Assert.Equal(44, e.AgeOn(new DateOnly(2026, 10, 15))); // (15/10/2026 − 20/10/1982) / 365 = 44.0 → 44
        Assert.Null(await AsHrAsync(s => s.Get<OnboardingDirectory>().GetEmployeeAsync(int.MaxValue)));
    }

    [Fact] // the legacy found no row when the กะ had no แผนก row; now the names are just blank
    public async Task Without_a_department_row_for_the_shift_the_signers_are_blank()
    {
        var department = await NewDepartmentAsync(new DepartmentShiftInput((await ShiftAsync("A")).Id, "หัวหน้า เอ", "ผจก.", "ผจก. โรงงาน"));
        var employee = await AddAsync(new EmployeeInput { Code = NewCode(), Name = "นายไม่มี กะ", DepartmentId = department.Id, ShiftId = (await ShiftAsync("D")).Id });

        var e = (await AsHrAsync(s => s.Get<OnboardingDirectory>().GetEmployeeAsync(employee.Id)))!;

        Assert.Equal((null, null, null), (e.SupervisorName, e.ManagerName, e.FactoryManagerName));
        Assert.False(e.IsMonthly);
    }

    [Fact] // BR-071: Sum(LEAVE.SUM_D) by SR 1, 2, 3, 4, 5, 8, the E_WORK rows with LATE and the ใบเตือน rows by CODE 1 / 2
    public async Task Probation_statistics_sum_leave_days_by_type_and_count_late_days()
    {
        var employee = await AddAsync(new EmployeeInput { Code = NewCode(), Name = "นายสถิติ ลา", ShiftId = (await ShiftAsync("A")).Id, DailyWage = 400 });
        var day = new DateOnly(2026, 6, 1);
        async Task Leave(int offset, double days, string type)
        {
            var result = await AsHrAsync(s => s.Get<LeaveRegistry>().SaveAsync(new LeaveInput(null, employee.Id, null, day.AddDays(offset),
                day.AddDays(offset), null, null, days, type, false, null, null, null)));
            Assert.True(result.Save.Succeeded, string.Join(", ", result.Save.Errors));
        }
        await Leave(0, 1, "1");
        await Leave(1, 0.5, "1");
        await Leave(2, 1, "3");
        await Leave(3, 2, "8");
        await Leave(5, 1, "6"); // ลาพักร้อน: not on the form
        foreach (var (offset, late) in new[] { (10, "08:15"), (11, "08:40"), (12, (string?)null) })
        {
            var result = await AsHrAsync(s => s.Get<WorkTimeRegistry>().SaveAsync(new WorkTimeInput(null, day.AddDays(offset), employee.Id, "W",
                new TimeOnly(8, 0), new TimeOnly(17, 0), 60, late is null ? null : TimeOnly.Parse(late), null, null)));
            Assert.True(result.Succeeded, string.Join(", ", result.Errors));
        }
        foreach (var (offset, level) in new[] { (20, "1"), (21, "1"), (22, "2"), (23, "3") }) // CODE 3 เลิกจ้าง: not on the form
        {
            var result = await AsHrAsync(s => s.Get<WarningRegistry>().SaveAsync(new WarningInput(null, day.AddDays(offset), employee.Id, level,
                null, null)));
            Assert.True(result.Succeeded, string.Join(", ", result.Errors));
        }

        var stats = await AsHrAsync(s => s.Get<OnboardingDirectory>().GetProbationStatisticsAsync(employee.Id));

        Assert.Equal(new ProbationStatistics(1.5, 0, 1, 0, 0, 2, 2, 2, 1), stats);
        Assert.Equal(-(1.5 * 2 + 1 * 2 + 2 * 1 + 2 * 3 + 2 * 5 + 1 * 10), stats.TotalDeduction); // −33
    }

    [Fact] // BR-073 รายชื่อเฉพาะแผนก: ยังไม่ลาออก, แผนก and กะ LIKE (blank = "*"), by รหัส; DateDiff("m") years / months / age
    public async Task Department_roster_lists_working_employees_of_the_department_and_shift()
    {
        var shiftA = await ShiftAsync("A");
        var shiftB = await ShiftAsync("B");
        var department = await NewDepartmentAsync();
        var start = Today.AddMonths(-27).AddDays(-3); // 27 month boundaries → 2 ปี 3 เดือน
        var first = await AddAsync(new EmployeeInput
        {
            Code = NewCode(), Name = "นายหนึ่ง", Sex = "M", DepartmentId = department.Id, ShiftId = shiftA.Id, StartDate = start,
            BirthDate = Today.AddMonths(-(40 * 12 + 11)), Salary = 18665, DailyWage = 622.17, Position = "หัวหน้า",
        });
        await AddAsync(new EmployeeInput { Code = NewCode(), Name = "นางสอง", Sex = "F", DepartmentId = department.Id, ShiftId = shiftB.Id,
            BirthDate = Today.AddMonths(-(30 * 12)) });
        await AddAsync(new EmployeeInput { Code = NewCode(), Name = "นางลาออก", Sex = "F", DepartmentId = department.Id, ShiftId = shiftA.Id,
            ResignDate = Today.AddDays(-1) });
        var directory = (Func<int?, int?, Task<DepartmentRoster>>)((d, s) => AsHrAsync(x => x.Get<OnboardingDirectory>().GetDepartmentRosterAsync(d, s)));

        var all = await directory(department.Id, null);
        var shiftOnly = await directory(department.Id, shiftA.Id);

        Assert.Equal("รายชื่อพนักงานแผนก" + department.Name, all.Title);
        Assert.Equal(["นายหนึ่ง", "นางสอง"], all.Lines.Select(l => l.Name));
        Assert.Equal((1, 1, 35.0), (all.Male, all.Female, all.AverageAge!.Value));
        Assert.Equal("รายชื่อพนักงานแผนก" + department.Name + "  กะA", shiftOnly.Title);
        var line = Assert.Single(shiftOnly.Lines);
        Assert.Equal((first.Code, start, "หัวหน้า", 2, 3, 40, 18665.0, 622.17),
            (line.Code, line.StartDate, line.Position, line.ServiceYears, line.ServiceMonths, line.Age, line.Salary, line.DailyWage));
        var everyone = await directory(null, shiftB.Id);
        Assert.StartsWith("รายชื่อพนักงานทั้งหมด  กะB", everyone.Title);
        Assert.Contains(everyone.Lines, l => l.Name == "นางสอง");
    }

    [Fact] // BR-073 รายชื่อทั้งหมด (EMPLO) and ทะเบียนประวัติพนักงาน (everyone, wage = เงินเดือน else ค่าแรง/วัน, เพศเป็นคำไทย)
    public async Task Working_list_and_register()
    {
        var department = await NewDepartmentAsync();
        var monthly = await AddAsync(new EmployeeInput { Code = NewCode(), Name = "นายเดือน", Sex = "M", Salary = 15000, DailyWage = 500,
            DepartmentId = department.Id, BirthDate = Today.AddDays(-365 * 30), Hospital = "รพ.ทดสอบ" });
        var daily = await AddAsync(new EmployeeInput { Code = NewCode(), Name = "นางวัน", Sex = "F", DailyWage = 372, ResignDate = Today.AddDays(-3) });
        var unknown = await AddAsync(new EmployeeInput { Code = NewCode(), Name = "ไม่ระบุเพศ" });

        var working = await AsHrAsync(s => s.Get<OnboardingDirectory>().GetWorkingEmployeesAsync());
        var register = await AsHrAsync(s => s.Get<OnboardingDirectory>().GetRegisterAsync());

        var w = Assert.Single(working.Lines, l => l.Code == monthly.Code);
        Assert.Equal((department.Name, 30.0, "รพ.ทดสอบ"), (w.DepartmentName, w.Age, w.Hospital));
        Assert.DoesNotContain(working.Lines, l => l.Code == daily.Code);
        Assert.Equal(working.Lines.Select(l => l.Code).Order(StringComparer.Ordinal), working.Lines.Select(l => l.Code));

        Assert.Equal(("ชาย", 15000.0), Assert.Single(register, l => l.Code == monthly.Code) is var m ? (m.Sex, m.Wage) : default);
        var r = Assert.Single(register, l => l.Code == daily.Code);
        Assert.Equal(("หญิง", 372.0, Today.AddDays(-3)), (r.Sex, r.Wage, r.ResignDate));
        Assert.Null(Assert.Single(register, l => l.Code == unknown.Code).Sex);
    }
}
