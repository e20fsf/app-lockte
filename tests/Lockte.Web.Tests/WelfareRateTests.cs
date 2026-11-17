using Lockte.Hr;
using Lockte.Org;
using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;
using Lockte.Web.Accounts;
using Lockte.Web.Audit;
using Lockte.Web.Reports.Hr;
using Microsoft.Extensions.DependencyInjection;

namespace Lockte.Web.Tests;

/// <summary>อัตราเงินสมทบกองทุนสงเคราะห์ลูกจ้าง (UAT-43, #436): the rules of the rate table without the database.</summary>
public sealed class WelfareRateTableTests
{
    private static DateOnly D(int year, int month, int day) => new(year, month, day);

    /// <summary>The two ranges of แก้ครั้งที่ 5, as the migration seeds them.</summary>
    private static readonly WelfareRateDto[] Seeded =
    [
        new(1, D(2027, 1, 1), D(2031, 9, 30), 0.25m, []),
        new(2, D(2031, 10, 1), null, 0.50m, []),
    ];

    [Theory]
    [InlineData(2027, 1, 1, "0.25")]
    [InlineData(2031, 9, 30, "0.25")]  // 30 ก.ย. 2574: the last day of 0.25%
    [InlineData(2031, 10, 1, "0.50")]  // 1 ต.ค. 2574: the first day of 0.50%
    [InlineData(2099, 12, 31, "0.50")] // เป็นต้นไป
    public void The_rate_in_force_is_the_range_covering_the_date(int year, int month, int day, string expected)
        => Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), WelfareRateTable.On(Seeded, D(year, month, day))!.Percent);

    [Fact] // ก่อน 1 ม.ค. 2570 no range: null, never a guess
    public void A_date_no_range_covers_has_no_rate()
    {
        Assert.Null(WelfareRateTable.On(Seeded, D(2026, 12, 31)));
        Assert.Contains("31/12/2569", WelfareRateTable.Missing(D(2026, 12, 31)));
    }

    [Theory]
    [InlineData(2031, 9, 1, 2031, 10, 31, true)]    // across the change
    [InlineData(2040, 1, 1, 2040, 12, 31, true)]    // inside เป็นต้นไป
    [InlineData(2026, 1, 1, 2027, 1, 1, true)]      // ends on the first day of a range
    [InlineData(2026, 1, 1, 2026, 12, 31, false)]   // just before
    [InlineData(2020, 1, 1, null, null, null, true)] // เป็นต้นไป from before overlaps all
    public void A_range_sharing_a_day_with_another_overlaps(int y1, int m1, int d1, int? y2, int? m2, int? d2, bool overlaps)
    {
        DateOnly? end = y2 is { } y ? D(y, m2!.Value, d2!.Value) : null;
        Assert.Equal(overlaps, WelfareRateTable.Overlapping(Seeded, D(y1, m1, d1), end) is not null);
    }

    [Theory]
    [InlineData("4692", "0.25", "12")]   // the slip of lot 4 (#180): 11.73 → 12
    [InlineData("10000", "0.50", "50")]
    [InlineData("200", "0.25", "1")]     // 0.5 → 1, half away from zero
    [InlineData("199", "0.25", "0")]
    public void The_contribution_is_the_wage_times_the_rate_in_whole_baht(string wage, string percent, string expected)
    {
        static decimal M(string s) => decimal.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(M(expected), WelfareRateTable.Contribution(M(wage), M(percent)));
    }

    [Fact]
    public void The_Excel_adds_both_portions_and_the_rate_after_the_funds_columns()
    {
        var report = new WelfareFundReport(D(2031, 9, 27), D(2031, 10, 26), null,
        [
            new WelfareFundLine("J001", "นายสมมุติ ทดสอบ", 30, "ไทย", "1234567890123", EmployeeTypes.Daily, 10000m, D(2020, 3, 1), null) { Contribution = 50m },
        ]) { RatePercent = 0.50m };

        var table = WelfareFundTable.Build(report, "บริษัท ทดสอบ จำกัด", D(2031, 11, 5));

        Assert.Equal(["เงินสมทบลูกจ้าง (บาท)", "เงินสมทบนายจ้าง (บาท)"], table.Columns.Skip(11).Select(c => c.Header));
        Assert.Equal(new object?[] { 50m, 50m }, table.Rows[0][11..]);
        Assert.Contains("ฝ่ายละ 0.5%", table.Filter);
    }
}

