using Lockte.Hr;
using Lockte.Org;
using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;
using Lockte.Web.Audit;

namespace Lockte.Web.Tests;

/// <summary>
/// ไม่มาทำงาน + สร้างเวลาปกติ (#48, SCR-10/11, BR-010, BR-011, BR-061, AC-11) through the public services of module
/// พนักงานและค่าแรง, in the order HR works a day: บันทึกไม่มาทำงาน → สร้างเวลาปกติ.
/// </summary>
[Collection(LocalDbCollection.Name)]
public sealed class DailyTimeTests(LocalDbFixture db)
{
    private static int lastCode;

    /// <summary>A counter (letter + 3 digits, T-EMPLO.ID is Text(4)); "N" keeps clear of the other test classes' codes.</summary>
    private static string NewCode() => $"N{Interlocked.Increment(ref lastCode):D3}";

    /// <summary>Tests share one database: each test works on its own far-away day.</summary>
    private static DateOnly NewDay() => new DateOnly(Random.Shared.Next(3000, 9000), 1, 1).AddDays(Random.Shared.Next(0, 360));

    private async Task<T> AsHrAsync<T>(Func<TestScope, Task<T>> action)
    {
        await using var scope = db.Scope("hr1");
        return await action(scope);
    }

    private Task<ShiftDto> ShiftAsync(string code) => AsHrAsync(async s => (await s.Get<OrgDirectory>().FindShiftByCodeAsync(code))!);

    private async Task<EmployeeDto> NewEmployeeAsync(string? shift = "A", DateOnly? resigned = null)
    {
        int? shiftId = shift is null ? null : (await ShiftAsync(shift)).Id;
        var result = await AsHrAsync(s => s.Get<EmployeeRegistry>().SaveAsync(new EmployeeInput
        {
            Code = NewCode(), Name = "นาย ทดสอบ " + Guid.NewGuid().ToString("N")[..4], ShiftId = shiftId, ResignDate = resigned,
        }));
        Assert.True(result.Succeeded, string.Join(", ", result.Errors));
        return (await AsHrAsync(s => s.Get<EmployeeDirectory>().GetAsync(result.Id!.Value)))!;
    }

    private async Task RecordAbsentAsync(DateOnly date, EmployeeDto employee, TimeOnly? workIn = null, string? remark = null)
    {
        var result = await AsHrAsync(s => s.Get<AbsenceRegistry>().SaveAsync(new AbsenceInput(null, date, employee.Id, workIn, remark, null)));
        Assert.True(result.Succeeded, string.Join(", ", result.Errors));
    }

    private Task<ServiceResult> GenerateAsync(DateOnly date) => AsHrAsync(s => s.Get<WorkTimeRegistry>().GenerateNormalTimeAsync(date));

    private Task<IReadOnlyList<WorkTimeDto>> WorkTimesAsync(DateOnly date) => AsHrAsync(s => s.Get<WorkTimeDirectory>().ListAsync(date));

    /// <summary>พนักงานที่ทำงานอยู่ with a shift — whom BR-010 gives เวลาปกติ (every test's employees, the database is shared).</summary>
    private async Task<int> WorkingWithShiftCountAsync()
    {
        var working = (await AsHrAsync(s => s.Get<EmployeeDirectory>().SearchAsync())).Where(e => e.IsWorking).ToList();
        var count = 0;
        foreach (var e in working)
            if ((await AsHrAsync(s => s.Get<EmployeeDirectory>().GetAsync(e.Id)))!.ShiftId is not null) count++;
        return count;
    }

    [Fact] // AC-11
    public async Task Three_absent_leave_every_other_working_employee_one_normal_time_row_at_their_shift()
    {
        var day = NewDay();
        var present = await NewEmployeeAsync("A");
        var night = await NewEmployeeAsync("B");
        var absent = new[] { await NewEmployeeAsync(), await NewEmployeeAsync(), await NewEmployeeAsync("B") };
        foreach (var e in absent) await RecordAbsentAsync(day, e);
        var expected = await WorkingWithShiftCountAsync() - 3;

        var result = await GenerateAsync(day);

        Assert.True(result.Succeeded, string.Join(", ", result.Errors));
        var rows = await WorkTimesAsync(day);
        Assert.Equal(expected, rows.Count);
        Assert.All(rows, r => Assert.Equal(WorkTypes.Normal, r.Type));
        Assert.Equal(expected, rows.Select(r => r.EmployeeId).Distinct().Count());
        Assert.DoesNotContain(rows, r => absent.Any(a => a.Id == r.EmployeeId));

        var a = await ShiftAsync("A");
        var b = await ShiftAsync("B");
        var dayRow = Assert.Single(rows, r => r.EmployeeId == present.Id);
        Assert.Equal((present.Code, a.WorkIn, a.WorkOut, a.RestMinutes), (dayRow.EmployeeCode, dayRow.WorkIn, dayRow.WorkOut, dayRow.RestMinutes));
        var nightRow = Assert.Single(rows, r => r.EmployeeId == night.Id);
        Assert.Equal((b.WorkIn, b.WorkOut), (nightRow.WorkIn, nightRow.WorkOut));
        Assert.True(nightRow.CrossesMidnight); // B 20:10–05:10
        Assert.Null(dayRow.Late);
    }

