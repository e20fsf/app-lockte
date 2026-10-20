using Lockte.Hr;
using Lockte.Org;
using Lockte.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace Lockte.Web.Tests;

/// <summary>
/// The reports over many งวดค่าแรง (#56: RPT-08 BR-063, RPT-09 and RPT-10 BR-062): which งวด the dates choose, the legacy
/// joins on the employee of today and the legacy sums. The results go in as the migration tool puts the legacy history
/// (<see cref="HrPayHistoryImport"/>) — the same table คำนวณค่าแรง writes. Each test works in years (900+) and departments
/// of its own, so the rows of the other tests (which share the database) do not reach its numbers.
/// </summary>
[Collection(LocalDbCollection.Name)]
public sealed class PayHistoryReportTests(LocalDbFixture db)
{
    private static int lastCode;
    private static int lastYear = 900; // after PaySlipDataTests' years (750+), before PayPeriodTests' (1100+)

    /// <summary>
    /// "U" keeps clear of the other test classes' codes (T-EMPLO.ID is Text(4)); RPT-09 also needs codes by first letter
    /// (<see cref="CodeDepartments"/>), which take A and D with the same counter.
    /// </summary>
    private static string NewCode(char prefix = 'U') => $"{prefix}{Interlocked.Increment(ref lastCode):D3}";

    private static int NewYear() => Interlocked.Increment(ref lastYear);

