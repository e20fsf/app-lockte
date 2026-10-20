using System.Globalization;
using Lockte.Hr;
using Lockte.Org;
using Lockte.SharedKernel.Persistence;

namespace Lockte.Web.Tests;

/// <summary>
/// เอกสารพิมพ์รายบุคคล (#65) through the public services of module พนักงานและค่าแรง: BR-044 บันทึกการลาออก, BR-065
/// ใบลา, BR-070 หนังสือรับรอง / เกษียณ / ปลดออก — the values the legacy queries computed, ready for the ระดับ A pages.
/// </summary>
[Collection(LocalDbCollection.Name)]
public sealed class HrDocumentTests(LocalDbFixture db)
{
    private static int lastCode;

    /// <summary>"R" keeps clear of the other test classes' codes (T-EMPLO.ID is Text(4)).</summary>
    private static string NewCode() => $"R{Interlocked.Increment(ref lastCode):D3}";

    private static DateOnly Today => DateOnly.FromDateTime(TimeProvider.System.GetLocalNow().DateTime);

    private static DateOnly D(int year, int month, int day) => new(year, month, day);

    private static DateOnly Iso(string date) => DateOnly.ParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private async Task<T> AsHrAsync<T>(Func<TestScope, Task<T>> action)
    {
        await using var scope = db.Scope("hr1");
        return await action(scope);
    }

    private async Task<EmployeeDto> NewEmployeeAsync(EmployeeInput input)
    {
        var result = await AsHrAsync(s => s.Get<EmployeeRegistry>().SaveAsync(input with { Code = NewCode() }));
        Assert.True(result.Succeeded, string.Join(", ", result.Errors));
        return (await AsHrAsync(s => s.Get<EmployeeDirectory>().GetAsync(result.Id!.Value)))!;
    }

    /// <summary>A แผนก working กะ A and B with different names printed for each กะ.</summary>
    private Task<(int DepartmentId, int ShiftA, int ShiftB)> NewDepartmentAsync() => AsHrAsync(async s =>
    {
        var org = s.Get<OrgRegistry>();
        var directory = s.Get<OrgDirectory>();
        var unique = Guid.NewGuid().ToString("N")[..6];
        var section = (await org.SaveSectionAsync(new SectionInput(null, "ฝ่าย" + unique, 1, null))).Id!.Value;
        var a = (await directory.FindShiftByCodeAsync("A"))!.Id;
        var b = (await directory.FindShiftByCodeAsync("B"))!.Id;
        var saved = await org.SaveDepartmentAsync(new DepartmentInput(null, "R" + unique, "ประกอบ" + unique, "r", 1, section, null,
            [new DepartmentShiftInput(a, "หัวหน้า เอ", "ผจก.ฝ่าย เอ", "ผจก.โรงงาน เอ"), new DepartmentShiftInput(b, "หัวหน้า บี", "ผจก.ฝ่าย บี", "ผจก.โรงงาน บี")],
            null));
        Assert.True(saved.Succeeded, string.Join(", ", saved.Errors));
        return (saved.Id!.Value, a, b);
    });

    [Fact] // BR-044: วันที่ลาออก → วันลาออก, เหตุผล → หมายเหตุ; the employee is then in รายงานพนักงานที่ลาออก
    public async Task Resigning_writes_the_date_and_reason_and_lists_the_employee()
    {
        var employee = await NewEmployeeAsync(new EmployeeInput { Name = "นาย ลาออก ทดสอบ", StartDate = D(2020, 2, 10) });

        var result = await AsHrAsync(s => s.Get<ResignationRegistry>().ResignAsync(employee.Id, D(2021, 1, 4), " กลับบ้าน ", employee.RowVersion));

        Assert.True(result.Succeeded, string.Join(", ", result.Errors));
        var saved = (await AsHrAsync(s => s.Get<EmployeeDirectory>().GetAsync(employee.Id)))!;
        Assert.Equal(D(2021, 1, 4), saved.ResignDate);
        Assert.Equal("กลับบ้าน", saved.Memo);
        var row = Assert.Single(await AsHrAsync(s => s.Get<HrDocumentDirectory>().ListResignedAsync()), r => r.Code == employee.Code);
        Assert.Equal(329 / 365.0, row.Years!.Value, 10); // (RESIGN − START) / 365 → prints 0.90
        Assert.Equal("กลับบ้าน", row.Reason);
    }

