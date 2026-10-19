using Lockte.Hr;
using Lockte.Hr.Data;
using Lockte.Org;
using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;
using Lockte.Web.Audit;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Web.Tests;

/// <summary>
/// ตั้งงวดค่าแรงและคำนวณเวลาทำงาน (#52: SCR-40, BR-001, BR-002, BR-007, RPT-01 data) through the public services of
/// module พนักงานและค่าแรง, in the order HR works: วันหยุด → เวลาทำงาน/การลา → ตั้งงวด → ตรวจ → คำนวณ.
/// </summary>
[Collection(LocalDbCollection.Name)]
public sealed class PayPeriodTests(LocalDbFixture db)
{
    private static int lastCode;
    private static int lastYear = 1100;

    /// <summary>"P" keeps clear of the other test classes' codes (T-EMPLO.ID is Text(4)).</summary>
    private static string NewCode() => $"P{Interlocked.Increment(ref lastCode):D3}";

    /// <summary>
    /// Each test works in a year of its own, far before the years other tests use: a period counts every employee and
    /// every วันหยุด of its dates. Years only grow, so the newest period is always the current test's.
    /// </summary>
    private static int NewYear() => Interlocked.Increment(ref lastYear);

    private static DateOnly D(int year, int month, int day) => new(year, month, day);

    private async Task<T> AsHrAsync<T>(Func<TestScope, Task<T>> action)
    {
        await using var scope = db.Scope("hr1");
        return await action(scope);
    }

    private static void Ok(ServiceResult result) => Assert.True(result.Succeeded, string.Join(", ", result.Errors));

    private async Task<EmployeeDto> NewEmployeeAsync(DateOnly? start, DateOnly? resign = null, int? type = EmployeeTypes.Daily,
        string? code = null)
    {
        var shift = await AsHrAsync(async s => (await s.Get<OrgDirectory>().FindShiftByCodeAsync("A"))!);
        var result = await AsHrAsync(s => s.Get<EmployeeRegistry>().SaveAsync(new EmployeeInput
        {
            Code = code ?? NewCode(), Name = "นาย ทดสอบ " + Guid.NewGuid().ToString("N")[..4], ShiftId = shift.Id,
            StartDate = start, ResignDate = resign, EmployeeType = type,
        }));
        Ok(result);
        return (await AsHrAsync(s => s.Get<EmployeeDirectory>().GetAsync(result.Id!.Value)))!;
    }

    private async Task WorkAsync(EmployeeDto employee, DateOnly date, string type = "W", string workIn = "08:00", string workOut = "17:00",
        int rest = 60, string? late = null)
        => Ok(await AsHrAsync(s => s.Get<WorkTimeRegistry>().SaveAsync(new WorkTimeInput(null, date, employee.Id, type,
            TimeOnly.Parse(workIn), TimeOnly.Parse(workOut), rest, late is null ? null : TimeOnly.Parse(late), null, null))));

    private async Task LeaveAsync(EmployeeDto employee, DateOnly from, DateOnly to, double days, bool paid)
    {
        var result = await AsHrAsync(s => s.Get<LeaveRegistry>().SaveAsync(new LeaveInput(null, employee.Id, from, from, to,
            null, null, days, "1", paid, null, 300, null)));
        Ok(result.Save);
    }

    private async Task WeeklyHolidayAsync(DateOnly date)
        => Ok(await AsHrAsync(s => s.Get<CalendarRegistry>().SaveWeeklyHolidayAsync(new WeeklyHolidayInput(null, date, "วันอาทิตย์", null))));

    private async Task PublicHolidayAsync(DateOnly date)
        => Ok(await AsHrAsync(s => s.Get<CalendarRegistry>().SavePublicHolidayAsync(new PublicHolidayInput(null, date, "วันหยุดทดสอบ", null))));

    private Task<ServiceResult> SavePeriodAsync(PayPeriodInput input) => AsHrAsync(s => s.Get<PayPeriodRegistry>().SaveAsync(input));

    private async Task<PayPeriodDto> NewPeriodAsync(DateOnly from, DateOnly to, DateOnly? pay = null)
    {
        var result = await SavePeriodAsync(new PayPeriodInput(null, from, to, pay ?? to.AddDays(5), null));
        Ok(result);
        return await GetPeriodAsync(result.Id!.Value);
    }

