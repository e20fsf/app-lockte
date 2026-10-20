using Lockte.Hr;
using Lockte.Org;
using Lockte.Sales;
using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;

namespace Lockte.Web.Tests;

/// <summary>
/// ค่าแรงงานรับจ้าง (#61: BR-009, SCR-44, RPT-58 data) through the public services: the rows of one ลูกค้า from ทะเบียนลูกค้า
/// of module ขายและส่งของ, paid by the usual formulas as มาโคร คำนวณเวลาทำงาน1 did (no การลา, no SUN_DAY, HOLI/SUN for
/// everyone), summed by แผนก. Each test has its own แผนก and year, so the employees of other classes stay out of the lines
/// it checks.
/// </summary>
[Collection(LocalDbCollection.Name)]
public sealed class ContractLaborTests(LocalDbFixture db)
{
    private static int lastCode;
    private static int lastCustomer;
    private static int lastYear = 1050; // after PayHistoryReportTests' years (900+), before PayPeriodTests' (1100+)

    /// <summary>
    /// "UC" + 2 digits (รหัส is 4 characters): starts with U, the รหัส แผนกตกแต่ง2ทำงานยามาดะ marks, and keeps clear of
    /// PayHistoryReportTests' U + 3 digits.
    /// </summary>
    private static string NewCode() => $"UC{Interlocked.Increment(ref lastCode):D2}";

    /// <summary>MARK is 2 characters: "Z" + a letter of its own.</summary>
    private static string NewCustomerCode() => "Z" + (char)('A' + Interlocked.Increment(ref lastCustomer));

    private static int NewYear() => Interlocked.Increment(ref lastYear);

    private static DateOnly D(int year, int month, int day) => new(year, month, day);