    [Fact] // BR-010: only RESIGN blank and a shift that is in T-SHIFT (INNER JOIN)
    public async Task Resigned_employees_and_employees_without_a_shift_get_no_normal_time()
    {
        var day = NewDay();
        var resigned = await NewEmployeeAsync("A", resigned: new DateOnly(2020, 1, 31));
        var noShift = await NewEmployeeAsync(shift: null);

        await GenerateAsync(day);

        var rows = await WorkTimesAsync(day);
        Assert.NotEmpty(rows);
        Assert.DoesNotContain(rows, r => r.EmployeeId == resigned.Id || r.EmployeeId == noShift.Id);
    }

    [Fact] // BR-010 ⚠ kept (#41): no check that the day was already made — a second press adds the rows again
    public async Task Generating_the_same_day_twice_adds_the_rows_again()
    {
        var day = NewDay();
        var employee = await NewEmployeeAsync();

        var first = await GenerateAsync(day);
        var once = (await WorkTimesAsync(day)).Count;
        var second = await GenerateAsync(day);

        Assert.True(first.Succeeded && second.Succeeded);
        Assert.Equal(2 * once, (await WorkTimesAsync(day)).Count);
        Assert.Equal(2, (await WorkTimesAsync(day)).Count(r => r.EmployeeId == employee.Id));
    }

    [Fact] // BR-010 step 2 deletes the day's rows of everyone in NO_WORK, also rows made before the absence was recorded
    public async Task Generating_again_after_recording_an_absence_removes_that_persons_rows_of_the_day()
    {
        var day = NewDay();
        var late = await NewEmployeeAsync();
        var other = await NewEmployeeAsync();
        await GenerateAsync(day);

        await RecordAbsentAsync(day, late);
        await GenerateAsync(day);

        var rows = await WorkTimesAsync(day);
        Assert.DoesNotContain(rows, r => r.EmployeeId == late.Id);
        Assert.Equal(2, rows.Count(r => r.EmployeeId == other.Id));
    }

    [Fact] // BR-010 only looks at the day: an absence on another day does not stop the row
    public async Task An_absence_on_another_day_does_not_stop_the_normal_time()
    {
        var day = NewDay();
        var employee = await NewEmployeeAsync();
        await RecordAbsentAsync(day.AddDays(-1), employee);

        await GenerateAsync(day);

        Assert.Single(await WorkTimesAsync(day), r => r.EmployeeId == employee.Id);
    }

    [Fact] // #41: งานกลุ่มบันทึกเป็นเหตุการณ์เดียว
    public async Task Generating_is_one_history_event_not_a_row_per_employee()
    {
        var day = NewDay();
        var employee = await NewEmployeeAsync();

        await GenerateAsync(day);

        var rowCount = (await WorkTimesAsync(day)).Count;
        var history = await HistoryAsync(new(Table: WorkTimeRegistry.WorkTimeTable));
        Assert.DoesNotContain(history, h => h.Kind != HistoryKind.Event && h.RowKey?.Contains(employee.Code) == true);
        var job = Assert.Single(history, h => h.Kind == HistoryKind.Event && h.Detail?.Contains(Iso(day)) == true);
        Assert.Equal((WorkTimeRegistry.GenerateAction, (int?)rowCount, "hr1"), (job.Action, job.RowCount, job.UserName));
    }