    private async Task<PayPeriodDto> GetPeriodAsync(int id) => (await AsHrAsync(s => s.Get<PayPeriodDirectory>().GetAsync(id)))!;

    private Task<ServiceResult> CalculateAsync(PayPeriodDto period)
        => AsHrAsync(s => s.Get<PayPeriodRegistry>().CalculateTimeAsync(period.Id));

    private async Task<IReadOnlyList<EmployeeTimeTotal>> CalculatedAsync(PayPeriodDto period)
    {
        Ok(await CalculateAsync(period));
        return await AsHrAsync(s => s.Get<PayPeriodDirectory>().GetTimeTotalsAsync(period.Id));
    }

    /// <summary>BR-002's example period: 27/08 – 11/09 (D = 16), วันอาทิตย์ 30/08 and 06/09, no วันหยุดประเพณี.</summary>
    private async Task<PayPeriodDto> ExamplePeriodAsync(int year)
    {
        await WeeklyHolidayAsync(D(year, 8, 30));
        await WeeklyHolidayAsync(D(year, 9, 6));
        return await NewPeriodAsync(D(year, 8, 27), D(year, 9, 11), D(year, 9, 16));
    }

    /// <summary>The 14 working days of the example period (all days but the two วันอาทิตย์).</summary>
    private static IEnumerable<DateOnly> WorkingDays(int year)
        => Enumerable.Range(0, 16).Select(i => D(year, 8, 27).AddDays(i)).Where(d => d != D(year, 8, 30) && d != D(year, 9, 6));

    [Fact] // SCR-40: จากวันที่ / ถึงวันที่ / วันที่จ่าย
    public async Task Setting_up_a_period_keeps_its_dates_and_starts_at_the_first_step()
    {
        var year = NewYear();

        var period = await NewPeriodAsync(D(year, 10, 12), D(year, 10, 26), D(year, 11, 1));

        Assert.Equal((D(year, 10, 12), D(year, 10, 26), D(year, 11, 1), 15, PayPeriodStep.Set),
            (period.From, period.To, period.PayDate, period.Days, period.CompletedStep));
    }

    [Fact] // #23 Q-04: ระบบเติมวันที่ให้ แก้เองได้
    public async Task The_default_dates_follow_the_latest_period()
    {
        var year = NewYear();
        var latest = await NewPeriodAsync(D(year, 10, 12), D(year, 10, 26), D(year, 11, 1));

        var next = await AsHrAsync(s => s.Get<PayPeriodDirectory>().DefaultDatesAsync());

        Assert.Equal(PayPeriodDates.After(latest.Dates), next);
        Assert.Equal(new PayPeriodDates(D(year, 10, 27), D(year, 11, 11), D(year, 11, 16)), next);
    }

    [Fact]
    public async Task A_period_needs_its_three_dates_in_order_and_its_own_start_day()
    {
        var year = NewYear();
        await NewPeriodAsync(D(year, 10, 12), D(year, 10, 26));

        var missing = await SavePeriodAsync(new PayPeriodInput(null, D(year, 11, 12), null, D(year, 12, 1), null));
        var reversed = await SavePeriodAsync(new PayPeriodInput(null, D(year, 11, 26), D(year, 11, 12), D(year, 12, 1), null));
        var sameStart = await SavePeriodAsync(new PayPeriodInput(null, D(year, 10, 12), D(year, 10, 20), D(year, 10, 25), null));

        Assert.Equal(["กรุณากรอกจากวันที่ ถึงวันที่ และวันที่จ่าย"], missing.Errors);
        Assert.Equal(["จากวันที่ต้องไม่หลังถึงวันที่"], reversed.Errors);
        Assert.Equal([$"มีงวดค่าแรงที่เริ่มวันที่ {ThaiDate.Format(D(year, 10, 12))} อยู่แล้ว"], sameStart.Errors);
    }

