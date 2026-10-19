using Lockte.Hr;
using Lockte.Org;
using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;
using Lockte.Web.Audit;

namespace Lockte.Web.Tests;

/// <summary>
/// บันทึก/แก้/ลบเวลารายคน (#49, SCR-12/13, BR-013, BR-015, BR-001) through the public services of module พนักงานและค่าแรง.
/// </summary>
[Collection(LocalDbCollection.Name)]
public sealed class TimeEntryTests(LocalDbFixture db)
{
    private static int lastCode;

    /// <summary>"K" keeps clear of the other test classes' codes (T-EMPLO.ID is Text(4)).</summary>
    private static string NewCode() => $"K{Interlocked.Increment(ref lastCode):D3}";

    /// <summary>
    /// Tests share one database: each test works on its own far-away days, after the years DailyTimeTests uses, as the
    /// ลบทั้งวัน tests delete every employee's rows of their days.
    /// </summary>
    private static DateOnly NewDay() => new DateOnly(Random.Shared.Next(9100, 9990), 1, 1).AddDays(Random.Shared.Next(0, 350));

    private async Task<T> AsHrAsync<T>(Func<TestScope, Task<T>> action)
    {
        await using var scope = db.Scope("hr1");
        return await action(scope);
    }

    private async Task<EmployeeDto> NewEmployeeAsync()
    {
        var shift = await AsHrAsync(async s => (await s.Get<OrgDirectory>().FindShiftByCodeAsync("A"))!);
        var result = await AsHrAsync(s => s.Get<EmployeeRegistry>().SaveAsync(new EmployeeInput
        {
            Code = NewCode(), Name = "นาย ทดสอบ " + Guid.NewGuid().ToString("N")[..4], ShiftId = shift.Id,
        }));
        Assert.True(result.Succeeded, string.Join(", ", result.Errors));
        return (await AsHrAsync(s => s.Get<EmployeeDirectory>().GetAsync(result.Id!.Value)))!;
    }

    private static WorkTimeInput Row(DateOnly date, EmployeeDto employee, string type = "O1", string workIn = "17:20", string workOut = "20:10",
        int rest = 0) => new(null, date, employee.Id, type, TimeOnly.Parse(workIn), TimeOnly.Parse(workOut), rest, null, null, null);

    private Task<ServiceResult> SaveAsync(WorkTimeInput input) => AsHrAsync(s => s.Get<WorkTimeRegistry>().SaveAsync(input));

    private async Task AddAsync(WorkTimeInput input)
    {
        var result = await SaveAsync(input);
        Assert.True(result.Succeeded, string.Join(", ", result.Errors));
    }

    private Task<IReadOnlyList<WorkTimeDto>> DayAsync(DateOnly date) => AsHrAsync(s => s.Get<WorkTimeDirectory>().ListAsync(date));

    private Task<IReadOnlyList<WorkTimeDto>> OfEmployeeAsync(EmployeeDto employee, DateOnly from, DateOnly to)
        => AsHrAsync(s => s.Get<WorkTimeDirectory>().ListOfEmployeeAsync(employee.Id, from, to));

    [Fact] // SCR-12, BR-013: OT/วันอาทิตย์/วันหยุด go straight into เวลาทำงาน
    public async Task An_overtime_row_is_saved_with_its_WORK_T_in_hours()
    {
        var day = NewDay();
        var employee = await NewEmployeeAsync();

        var result = await SaveAsync(Row(day, employee, "O1", "17:20", "20:10") with { Mark = " y " });

        Assert.True(result.Succeeded, string.Join(", ", result.Errors));
        var row = Assert.Single(await DayAsync(day));
        Assert.Equal(result.Id, row.Id);
        Assert.Equal((employee.Id, employee.Code, "O1", (TimeOnly?)new TimeOnly(17, 20), (TimeOnly?)new TimeOnly(20, 10), 0, "Y"),
            (row.EmployeeId, row.EmployeeCode, row.Type, row.WorkIn, row.WorkOut, row.RestMinutes, row.Mark));
        Assert.Equal(17.0 / 6, row.WorkT!.Value, 12);
    }

    [Fact] // BR-001 #5: กะ B 20:10–05:10 keeps W_OUT as the next day
    public async Task A_night_shift_row_crosses_midnight_and_counts_one_day()
    {
        var day = NewDay();
        var employee = await NewEmployeeAsync();

        await AddAsync(Row(day, employee, "W", "20:10", "05:10", 60));

        var row = Assert.Single(await DayAsync(day));
        Assert.True(row.CrossesMidnight);
        Assert.Equal((8.0, 1.0), (row.Hours, row.WorkT));
    }

