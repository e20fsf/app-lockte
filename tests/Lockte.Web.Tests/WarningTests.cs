using Lockte.Hr;
using Lockte.Org;
using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;
using Lockte.Web.Audit;

namespace Lockte.Web.Tests;

/// <summary>
/// ใบเตือน (#59, SCR-32, BR-023) and its report RPT-30 through the public services of module พนักงานและค่าแรง. Warnings
/// are dated in a year of their own (2081) so the report sees only this class's rows.
/// </summary>
[Collection(LocalDbCollection.Name)]
public sealed class WarningTests(LocalDbFixture db)
{
    private const int Year = 2081;
    private static int lastCode;

    /// <summary>"W" keeps clear of the other test classes' codes (T-EMPLO.ID is Text(4)).</summary>
    private static string NewCode() => $"W{Interlocked.Increment(ref lastCode):D3}";

    private static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..6];

    private static DateOnly Day(int month, int day) => new(Year, month, day);

    private async Task<T> AsHrAsync<T>(Func<TestScope, Task<T>> action)
    {
        await using var scope = db.Scope("hr1");
        return await action(scope);
    }

    private static void Ok(ServiceResult result) => Assert.True(result.Succeeded, string.Join(", ", result.Errors));

    private async Task<DepartmentDto> NewDepartmentAsync()
    {
        var sectionId = (await AsHrAsync(s => s.Get<OrgRegistry>().SaveSectionAsync(new SectionInput(null, Unique("ฝ่าย"), 1, null)))).Id!.Value;
        var id = (await AsHrAsync(s => s.Get<OrgRegistry>().SaveDepartmentAsync(new DepartmentInput(
            null, Unique("W"), Unique("แผนก"), "W", 9, sectionId, null, [], null)))).Id!.Value;
        return (await AsHrAsync(s => s.Get<OrgDirectory>().GetDepartmentAsync(id)))!;
    }

    private async Task<EmployeeDto> NewEmployeeAsync(DepartmentDto? department = null, DateOnly? resign = null, string? code = null)
    {
        var result = await AsHrAsync(s => s.Get<EmployeeRegistry>().SaveAsync(new EmployeeInput
        {
            Code = code ?? NewCode(), Name = "นาย ทดสอบ " + Guid.NewGuid().ToString("N")[..4], DepartmentId = department?.Id,
            SectionId = department?.SectionId, StartDate = new DateOnly(2020, 1, 1), ResignDate = resign,
        }));
        Ok(result);
        return (await AsHrAsync(s => s.Get<EmployeeDirectory>().GetAsync(result.Id!.Value)))!;
    }

    private Task<ServiceResult> SaveAsync(WarningInput input) => AsHrAsync(s => s.Get<WarningRegistry>().SaveAsync(input));

    private async Task<int> AddAsync(EmployeeDto employee, DateOnly date, string? level = WarningLevels.Verbal, string? remark = "มาสายซ้ำ")
    {
        var result = await SaveAsync(new WarningInput(null, date, employee.Id, level, remark, null));
        Ok(result);
        return result.Id!.Value;
    }

    private Task<IReadOnlyList<WarningDto>> ListAsync(EmployeeDto employee)
        => AsHrAsync(s => s.Get<WarningDirectory>().ListOfEmployeeAsync(employee.Id));

    [Fact] // SCR-32: วันที่ รหัส ระดับ รายละเอียด — ชื่อและแผนกมาจากพนักงาน, ชื่อระดับจาก T-ชื่อใบเตือน
    public async Task A_warning_is_saved_with_the_employee_department_and_level_name()
    {
        var department = await NewDepartmentAsync();
        var employee = await NewEmployeeAsync(department);

        await AddAsync(employee, Day(3, 2), WarningLevels.Written, "  ทะเลาะวิวาท  ");

        var row = Assert.Single(await ListAsync(employee));
        Assert.Equal((Day(3, 2), employee.Code, department.Id, department.Name), (row.Date, row.EmployeeCode, row.DepartmentId, row.DepartmentName));
        Assert.Equal(("2", "ใบเตือนเป็นหนังสือ", "ทะเลาะวิวาท"), (row.Level, row.LevelName, row.Remark));
    }

    [Theory] // a blank row is never saved; ระดับ only from T-ชื่อใบเตือน (or blank, as the legacy allowed — DQ-9)
    [InlineData(false, true, "1", null, "กรุณากรอกวันที่")]
    [InlineData(true, false, "1", null, "กรุณาเลือกพนักงาน")]
    [InlineData(true, true, "5", null, "ไม่พบระดับใบเตือนนี้")]
    [InlineData(true, true, null, 256, "รายละเอียดยาวได้ไม่เกิน 255 ตัว")]
    public async Task A_warning_needs_a_date_an_employee_and_a_known_level(bool date, bool employee, string? level, int? remarkLength, string error)
    {
        var someone = await NewEmployeeAsync();

        var result = await SaveAsync(new WarningInput(null, date ? Day(1, 5) : null, employee ? someone.Id : null, level,
            remarkLength is { } n ? new string('ก', n) : null, null));

        Assert.Equal([error], result.Errors);
        Assert.Empty(await ListAsync(someone));
    }

    [Fact] // DQ-9: ระดับว่างบันทึกได้
    public async Task A_warning_without_a_level_is_saved()
    {
        var employee = await NewEmployeeAsync();

        await AddAsync(employee, Day(1, 6), level: null);

        Assert.Null(Assert.Single(await ListAsync(employee)).LevelName);
    }

    [Fact] // DEPART is copied when the employee is chosen (ID_AfterUpdate): a move later does not rewrite old warnings
    public async Task An_edit_keeps_the_department_the_warning_was_given_in()
    {
        var before = await NewDepartmentAsync();
        var after = await NewDepartmentAsync();
        var employee = await NewEmployeeAsync(before);
        await AddAsync(employee, Day(2, 1));
        Ok(await AsHrAsync(s => s.Get<EmployeeRegistry>().SaveAsync(employee.ToInput() with { DepartmentId = after.Id, SectionId = after.SectionId })));
        var row = Assert.Single(await ListAsync(employee));

        Ok(await SaveAsync(new WarningInput(row.Id, row.Date, row.EmployeeId, "3", "แก้แล้ว", row.RowVersion)));

        var edited = Assert.Single(await ListAsync(employee));
        Assert.Equal((before.Id, "3", "แก้แล้ว"), (edited.DepartmentId, edited.Level, edited.Remark));
        await AddAsync(employee, Day(2, 2));
        Assert.Equal(after.Id, (await ListAsync(employee))[1].DepartmentId);
    }

    [Fact] // edits and deletes carry the rowversion; each change is kept in the history (#39)
    public async Task Edits_and_deletes_check_the_row_version_and_are_kept_in_the_history()
    {
        var employee = await NewEmployeeAsync();
        await AddAsync(employee, Day(4, 1));
        var row = Assert.Single(await ListAsync(employee));
        Ok(await SaveAsync(new WarningInput(row.Id, row.Date, row.EmployeeId, "2", row.Remark, row.RowVersion)));

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => SaveAsync(new WarningInput(row.Id, row.Date, row.EmployeeId, "4", null, row.RowVersion)));
        var current = Assert.Single(await ListAsync(employee));
        Ok(await AsHrAsync(s => s.Get<WarningRegistry>().DeleteAsync(current.Id, current.RowVersion)));
        Ok(await AsHrAsync(s => s.Get<WarningRegistry>().DeleteAsync(current.Id, current.RowVersion))); // already gone

        Assert.Empty(await ListAsync(employee));
        await using var scope = db.Scope();
        var history = await scope.Get<HistoryService>().SearchAsync(new(Table: WarningRegistry.WarningTable, RowKey: $"{Year}-04-01 {employee.Code}"));
        Assert.Contains(history, h => h.Action == HistoryActions.Added && h.UserName == "hr1");
        Assert.Contains(history, h => h.Action == HistoryActions.Modified);
        Assert.Contains(history, h => h.Action == HistoryActions.Deleted);
    }

    [Fact] // RPT-30 (คิวรี ใบเตือน1): คนที่ยังไม่ลาออก, เรียง รหัส แล้ว วันที่, เฉพาะช่วงวันที่ที่เลือก
    public async Task The_report_lists_warnings_of_working_employees_by_code_then_date_within_the_dates()
    {
        var department = await NewDepartmentAsync();
        var second = await NewEmployeeAsync(department, code: "W902");
        var first = await NewEmployeeAsync(department, code: "W901");
        var resigned = await NewEmployeeAsync(department, resign: Day(1, 31), code: "W903");
        await AddAsync(second, Day(5, 1), WarningLevels.Verbal, "ข้อ 1");
        await AddAsync(first, Day(6, 1), "4", "ข้อ 3");
        await AddAsync(first, Day(5, 20), WarningLevels.Written, "ข้อ 2");
        await AddAsync(resigned, Day(5, 2));
        await AddAsync(first, Day(12, 31).AddDays(1)); // next year
        await AddAsync(first, Day(4, 30)); // before the dates

        var report = await AsHrAsync(s => s.Get<WarningDirectory>().GetReportAsync(Day(5, 1), Day(12, 31)));

        Assert.Equal(
            [
                new WarningReportLine(Day(5, 20), "W901", first.Name, department.Name, "ใบเตือนเป็นหนังสือ", "ข้อ 2"),
                new WarningReportLine(Day(6, 1), "W901", first.Name, department.Name, "เลิกจ้าง", "ข้อ 3"),
                new WarningReportLine(Day(5, 1), "W902", second.Name, department.Name, "ใบเตือนด้วยวาจา", "ข้อ 1"),
            ],
            report.Lines);
    }
}