    [Fact] // #22: ล็อกไม่ให้ข้ามขั้น
    public async Task A_step_cannot_be_done_before_the_one_before_it()
    {
        var year = NewYear();
        var period = await NewPeriodAsync(D(year, 10, 12), D(year, 10, 26));

        var skipped = await AsHrAsync(s => s.Get<PayPeriodRegistry>().CompleteStepAsync(period.Id, PayPeriodStep.PayCalculated));
        Ok(await CalculateAsync(period));
        var inOrder = await AsHrAsync(s => s.Get<PayPeriodRegistry>().CompleteStepAsync(period.Id, PayPeriodStep.PayCalculated));

        Assert.Equal(["ต้องทำขั้น \"พิมพ์เวลาทำงาน\" ก่อน"], skipped.Errors);
        Ok(inOrder);
        Assert.Equal(PayPeriodStep.PayCalculated, (await GetPeriodAsync(period.Id)).CompletedStep);
    }

    [Fact] // BR-002 ตัวอย่าง 1–4
    public async Task Time_is_totalled_per_employee_as_the_legacy_example()
    {
        var year = NewYear();
        var period = await ExamplePeriodAsync(year);
        var before = D(year - 1, 1, 1);

        // 1: W 13 แถว WORK_T 1 + 1 แถว 0.9375, O1 2 แถว 2.8333 ชม., ลาได้ค่าแรง 1 วัน 28/08
        var one = await NewEmployeeAsync(before);
        var days = WorkingDays(year).ToList();
        foreach (var day in days.Take(13)) await WorkAsync(one, day);
        await WorkAsync(one, days[13], workOut: "16:30");
        await WorkAsync(one, days[0], "O1", "17:20", "20:10", 0);
        await WorkAsync(one, days[1], "O1", "17:20", "20:10", 0);
        await LeaveAsync(one, D(year, 8, 28), D(year, 8, 28), 1, paid: true);
        // 2: ลาไม่ได้ค่าแรง 0.5 วัน 01/09 and ลาได้ค่าแรง 2 วัน 10/09–12/09 คร่อมสิ้นงวด (Q-18: not counted)
        var two = await NewEmployeeAsync(before);
        await LeaveAsync(two, D(year, 9, 1), D(year, 9, 1), 0.5, paid: false);
        await LeaveAsync(two, D(year, 9, 10), D(year, 9, 12), 2, paid: true);
        // 3: เริ่มงาน 01/09 (START > FROM_DA)
        var three = await NewEmployeeAsync(D(year, 9, 1));
        await WorkAsync(three, D(year, 9, 2));
        // 4: ลาออก 03/09 รายเดือน (CLAS 1)
        var four = await NewEmployeeAsync(before, resign: D(year, 9, 3), type: EmployeeTypes.Monthly);

        var totals = (await CalculatedAsync(period)).ToDictionary(t => t.EmployeeId);

        var t1 = totals[one.Id];
        Assert.Equal((13.9375, 1.0, 0.0, 2, 0), (t1.Normal, t1.PaidLeave, t1.UnpaidLeave, t1.WeeklyHolidays, t1.PublicHolidays));
        Assert.Equal(17.0 / 3, t1.Overtime, 10);
        Assert.Equal(14.9375, t1.EntitledDays);
        Assert.Equal((0.0, 0.5, 2), (totals[two.Id].PaidLeave, totals[two.Id].UnpaidLeave, totals[two.Id].WeeklyHolidays));
        Assert.Equal((1.0, 0), (totals[three.Id].Normal, totals[three.Id].WeeklyHolidays));
        Assert.Equal((2, 1), (totals[four.Id].WeeklyHolidays, totals[four.Id].WeeklyHolidaysToResign));
        Assert.Equal(0, t1.WeeklyHolidaysToResign); // รายวัน, not resigned
    }