    [Fact] // BR-013 ⚠ kept: no check for repeats or against the holiday calendar
    public async Task The_same_row_can_be_entered_twice()
    {
        var day = NewDay();
        var employee = await NewEmployeeAsync();

        await AddAsync(Row(day, employee));
        await AddAsync(Row(day, employee));

        Assert.Equal(2, (await DayAsync(day)).Count);
    }

    [Fact] // BR-051 (a blank row is never saved) + the values BR-001 needs
    public async Task A_row_needs_a_date_an_employee_a_type_and_both_times()
    {
        var day = NewDay();
        var employee = await NewEmployeeAsync();
        var ok = Row(day, employee);

        Assert.Equal(["กรุณากรอกวันที่"], (await SaveAsync(ok with { Date = null })).Errors);
        Assert.Equal(["กรุณาเลือกพนักงาน"], (await SaveAsync(ok with { EmployeeId = null })).Errors);
        Assert.Equal(["ไม่พบพนักงานที่เลือก"], (await SaveAsync(ok with { EmployeeId = -1 })).Errors);
        Assert.Equal(["กรุณาเลือกประเภท"], (await SaveAsync(ok with { Type = " " })).Errors);
        Assert.Single((await SaveAsync(ok with { Type = "X" })).Errors, e => e.Contains("O1"));
        Assert.Equal(["กรุณากรอกเวลาเข้าและเวลาออก"], (await SaveAsync(ok with { WorkOut = null })).Errors);
        Assert.Equal(["นาทีพักต้องไม่ติดลบ"], (await SaveAsync(ok with { RestMinutes = -5 })).Errors);
        Assert.Single((await SaveAsync(ok with { Mark = "ABC" })).Errors, e => e.Contains("2"));
        Assert.Empty(await DayAsync(day));
    }

    [Fact] // SCR-13: แก้ทีละแถว with the rowversion the screen loaded; ลบทีละแถว
    public async Task A_row_is_edited_and_deleted_through_the_rowversion_the_screen_loaded()
    {
        var day = NewDay();
        var employee = await NewEmployeeAsync();
        await AddAsync(Row(day, employee, "W", "08:00", "17:00", 60));
        var loaded = Assert.Single(await DayAsync(day));

        var edited = await SaveAsync(new WorkTimeInput(loaded.Id, day, employee.Id, "W", new TimeOnly(8, 0), new TimeOnly(16, 30), 60,
            new TimeOnly(8, 25), null, loaded.RowVersion));

        Assert.True(edited.Succeeded, string.Join(", ", edited.Errors));
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            AsHrAsync(s => s.Get<WorkTimeRegistry>().DeleteAsync(loaded.Id, loaded.RowVersion)));
        var now = Assert.Single(await DayAsync(day));
        Assert.Equal((new TimeOnly(16, 30), new TimeOnly(8, 25), 0.9375), (now.WorkOut, now.Late, now.WorkT));