    [Fact] // the list is by วันลาออก, then รหัส, and has no one still working
    public async Task The_resigned_list_is_ordered_by_resign_date_then_code()
    {
        var late = await NewEmployeeAsync(new EmployeeInput { Name = "ก", ResignDate = D(1990, 1, 2) });
        var early = await NewEmployeeAsync(new EmployeeInput { Name = "ข", ResignDate = D(1990, 1, 1) });
        var working = await NewEmployeeAsync(new EmployeeInput { Name = "ค" });

        var codes = (await AsHrAsync(s => s.Get<HrDocumentDirectory>().ListResignedAsync())).Select(r => r.Code).ToList();

        Assert.True(codes.IndexOf(early.Code) < codes.IndexOf(late.Code));
        Assert.DoesNotContain(working.Code, codes);
    }

    [Fact] // a save by someone else in between is not overwritten
    public async Task Resigning_with_a_stale_rowversion_is_refused()
    {
        var employee = await NewEmployeeAsync(new EmployeeInput { Name = "นาย ชน ทดสอบ" });
        await AsHrAsync(s => s.Get<ResignationRegistry>().ResignAsync(employee.Id, D(2026, 1, 1), "ก", employee.RowVersion));

        await Assert.ThrowsAsync<ConcurrencyConflictException>(
            () => AsHrAsync(s => s.Get<ResignationRegistry>().ResignAsync(employee.Id, D(2026, 2, 1), "ข", employee.RowVersion)));
    }

    [Fact] // RPT-55 ใบลาออก: names of the แผนก in the employee's own กะ (JOIN ON DEPART AND SHIFT), date/reason as typed
    public async Task The_resignation_letter_prints_the_names_of_the_department_in_the_employees_shift()
    {
        var (department, _, shiftB) = await NewDepartmentAsync();
        var employee = await NewEmployeeAsync(new EmployeeInput { Name = "นางสาว ทองใส", DepartmentId = department, ShiftId = shiftB, StartDate = D(1992, 10, 26) });

        var letter = (await AsHrAsync(s => s.Get<HrDocumentDirectory>().GetResignationLetterAsync(employee.Id, D(2026, 10, 2), "กลับบ้าน")))!;

        Assert.Equal(("B", "หัวหน้า บี", "ผจก.ฝ่าย บี", "ผจก.โรงงาน บี"), (letter.ShiftCode, letter.SupervisorName, letter.ManagerName, letter.FactoryManagerName));
        Assert.Equal((D(1992, 10, 26), (DateOnly?)D(2026, 10, 2), "กลับบ้าน"), (letter.StartDate!.Value, letter.ResignDate, letter.Reason));
        Assert.StartsWith("ประกอบ", letter.DepartmentName);
    }

    [Fact] // RPT-50: วันลาสะสมของปีนี้ by SR and the total; leave of last year is not counted
    public async Task The_leave_form_sums_this_years_leave_by_type()
    {
        var employee = await NewEmployeeAsync(new EmployeeInput { Name = "นาง ใบลา", Sex = "F", StartDate = Today.AddYears(-3), DailyWage = 400 });
        async Task LeaveAsync(DateOnly from, double days, string type)
        {
            var saved = await AsHrAsync(s => s.Get<LeaveRegistry>().SaveAsync(
                new LeaveInput(null, employee.Id, null, from, from, null, null, days, type, false, null, null, null)));
            Assert.True(saved.Save.Succeeded, string.Join(", ", saved.Save.Errors));
        }
        var thisYear = new DateOnly(Today.Year, 1, 5);
        await LeaveAsync(thisYear, 2.5, "1");
        await LeaveAsync(thisYear.AddDays(1), 2, "1");
        await LeaveAsync(thisYear.AddDays(2), 0.125, "3");
        await LeaveAsync(thisYear.AddYears(-1), 7, "1");

        var form = (await AsHrAsync(s => s.Get<HrDocumentDirectory>().GetLeaveFormAsync(employee.Id)))!;

        Assert.Equal(4.625, form.TotalDays);
        Assert.Equal(4.5, form.DaysByType["1"]);
        Assert.Equal(0.125, form.DaysByType["3"]);
        Assert.False(form.DaysByType.ContainsKey("2"));
        Assert.False(form.OnProbation);
        Assert.Equal(Today.Year, form.Year);
        Assert.Equal("มีสิทธิลาพักร้อน 6 วัน", form.VacationText);
        Assert.Equal("ลากิจได้ค่าแรง 6 วัน", form.PersonalLeaveText);
        Assert.Equal((0, 0), (form.VerbalWarnings, form.WrittenWarnings));
    }