    [Fact] // BR-002: S, O2, H, O3 by TYPE in hours
    public async Task Each_type_of_work_has_its_own_total()
    {
        var year = NewYear();
        var period = await ExamplePeriodAsync(year);
        var employee = await NewEmployeeAsync(D(year - 1, 1, 1));
        await WorkAsync(employee, D(year, 8, 30), "S", "08:00", "17:00", 60);
        await WorkAsync(employee, D(year, 8, 30), "O2", "17:20", "19:20", 0);
        await WorkAsync(employee, D(year, 9, 6), "H", "08:00", "12:00", 0);
        await WorkAsync(employee, D(year, 9, 6), "O3", "13:00", "14:30", 0);

        var total = Assert.Single(await CalculatedAsync(period), t => t.EmployeeId == employee.Id);

        Assert.Equal((0.0, 8.0, 2.0, 4.0, 1.5), (total.Normal, total.Sunday, total.SundayOvertime, total.Holiday, total.HolidayOvertime));
    }

    [Fact] // BR-002: HOLI counts วันหยุดประเพณี from START, for whoever has not resigned by FROM_DA
    public async Task Public_holidays_count_from_the_start_date()
    {
        var year = NewYear();
        var period = await ExamplePeriodAsync(year);
        await PublicHolidayAsync(D(year, 8, 31));
        await PublicHolidayAsync(D(year, 9, 3));
        var before = await NewEmployeeAsync(D(year - 1, 1, 1));
        var newcomer = await NewEmployeeAsync(D(year, 9, 1));
        await WorkAsync(newcomer, D(year, 9, 2));
        var resigned = await NewEmployeeAsync(D(year - 1, 1, 1), resign: D(year, 8, 28));

        var totals = (await CalculatedAsync(period)).ToDictionary(t => t.EmployeeId);
        var report = (await AsHrAsync(s => s.Get<PayPeriodDirectory>().GetTimeReportAsync(period.Id)))!;

        Assert.Equal(2, totals[before.Id].PublicHolidays);
        Assert.Equal(1, totals[newcomer.Id].PublicHolidays);
        Assert.Equal((2, 2), (totals[resigned.Id].PublicHolidays, totals[resigned.Id].WeeklyHolidays)); // RESIGN > FROM_DA
        Assert.Equal((D(year, 8, 31), 2, 2, 12, 16),
            (report.FirstPublicHoliday, report.PublicHolidays, report.WeeklyHolidays, report.WorkDays, report.TotalDays));
    }

    [Fact] // BR-002 / BR-005: W+L+S+H+HOLI+SUN = 0 and รหัส LT* are not calculated
    public async Task Employees_without_time_and_LT_codes_are_left_out()
    {
        var year = NewYear();
        var period = await ExamplePeriodAsync(year);
        var gone = await NewEmployeeAsync(D(year - 2, 1, 1), resign: D(year - 1, 6, 30));
        var overtimeOnly = await NewEmployeeAsync(D(year, 9, 1));
        await WorkAsync(overtimeOnly, D(year, 9, 2), "O1", "17:20", "20:10", 0);
        var lt = await NewEmployeeAsync(D(year - 1, 1, 1), code: $"LT{Interlocked.Increment(ref lastCode) % 100:D2}");
        await WorkAsync(lt, D(year, 9, 2));
        var counted = await NewEmployeeAsync(D(year - 1, 1, 1));

        var totals = await CalculatedAsync(period);

        Assert.DoesNotContain(totals, t => t.EmployeeId == gone.Id || t.EmployeeId == overtimeOnly.Id || t.EmployeeId == lt.Id);
        Assert.Contains(totals, t => t.EmployeeId == counted.Id);
    }

    [Fact] // BR-007: ลบรายการซ้ำก่อนคำนวณ (DATE, W_IN, TYPE, ID) and (FROM_D, TO_D, FROM_T, ID)
    public async Task Repeated_rows_are_removed_before_calculating()
    {
        var year = NewYear();
        var period = await ExamplePeriodAsync(year);
        var employee = await NewEmployeeAsync(D(year - 1, 1, 1));
        var day = D(year, 9, 1);
        await WorkAsync(employee, day);
        await WorkAsync(employee, day);
        await WorkAsync(employee, day, "O1", "17:20", "20:10", 0);
        await WorkAsync(employee, day, "O1", "17:20", "19:20", 0); // another W_OUT, same key: also a repeat
        await LeaveAsync(employee, D(year, 9, 2), D(year, 9, 2), 1, paid: true);
        await LeaveAsync(employee, D(year, 9, 2), D(year, 9, 2), 1, paid: true);
        var other = D(year + 1000, 1, 1); // outside the period: left as it is
        await WorkAsync(employee, other);
        await WorkAsync(employee, other);

        var total = Assert.Single(await CalculatedAsync(period), t => t.EmployeeId == employee.Id);

        Assert.Equal((1.0, 1.0), (total.Normal, total.PaidLeave));
        var rows = await AsHrAsync(s => s.Get<WorkTimeDirectory>().ListOfEmployeeAsync(employee.Id, period.From, period.To));
        Assert.Equal(["O1", "W"], rows.Select(r => r.Type!).Order());
        Assert.Single(await AsHrAsync(s => s.Get<LeaveDirectory>().ListOfEmployeeAsync(employee.Id, year)));
        Assert.Equal(2, (await AsHrAsync(s => s.Get<WorkTimeDirectory>().ListOfEmployeeAsync(employee.Id, other, other))).Count);
    }

