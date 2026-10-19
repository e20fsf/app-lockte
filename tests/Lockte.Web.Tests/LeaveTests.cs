using Lockte.Hr;
using Lockte.SharedKernel.Persistence;
using Lockte.Web.Audit;

namespace Lockte.Web.Tests;

/// <summary>
/// การลา + โควตา (#50, SCR-30/31, BR-020..022) through the public services of module พนักงานและค่าแรง, in the order HR
/// works: the quota is checked when ได้ค่าแรง is chosen, which is also when the legacy saved the row (Macro1).
/// Every test has its own employees, so their quotas never meet.
/// </summary>
[Collection(LocalDbCollection.Name)]
public sealed class LeaveTests(LocalDbFixture db)
{
    private const string Saved = "คลิกปุ่ม OK เพื่อทำการบันทึก";
    private const int Year = 2026;
    private static int lastCode;

    /// <summary>"L" keeps clear of the other test classes' codes (T-EMPLO.ID is Text(4)).</summary>
    private static string NewCode() => $"L{Interlocked.Increment(ref lastCode):D3}";

    private static DateOnly Today => DateOnly.FromDateTime(TimeProvider.System.GetLocalNow().DateTime);

    private static DateOnly Day(int month, int day, int year = Year) => new(year, month, day);

    private async Task<T> AsHrAsync<T>(Func<TestScope, Task<T>> action)
    {
        await using var scope = db.Scope("hr1");
        return await action(scope);
    }

    private async Task<EmployeeDto> NewEmployeeAsync(string sex = "F", DateOnly? start = null, double dailyWage = 400)
    {
        var result = await AsHrAsync(s => s.Get<EmployeeRegistry>().SaveAsync(new EmployeeInput
        {
            Code = NewCode(), Name = "นาง ทดสอบ " + Guid.NewGuid().ToString("N")[..4], Sex = sex,
            StartDate = start ?? Today.AddYears(-3), DailyWage = dailyWage,
        }));
        Assert.True(result.Succeeded, string.Join(", ", result.Errors));
        return (await AsHrAsync(s => s.Get<EmployeeDirectory>().GetAsync(result.Id!.Value)))!;
    }

    private static LeaveInput Leave(EmployeeDto employee, DateOnly from, double days, string type, bool? paid, DateOnly? to = null)
        => new(null, employee.Id, null, from, to ?? from, null, null, days, type, paid, null, null, null);

    private Task<LeaveSaveResult> SaveAsync(LeaveInput input) => AsHrAsync(s => s.Get<LeaveRegistry>().SaveAsync(input));

    private async Task<int> AddAsync(LeaveInput input)
    {
        var result = await SaveAsync(input);
        Assert.True(result.Save.Succeeded, string.Join(", ", result.Save.Errors));
        return result.Save.Id!.Value;
    }

    private Task<IReadOnlyList<LeaveDto>> ListAsync(EmployeeDto employee, int year = Year)
        => AsHrAsync(s => s.Get<LeaveDirectory>().ListOfEmployeeAsync(employee.Id, year));

    private Task<LeaveSummary> SummaryAsync(EmployeeDto employee, int year = Year)
        => AsHrAsync(s => s.Get<LeaveDirectory>().GetSummaryAsync(employee.Id, year));

    [Fact] // AC-08 #1 (BR-020): ใช้ไปแล้ว SR1 + SR9 = 5, ลาใหม่ SR1 1 วันได้ค่าแรง → บันทึก (=6 พอดี)
    public async Task Case_1_personal_leave_up_to_six_days_is_saved_with_the_legacy_message()
    {
        var employee = await NewEmployeeAsync();
        await AddAsync(Leave(employee, Day(2, 2), 3, "1", true, Day(2, 4)));
        await AddAsync(Leave(employee, Day(3, 9), 2, "9", true, Day(3, 10)));

        var result = await SaveAsync(Leave(employee, Day(5, 4), 1, "1", true));

        Assert.True(result.Save.Succeeded, string.Join(", ", result.Save.Errors));
        Assert.Equal(new LeaveQuotaCheck(true, Saved), result.Quota);
        Assert.Equal("บันทึก", result.Quota!.Title);
        Assert.Equal(3, (await ListAsync(employee)).Count);
        Assert.Equal(6, (await SummaryAsync(employee)).Quotas.Single(q => q.Name == "ลากิจ").Used);
    }