    [Fact] // no leave this year → total and types print blank; under 90 days the X goes in รอบรรจุ
    public async Task A_new_employee_without_leave_has_no_totals_and_is_waiting_for_probation()
    {
        var employee = await NewEmployeeAsync(new EmployeeInput { Name = "นาย ใหม่", StartDate = Today.AddDays(-10) });

        var form = (await AsHrAsync(s => s.Get<HrDocumentDirectory>().GetLeaveFormAsync(employee.Id)))!;

        Assert.Null(form.TotalDays);
        Assert.Empty(form.DaysByType);
        Assert.True(form.OnProbation);
        Assert.Equal("ไม่มีสิทธิลาพักร้อน", form.VacationText);
        Assert.Equal("ลากิจได้ค่าแรง 3 วัน", form.PersonalLeaveText);
    }

    [Theory] // BR-065 table (วันนี้ = 04/10/2026) + the month steps of คิวรี พิมพ์ใบลางาน14
    [InlineData("2026-03-10", "ไม่มีสิทธิลาพักร้อน")]
    [InlineData("2025-03-15", "มีสิทธิลาพักร้อน 11 วัน")]
    [InlineData("2020-01-01", "มีสิทธิลาพักร้อน 6 วัน")]
    [InlineData("2025-01-01", "มีสิทธิลาพักร้อน 12 วัน")]
    [InlineData("2024-12-01", "มีสิทธิลาพักร้อน 6 วัน")]
    [InlineData("2025-10-05", "มีสิทธิลาพักร้อน 7.5 วัน")] // วันนี้ − START + 1 = 365
    [InlineData("2025-10-06", "ไม่มีสิทธิลาพักร้อน")]     // = 364
    public void Vacation_entitlement_text_follows_BR_065(string start, string text)
        => Assert.Equal(text, HrDocumentDirectory.VacationText(Iso(start), D(2026, 10, 4)));

    [Theory] // Expr10: < 30 วัน → "n วัน"; ปี ≤ 0 → "m  เดือน"; เดือน ≤ 0 → "y  ปี"; else "y  ปี  m  เดือน" (DateDiff "m")
    [InlineData("2026-10-01", "14 วัน")]
    [InlineData("2026-09-10", "1  เดือน")]
    [InlineData("2025-10-20", "1  ปี")] // DateDiff counts calendar months: 12, though not a full year
    [InlineData("2019-07-22", "7  ปี  3  เดือน")]
    [InlineData("1990-03-21", "36  ปี  7  เดือน")]
    public void Service_text_follows_the_legacy_query(string start, string text)
        => Assert.Equal(text, HrDocumentDirectory.ServiceText(Iso(start), new DateTime(2026, 10, 15, 9, 0, 0)));

    [Theory] // Expr1 = Round(Now − START) with the time of day, banker's rounding (RND-B)
    [InlineData(11, 100)]
    [InlineData(12, 100)] // 100.5 → 100 (to even)
    [InlineData(13, 101)]
    public void Service_days_round_now_minus_start(int hour, double days)
        => Assert.Equal(days, HrDocumentDirectory.ServiceDays(D(2026, 7, 7), new DateTime(2026, 10, 15, hour, 0, 0)));