    [Fact] // Q-22: คำนวณซ้ำได้ ผลล่าสุดทับผลเดิม; ขั้นหลังจากนั้นต้องทำใหม่
    public async Task Recalculating_replaces_the_totals_and_reopens_the_later_steps()
    {
        var year = NewYear();
        var period = await ExamplePeriodAsync(year);
        var employee = await NewEmployeeAsync(D(year - 1, 1, 1));
        await WorkAsync(employee, D(year, 9, 1));
        await CalculatedAsync(period);
        Ok(await AsHrAsync(s => s.Get<PayPeriodRegistry>().CompleteStepAsync(period.Id, PayPeriodStep.PayCalculated)));
        await WorkAsync(employee, D(year, 9, 2));

        var total = Assert.Single(await CalculatedAsync(period), t => t.EmployeeId == employee.Id);

        Assert.Equal(2.0, total.Normal);
        var after = await GetPeriodAsync(period.Id);
        Assert.Equal((PayPeriodStep.TimeCalculated, "hr1"), (after.CompletedStep, after.TimeCalculatedBy));
    }

    [Fact]
    public async Task Changing_the_dates_after_calculating_goes_back_to_the_first_step()
    {
        var year = NewYear();
        var period = await ExamplePeriodAsync(year);
        var employee = await NewEmployeeAsync(D(year - 1, 1, 1));
        await WorkAsync(employee, D(year, 9, 1));
        await CalculatedAsync(period);

        Ok(await SavePeriodAsync(new PayPeriodInput(period.Id, period.From, period.To.AddDays(-1), period.PayDate,
            (await GetPeriodAsync(period.Id)).RowVersion)));

        Assert.Equal(PayPeriodStep.Set, (await GetPeriodAsync(period.Id)).CompletedStep);
        Assert.Empty(await AsHrAsync(s => s.Get<PayPeriodDirectory>().GetTimeTotalsAsync(period.Id)));
    }

    [Fact] // Only the pay date changes: the time totals stay good
    public async Task Changing_only_the_pay_date_keeps_the_step()
    {
        var year = NewYear();
        var period = await ExamplePeriodAsync(year);
        await CalculatedAsync(period);
        var loaded = await GetPeriodAsync(period.Id);

        Ok(await SavePeriodAsync(new PayPeriodInput(period.Id, period.From, period.To, period.PayDate.AddDays(1), loaded.RowVersion)));

        Assert.Equal(PayPeriodStep.TimeCalculated, (await GetPeriodAsync(period.Id)).CompletedStep);
    }

    [Fact] // #41: งานกลุ่มและผลคำนวณบันทึกเป็นเหตุการณ์เดียว
    public async Task Calculating_is_one_history_event()
    {
        var year = NewYear();
        var period = await ExamplePeriodAsync(year);
        var employee = await NewEmployeeAsync(D(year - 1, 1, 1));
        await WorkAsync(employee, D(year, 9, 1));
        await WorkAsync(employee, D(year, 9, 1));

        var result = await CalculateAsync(period);

        Ok(result);
        await using var scope = db.Scope();
        var history = await scope.Get<HistoryService>().SearchAsync(new HistoryFilter(Table: PayPeriodRegistry.PayPeriodTable));
        var job = Assert.Single(history, h => h.Kind == HistoryKind.Event && h.Detail?.Contains($"{year:D4}-08-27") == true);
        Assert.Equal((PayPeriodRegistry.CalculateTimeAction, (int?)result.Id, "hr1"), (job.Action, job.RowCount, job.UserName));
        Assert.Contains("\"ลบรายการซ้ำ\":1", job.Detail);
        Assert.DoesNotContain(history, h => h.Kind != HistoryKind.Event && h.RowKey?.Contains(employee.Code) == true);
    }