    private static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..6];

    private async Task<T> AsHrAsync<T>(Func<TestScope, Task<T>> action)
    {
        await using var scope = db.Scope("hr1");
        return await action(scope);
    }

    private static void Ok(ServiceResult result) => Assert.True(result.Succeeded, string.Join(", ", result.Errors));

    private async Task<CustomerDto> NewCustomerAsync()
    {
        var code = NewCustomerCode();
        Ok(await AsHrAsync(s => s.Get<CustomerRegistry>().SaveAsync(new CustomerInput(null, code, "ลูกค้าสมมุติ " + code, null))));
        return (await AsHrAsync(s => s.Get<CustomerDirectory>().FindByCodeAsync(code)))!;
    }

    private async Task<DepartmentDto> NewDepartmentAsync()
    {
        var sectionId = (await AsHrAsync(s => s.Get<OrgRegistry>().SaveSectionAsync(new SectionInput(null, Unique("ผลิต"), 1, null)))).Id!.Value;
        var result = await AsHrAsync(s => s.Get<OrgRegistry>().SaveDepartmentAsync(new DepartmentInput(
            null, Unique("U"), Unique("แผนกรับจ้าง"), "U", 3, sectionId, null, [], null)));
        Ok(result);
        return (await AsHrAsync(s => s.Get<OrgDirectory>().GetDepartmentAsync(result.Id!.Value)))!;
    }

    private async Task<EmployeeDto> NewEmployeeAsync(int year, int departmentId, int type = EmployeeTypes.Daily, double salary = 0,
        double dailyWage = 400, string? code = null, DateOnly? resign = null)
    {
        var shift = await AsHrAsync(async s => (await s.Get<OrgDirectory>().FindShiftByCodeAsync("A"))!);
        var result = await AsHrAsync(s => s.Get<EmployeeRegistry>().SaveAsync(new EmployeeInput
        {
            Code = code ?? NewCode(), Name = "นาย ทดสอบ " + Guid.NewGuid().ToString("N")[..4], ShiftId = shift.Id, StartDate = D(year - 1, 1, 1),
            ResignDate = resign, EmployeeType = type, Salary = salary, DailyWage = dailyWage, DepartmentId = departmentId,
        }));
        Ok(result);
        return (await AsHrAsync(s => s.Get<EmployeeDirectory>().GetAsync(result.Id!.Value)))!;
    }

    private async Task WorkAsync(EmployeeDto employee, DateOnly date, string? mark, string type = "W", string workIn = "08:00",
        string workOut = "17:00", int rest = 60)
        => Ok(await AsHrAsync(s => s.Get<WorkTimeRegistry>().SaveAsync(new WorkTimeInput(null, date, employee.Id, type,
            TimeOnly.Parse(workIn), TimeOnly.Parse(workOut), rest, null, mark, null))));

    /// <summary>BR-002's example dates: 27/08 – 11/09 (D = 16), วันอาทิตย์ 30/08 and 06/09.</summary>
    private async Task ExampleWeeklyHolidaysAsync(int year)
    {
        foreach (var day in new[] { D(year, 8, 30), D(year, 9, 6) })
            Ok(await AsHrAsync(s => s.Get<CalendarRegistry>().SaveWeeklyHolidayAsync(new WeeklyHolidayInput(null, day, "วันอาทิตย์", null))));
    }

    private Task<ContractLaborReport?> ReportAsync(int year, string customer)
        => AsHrAsync(s => s.Get<ContractLaborDirectory>().GetReportAsync(D(year, 8, 27), D(year, 9, 11), customer));

    private static DepartmentPayTotal LineOf(ContractLaborReport report, DepartmentDto department)
        => Assert.Single(report.Lines, l => l.DepartmentName == department.Name);

    [Fact] // BR-009: only the customer's rows count; L is not appended; who never worked for the customer is still paid HOLI/SUN
    public async Task Only_the_customers_rows_are_paid_and_everyone_gets_the_period_holidays_as_the_legacy()
    {
        var year = NewYear();
        await ExampleWeeklyHolidaysAsync(year);
        var customer = await NewCustomerAsync();
        var other = await NewCustomerAsync();
        var department = await NewDepartmentAsync();
        var worker = await NewEmployeeAsync(year, department.Id);
        var monthly = await NewEmployeeAsync(year, department.Id, EmployeeTypes.Monthly, salary: 15000, dailyWage: 500);
        foreach (var day in new[] { 27, 28, 29, 31 }) await WorkAsync(worker, D(year, 8, day), customer.Code);
        await WorkAsync(worker, D(year, 9, 1), customer.Code.ToLowerInvariant()); // MARK is kept upper-case
        foreach (var day in new[] { 2, 3, 4 }) await WorkAsync(worker, D(year, 9, day), null);
        await WorkAsync(worker, D(year, 9, 5), other.Code);
        await WorkAsync(worker, D(year, 8, 27), customer.Code, "O1", "17:00", "19:00", 0);
        await WorkAsync(worker, D(year, 8, 28), null, "O1", "17:00", "20:00", 0);
        await WorkAsync(worker, D(year, 9, 12), customer.Code); // after TO_DA
        var leave = await AsHrAsync(s => s.Get<LeaveRegistry>().SaveAsync(new LeaveInput(null, worker.Id, D(year, 9, 7), D(year, 9, 7),
            D(year, 9, 7), null, null, 1, "1", true, null, 400, null)));
        Ok(leave.Save);

        var report = (await ReportAsync(year, customer.Code))!;

        Assert.Equal((customer.Code, customer.Name), (report.CustomerCode, report.CustomerName));
        Assert.Equal((2, 0, 16, 14, 14), (report.WeeklyHolidays, report.PublicHolidays, report.TotalDays, report.WorkDays, report.PaidDays));
        var pay = Assert.Single(report.Employees, l => l.Pay.EmployeeId == worker.Id).Pay;
        Assert.Equal((5.0, 0.0, 2.0), (pay.Normal, pay.PaidLeave, pay.Overtime));
        Assert.Equal((2000m, 150m), (pay.PayNormal, pay.PayOvertime)); // 5 × 400, RND-A(2 × 400 / 8 × 1.5)
        // รายเดือน, nothing for the customer: Expr1 + HOLI = 0 < D − SUN → (15 − L(NO)) × SA_DAY
        Assert.Equal(7500m, Assert.Single(report.Employees, l => l.Pay.EmployeeId == monthly.Id).Pay.PayNormal);
        var line = LineOf(report, department);
        Assert.Equal((2, 9500m, 150m, 9650m), (line.Employees, line.PayNormal, line.PayOvertime, line.Total));
    }

    [Fact] // BR-009 "สูตรเดียวกัน": every row marked, no การลา → the same ค่าแรง as the usual calculation (BR-003/004)
    public async Task With_every_row_marked_the_pay_is_the_usual_pay()
    {
        var year = NewYear();
        await ExampleWeeklyHolidaysAsync(year);
        Ok(await AsHrAsync(s => s.Get<CalendarRegistry>().SavePublicHolidayAsync(new PublicHolidayInput(null, D(year, 9, 4), "วันหยุดทดสอบ", null))));
        var customer = await NewCustomerAsync();
        var department = await NewDepartmentAsync();
        var daily = await NewEmployeeAsync(year, department.Id, dailyWage: 437);
        var monthly = await NewEmployeeAsync(year, department.Id, EmployeeTypes.Monthly, salary: 16100, dailyWage: 536.67);
        foreach (var employee in new[] { daily, monthly })
        {
            foreach (var day in new[] { 27, 28, 29, 31 }) await WorkAsync(employee, D(year, 8, day), customer.Code);
            await WorkAsync(employee, D(year, 9, 1), customer.Code, workOut: "16:30");
            await WorkAsync(employee, D(year, 8, 27), customer.Code, "O1", "17:20", "20:10", 0);
            await WorkAsync(employee, D(year, 8, 30), customer.Code, "S", "08:00", "17:00", 60);
            await WorkAsync(employee, D(year, 8, 30), customer.Code, "O2", "17:20", "19:20", 0);
            await WorkAsync(employee, D(year, 9, 4), customer.Code, "H", "08:00", "12:00", 0);
        }
        var period = await AsHrAsync(s => s.Get<PayPeriodRegistry>().SaveAsync(new PayPeriodInput(null, D(year, 8, 27), D(year, 9, 11), D(year, 9, 16), null)));
        Ok(period);
        Ok(await AsHrAsync(s => s.Get<PayPeriodRegistry>().CalculateTimeAsync(period.Id!.Value)));
        Ok(await AsHrAsync(s => s.Get<PayPeriodRegistry>().CalculatePayAsync(period.Id!.Value)));
        var usual = await AsHrAsync(s => s.Get<PayPeriodDirectory>().GetPayAsync(period.Id!.Value));

        var report = (await ReportAsync(year, customer.Code))!;

        foreach (var employee in new[] { daily, monthly })
        {
            var expected = usual.Single(p => p.EmployeeId == employee.Id);
            var actual = report.Employees.Single(l => l.Pay.EmployeeId == employee.Id).Pay;
            Assert.Equal(expected, actual);
        }
    }

    [Fact] // มาโคร คำนวณเวลาทำงาน1 has no คำนวณค่าแรง01: รายเดือนที่ลาออกในงวดได้ SUN_DAY = 0
    public async Task A_monthly_employee_resigning_in_the_period_gets_no_weekly_holidays_to_the_resign_date()
    {
        var year = NewYear();
        await ExampleWeeklyHolidaysAsync(year);
        var customer = await NewCustomerAsync();
        var department = await NewDepartmentAsync();
        var monthly = await NewEmployeeAsync(year, department.Id, EmployeeTypes.Monthly, salary: 15000, dailyWage: 500, resign: D(year, 9, 3));
        foreach (var day in new[] { 27, 28, 29, 31 }) await WorkAsync(monthly, D(year, 8, day), customer.Code);
        foreach (var day in new[] { 1, 2 }) await WorkAsync(monthly, D(year, 9, day), customer.Code);

        var report = (await ReportAsync(year, customer.Code))!;

        // (Expr1 + SUN_DAY) × SA_DAY with SUN_DAY 0 (the usual run: 1 for 30/08)
        Assert.Equal(3000m, Assert.Single(report.Employees, l => l.Pay.EmployeeId == monthly.Id).Pay.PayNormal);
    }

    [Fact]
    public async Task A_code_not_in_the_customer_register_has_no_report()
        => Assert.Null(await ReportAsync(NewYear(), "#"));

    [Fact] // ลูกค้าอ่านผ่าน public service ของ module ขายและส่งของ
    public async Task The_customers_to_choose_are_those_of_the_customer_register()
    {
        var customer = await NewCustomerAsync();

        var choices = await AsHrAsync(s => s.Get<ContractLaborDirectory>().ListCustomersAsync());

        Assert.Contains(choices, c => c.Code == customer.Code && c.Name == customer.Name);
    }

    [Fact] // ปุ่ม แผนกตกแต่ง2ทำงานยามาดะ: MARK = Y on the blank-MARK rows of รหัส U* in the day's งวด; one history event
    public async Task Marking_yamada_flags_the_blank_rows_of_U_codes_in_the_days_period()
    {
        var year = NewYear();
        var department = await NewDepartmentAsync();
        var u = await NewEmployeeAsync(year, department.Id);
        var notU = await NewEmployeeAsync(year, department.Id, code: "XC" + NewCode()[2..]);
        await WorkAsync(u, D(year, 8, 27), null);
        await WorkAsync(u, D(year, 9, 11), null);
        await WorkAsync(u, D(year, 9, 1), "S");
        await WorkAsync(u, D(year, 9, 12), null); // the next period
        await WorkAsync(notU, D(year, 8, 27), null);

        var result = await AsHrAsync(s => s.Get<WorkTimeRegistry>().MarkYamadaAsync(D(year, 9, 1)));

        Ok(result);
        Assert.Equal(2, result.Id);
        async Task<string?> MarkAsync(EmployeeDto employee, DateOnly date)
            => (await AsHrAsync(s => s.Get<WorkTimeDirectory>().ListAsync(date))).Single(w => w.EmployeeId == employee.Id).Mark;
        Assert.Equal("Y", await MarkAsync(u, D(year, 8, 27)));
        Assert.Equal("Y", await MarkAsync(u, D(year, 9, 11)));
        Assert.Equal("S", await MarkAsync(u, D(year, 9, 1)));
        Assert.Null(await MarkAsync(u, D(year, 9, 12)));
        Assert.Null(await MarkAsync(notU, D(year, 8, 27)));
    }
}
