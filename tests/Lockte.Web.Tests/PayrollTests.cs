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
/// คำนวณค่าแรง (#53: BR-003..006, SCR-41, RPT-02/03 data) through the public services of module พนักงานและค่าแรง, the
/// way HR works: เวลาทำงาน → ตั้งงวด → คำนวณเวลาทำงาน → คำนวณค่าแรง. The formulas themselves are in
/// <see cref="PayCalculationTests"/>; these check what is stored and read back.
/// </summary>
[Collection(LocalDbCollection.Name)]
public sealed class PayrollTests(LocalDbFixture db)
{
    private static int lastCode;
    private static int lastYear = 600; // below PayPeriodTests' years: its default-dates test needs the latest period

    /// <summary>"Q" keeps clear of the other test classes' codes.</summary>
    private static string NewCode() => $"Q{Interlocked.Increment(ref lastCode):D3}";

    /// <summary>A year of its own per test, after PayPeriodTests' years (they start at 1101).</summary>
    private static int NewYear() => Interlocked.Increment(ref lastYear);

    private static DateOnly D(int year, int month, int day) => new(year, month, day);

    private static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..6];

    private async Task<T> AsHrAsync<T>(Func<TestScope, Task<T>> action)
    {
        await using var scope = db.Scope("hr1");
        return await action(scope);
    }

    private static void Ok(ServiceResult result) => Assert.True(result.Succeeded, string.Join(", ", result.Errors));

    private async Task<DepartmentDto> NewDepartmentAsync()
    {
        var sectionId = (await AsHrAsync(s => s.Get<OrgRegistry>().SaveSectionAsync(new SectionInput(null, Unique("ผลิต"), 1, null)))).Id!.Value;
        var result = await AsHrAsync(s => s.Get<OrgRegistry>().SaveDepartmentAsync(new DepartmentInput(
            null, Unique("Q"), Unique("แผนกค่าแรง"), "Q", 3, sectionId, null, [], null)));
        Ok(result);
        return (await AsHrAsync(s => s.Get<OrgDirectory>().GetDepartmentAsync(result.Id!.Value)))!;
    }

    private async Task<EmployeeDto> NewEmployeeAsync(int year, int type = EmployeeTypes.Daily, double salary = 0, double dailyWage = 400,
        int? departmentId = null, string? code = null)
    {
        var shift = await AsHrAsync(async s => (await s.Get<OrgDirectory>().FindShiftByCodeAsync("A"))!);
        var result = await AsHrAsync(s => s.Get<EmployeeRegistry>().SaveAsync(new EmployeeInput
        {
            Code = code ?? NewCode(), Name = "นาย ทดสอบ " + Guid.NewGuid().ToString("N")[..4], ShiftId = shift.Id, StartDate = D(year - 1, 1, 1),
            EmployeeType = type, Salary = salary, DailyWage = dailyWage, DepartmentId = departmentId,
        }));
        Ok(result);
        return (await AsHrAsync(s => s.Get<EmployeeDirectory>().GetAsync(result.Id!.Value)))!;
    }

    private async Task WorkAsync(EmployeeDto employee, DateOnly date, string type = "W", string workIn = "08:00", string workOut = "17:00",
        int rest = 60)
        => Ok(await AsHrAsync(s => s.Get<WorkTimeRegistry>().SaveAsync(new WorkTimeInput(null, date, employee.Id, type,
            TimeOnly.Parse(workIn), TimeOnly.Parse(workOut), rest, null, null, null))));

    /// <summary>BR-002's example period: 27/08 – 11/09 (D = 16), วันอาทิตย์ 30/08 and 06/09.</summary>
    private async Task<PayPeriodDto> ExamplePeriodAsync(int year)
    {
        foreach (var day in new[] { D(year, 8, 30), D(year, 9, 6) })
            Ok(await AsHrAsync(s => s.Get<CalendarRegistry>().SaveWeeklyHolidayAsync(new WeeklyHolidayInput(null, day, "วันอาทิตย์", null))));
        var result = await AsHrAsync(s => s.Get<PayPeriodRegistry>().SaveAsync(new PayPeriodInput(null, D(year, 8, 27), D(year, 9, 11), D(year, 9, 16), null)));
        Ok(result);
        return await GetPeriodAsync(result.Id!.Value);
    }

    private static IEnumerable<DateOnly> WorkingDays(int year)
        => Enumerable.Range(0, 16).Select(i => D(year, 8, 27).AddDays(i)).Where(d => d != D(year, 8, 30) && d != D(year, 9, 6));

    private async Task<PayPeriodDto> GetPeriodAsync(int id) => (await AsHrAsync(s => s.Get<PayPeriodDirectory>().GetAsync(id)))!;

    private Task<ServiceResult> CalculateTimeAsync(PayPeriodDto period) => AsHrAsync(s => s.Get<PayPeriodRegistry>().CalculateTimeAsync(period.Id));

    private Task<ServiceResult> CalculatePayAsync(PayPeriodDto period) => AsHrAsync(s => s.Get<PayPeriodRegistry>().CalculatePayAsync(period.Id));

    private Task<IReadOnlyList<EmployeePay>> PayAsync(PayPeriodDto period) => AsHrAsync(s => s.Get<PayPeriodDirectory>().GetPayAsync(period.Id));

    private async Task<IReadOnlyList<EmployeePay>> CalculatedPayAsync(PayPeriodDto period)
    {
        Ok(await CalculateTimeAsync(period));
        Ok(await CalculatePayAsync(period));
        return await PayAsync(period);
    }

    [Fact] // BR-003 ตัวอย่าง 1 + 5, BR-004 ตัวอย่าง 1: one daily and one monthly employee, end to end
    public async Task Pay_is_calculated_from_the_period_time_and_stored()
    {
        var year = NewYear();
        var period = await ExamplePeriodAsync(year);
        var daily = await NewEmployeeAsync(year);
        var monthly = await NewEmployeeAsync(year, EmployeeTypes.Monthly, 15000, 500);
        foreach (var day in WorkingDays(year)) { await WorkAsync(daily, day); await WorkAsync(monthly, day); }
        await WorkAsync(daily, D(year, 9, 1), "O1", "17:00", "20:30", 0); // 3.5 ชม.

        var pay = await CalculatedPayAsync(period);

        var d = Assert.Single(pay, p => p.EmployeeId == daily.Id);
        Assert.Equal((5600m, 263m, 0m, 400.0, 14.0, 3.5, 0, 2), (d.PayNormal, d.PayOvertime, d.Hot, d.Rate, d.Normal, d.Overtime, d.HolidayHours, d.WeeklyHolidays));
        var m = Assert.Single(pay, p => p.EmployeeId == monthly.Id);
        Assert.Equal((7500m, 0m, 15000.0), (m.PayNormal, m.PayOvertime, m.Rate));
        var after = await GetPeriodAsync(period.Id);
        Assert.Equal((PayPeriodStep.PayCalculated, "hr1"), (after.CompletedStep, after.PayCalculatedBy));
    }

    [Fact] // #22: คำนวณค่าแรงได้หลังคำนวณเวลาทำงานแล้วเท่านั้น
    public async Task Pay_cannot_be_calculated_before_the_time()
    {
        var period = await ExamplePeriodAsync(NewYear());

        var result = await CalculatePayAsync(period);

        Assert.Equal(["ต้องทำขั้น \"พิมพ์เวลาทำงาน\" ก่อน"], result.Errors);
    }

    [Fact] // BR-006 / Q-22: คำนวณซ้ำทับผลเดิม
    public async Task Recalculating_pay_replaces_the_results()
    {
        var year = NewYear();
        var period = await ExamplePeriodAsync(year);
        var employee = await NewEmployeeAsync(year);
        await WorkAsync(employee, D(year, 9, 1));
        await CalculatedPayAsync(period);
        await WorkAsync(employee, D(year, 9, 2));

        var pay = Assert.Single(await CalculatedPayAsync(period), p => p.EmployeeId == employee.Id);

        Assert.Equal(800m, pay.PayNormal);
    }

    [Fact] // the time changed: the old ค่าแรง no longer holds
    public async Task Recalculating_the_time_drops_the_pay_until_it_is_calculated_again()
    {
        var year = NewYear();
        var period = await ExamplePeriodAsync(year);
        var employee = await NewEmployeeAsync(year);
        await WorkAsync(employee, D(year, 9, 1));
        await CalculatedPayAsync(period);

        Ok(await CalculateTimeAsync(period));

        Assert.Empty(await PayAsync(period));
        Assert.Null(await AsHrAsync(s => s.Get<PayPeriodDirectory>().GetPayReportAsync(period.Id, null)));
        Assert.Equal((PayPeriodStep.TimeCalculated, (string?)null), ((await GetPeriodAsync(period.Id)).CompletedStep, (await GetPeriodAsync(period.Id)).PayCalculatedBy));
    }

    [Fact] // #41: ผลคำนวณบันทึกเป็นเหตุการณ์เดียว ไม่บันทึกรายแถว
    public async Task Calculating_pay_is_one_history_event()
    {
        var year = NewYear();
        var period = await ExamplePeriodAsync(year);
        var employee = await NewEmployeeAsync(year);
        await WorkAsync(employee, D(year, 9, 1));
        Ok(await CalculateTimeAsync(period));

        var result = await CalculatePayAsync(period);

        Ok(result);
        await using var scope = db.Scope();
        var history = await scope.Get<HistoryService>().SearchAsync(new HistoryFilter(Table: PayPeriodRegistry.PayPeriodTable));
        var job = Assert.Single(history, h => h.Action == PayPeriodRegistry.CalculatePayAction && h.Detail?.Contains($"{year:D4}-08-27") == true);
        Assert.Equal((HistoryKind.Event, (int?)result.Id, "hr1"), (job.Kind, job.RowCount, job.UserName));
        Assert.DoesNotContain(history, h => h.Kind != HistoryKind.Event && h.RowKey?.Contains(employee.Code) == true);
    }

    [Fact] // #41: คำนวณ/ปิดงวดค่าแรงล็อกต่องวด ทีละคน
    public async Task Pay_of_a_period_being_calculated_by_someone_else_cannot_be_calculated()
    {
        var period = await ExamplePeriodAsync(NewYear());
        Ok(await CalculateTimeAsync(period));
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

        var blocked = await CalculatePayAsync(period);

        Assert.Equal([PayPeriodRegistry.BusyMessage], blocked.Errors);
        await transaction.RollbackAsync();
        Ok(await CalculatePayAsync(period));
    }

    [Fact] // RPT-02 (คิวรี คำนวณค่าแรง73): แผนก LIKE NAME_DE และ PAY_W + PAY_OT > 0, เรียง ID ↑
    public async Task The_pay_report_lists_one_department_with_pay_by_code()
    {
        var year = NewYear();
        var period = await ExamplePeriodAsync(year);
        var department = await NewDepartmentAsync();
        var other = await NewDepartmentAsync();
        var (first, second) = (NewCode(), NewCode());
        var b = await NewEmployeeAsync(year, departmentId: department.Id, code: second); // added first, sorts second
        var a = await NewEmployeeAsync(year, departmentId: department.Id, code: first);
        var unpaid = await NewEmployeeAsync(year, departmentId: department.Id, dailyWage: 0);
        var elsewhere = await NewEmployeeAsync(year, departmentId: other.Id);
        foreach (var e in new[] { a, b, unpaid, elsewhere }) await WorkAsync(e, D(year, 9, 1));
        await CalculatedPayAsync(period);

        var report = (await AsHrAsync(s => s.Get<PayPeriodDirectory>().GetPayReportAsync(period.Id, department.Id)))!;
        var all = (await AsHrAsync(s => s.Get<PayPeriodDirectory>().GetPayReportAsync(period.Id, null)))!;

        Assert.Equal([a.Code, b.Code], report.Lines.Select(l => l.Pay.Code));
        Assert.Equal(department.Name, report.DepartmentName);
        Assert.Contains(all.Lines, l => l.Pay.EmployeeId == elsewhere.Id);
        Assert.DoesNotContain(all.Lines, l => l.Pay.EmployeeId == unpaid.Id);
    }

    [Fact] // RPT-03 (BR-060): จำนวนคน รวมคนที่ได้ 0, ΣPAY_W, ΣPAY_OT, %OT ต่อแผนก
    public async Task The_summary_totals_each_department()
    {
        var year = NewYear();
        var period = await ExamplePeriodAsync(year);
        var department = await NewDepartmentAsync();
        var a = await NewEmployeeAsync(year, departmentId: department.Id);
        var b = await NewEmployeeAsync(year, departmentId: department.Id);
        var unpaid = await NewEmployeeAsync(year, departmentId: department.Id, dailyWage: 0);
        foreach (var e in new[] { a, b, unpaid }) await WorkAsync(e, D(year, 9, 1));
        await WorkAsync(a, D(year, 9, 1), "O1", "17:00", "21:00", 0); // 4 × 400 / 8 × 1.5 = 300
        await CalculatedPayAsync(period);

        var summary = (await AsHrAsync(s => s.Get<PayPeriodDirectory>().GetPaySummaryAsync(period.Id)))!;

        var line = Assert.Single(summary.Lines, l => l.DepartmentName == department.Name);
        Assert.Equal((3, 800m, 300m, 1100m, 37.5m), (line.Employees, line.PayNormal, line.PayOvertime, line.Total, line.OvertimePercent));
        Assert.Equal((2, 0, 14, 16, 14), (summary.WeeklyHolidays, summary.PublicHolidays, summary.WorkDays, summary.TotalDays, summary.PaidDays));
    }
}