    [Fact] // AC-08 #2: SR=9, ใช้ไปแล้ว 5.5, ลาใหม่ 1 → ไม่บันทึก
    public async Task Case_2_emergency_leave_over_six_days_is_not_saved()
    {
        var employee = await NewEmployeeAsync();
        await AddAsync(Leave(employee, Day(1, 12), 5.5, "1", true, Day(1, 17)));

        var result = await SaveAsync(Leave(employee, Day(6, 1), 1, "9", true));

        Assert.False(result.Save.Succeeded);
        Assert.Equal(new LeaveQuotaCheck(false, "ไม่สามารถบันทึกได้ เนื่องจากลากิจได้ค่าแรงเกิน 6 วัน"), result.Quota);
        Assert.Equal("ไม่บันทึก", result.Quota!.Title);
        Assert.Equal([result.Quota.Message!], result.Save.Errors);
        Assert.Single(await ListAsync(employee));
    }

    [Fact] // AC-08 #3: SR=6, อายุงาน 200 วัน → ไม่บันทึก "ยังไม่มีพักร้อน"
    public async Task Case_3_vacation_before_a_year_of_work_is_not_saved()
    {
        var employee = await NewEmployeeAsync(start: Today.AddDays(-200));

        var result = await SaveAsync(Leave(employee, Today, 1, "6", true));

        Assert.Equal(new LeaveQuotaCheck(false, "ไม่สามารถบันทึกได้ เนื่องจากยังไม่มีพักร้อน"), result.Quota);
        Assert.Empty(await ListAsync(employee, Today.Year));
    }

    [Fact] // AC-08 #4: SR=4, SEX=M → ไม่บันทึก "เพศชายลาคลอดไม่ได้"
    public async Task Case_4_maternity_leave_of_a_man_is_not_saved()
    {
        var employee = await NewEmployeeAsync(sex: "M");

        var result = await SaveAsync(Leave(employee, Day(7, 1), 1, "4", true));

        Assert.Equal(new LeaveQuotaCheck(false, "ไม่สามารถบันทึกได้ เนื่องจากเพศชายลาคลอดไม่ได้"), result.Quota);
        Assert.Empty(await ListAsync(employee));
    }

    [Fact] // AC-08 #5: SR=8, SAL=N → บันทึก (ไม่ตรวจ)
    public async Task Case_5_unpaid_absence_is_saved_unchecked()
    {
        var employee = await NewEmployeeAsync();

        var result = await SaveAsync(Leave(employee, Day(8, 3), 1, "8", false));

        Assert.True(result.Save.Succeeded, string.Join(", ", result.Save.Errors));
        Assert.Equal(new LeaveQuotaCheck(true, Saved), result.Quota);
        var row = Assert.Single(await ListAsync(employee));
        Assert.Equal(("8", (bool?)false, 1.0, 0.0), (row.Type, row.Paid, row.Days, row.Pay));
    }

    [Fact] // Q-27 (#23): โควตานับรอบปีปฏิทิน 1 ม.ค.–31 ธ.ค. ตามวันเริ่มลา แทนการรีเซ็ตเมื่อย้ายเข้า LEAVE_ADD
    public async Task The_quota_counts_the_calendar_year_of_the_leave()
    {
        var employee = await NewEmployeeAsync();
        await AddAsync(Leave(employee, Day(12, 31, Year - 1), 6, "1", true, Day(1, 7)));   // starts last year
        await AddAsync(Leave(employee, Day(1, 1, Year + 1), 6, "1", true));                 // next year

        Assert.Equal(new LeaveQuotaCheck(true, Saved), (await SaveAsync(Leave(employee, Day(1, 1), 6, "1", true))).Quota);
        Assert.Equal(new LeaveQuotaCheck(false, "ไม่สามารถบันทึกได้ เนื่องจากลากิจได้ค่าแรงเกิน 6 วัน"),
            (await SaveAsync(Leave(employee, Day(12, 31), 0.125, "9", true))).Quota);
        Assert.Equal([Day(1, 1)], (await ListAsync(employee)).Select(r => r.FromDate));
    }