    private static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..6];

    private static DateOnly D(int year, int month, int day) => new(year, month, day);

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
            null, Unique("U"), Unique("แผนก"), "U", 9, sectionId, null, [], null)))).Id!.Value;
        return (await AsHrAsync(s => s.Get<OrgDirectory>().GetDepartmentAsync(id)))!;
    }

    private async Task<EmployeeDto> NewEmployeeAsync(DepartmentDto? department, int type = EmployeeTypes.Daily, double salary = 0,
        double dailyWage = 372, string? grade = null, string? code = null)
    {
        var result = await AsHrAsync(s => s.Get<EmployeeRegistry>().SaveAsync(new EmployeeInput
        {
            Code = code ?? NewCode(), Name = "นาย ทดสอบ " + Guid.NewGuid().ToString("N")[..4], DepartmentId = department?.Id,
            SectionId = department?.SectionId, StartDate = D(2015, 3, 1), EmployeeType = type, Salary = salary, DailyWage = dailyWage,
            NationalId = "1" + Random.Shared.NextInt64(100000000000, 999999999999), Evaluation1Grade = grade,
        }));
        Ok(result);
        return (await AsHrAsync(s => s.Get<EmployeeDirectory>().GetAsync(result.Id!.Value)))!;
    }

    private static PayResultImport Pay(EmployeeDto employee, decimal payNormal, decimal payOvertime = 0)
        => new(employee.Id, 0, 0, 0, 0, 0, payNormal, payOvertime, 0, 0, 0);

    /// <summary>One งวด with its ค่าแรง, as #51 moved the legacy history in.</summary>
    private Task PeriodAsync(DateOnly from, DateOnly to, DateOnly payDate, params PayResultImport[] results)
        => AsHrAsync(async s =>
        {
            await ActivatorUtilities.CreateInstance<HrPayHistoryImport>(s.Get<IServiceProvider>())
                .AddAsync([new PayPeriodImport(from, to, payDate, results)]);
            return 0;
        });

    private Task<T> ReportAsync<T>(Func<PayHistoryReportDirectory, Task<T>> query) => AsHrAsync(s => query(s.Get<PayHistoryReportDirectory>()));

    // ---------- RPT-08 ค่าแรงสิ้นเดือน (BR-063) ----------

    [Fact] // งวดที่ F_D ≤ TO_DATE ≤ T_D, รวมต่อคน; รายเดือน ค่าจ้าง = SALARY จำนวนวันว่าง; รายวัน Round(ΣPAY_W/SA_DAY, 1) แบบ Access
    public async Task The_month_end_report_sums_the_periods_ending_in_the_dates_per_employee()
    {
        var year = NewYear();
        var department = await NewDepartmentAsync();
        var daily = await NewEmployeeAsync(department);
        var monthly = await NewEmployeeAsync(department, EmployeeTypes.Monthly, salary: 12340, dailyWage: 411.33);
        var half = await NewEmployeeAsync(department, dailyWage: 400);
        // 27/08–11/09 and 12/09–26/09 end in the dates; 12/08–26/08 and 27/09–11/10 do not.
        await PeriodAsync(D(year, 8, 12), D(year, 8, 26), D(year, 9, 1), Pay(daily, 1000m));
        await PeriodAsync(D(year, 8, 27), D(year, 9, 11), D(year, 9, 16), Pay(daily, 5208m, 150m), Pay(monthly, 6170m, 80m), Pay(half, 4100m));
        await PeriodAsync(D(year, 9, 12), D(year, 9, 26), D(year, 10, 1), Pay(daily, 4650m), Pay(monthly, 6170m), Pay(half, 4150m));
        await PeriodAsync(D(year, 9, 27), D(year, 10, 11), D(year, 10, 16), Pay(daily, 2000m));

        var report = await ReportAsync(p => p.GetMonthEndReportAsync(D(year, 8, 27), D(year, 9, 26), department.Id));

        Assert.Equal(department.Name, report.DepartmentName);
        Assert.Equal([daily.Code, monthly.Code, half.Code], report.Lines.Select(l => l.Code));
        var d = report.Lines[0];
        Assert.Equal((372.0, (double?)26.5, 9858m, 9858m, 150m, 10008m), (d.Rate, d.Days, d.Wage, d.PayNormal, d.PayOvertime, d.Total));
        Assert.Equal(daily.NationalId, d.NationalId);
        var m = report.Lines[1];
        Assert.Equal((12340.0, (double?)null, 12340m, 12340m, 80m, 12420m), (m.Rate, m.Days, m.Wage, m.PayNormal, m.PayOvertime, m.Total));
        Assert.Equal((double?)20.6, report.Lines[2].Days); // 8250 / 400 = 20.625 → 20.6
        Assert.Equal(9858m + 12340m + 8250m, report.Wages);
    }

    [Fact] // Access Round(x, 1) ปัดครึ่งไปเลขคู่: 0.25 → 0.2, 0.35 → 0.4
    public async Task The_days_round_half_to_even_as_Access()
    {
        var year = NewYear();
        var department = await NewDepartmentAsync();
        var a = await NewEmployeeAsync(department, dailyWage: 400);
        var b = await NewEmployeeAsync(department, dailyWage: 400);
        await PeriodAsync(D(year, 8, 27), D(year, 9, 11), D(year, 9, 16), Pay(a, 100m), Pay(b, 140m));

        var report = await ReportAsync(p => p.GetMonthEndReportAsync(D(year, 8, 27), D(year, 9, 26), department.Id));

        Assert.Equal([0.2, 0.4], report.Lines.Select(l => l.Days!.Value));
    }

    [Fact] // แผนกของวันนี้ (ไม่ใช่ตอนคำนวณ); มี GRED_1 → ไม่อยู่ในรายงาน (❓ Q-44); ทั้งหมด = ทุกแผนก
    public async Task The_month_end_report_takes_the_department_of_today_and_leaves_out_employees_with_a_first_grade()
    {
        var year = NewYear();
        var before = await NewDepartmentAsync();
        var after = await NewDepartmentAsync();
        var moved = await NewEmployeeAsync(before);
        var graded = await NewEmployeeAsync(after, grade: "B");
        var stays = await NewEmployeeAsync(before);
        await PeriodAsync(D(year, 8, 27), D(year, 9, 11), D(year, 9, 16), Pay(moved, 3000m), Pay(graded, 3000m), Pay(stays, 2000m));
        Ok(await AsHrAsync(s => s.Get<EmployeeRegistry>().SaveAsync(moved.ToInput() with { DepartmentId = after.Id, SectionId = after.SectionId })));

        var inAfter = await ReportAsync(p => p.GetMonthEndReportAsync(D(year, 8, 27), D(year, 9, 26), after.Id));
        var all = await ReportAsync(p => p.GetMonthEndReportAsync(D(year, 8, 27), D(year, 9, 26), null));
        var summary = await ReportAsync(p => p.GetMonthEndSummaryAsync(D(year, 8, 27), D(year, 9, 26), null));

        Assert.Equal([moved.Code], inAfter.Lines.Select(l => l.Code));
        Assert.Null(all.DepartmentName);
        Assert.Equal([moved.Code, stays.Code], all.Lines.Select(l => l.Code));
        Assert.Equal([(after.Name, 1, 3000m), (before.Name, 1, 2000m)],
            summary.Lines.Select(l => (l.DepartmentName!, l.Employees, l.Wages)).OrderBy(l => l.Item3 == 2000m));
    }

    [Fact] // สรุปบัญชีคำนวณค่าจ้าง = Σค่าจ้าง (รายเดือน SALARY), สรุปค่าแรงประจำเดือน (ต้นทุน) = Σ(PAY_W + PAY_OT)
    public async Task The_summaries_add_up_the_wages_and_the_cost_by_department()
    {
        var year = NewYear();
        var department = await NewDepartmentAsync();
        var daily = await NewEmployeeAsync(department);
        var monthly = await NewEmployeeAsync(department, EmployeeTypes.Monthly, salary: 15000, dailyWage: 500);
        await PeriodAsync(D(year, 8, 27), D(year, 9, 11), D(year, 9, 16), Pay(daily, 5000m, 300m), Pay(monthly, 7000m, 200m));

        var summary = await ReportAsync(p => p.GetMonthEndSummaryAsync(D(year, 8, 27), D(year, 9, 26), department.Id));

        var line = Assert.Single(summary.Lines);
        Assert.Equal((department.Name, 2, 20000m, 12500m), (line.DepartmentName, line.Employees, line.Wages, line.Total));
        Assert.Equal((2, 20000m, 12500m), (summary.Sum.Employees, summary.Sum.Wages, summary.Sum.Total));
    }

    // ---------- RPT-10 รวมค่าแรง (BR-062) ----------

    [Fact] // งวดที่ FROM_DATE ≥ F_D และ TO_DATE ≤ T_D ทั้งงวด, รวมตามแผนกของวันนี้
    public async Task The_pay_totals_take_the_periods_wholly_inside_the_dates_by_the_department_of_today()
    {
        var year = NewYear();
        var first = await NewDepartmentAsync();
        var second = await NewDepartmentAsync();
        var a = await NewEmployeeAsync(first);
        var b = await NewEmployeeAsync(first);
        var c = await NewEmployeeAsync(second);
        await PeriodAsync(D(year, 7, 12), D(year, 7, 26), D(year, 8, 1), Pay(a, 999m)); // starts before
        await PeriodAsync(D(year, 7, 27), D(year, 8, 11), D(year, 8, 16), Pay(a, 1000m, 10m), Pay(b, 2000m), Pay(c, 3000m, 30m));
        await PeriodAsync(D(year, 8, 12), D(year, 8, 26), D(year, 9, 1), Pay(a, 1100m, 11m), Pay(c, 3300m));
        await PeriodAsync(D(year, 8, 27), D(year, 9, 11), D(year, 9, 16), Pay(c, 999m)); // ends after

        var report = await ReportAsync(p => p.GetPayTotalsReportAsync(D(year, 7, 27), D(year, 8, 26)));

        Assert.Equal([(first.Name, 4100m, 21m, 4121m), (second.Name, 6300m, 30m, 6330m)],
            report.Lines.Select(l => (l.DepartmentName!, l.PayNormal, l.PayOvertime, l.Total)).OrderBy(l => l.Item2));
        Assert.Equal((10400m, 51m, 10451m), (report.Sum.PayNormal, report.Sum.PayOvertime, report.Sum.Total));
    }

    // ---------- RPT-09 สรุปค่าแรงแต่ละเดือน (BR-062) ----------

    [Fact] // PAY_D ในช่วง; จ่ายวันที่ 1 → เดือนก่อน (1 ม.ค. → ธ.ค.); แผนกจากตัวอักษรแรกของรหัส อื่น ๆ = แผนกแม่พิมพ์
    public async Task The_monthly_report_puts_each_payment_in_its_month_and_the_department_of_its_code_letter()
    {
        var year = NewYear();
        var department = await NewDepartmentAsync();
        var assembly = await NewEmployeeAsync(department, code: NewCode('A'));
        var dieCast = await NewEmployeeAsync(department, code: NewCode('D'));
        var other = await NewEmployeeAsync(department);
        await PeriodAsync(D(year - 1, 12, 12), D(year - 1, 12, 26), D(year, 1, 1), Pay(assembly, 900m)); // before the dates
        await PeriodAsync(D(year, 1, 12), D(year, 1, 26), D(year, 2, 1), Pay(assembly, 100m, 5m), Pay(other, 300m)); // → มกราคม
        await PeriodAsync(D(year, 1, 27), D(year, 2, 11), D(year, 2, 16), Pay(assembly, 200m), Pay(dieCast, 400m, 40m)); // กุมภาพันธ์
        await PeriodAsync(D(year, 12, 12), D(year, 12, 26), D(year + 1, 1, 1), Pay(dieCast, 500m)); // → ธันวาคม

        var report = await ReportAsync(p => p.GetMonthlyReportAsync(D(year, 1, 2), D(year + 1, 1, 1)));

        Assert.Equal(["แผนกไดคาสท์", "แผนกประกอบ", "แผนกแม่พิมพ์"], report.Lines.Select(l => l.Department));
        var dieCastLine = report.Lines[0];
        Assert.Equal([null, 440m, null, null, null, null, null, null, null, null, null, 500m], dieCastLine.Months);
        Assert.Equal(940m, dieCastLine.Total);
        Assert.Equal([105m, 200m], report.Lines[1].Months.Take(2));
        Assert.Equal(300m, report.Lines[2].Months[0]);
        Assert.Equal([405m, 640m, null], report.MonthSums.Take(3));
        Assert.Equal(1545m, report.Total);
    }

    [Theory] // BR-062 ตาราง: 16/09 → ก.ย., 01/10 → ก.ย., 01/01 → ธ.ค.
    [InlineData(2026, 9, 16, 9)]
    [InlineData(2026, 10, 1, 9)]
    [InlineData(2026, 1, 1, 12)]
    public void A_payment_on_the_first_counts_in_the_month_before(int year, int month, int day, int expected)
        => Assert.Equal(expected, PayHistoryReportDirectory.PayMonth(D(year, month, day)));

    [Theory] // คิวรี สรุปค่าแรงแต่ละเดือน2: Access เทียบข้อความไม่สนตัวพิมพ์
    [InlineData("A005", "แผนกประกอบ")]
    [InlineData("k012", "แผนกผ้าคลัทช์และดิสเบรก")]
    [InlineData("T100", "แผนกตกแต่ง")]
    [InlineData("Z019", "แผนกแม่พิมพ์")]
    [InlineData("C001", "แผนกแม่พิมพ์")]
    public void The_code_letter_gives_the_department(string code, string expected) => Assert.Equal(expected, CodeDepartments.Of(code));
}
