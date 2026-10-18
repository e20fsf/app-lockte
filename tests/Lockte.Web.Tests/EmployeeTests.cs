using Lockte.Hr;
using Lockte.Org;
using Lockte.SharedKernel.Persistence;
using Lockte.Web.Audit;

namespace Lockte.Web.Tests;

/// <summary>ทะเบียนพนักงาน (#45, SCR-02/03, BR-050..054) through the public services of module พนักงานและค่าแรง.</summary>
[Collection(LocalDbCollection.Name)]
public sealed class EmployeeTests(LocalDbFixture db)
{
    private static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..6];

    private static int lastCode;

    /// <summary>
    /// Tests share one fresh database per run: every test makes its own employee codes (letter + 3 digits).
    /// A counter, not random characters, so two codes never collide (4 characters leave too little room for chance).
    /// </summary>
    private static string NewCode() => $"T{Interlocked.Increment(ref lastCode):D3}";

    private static DateOnly Today => DateOnly.FromDateTime(TimeProvider.System.GetLocalNow().DateTime);

    private async Task<T> AsHrAsync<T>(Func<EmployeeRegistry, EmployeeDirectory, Task<T>> action)
    {
        await using var scope = db.Scope("hr1");
        return await action(scope.Get<EmployeeRegistry>(), scope.Get<EmployeeDirectory>());
    }

    private async Task<T> OrgAsync<T>(Func<OrgRegistry, OrgDirectory, Task<T>> action)
    {
        await using var scope = db.Scope("hr1");
        return await action(scope.Get<OrgRegistry>(), scope.Get<OrgDirectory>());
    }

    private async Task<DepartmentDto> NewDepartmentAsync(int? staffingTarget = null)
    {
        var sectionId = (await OrgAsync((r, _) => r.SaveSectionAsync(new SectionInput(null, Unique("ผลิต"), 1, null)))).Id!.Value;
        var id = (await OrgAsync((r, _) => r.SaveDepartmentAsync(new DepartmentInput(
            null, Unique("D"), Unique("ไดคาสท์"), "D", 3, sectionId, null, [], null, staffingTarget)))).Id!.Value;
        return (await OrgAsync((_, d) => d.GetDepartmentAsync(id)))!;
    }

    private async Task<EmployeeDto> AddAsync(EmployeeInput input)
    {
        var result = await AsHrAsync((r, _) => r.SaveAsync(input));
        Assert.True(result.Succeeded, string.Join(", ", result.Errors));
        return (await AsHrAsync((_, d) => d.GetAsync(result.Id!.Value)))!;
    }

    [Fact]
    public async Task Added_employee_is_stored_with_an_upper_case_code_and_org_names()
    {
        var department = await NewDepartmentAsync();
        var shiftD = (await OrgAsync((_, d) => d.FindShiftByCodeAsync("D")))!;
        var code = NewCode();

        var employee = await AddAsync(new EmployeeInput
        {
            Code = $" {code.ToLowerInvariant()} ", Name = "นาย  สมชาย  ใจดี", Sex = "M", StartDate = new(2024, 5, 1),
            DepartmentId = department.Id, SectionId = department.SectionId, ShiftId = shiftD.Id, EmployeeType = EmployeeTypes.Monthly,
            Salary = 12_295, DailyWage = 409.83, BankAccountTtb = "1234567890",
        });

        Assert.Equal((code, "นาย  สมชาย  ใจดี", department.Name, department.SectionName, "D", "ไทย", false),
            (employee.Code, employee.Name, employee.DepartmentName, employee.SectionName, employee.ShiftCode,
             employee.Nationality, employee.HotAllowance));
        Assert.Equal(employee.Id, (await AsHrAsync((_, d) => d.FindByCodeAsync(code.ToLowerInvariant())))!.Id);
    }

    [Fact]
    public async Task Code_is_required_unique_and_at_most_four_characters()
    {
        var first = await AddAsync(new EmployeeInput { Code = NewCode(), Name = "คนแรก" });
        var second = await AddAsync(new EmployeeInput { Code = NewCode(), Name = "คนที่สอง" });

        var duplicate = await AsHrAsync((r, _) => r.SaveAsync(new EmployeeInput { Code = first.Code.ToLowerInvariant(), Name = "ซ้ำ" }));
        var editToDuplicate = await AsHrAsync((r, _) => r.SaveAsync(second.ToInput() with { Code = first.Code }));
        var tooLong = await AsHrAsync((r, _) => r.SaveAsync(new EmployeeInput { Code = "D1000", Name = "ยาว" }));
        var blank = await AsHrAsync((r, _) => r.SaveAsync(new EmployeeInput { Code = " ", Name = " " }));

        Assert.Equal([$"มีรหัสพนักงาน '{first.Code}' อยู่แล้ว"], duplicate.Errors);
        Assert.Equal([$"มีรหัสพนักงาน '{first.Code}' อยู่แล้ว"], editToDuplicate.Errors);
        Assert.Equal(["รหัสพนักงานยาวได้ไม่เกิน 4 ตัว"], tooLong.Errors);
        Assert.Equal(["กรุณากรอกรหัสพนักงาน", "กรุณากรอกชื่อ-สกุล"], blank.Errors);
    }

    [Fact]
    public async Task Code_stays_the_same_when_the_employee_moves_department()
    {
        var (from, to) = (await NewDepartmentAsync(), await NewDepartmentAsync());
        var employee = await AddAsync(new EmployeeInput { Code = NewCode(), Name = "ย้ายแผนก", DepartmentId = from.Id });

        var moved = await AsHrAsync((r, _) => r.SaveAsync(employee.ToInput() with { DepartmentId = to.Id }));

        Assert.True(moved.Succeeded);
        var after = (await AsHrAsync((_, d) => d.GetAsync(employee.Id)))!;
        Assert.Equal((employee.Code, to.Name), (after.Code, after.DepartmentName));
        await using var scope = db.Scope();
        var change = Assert.Single(await scope.Get<HistoryService>().SearchAsync(new(Table: "พนักงาน", RowKey: employee.Code)),
            r => r.Action == HistoryActions.Modified);
        Assert.Equal(("แผนก", from.Id.ToString(), to.Id.ToString()), (change.Column, change.OldValue, change.NewValue));
    }

    [Fact]
    public async Task Department_and_shift_must_be_in_the_org_registers()
    {
        var result = await AsHrAsync((r, _) => r.SaveAsync(new EmployeeInput
        {
            Code = NewCode(), Name = "ไม่มีแผนก", DepartmentId = int.MaxValue, ShiftId = int.MaxValue,
        }));

        Assert.Equal(["ไม่พบแผนกที่เลือก", "ไม่พบกะที่เลือก"], result.Errors);
    }

    [Theory] // BR-052: SA_DAY = Round(SALARY / 30, 2) on SCR-02 (RND-B), not rounded on SCR-03 ⚠
    [InlineData(12_295, 409.83)]
    [InlineData(15_000, 500)]
    public void Adding_rounds_the_daily_wage_from_the_salary(double salary, double dailyWage)
        => Assert.Equal(dailyWage, EmployeeFormRules.DailyWageOnAdd(salary));

    [Fact]
    public void Editing_does_not_round_the_daily_wage_like_the_legacy_edit_form()
        => Assert.Equal(12_295 / 30d, EmployeeFormRules.DailyWageOnEdit(12_295));

    [Fact]
    public void National_id_is_copied_to_the_social_security_number()
        => Assert.Equal("3100700000001", EmployeeFormRules.SocialSecurityNoFor(" 3100700000001 "));

    [Fact]
    public async Task Section_is_copied_from_the_department_and_stays_the_employees_own()
    {
        var department = await NewDepartmentAsync();
        var otherSection = (await OrgAsync((r, _) => r.SaveSectionAsync(new SectionInput(null, Unique("สโตร์"), 2, null)))).Id!.Value;

        var copied = EmployeeFormRules.SectionOnDepartmentChosen(department);
        var employee = await AddAsync(new EmployeeInput
        {
            Code = NewCode(), Name = "เลือกแผนก", DepartmentId = department.Id, SectionId = copied,
        });
        var edited = await AsHrAsync((r, _) => r.SaveAsync(employee.ToInput() with { SectionId = otherSection }));
        var unknown = await AsHrAsync((r, _) => r.SaveAsync(employee.ToInput() with { SectionId = int.MaxValue }));

        Assert.Equal((department.SectionId, department.SectionName), (employee.SectionId, employee.SectionName));
        Assert.True(edited.Succeeded); // HR may set another ฝ่าย, as SECTION was editable
        var after = (await AsHrAsync((_, d) => d.GetAsync(employee.Id)))!;
        Assert.Equal((otherSection, department.Id), (after.SectionId!.Value, after.DepartmentId!.Value));
        Assert.Equal(["ไม่พบฝ่ายที่เลือก"], unknown.Errors);
    }

    [Fact]
    public async Task Evaluation_fields_are_kept_when_the_form_saves_other_fields()
    {
        var employee = await AddAsync(new EmployeeInput
        {
            Code = NewCode(), Name = "ประเมิน", Evaluation1Date = new(2026, 1, 15), Evaluation1Score = 80, Evaluation1Grade = "B",
            Evaluation2Date = new(2026, 3, 15), Evaluation2Score = 90, Evaluation2Grade = "A", ProbationPassedDate = new(2026, 4, 1),
        });

        await AsHrAsync((r, _) => r.SaveAsync(employee.ToInput() with { Telephone = "081" }));

        var after = (await AsHrAsync((_, d) => d.GetAsync(employee.Id)))!;
        Assert.Equal((new DateOnly(2026, 1, 15), (int?)80, "B", new DateOnly(2026, 3, 15), (int?)90, "A", new DateOnly(2026, 4, 1)),
            (after.Evaluation1Date!.Value, after.Evaluation1Score, after.Evaluation1Grade, after.Evaluation2Date!.Value,
             after.Evaluation2Score, after.Evaluation2Grade, after.ProbationPassedDate!.Value));
    }

    [Fact]
    public async Task Editing_needs_the_rowversion_the_screen_loaded()
    {
        var employee = await AddAsync(new EmployeeInput { Code = NewCode(), Name = "ไม่มี rowversion" });

        await Assert.ThrowsAsync<ArgumentException>(() =>
            AsHrAsync((r, _) => r.SaveAsync(employee.ToInput() with { RowVersion = null })));
    }

    [Fact]
    public async Task Probation_lasts_until_day_89_and_the_contract_counts_118_days()
    {
        var onDay89 = await AddAsync(new EmployeeInput { Code = NewCode(), Name = "วันที่ 89", StartDate = Today.AddDays(-89) });
        var onDay90 = await AddAsync(new EmployeeInput { Code = NewCode(), Name = "วันที่ 90", StartDate = Today.AddDays(-90) });
        var resigned = await AddAsync(new EmployeeInput
        {
            Code = NewCode(), Name = "ลาออกแล้ว", StartDate = Today.AddDays(-10), ResignDate = Today.AddDays(-1),
        });

        Assert.True(onDay89.OnProbation);
        Assert.False(onDay90.OnProbation);
        Assert.False(resigned.OnProbation);
        Assert.Equal((Today.AddDays(-89 + 89), Today.AddDays(-89 + 118)), (onDay89.NinetyDayDate, onDay89.ProbationEndDate));
        var list = await AsHrAsync((_, d) => d.ListOnProbationAsync());
        Assert.Contains(list, e => e.Id == onDay89.Id);
        Assert.DoesNotContain(list, e => e.Id == onDay90.Id || e.Id == resigned.Id);
    }

    [Fact]
    public async Task Statistics_count_working_employees_by_department_sex_education_and_hospital()
    {
        var department = await NewDepartmentAsync(staffingTarget: 5);
        var hospital = Unique("โรงพยาบาล");
        EmployeeInput Person(string sex, string education, DateOnly? resign = null) => new()
        {
            Code = NewCode(), Name = Unique("คน"), Sex = sex, Education = education, DepartmentId = department.Id,
            Hospital = hospital, ResignDate = resign,
        };
        await AddAsync(Person("M", "ม.3"));
        await AddAsync(Person("M", "ปวช."));
        await AddAsync(Person("F", "ม.3"));
        await AddAsync(Person("F", "ม.3", resign: Today.AddDays(-1))); // resigned: not counted

        var stats = await AsHrAsync((_, d) => d.GetStatisticsAsync());

        var row = Assert.Single(stats.ByDepartment, r => r.DepartmentId == department.Id);
        Assert.Equal((2, 1, 3, (int?)5, (int?)-2), (row.Male, row.Female, row.Total, row.StaffingTarget, row.Difference));
        var education = Assert.Single(stats.ByEducation, r => r.DepartmentId == department.Id);
        Assert.Equal(EmployeeStatistics.EducationLevels, education.Counts.Keys);
        Assert.Equal((2, 1, 0), (education.Counts["ม.3"], education.Counts["ปวช."], education.Counts["ปริญญาตรี"]));
        Assert.Equal(3, Assert.Single(stats.ByHospital, h => h.Hospital == hospital).Count);
    }

    [Fact]
    public async Task Duty_and_hospital_offer_the_values_already_entered()
    {
        var (duty, hospital, resignedHospital) = (Unique("D11 ฉีดก้ามเบรก"), Unique("รพ."), Unique("รพ.เก่า"));
        await AddAsync(new EmployeeInput { Code = NewCode(), Name = Unique("คน"), Duty = duty, Hospital = hospital });
        await AddAsync(new EmployeeInput { Code = NewCode(), Name = Unique("คน"), Duty = duty, Hospital = hospital });
        await AddAsync(new EmployeeInput
        {
            Code = NewCode(), Name = Unique("คน"), Hospital = resignedHospital, ResignDate = Today.AddDays(-1),
        });

        var choices = await AsHrAsync((_, d) => d.GetChoicesAsync());

        Assert.Single(choices.Duties, d => d == duty);
        Assert.Single(choices.Hospitals, h => h == hospital);
        Assert.DoesNotContain(resignedHospital, choices.Hospitals); // only of employees still working, as before
    }

    [Fact]
    public async Task Saving_over_someone_elses_newer_save_reports_a_conflict()
    {
        var employee = await AddAsync(new EmployeeInput { Code = NewCode(), Name = "แก้ชนกัน" });

        await AsHrAsync((r, _) => r.SaveAsync(employee.ToInput() with { Telephone = "081" }));

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            AsHrAsync((r, _) => r.SaveAsync(employee.ToInput() with { Telephone = "089" })));
        Assert.Equal("081", (await AsHrAsync((_, d) => d.GetAsync(employee.Id)))!.Telephone);
    }

    [Fact]
    public async Task Search_finds_by_code_or_name_and_lists_working_employees_first()
    {
        var name = Unique("ค้นหา");
        var working = await AddAsync(new EmployeeInput { Code = NewCode(), Name = name });
        var resigned = await AddAsync(new EmployeeInput { Code = NewCode(), Name = name + " ออก", ResignDate = Today });

        var byName = await AsHrAsync((_, d) => d.SearchAsync(name));
        var byCode = await AsHrAsync((_, d) => d.SearchAsync(working.Code.ToLowerInvariant()));

        Assert.Equal([working.Id, resigned.Id], byName.Select(e => e.Id));
        Assert.Contains(byCode, e => e.Id == working.Id);
    }
}