    [Fact] // BR-020: only ได้ค่าแรง rows use the quota
    public async Task Unpaid_leave_does_not_use_the_quota()
    {
        var employee = await NewEmployeeAsync();
        await AddAsync(Leave(employee, Day(2, 2), 10, "2", false));

        Assert.True((await SaveAsync(Leave(employee, Day(3, 2), 30, "3", true))).Save.Succeeded);
    }

    [Fact] // ⚠ kept (#41 จุดตรวจเดิม): the check runs when ได้ค่าแรง is chosen; editing SUM_D later is not checked again
    public async Task The_quota_is_checked_only_when_paid_is_chosen()
    {
        var employee = await NewEmployeeAsync();
        await AddAsync(Leave(employee, Day(4, 1), 6, "1", true));
        var row = Assert.Single(await ListAsync(employee));

        var longer = await SaveAsync(Leave(employee, Day(4, 1), 8, "1", true) with { Id = row.Id, RowVersion = row.RowVersion });

        Assert.True(longer.Save.Succeeded, string.Join(", ", longer.Save.Errors));
        Assert.Null(longer.Quota);
        Assert.Equal(8, Assert.Single(await ListAsync(employee)).Days);
    }

    [Fact] // choosing ได้ค่าแรง on a saved row checks it without counting the row itself
    public async Task Changing_a_saved_row_to_paid_checks_the_quota_without_the_row_itself()
    {
        var employee = await NewEmployeeAsync();
        await AddAsync(Leave(employee, Day(4, 1), 2, "1", true));
        var id = await AddAsync(Leave(employee, Day(5, 1), 4, "1", false));
        var row = (await ListAsync(employee)).Single(r => r.Id == id);

        var paid = await SaveAsync(Leave(employee, Day(5, 1), 4, "1", true) with { Id = id, RowVersion = row.RowVersion });

        Assert.Equal(new LeaveQuotaCheck(true, Saved), paid.Quota);
        row = (await ListAsync(employee)).Single(r => r.Id == id);
        var over = await SaveAsync(Leave(employee, Day(5, 1), 4.5, "1", false) with { Id = id, RowVersion = row.RowVersion });
        Assert.Equal(new LeaveQuotaCheck(true, Saved), over.Quota); // SAL = N: never checked
        row = (await ListAsync(employee)).Single(r => r.Id == id);
        var again = await SaveAsync(Leave(employee, Day(5, 1), 4.5, "1", true) with { Id = id, RowVersion = row.RowVersion });
        Assert.False(again.Quota!.CanSave);
        row = (await ListAsync(employee)).Single(r => r.Id == id);
        Assert.Equal(((bool?)false, 4.5), (row.Paid, row.Days));
    }

    [Fact] // BR-021: PAY = SUM_D × SALA_DAY (ไม่ปัด) for ได้ค่าแรง; SALA_DAY is the employee's ค่าแรง/วัน when recorded
    public async Task Paid_leave_gets_its_pay_from_the_daily_wage_copied_when_recorded()
    {
        var employee = await NewEmployeeAsync(dailyWage: 400);

        await AddAsync(Leave(employee, Day(9, 1), 0.5, "2", true));
        await AddAsync(Leave(employee, Day(9, 2), 0.375, "2", true) with { DailyWage = 333.5 });
        await AddAsync(Leave(employee, Day(9, 3), 1, "8", false));

        Assert.Equal([(400.0, 200.0), (333.5, 0.375 * 333.5), (400.0, 0.0)], (await ListAsync(employee)).Select(r => (r.DailyWage, r.Pay)));
    }

    [Fact] // BR-021 ⚠ kept: ลางานได้ค่าแรง only sets rows whose PAY = 0, so a later edit keeps the first PAY
    public async Task The_pay_is_set_once_and_kept_on_later_edits()
    {
        var employee = await NewEmployeeAsync(dailyWage: 400);
        var id = await AddAsync(Leave(employee, Day(9, 1), 0.5, "2", true));
        var row = Assert.Single(await ListAsync(employee));

        await AddAsync(Leave(employee, Day(9, 1), 1, "2", true) with { Id = id, RowVersion = row.RowVersion });

        var edited = Assert.Single(await ListAsync(employee));
        Assert.Equal((1.0, 200.0), (edited.Days, edited.Pay));
    }