    [Theory] // BR-070 ค่าชดเชย table: < 120 → 0, then columns 1..6
    [InlineData(100, 0)]
    [InlineData(119, 0)]
    [InlineData(120, 30)]
    [InlineData(364, 30)]
    [InlineData(400, 90)]
    [InlineData(1095, 180)]
    [InlineData(2190, 240)]
    [InlineData(3649, 240)]
    [InlineData(3650, 300)]
    [InlineData(7299, 300)]
    [InlineData(8000, 400)]
    public void Compensation_days_follow_the_scale(double serviceDays, int days)
        => Assert.Equal(days, SeveranceScale.Legacy.DaysFor(serviceDays));

    [Fact] // BR-070 #3: ค่าชดเชย = RND-A(วันชดเชย × SA_DAY); a scale typed on the screen is used; the reason is kept for ปลดออก
    public async Task Severance_pays_the_days_of_the_scale_times_the_daily_wage()
    {
        var employee = await NewEmployeeAsync(new EmployeeInput { Name = "นาง เกษียณ", StartDate = Today.AddDays(-8000), DailyWage = 520.25 });

        var data = (await AsHrAsync(s => s.Get<HrDocumentDirectory>().GetSeveranceAsync(employee.Id, SeveranceScale.Legacy, " ป่วยบ่อย ")))!;

        Assert.Equal(400, data.CompensationDays);
        Assert.Equal(208100m, data.Compensation); // 400 × 520.25
        Assert.Equal("ป่วยบ่อย", data.Reason);
        Assert.Equal(520.25, data.DailyWage);

        var scale = new SeveranceScale([1, 2, 3, 4, 5, 7], SeveranceScale.Legacy.Lines);
        var custom = (await AsHrAsync(s => s.Get<HrDocumentDirectory>().GetSeveranceAsync(employee.Id, scale, null)))!;
        Assert.Equal(3642m, custom.Compensation); // 7 × 520.25 = 3641.75 → 3642
    }

    [Fact] // BR-070: monthly → SALARY, daily → SA_DAY × 26; texts as the legacy query joined them; age = Int((Now − BORN)/365)
    public async Task The_certificate_prints_the_monthly_pay_and_the_legacy_texts()
    {
        var (department, shiftA, _) = await NewDepartmentAsync();
        var monthly = await NewEmployeeAsync(new EmployeeInput
        {
            Name = "นางสาว หนูกานต์", EmployeeType = EmployeeTypes.Monthly, Salary = 15600, DailyWage = 520, Position = "พนักงาน",
            DepartmentId = department, ShiftId = shiftA, StartDate = D(2019, 7, 22), BirthDate = Today.AddDays(-(365 * 44 + 20)),
        });
        var daily = await NewEmployeeAsync(new EmployeeInput { Name = "นาย รายวัน", EmployeeType = EmployeeTypes.Daily, DailyWage = 372, StartDate = D(2019, 7, 22) });

        var person = (await AsHrAsync(s => s.Get<HrDocumentDirectory>().GetCertificateAsync(monthly.Id)))!;
        var other = (await AsHrAsync(s => s.Get<HrDocumentDirectory>().GetCertificateAsync(daily.Id)))!;

        Assert.Equal(15600, person.MonthlyPay);
        Assert.Equal(9672, other.MonthlyPay);
        Assert.Equal("22  เดือน  กรกฎาคม  พ.ศ.  2562", person.StartText);
        Assert.Equal(44, person.Age);
        Assert.Equal("พนักงาน", person.Position);
        Assert.Equal("ผจก.โรงงาน เอ", person.FactoryManagerName);
        Assert.Equal(HrDocumentDirectory.LongText(Today), person.IssuedText);
        Assert.Null(other.Age); // no birth date: blank, not #Error (D-3)
        Assert.Equal("", other.BirthText);
    }

    [Fact]
    public void Long_text_joins_day_month_name_and_buddhist_year_with_two_spaces()
        => Assert.Equal("28  เดือน  กันยายน  พ.ศ.  2525", HrDocumentDirectory.LongText(D(1982, 9, 28)));
}