    [Fact] // BR-011
    public async Task Absences_are_listed_by_day_and_need_a_date_and_an_employee()
    {
        var day = NewDay();
        var employee = await NewEmployeeAsync();
        await RecordAbsentAsync(day, employee, remark: "  ป่วย  ");

        var noDate = await AsHrAsync(s => s.Get<AbsenceRegistry>().SaveAsync(new AbsenceInput(null, null, employee.Id, null, null, null)));
        var noEmployee = await AsHrAsync(s => s.Get<AbsenceRegistry>().SaveAsync(new AbsenceInput(null, day, null, null, null, null)));
        var unknown = await AsHrAsync(s => s.Get<AbsenceRegistry>().SaveAsync(new AbsenceInput(null, day, -1, null, null, null)));
        var twice = await AsHrAsync(s => s.Get<AbsenceRegistry>().SaveAsync(new AbsenceInput(null, day, employee.Id, null, null, null)));

        Assert.Equal(["กรุณากรอกวันที่"], noDate.Errors);
        Assert.Equal(["กรุณาเลือกพนักงาน"], noEmployee.Errors);
        Assert.Equal(["ไม่พบพนักงานที่เลือก"], unknown.Errors);
        Assert.Single(twice.Errors, e => e.Contains(employee.Code));
        var row = Assert.Single(await AsHrAsync(s => s.Get<AbsenceDirectory>().ListAsync(day)));
        Assert.Equal((employee.Id, employee.Code, employee.Name, "ป่วย", AbsenceRegistry.DefaultWorkIn),
            (row.EmployeeId, row.EmployeeCode, row.EmployeeName, row.Remark, row.WorkIn));
    }

    [Fact]
    public async Task Absences_are_edited_and_deleted_through_the_rowversion_the_screen_loaded()
    {
        var day = NewDay();
        var (first, second) = (await NewEmployeeAsync(), await NewEmployeeAsync());
        await RecordAbsentAsync(day, first);
        var loaded = Assert.Single(await AsHrAsync(s => s.Get<AbsenceDirectory>().ListAsync(day)));

        var edited = await AsHrAsync(s => s.Get<AbsenceRegistry>().SaveAsync(
            new AbsenceInput(loaded.Id, day, second.Id, new TimeOnly(20, 10), "ลากิจ", loaded.RowVersion)));

        Assert.True(edited.Succeeded, string.Join(", ", edited.Errors));
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            AsHrAsync(s => s.Get<AbsenceRegistry>().DeleteAsync(loaded.Id, loaded.RowVersion)));
        var now = Assert.Single(await AsHrAsync(s => s.Get<AbsenceDirectory>().ListAsync(day)));
        Assert.Equal((second.Id, new TimeOnly(20, 10), "ลากิจ"), (now.EmployeeId, now.WorkIn, now.Remark));

        await AsHrAsync(s => s.Get<AbsenceRegistry>().DeleteAsync(now.Id, now.RowVersion));
        Assert.Empty(await AsHrAsync(s => s.Get<AbsenceDirectory>().ListAsync(day)));
    }

    [Fact] // BR-061, RPT-17: the day's day shift (W_IN < 17:00) and yesterday's night shift (W_IN > 17:00)
    public async Task The_daily_report_covers_the_day_shift_and_the_night_shift_that_started_the_day_before()
    {
        var day = NewDay();
        var (dayShift, lastNight, tonight, yesterday, atFive) =
            (await NewEmployeeAsync(), await NewEmployeeAsync("B"), await NewEmployeeAsync("B"), await NewEmployeeAsync(), await NewEmployeeAsync("F"));
        await RecordAbsentAsync(day, dayShift, new TimeOnly(8, 0), "ป่วย");
        await RecordAbsentAsync(day.AddDays(-1), lastNight, new TimeOnly(20, 10));
        await RecordAbsentAsync(day, tonight, new TimeOnly(20, 10));
        await RecordAbsentAsync(day.AddDays(-1), yesterday, new TimeOnly(8, 0));
        await RecordAbsentAsync(day, atFive, new TimeOnly(17, 0)); // neither < nor > 17:00

        var report = await AsHrAsync(s => s.Get<AbsenceDirectory>().GetDailyReportAsync(day));

        Assert.Equal([(day.AddDays(-1), lastNight.Code), (day, dayShift.Code)], report.Lines.Select(l => (l.Date, l.EmployeeCode)));
        Assert.Equal("ป่วย", report.Lines[1].Remark);
        Assert.Equal(dayShift.Name, report.Lines[1].EmployeeName);
        var working = (await AsHrAsync(s => s.Get<EmployeeDirectory>().SearchAsync())).Count(e => e.IsWorking);
        Assert.Equal((working, 2, working - 2), (report.WorkingEmployees, report.Absent, report.Present));
        Assert.Equal(2.0 / working * 100, report.AbsentPercent, 10);
    }

    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    private async Task<IReadOnlyList<HistoryRow>> HistoryAsync(HistoryFilter filter)
    {
        await using var scope = db.Scope();
        return await scope.Get<HistoryService>().SearchAsync(filter);
    }
}