        await AsHrAsync(s => s.Get<WorkTimeRegistry>().DeleteAsync(now.Id, now.RowVersion));
        Assert.Empty(await DayAsync(day));
    }

    [Fact] // SCR-13 subform E_WORK1: the employee's rows of the period, by date
    public async Task The_rows_of_one_employee_are_listed_for_a_date_range_by_date()
    {
        var day = NewDay();
        var (employee, other) = (await NewEmployeeAsync(), await NewEmployeeAsync());
        await AddAsync(Row(day.AddDays(2), employee, "W", "08:00", "17:00", 60));
        await AddAsync(Row(day, employee, "O1"));
        await AddAsync(Row(day, employee, "W", "08:00", "17:00", 60));
        await AddAsync(Row(day.AddDays(5), employee, "W", "08:00", "17:00", 60));
        await AddAsync(Row(day, other, "W", "08:00", "17:00", 60));

        var rows = await OfEmployeeAsync(employee, day, day.AddDays(2));

        Assert.Equal([(day, "W"), (day, "O1"), (day.AddDays(2), "W")], rows.Select(r => (r.Date, r.Type)));
    }

    [Fact] // BR-015 #1: รายคน DAT + CODE, ลบเวลาปกติ → only that person's W of that day
    public async Task Deleting_one_persons_normal_time_leaves_their_overtime_other_days_and_other_people()
    {
        var day = NewDay();
        var (employee, other) = (await NewEmployeeAsync(), await NewEmployeeAsync());
        await AddAsync(Row(day, employee, "W", "08:00", "17:00", 60));
        await AddAsync(Row(day, employee, "W", "08:00", "17:00", 60)); // a repeat goes too
        await AddAsync(Row(day, employee, "O1"));
        await AddAsync(Row(day, employee, "S", "08:00", "17:00", 60));
        await AddAsync(Row(day.AddDays(1), employee, "W", "08:00", "17:00", 60));
        await AddAsync(Row(day, other, "W", "08:00", "17:00", 60));

        var result = await AsHrAsync(s => s.Get<WorkTimeRegistry>().DeleteOfEmployeeAsync(day, employee.Id, WorkTypes.Normal));

        Assert.Equal((true, (int?)2), (result.Succeeded, result.Id));
        Assert.Equal(["O1", "S"], (await OfEmployeeAsync(employee, day, day)).Select(r => r.Type));
        Assert.Single(await OfEmployeeAsync(employee, day.AddDays(1), day.AddDays(1)));
        Assert.Single(await OfEmployeeAsync(other, day, day));
    }

    [Fact] // BR-015 #2: ทั้งวัน DAT–DAT1, ลบ OT → O1 of everyone on those days
    public async Task Deleting_overtime_for_a_date_range_removes_everyones_O1_of_those_days_only()
    {
        var day = NewDay();
        var (first, second) = (await NewEmployeeAsync(), await NewEmployeeAsync());
        await AddAsync(Row(day, first, "O1"));
        await AddAsync(Row(day.AddDays(1), second, "O1"));
        await AddAsync(Row(day, first, "W", "08:00", "17:00", 60));
        await AddAsync(Row(day, second, "O2"));
        await AddAsync(Row(day.AddDays(2), first, "O1"));

        var result = await AsHrAsync(s => s.Get<WorkTimeRegistry>().DeleteRangeAsync(day, day.AddDays(1), WorkTypes.Overtime));

        Assert.Equal((true, (int?)2), (result.Succeeded, result.Id));
        Assert.Equal(["W", "O2"], (await DayAsync(day)).OrderBy(r => r.Type == "O2").Select(r => r.Type));
        Assert.Empty(await DayAsync(day.AddDays(1)));
        Assert.Single(await DayAsync(day.AddDays(2)));
    }

    [Fact] // BR-015 ⚠ kept: only W and O1 have delete buttons; S, O2, H, O3 go row by row
    public async Task The_delete_buttons_only_take_W_or_O1_and_a_range_in_order()
    {
        var day = NewDay();
        var employee = await NewEmployeeAsync();
        await AddAsync(Row(day, employee, "S", "08:00", "17:00", 60));

        var byPerson = await AsHrAsync(s => s.Get<WorkTimeRegistry>().DeleteOfEmployeeAsync(day, employee.Id, "S"));
        var byRange = await AsHrAsync(s => s.Get<WorkTimeRegistry>().DeleteRangeAsync(day, day, "S"));
        var backwards = await AsHrAsync(s => s.Get<WorkTimeRegistry>().DeleteRangeAsync(day, day.AddDays(-1), WorkTypes.Normal));

        Assert.False(byPerson.Succeeded || byRange.Succeeded || backwards.Succeeded);
        Assert.Equal(["วันที่เริ่มต้องไม่หลังวันที่สิ้นสุด"], backwards.Errors);
        Assert.Single(await DayAsync(day));
    }

    [Fact] // #41: คีย์มือเข้าประวัติรายช่อง; ลบทั้งโรงงานเป็นงานกลุ่ม = เหตุการณ์เดียว
    public async Task Hand_entry_is_row_history_and_the_factory_wide_delete_is_one_event()
    {
        var day = NewDay();
        var employee = await NewEmployeeAsync();
        await AddAsync(Row(day, employee, "W", "08:00", "17:00", 60));

        var history = await HistoryAsync(new(Table: WorkTimeRegistry.WorkTimeTable, RowKey: employee.Code));
        Assert.Contains(history, h => h.Kind != HistoryKind.Event && h.RowKey!.Contains(employee.Code));

        await AsHrAsync(s => s.Get<WorkTimeRegistry>().DeleteRangeAsync(day, day, WorkTypes.Normal));

        var all = await HistoryAsync(new(Table: WorkTimeRegistry.WorkTimeTable));
        var job = Assert.Single(all, h => h.Kind == HistoryKind.Event && h.Action == WorkTimeRegistry.DeleteRangeAction
            && h.Detail?.Contains(day.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)) == true);
        Assert.Equal(((int?)1, "hr1"), (job.RowCount, job.UserName));
    }

    private async Task<IReadOnlyList<HistoryRow>> HistoryAsync(HistoryFilter filter)
    {
        await using var scope = db.Scope();
        return await scope.Get<HistoryService>().SearchAsync(filter);
    }
}