/// <summary>
/// อัตราเงินสมทบกองทุนสงเคราะห์ลูกจ้าง (UAT-43, #436) through the public services: the seeded ranges, saving with a history, overlap
/// refused, and ยอดเงินส่งกองทุนสงเคราะห์ลูกจ้าง taking the rate of its งวด. The seed covers 1 ม.ค. 2570 onward, so the ranges added
/// here sit before it. The report tests put their งวด and ranges in years 300+ of a แผนก of their own — below every other class's งวด
/// (MigratedPayPeriodTests' 450+), since PayPeriodTests' default-dates test needs its own period to be the latest — and their employees
/// take codes "I…", which no other class uses. Every name here is made up.
/// </summary>
[Collection(LocalDbCollection.Name)]
public sealed class WelfareRateDataTests(LocalDbFixture db)
{
    private static int lastCode;
    private static int lastYear = 300;

    private static int NewYear() => Interlocked.Increment(ref lastYear);

    private static DateOnly D(int year, int month, int day) => new(year, month, day);

    private static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..6];

    private async Task<T> AsHrAsync<T>(Func<TestScope, Task<T>> action)
    {
        await using var scope = db.Scope("hr1");
        return await action(scope);
    }

    private Task<T> RatesAsync<T>(Func<WelfareRateRegistry, Task<T>> action) => AsHrAsync(s => action(s.Get<WelfareRateRegistry>()));

    private static void Ok(ServiceResult result) => Assert.True(result.Succeeded, string.Join(", ", result.Errors));

    [Fact] // แก้ครั้งที่ 5: 1 ม.ค. 2570 – 30 ก.ย. 2574 = 0.25%, 1 ต.ค. 2574 เป็นต้นไป = 0.50%
    public async Task The_two_ranges_of_the_request_are_seeded_and_switch_on_1_October_2574()
    {
        var rates = await RatesAsync(r => r.ListAsync());

        Assert.Contains(rates, r => (r.StartDate, r.EndDate, r.Percent) == (D(2027, 1, 1), D(2031, 9, 30), 0.25m));
        Assert.Contains(rates, r => (r.StartDate, r.EndDate, r.Percent) == (D(2031, 10, 1), null, 0.50m));
        Assert.Equal(0.25m, await RatesAsync(r => r.PercentOnAsync(D(2031, 9, 30))));
        Assert.Equal(0.50m, await RatesAsync(r => r.PercentOnAsync(D(2031, 10, 1))));
        Assert.Null(await RatesAsync(r => r.PercentOnAsync(D(1985, 6, 1))));
    }

    [Fact] // a range sharing a day with another is refused, adding or editing; the seeded rows stay as they were
    public async Task An_overlapping_range_is_refused()
    {
        var after = await RatesAsync(r => r.SaveAsync(new WelfareRateInput(null, D(2040, 1, 1), D(2040, 12, 31), 0.75m, null)));
        Assert.False(after.Succeeded);
        Assert.Contains("ทับ", Assert.Single(after.Errors));

        var across = await RatesAsync(r => r.SaveAsync(new WelfareRateInput(null, D(2026, 6, 1), D(2027, 1, 1), 0.10m, null)));
        Assert.False(across.Succeeded);

        var seeded = (await RatesAsync(r => r.ListAsync())).Single(r => r.StartDate == D(2027, 1, 1));
        var stretched = await RatesAsync(r => r.SaveAsync(new WelfareRateInput(seeded.Id, seeded.StartDate, D(2031, 10, 1), seeded.Percent, seeded.RowVersion)));
        Assert.False(stretched.Succeeded);

        Assert.Equal(D(2031, 9, 30), (await RatesAsync(r => r.ListAsync())).Single(r => r.Id == seeded.Id).EndDate);
    }

    [Fact] // added, edited (its own dates are no overlap), deleted; a stale rowversion is a conflict; the history names who and what
    public async Task A_range_is_saved_edited_and_deleted_with_a_history()
    {
        Assert.False((await RatesAsync(r => r.SaveAsync(new WelfareRateInput(null, null, null, 0.25m, null)))).Succeeded);
        Assert.False((await RatesAsync(r => r.SaveAsync(new WelfareRateInput(null, D(1991, 1, 1), D(1990, 12, 31), 0.25m, null)))).Succeeded);
        Assert.False((await RatesAsync(r => r.SaveAsync(new WelfareRateInput(null, D(1991, 1, 1), D(1991, 12, 31), 0m, null)))).Succeeded);

        var added = await RatesAsync(r => r.SaveAsync(new WelfareRateInput(null, D(1991, 1, 1), D(1991, 12, 31), 0.3m, null)));
        Ok(added);
        var row = (await RatesAsync(r => r.ListAsync())).Single(r => r.Id == added.Id);
        Assert.Equal(0.3m, await RatesAsync(r => r.PercentOnAsync(D(1991, 12, 31))));

        Ok(await RatesAsync(r => r.SaveAsync(new WelfareRateInput(row.Id, D(1991, 1, 1), D(1991, 6, 30), 0.35m, row.RowVersion))));
        Assert.Null(await RatesAsync(r => r.PercentOnAsync(D(1991, 7, 1))));
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            RatesAsync(r => r.SaveAsync(new WelfareRateInput(row.Id, D(1991, 1, 1), D(1991, 3, 31), 0.4m, row.RowVersion))));

        var edited = (await RatesAsync(r => r.ListAsync())).Single(r => r.Id == added.Id);
        Ok(await RatesAsync(r => r.DeleteAsync(edited.Id, edited.RowVersion)));
        Assert.DoesNotContain(await RatesAsync(r => r.ListAsync()), r => r.Id == added.Id);

        await using var scope = db.Scope(AppUser.AdminUserName);
        var history = await scope.Get<HistoryService>().SearchAsync(new(Table: WelfareRateRegistry.Title));
        Assert.Contains(history, h => h.Column == "อัตรา %" && h.NewValue?.StartsWith("0.35") == true && h.UserName == "hr1");
    }

    // ---------- ยอดเงินส่งกองทุนสงเคราะห์ลูกจ้าง ----------

    private async Task<DepartmentDto> NewDepartmentAsync()
    {
        var sectionId = (await AsHrAsync(s => s.Get<OrgRegistry>().SaveSectionAsync(new SectionInput(null, Unique("ฝ่าย"), 1, null)))).Id!.Value;
        var id = (await AsHrAsync(s => s.Get<OrgRegistry>().SaveDepartmentAsync(new DepartmentInput(
            null, Unique("I"), Unique("แผนก"), "I", 9, sectionId, null, [], null)))).Id!.Value;
        return (await AsHrAsync(s => s.Get<OrgDirectory>().GetDepartmentAsync(id)))!;
    }

    private async Task<EmployeeDto> NewEmployeeAsync(DepartmentDto department, int type = EmployeeTypes.Daily, double salary = 0)
    {
        var result = await AsHrAsync(s => s.Get<EmployeeRegistry>().SaveAsync(new EmployeeInput
        {
            Code = $"I{Interlocked.Increment(ref lastCode):D3}", Name = "นาย ทดสอบ " + Guid.NewGuid().ToString("N")[..4], DepartmentId = department.Id,
            SectionId = department.SectionId, StartDate = D(2015, 3, 1), EmployeeType = type, Salary = salary, DailyWage = 400,
        }));
        Ok(result);
        return (await AsHrAsync(s => s.Get<EmployeeDirectory>().GetAsync(result.Id!.Value)))!;
    }

    private static PayResultImport Pay(EmployeeDto employee, decimal payNormal) => new(employee.Id, 0, 0, 0, 0, 0, payNormal, 0, 0, 0, 0);

    private Task PeriodAsync(DateOnly from, DateOnly to, params PayResultImport[] results)
        => AsHrAsync(async s =>
        {
            await ActivatorUtilities.CreateInstance<HrPayHistoryImport>(s.Get<IServiceProvider>())
                .AddAsync([new PayPeriodImport(from, to, to.AddDays(5), results)]);
            return 0;
        });

    private Task<WelfareFundReport> ReportAsync(DateOnly from, DateOnly to, DepartmentDto department)
        => AsHrAsync(s => s.Get<PayHistoryReportDirectory>().GetWelfareFundReportAsync(from, to, department.Id));

    [Fact] // the change of แก้ครั้งที่ 5 in a year of its own: งวดถึง 30 ก.ย. → 0.25%, งวดถึง 1 ต.ค. → 0.50%; both in one report: two rates, said not picked
    public async Task The_report_takes_the_rate_of_the_end_of_its_period_on_both_sides_of_1_October()
    {
        var year = NewYear();
        Ok(await RatesAsync(r => r.SaveAsync(new WelfareRateInput(null, D(year, 1, 1), D(year, 9, 30), 0.25m, null))));
        Ok(await RatesAsync(r => r.SaveAsync(new WelfareRateInput(null, D(year, 10, 1), D(year, 12, 31), 0.50m, null))));
        var department = await NewDepartmentAsync();
        var daily = await NewEmployeeAsync(department);
        var monthly = await NewEmployeeAsync(department, EmployeeTypes.Monthly, salary: 15000);
        await PeriodAsync(D(year, 9, 17), D(year, 9, 30), Pay(daily, 4692m), Pay(monthly, 7500m));
        await PeriodAsync(D(year, 10, 1), D(year, 10, 1), Pay(daily, 10000m), Pay(monthly, 7500m));

        var september = await ReportAsync(D(year, 9, 30), D(year, 9, 30), department);
        Assert.Equal((0.25m, (string?)null), (september.RatePercent, september.RateProblem));
        Assert.Equal([12m, 38m], september.Lines.Select(l => l.Contribution!.Value)); // 4692 → 11.73 → 12; รายเดือน 15000 → 37.5 → 38

        var october = await ReportAsync(D(year, 10, 1), D(year, 10, 1), department);
        Assert.Equal(0.50m, october.RatePercent);
        Assert.Equal([50m, 75m], october.Lines.Select(l => l.Contribution!.Value));

        var both = await ReportAsync(D(year, 9, 30), D(year, 10, 1), department);
        Assert.Null(both.RatePercent);
        Assert.Contains("อัตราเงินสมทบต่างกัน", both.RateProblem);
        Assert.All(both.Lines, l => Assert.Null(l.Contribution));
    }

    [Fact] // no range covers the งวด: the report says so (the Excel endpoint refuses with it), no amount is guessed
    public async Task A_period_no_rate_covers_is_reported_as_missing()
    {
        var year = NewYear();
        var department = await NewDepartmentAsync();
        var daily = await NewEmployeeAsync(department);
        await PeriodAsync(D(year, 5, 1), D(year, 5, 14), Pay(daily, 5000m));

        var report = await ReportAsync(D(year, 4, 27), D(year, 5, 26), department);

        Assert.Null(report.RatePercent);
        Assert.Equal(WelfareRateTable.Missing(D(year, 5, 14)), report.RateProblem);
        Assert.Null(Assert.Single(report.Lines).Contribution);
        Assert.Equal((null, report.RateProblem), await AsHrAsync(s => s.Get<PayHistoryReportDirectory>().WelfareRateOfAsync(D(year, 4, 27), D(year, 5, 26), department.Id)));
    }
}