    [Fact] // #22: คำเตือนก่อนคำนวณ — วันที่ยังไม่บันทึกเวลา และมาสาย
    public async Task The_check_warns_of_working_days_without_time_and_of_late_arrivals()
    {
        var year = NewYear();
        var period = await ExamplePeriodAsync(year);
        await PublicHolidayAsync(D(year, 9, 3));
        var employee = await NewEmployeeAsync(D(year - 1, 1, 1));
        var missing = D(year, 9, 4);
        foreach (var day in WorkingDays(year).Where(d => d != missing && d != D(year, 9, 3))) await WorkAsync(employee, day);
        await WorkAsync(employee, D(year, 9, 1), late: "08:17");

        var check = await AsHrAsync(s => s.Get<PayPeriodDirectory>().CheckAsync(period.Id));

        Assert.Equal([missing], check.DaysWithoutTime);
        var late = Assert.Single(check.LateArrivals);
        Assert.Equal((D(year, 9, 1), employee.Code, new TimeOnly(8, 17)), (late.Date, late.EmployeeCode, late.Late));
        Assert.True(check.HasWarnings);
    }

    [Fact] // RPT-01: เรียง ID ↑, หัว วันอาทิตย์/วันหยุด/วันทำงาน/รวมทั้งหมด
    public async Task The_time_report_lists_the_calculated_employees_by_code()
    {
        var year = NewYear();
        var period = await ExamplePeriodAsync(year);
        var b = await NewEmployeeAsync(D(year - 1, 1, 1));
        var a = await NewEmployeeAsync(D(year - 1, 1, 1));
        await WorkAsync(a, D(year, 9, 1));
        await WorkAsync(b, D(year, 9, 1), "O1", "17:20", "20:10", 0);

        var before = await AsHrAsync(s => s.Get<PayPeriodDirectory>().GetTimeReportAsync(period.Id));
        await CalculatedAsync(period);
        var report = (await AsHrAsync(s => s.Get<PayPeriodDirectory>().GetTimeReportAsync(period.Id)))!;

        Assert.Null(before); // not calculated yet
        var codes = report.Lines.Select(l => l.Code).ToList();
        Assert.Equal(codes.Order(StringComparer.Ordinal), codes);
        Assert.Contains(a.Code, codes);
        Assert.Contains(b.Code, codes);
        Assert.Equal((2, 0, 14, 16, period.Id), (report.WeeklyHolidays, report.PublicHolidays, report.WorkDays, report.TotalDays, report.Period.Id));
    }

    [Fact] // #41: คำนวณ/ปิดงวดค่าแรงล็อกต่องวด ทีละคน
    public async Task A_period_being_calculated_by_someone_else_cannot_be_calculated()
    {
        var year = NewYear();
        var period = await ExamplePeriodAsync(year);
        string connectionString;
        await using (var scope = db.Scope()) connectionString = scope.Get<HrDbContext>().Database.GetConnectionString()!;

        await using var other = new SqlConnection(connectionString);
        await other.OpenAsync();
        await using var transaction = (SqlTransaction)await other.BeginTransactionAsync();
        await using (var command = new SqlCommand("EXEC sp_getapplock @Resource = @r, @LockMode = 'Exclusive', @LockOwner = 'Transaction'",
            other, transaction))
        {
            command.Parameters.AddWithValue("@r", PayPeriodRegistry.CalculationLockName(period.Id));
            await command.ExecuteNonQueryAsync();
        }

        var blocked = await CalculateAsync(period);

        Assert.Equal([PayPeriodRegistry.BusyMessage], blocked.Errors);
        await transaction.RollbackAsync();
        Ok(await CalculateAsync(period));
    }
}