    [Fact] // SCR-30 defaults: วันที่บันทึก = วันนี้, 08:00–17:00
    public async Task A_new_row_takes_todays_date_and_the_default_times()
    {
        var employee = await NewEmployeeAsync();

        await AddAsync(Leave(employee, Day(10, 1), 1, "1", false));

        var row = Assert.Single(await ListAsync(employee));
        Assert.Equal((Today, new TimeOnly(8, 0), new TimeOnly(17, 0), employee.Code), (row.RecordedOn, row.FromTime, row.ToTime, row.EmployeeCode));
    }

    [Fact] // BR-051 (a row without SUM_D is never kept) + what a row needs
    public async Task A_row_needs_an_employee_dates_in_order_days_a_type_and_a_short_remark()
    {
        var employee = await NewEmployeeAsync();
        var ok = Leave(employee, Day(3, 3), 1, "1", false);

        Assert.Equal(["กรุณาเลือกพนักงาน"], (await SaveAsync(ok with { EmployeeId = null })).Save.Errors);
        Assert.Equal(["ไม่พบพนักงานที่เลือก"], (await SaveAsync(ok with { EmployeeId = -1 })).Save.Errors);
        Assert.Equal(["กรุณากรอกวันที่ลา"], (await SaveAsync(ok with { ToDate = null })).Save.Errors);
        Assert.Equal(["วันที่เริ่มต้องไม่หลังวันที่สิ้นสุด"], (await SaveAsync(ok with { ToDate = Day(3, 2) })).Save.Errors);
        Assert.Equal(["กรุณากรอกจำนวนวัน"], (await SaveAsync(ok with { Days = null })).Save.Errors);
        Assert.Equal(["จำนวนวันต้องมากกว่า 0"], (await SaveAsync(ok with { Days = 0 })).Save.Errors);
        Assert.Equal(["กรุณาเลือกประเภทการลา"], (await SaveAsync(ok with { Type = " " })).Save.Errors);
        Assert.Equal(["ไม่พบประเภทการลานี้"], (await SaveAsync(ok with { Type = "17" })).Save.Errors);
        Assert.Single((await SaveAsync(ok with { Remark = new string('ก', 29) })).Save.Errors, e => e.Contains("28"));
        var invalidPaid = await SaveAsync(ok with { Days = null, Paid = true });
        Assert.Null(invalidPaid.Quota); // not checked: the row is not valid yet
        Assert.Empty(await ListAsync(employee));
    }

    [Fact] // subform LEAVE1: the employee's leave of the year, OrderBy FROM_D, id
    public async Task The_leave_of_one_employee_and_year_is_listed_by_start_date_then_entry()
    {
        var (employee, other) = (await NewEmployeeAsync(), await NewEmployeeAsync());
        var may = await AddAsync(Leave(employee, Day(5, 1), 1, "1", false));
        var march = await AddAsync(Leave(employee, Day(3, 1), 1, "2", false));
        var mayAgain = await AddAsync(Leave(employee, Day(5, 1), 0.5, "3", false));
        await AddAsync(Leave(employee, Day(5, 1, Year + 1), 1, "1", false));
        await AddAsync(Leave(other, Day(4, 1), 1, "1", false));

        Assert.Equal([march, may, mayAgain], (await ListAsync(employee)).Select(r => r.Id));
    }

    [Fact] // SCR-30 สรุปการลา subform1: วันลาได้ค่าแรงแยกประเภท, รวม, รวมค่าแรง, และโควตาเทียบสิทธิ์
    public async Task The_summary_shows_paid_days_by_type_and_each_quota_against_its_limit()
    {
        var employee = await NewEmployeeAsync(sex: "M", start: Today.AddDays(-100), dailyWage: 500);
        await AddAsync(Leave(employee, Day(1, 5), 1.5, "1", true));
        await AddAsync(Leave(employee, Day(1, 6), 1, "9", true));
        await AddAsync(Leave(employee, Day(2, 1), 2, "2", true));
        await AddAsync(Leave(employee, Day(2, 2), 4, "3", false));
        await AddAsync(Leave(employee, Day(2, 1, Year - 1), 3, "2", true));

        var summary = await SummaryAsync(employee);

        Assert.Equal(new Dictionary<string, double> { ["1"] = 1.5, ["9"] = 1, ["2"] = 2 }, summary.PaidDaysByType.ToDictionary());
        Assert.Equal((4.5, 4.5 * 500), (summary.PaidDays, summary.PaidPay));
        Assert.Equal(
            [("ลากิจ", 2.5, 6.0, (string?)null), ("ลาป่วย", 2, 30, null), ("ลาพักร้อน", 0, 12, "ยังไม่มีพักร้อน"),
             ("ลาคลอด", 0, 60, "เพศชายลาคลอดไม่ได้"), ("ลาเพื่อระดมพล", 0, 60, null)],
            summary.Quotas.Select(q => (q.Name, q.Used, q.Limit, q.Note)));
        Assert.Equal(3.5, summary.Quotas[0].Left);
    }

    [Fact] // BR-022 (SCR-31): ลบการลาของพนักงานที่ FROM_D อยู่ในช่วง DATE_F–DATE_T
    public async Task Deleting_a_date_range_removes_the_employees_leave_starting_in_it()
    {
        var (employee, other) = (await NewEmployeeAsync(), await NewEmployeeAsync());
        await AddAsync(Leave(employee, Day(6, 1), 1, "1", false));
        await AddAsync(Leave(employee, Day(6, 10), 3, "2", false, Day(6, 20)));   // ends after the range: still deleted
        var before = await AddAsync(Leave(employee, Day(5, 31), 2, "2", false, Day(6, 1)));
        var after = await AddAsync(Leave(employee, Day(6, 11), 1, "1", false));
        await AddAsync(Leave(other, Day(6, 5), 1, "1", false));

        var result = await AsHrAsync(s => s.Get<LeaveRegistry>().DeleteRangeAsync(employee.Id, Day(6, 1), Day(6, 10)));

        Assert.Equal((true, (int?)2), (result.Succeeded, result.Id));
        Assert.Equal([before, after], (await ListAsync(employee)).Select(r => r.Id));
        Assert.Single(await ListAsync(other));
        var backwards = await AsHrAsync(s => s.Get<LeaveRegistry>().DeleteRangeAsync(employee.Id, Day(6, 2), Day(6, 1)));
        Assert.Equal(["วันที่เริ่มต้องไม่หลังวันที่สิ้นสุด"], backwards.Errors);
    }

    [Fact] // SCR-30: แก้และลบทีละแถว with the rowversion the screen loaded
    public async Task A_row_is_edited_and_deleted_through_the_rowversion_the_screen_loaded()
    {
        var employee = await NewEmployeeAsync();
        var id = await AddAsync(Leave(employee, Day(7, 7), 1, "1", false));
        var loaded = Assert.Single(await ListAsync(employee));

        await AddAsync(Leave(employee, Day(7, 7), 1, "2", false) with { Id = id, RowVersion = loaded.RowVersion, Remark = "ไข้หวัด" });

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            AsHrAsync(s => s.Get<LeaveRegistry>().DeleteAsync(id, loaded.RowVersion)));
        var now = Assert.Single(await ListAsync(employee));
        Assert.Equal(("2", "ลาป่วยมีใบแพทย์", "ไข้หวัด"), (now.Type, now.TypeName, now.Remark));
        Assert.True((await AsHrAsync(s => s.Get<LeaveRegistry>().DeleteAsync(id, now.RowVersion))).Succeeded);
        Assert.Empty(await ListAsync(employee));
    }

    [Fact] // #41: คีย์มือเข้าประวัติรายช่อง (also the rows ลบตามช่วงวัน takes, hand work on one person)
    public async Task Leave_entry_and_range_deletes_are_kept_row_by_row_in_the_history()
    {
        var employee = await NewEmployeeAsync();
        await AddAsync(Leave(employee, Day(8, 8), 1, "1", false));
        await AsHrAsync(s => s.Get<LeaveRegistry>().DeleteRangeAsync(employee.Id, Day(8, 8), Day(8, 8)));

        await using var scope = db.Scope();
        var history = await scope.Get<HistoryService>().SearchAsync(new(Table: LeaveRegistry.LeaveTable, RowKey: employee.Code));
        Assert.Contains(history, h => h.Action == HistoryActions.Added && h.UserName == "hr1");
        Assert.Contains(history, h => h.Action == HistoryActions.Deleted && h.UserName == "hr1");
    }
}
