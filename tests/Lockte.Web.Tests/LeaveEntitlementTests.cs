using Lockte.Hr;
using Lockte.SharedKernel;

namespace Lockte.Web.Tests;

/// <summary>ตั้งค่าสิทธิ์วันลา (UAT 10): the rules without the database.</summary>
public sealed class LeaveEntitlementRuleTests
{
    private static readonly LeaveEntitlementDays Law2570 = LeaveEntitlementDays.Default with { PersonalFirstYear = 4, Personal = 8, Sick = 35, Vacation = 12 };

    [Fact] // the defaults are the limits the program had before (BR-020, #346, #347)
    public void The_default_days_are_the_limits_before_uat_10()
    {
        Assert.Equal(new LeaveEntitlementDays(3, 6, 30, 6, 60, 60, 15, 15), LeaveEntitlementDays.Default);
        Assert.All(LeaveQuota.Limits, l => Assert.Equal(l.Types[0] == "6" ? 6 : l.Days, LeaveEntitlementDays.Default.LimitOf(l.Types[0])));
    }

    [Fact]
    public void A_changed_limit_lets_more_days_through_and_its_message_shows_the_new_days()
    {
        Assert.False(LeaveQuota.Check("2", true, 1, new Dictionary<string, double> { ["2"] = 30 }, "F", true).CanSave);
        Assert.True(LeaveQuota.Check("2", true, 5, new Dictionary<string, double> { ["2"] = 30 }, "F", true, entitlement: Law2570).CanSave);
        Assert.Equal("ไม่สามารถบันทึกได้ เนื่องจากลาป่วยได้ค่าแรงเกิน 35 วัน",
            LeaveQuota.Check("2", true, 6, new Dictionary<string, double> { ["2"] = 30 }, "F", true, entitlement: Law2570).Message);
    }

    [Fact] // ลากิจ: the first-year and the after-a-year days both come from the range
    public void Personal_leave_takes_both_days_from_the_entitlement()
    {
        var start = new DateOnly(2027, 3, 1);
        Assert.Equal(4, LeaveQuota.PersonalLimit(start, new DateOnly(2027, 6, 1), Law2570));
        Assert.Equal(8, LeaveQuota.PersonalLimit(start, new DateOnly(2028, 6, 1), Law2570));
        Assert.Equal(3, LeaveQuota.PersonalLimit(start, new DateOnly(2027, 6, 1)));
    }

    [Theory] // ลาพักร้อน: yearly days, and in the year of the anniversary a twelfth of them for each month from START (0.5 of 6)
    [InlineData(null, 2029, 6)]     // later year
    [InlineData(null, 2028, 9)]     // 6 + 0.5 × (13 − 7)
    [InlineData(12.0, 2029, 12)]
    [InlineData(12.0, 2028, 18)]    // 12 + 1 × (13 − 7)
    public void Vacation_takes_its_yearly_days_and_the_share_of_the_first_year_from_the_entitlement(double? yearly, int year, double expected)
    {
        var start = new DateOnly(2027, 7, 1);
        var entitlement = yearly is { } y ? LeaveEntitlementDays.Default with { Vacation = y } : null;
        Assert.Equal(expected, LeaveQuota.VacationEntitlement(start, new DateOnly(year, 7, 15), entitlement));
    }

    [Fact]
    public void A_date_no_range_covers_takes_the_defaults()
    {
        LeaveEntitlementDto[] ranges = [new(1, new DateOnly(2027, 1, 1), null, Law2570, [])];
        Assert.Equal(LeaveEntitlementDays.Default, LeaveEntitlementRegistry.On(ranges, new DateOnly(2026, 12, 31)));
        Assert.Equal(Law2570, LeaveEntitlementRegistry.On(ranges, new DateOnly(2027, 1, 1)));
    }
}

/// <summary>
/// ตั้งค่าสิทธิ์วันลา, ลบข้อมูลการลางาน and ย้ายข้อมูลการลางาน / ใบเตือน (UAT 10) through the public services. The ranges and the moves
/// act on every employee, so each test keeps to a year of its own no other test uses.
/// </summary>
[Collection(LocalDbCollection.Name)]
public sealed class LeaveEntitlementAndMoveTests(LocalDbFixture db)
{
    private static int lastCode;

    /// <summary>"E" keeps clear of the other test classes' codes (T-EMPLO.ID is Text(4)).</summary>
    private static string NewCode() => $"E{Interlocked.Increment(ref lastCode):D3}";

    private async Task<T> AsHrAsync<T>(Func<TestScope, Task<T>> action)
    {
        await using var scope = db.Scope("hr1");
        return await action(scope);
    }

    private static void Ok(ServiceResult result) => Assert.True(result.Succeeded, string.Join(", ", result.Errors));

    private async Task<EmployeeDto> NewEmployeeAsync()
    {
        var result = await AsHrAsync(s => s.Get<EmployeeRegistry>().SaveAsync(new EmployeeInput
        {
            Code = NewCode(), Name = "นาง ทดสอบ " + Guid.NewGuid().ToString("N")[..4], Sex = "F", StartDate = new DateOnly(2020, 1, 1), DailyWage = 400,
        }));
        Ok(result);
        return (await AsHrAsync(s => s.Get<EmployeeDirectory>().GetAsync(result.Id!.Value)))!;
    }

    private async Task<int> AddLeaveAsync(EmployeeDto employee, DateOnly from, double days, string type, bool? paid)
    {
        var result = await AsHrAsync(s => s.Get<LeaveRegistry>().SaveAsync(
            new LeaveInput(null, employee.Id, null, from, from.AddDays((int)Math.Ceiling(days) - 1), null, null, days, type, paid, null, null, null)));
        Ok(result.Save);
        return result.Save.Id!.Value;
    }

    [Fact]
    public async Task A_range_of_entitlement_changes_the_quota_of_the_leave_in_it_only()
    {
        const int year = 2091;
        var employee = await NewEmployeeAsync();
        var law = LeaveEntitlementDays.Default with { Sick = 35 };
        var saved = await AsHrAsync(s => s.Get<LeaveEntitlementRegistry>().SaveAsync(
            new LeaveEntitlementInput(null, new DateOnly(year, 1, 1), new DateOnly(year, 12, 31), law, null)));
        Ok(saved);
        try
        {
            var overlap = await AsHrAsync(s => s.Get<LeaveEntitlementRegistry>().SaveAsync(
                new LeaveEntitlementInput(null, new DateOnly(year, 6, 1), null, law, null)));
            Assert.Contains("ทับ", Assert.Single(overlap.Errors));

            await AddLeaveAsync(employee, new DateOnly(year, 2, 1), 30, "2", true);
            await AddLeaveAsync(employee, new DateOnly(year, 4, 1), 5, "2", true); // 35: within the new law
            var over = await AsHrAsync(s => s.Get<LeaveRegistry>().SaveAsync(new LeaveInput(null, employee.Id, null,
                new DateOnly(year, 5, 1), new DateOnly(year, 5, 1), null, null, 1, "2", true, null, null, null)));
            Assert.Equal(["ไม่สามารถบันทึกได้ เนื่องจากลาป่วยได้ค่าแรงเกิน 35 วัน"], over.Save.Errors);

            var summary = await AsHrAsync(s => s.Get<LeaveDirectory>().GetSummaryAsync(employee.Id, year));
            Assert.Equal(35, summary.Quotas.Single(q => q.Name == "ลาป่วย").Limit);
            var nextYear = await AsHrAsync(s => s.Get<LeaveDirectory>().GetSummaryAsync(employee.Id, year + 1));
            Assert.Equal(30, nextYear.Quotas.Single(q => q.Name == "ลาป่วย").Limit); // no range: the default
        }
        finally
        {
            var range = (await AsHrAsync(s => s.Get<LeaveEntitlementRegistry>().ListAsync())).Single(r => r.Id == saved.Id);
            Ok(await AsHrAsync(s => s.Get<LeaveEntitlementRegistry>().DeleteAsync(range.Id, range.RowVersion)));
        }
    }

    [Theory]
    [InlineData(-1.0, "จำนวนวันต้องอยู่ระหว่าง")]
    [InlineData(0.1, "1/8 วัน")]
    public async Task Entitlement_days_must_be_whole_eighths_from_0_to_365(double sick, string error)
    {
        var result = await AsHrAsync(s => s.Get<LeaveEntitlementRegistry>().SaveAsync(new LeaveEntitlementInput(null,
            new DateOnly(2092, 1, 1), null, LeaveEntitlementDays.Default with { Sick = sick }, null)));
        Assert.Contains(error, Assert.Single(result.Errors));
    }

    [Fact] // SCR-60 + SCR-31: a moved leave leaves the screen and the delete, still counts in the quota, and comes back
    public async Task A_moved_leave_is_hidden_from_the_screen_and_the_delete_but_still_counts()
    {
        const int year = 2093;
        var employee = await NewEmployeeAsync();
        var early = await AddLeaveAsync(employee, new DateOnly(year, 1, 10), 2, "1", true);
        var late = await AddLeaveAsync(employee, new DateOnly(year, 8, 10), 1, "1", true);
        var (from, to) = (new DateOnly(year, 1, 1), new DateOnly(year, 6, 30));

        Assert.Equal(new MoveCount(1, 0), await AsHrAsync(s => s.Get<LeaveDirectory>().CountForMoveAsync(from, to)));
        var moved = await AsHrAsync(s => s.Get<LeaveRegistry>().MoveAsync(from, to, true));
        Assert.Equal((true, (int?)1), (moved.Succeeded, moved.Id));

        Assert.Equal([late], (await AsHrAsync(s => s.Get<LeaveDirectory>().ListOfEmployeeAsync(employee.Id, year))).Select(r => r.Id));
        Assert.Equal(1, await AsHrAsync(s => s.Get<LeaveDirectory>().CountMovedOfEmployeeAsync(employee.Id, year)));
        Assert.Equal(3, (await AsHrAsync(s => s.Get<LeaveDirectory>().GetSummaryAsync(employee.Id, year))).Quotas.Single(q => q.Name == "ลากิจ").Used);
        Assert.Empty(await AsHrAsync(s => s.Get<LeaveDirectory>().ListToDeleteAsync(employee.Id, from, to)));
        var deleted = await AsHrAsync(s => s.Get<LeaveRegistry>().DeleteRangeAsync(employee.Id, new DateOnly(year, 1, 1), new DateOnly(year, 12, 31)));
        Assert.Equal((int?)1, deleted.Id); // only the one not moved

        var back = await AsHrAsync(s => s.Get<LeaveRegistry>().MoveAsync(from, to, false));
        Assert.Equal((int?)1, back.Id);
        Assert.Equal([early], (await AsHrAsync(s => s.Get<LeaveDirectory>().ListOfEmployeeAsync(employee.Id, year))).Select(r => r.Id));
        Assert.Equal([DateRangeRule.Message], (await AsHrAsync(s => s.Get<LeaveRegistry>().MoveAsync(to, from, true))).Errors);
    }

    [Fact] // SCR-60: a moved warning leaves SCR-32 but stays in RPT-30
    public async Task A_moved_warning_is_hidden_from_the_screen_but_stays_in_the_report()
    {
        const int year = 2094;
        var employee = await NewEmployeeAsync();
        var (day, other) = (new DateOnly(year, 3, 3), new DateOnly(year, 9, 9));
        Ok(await AsHrAsync(s => s.Get<WarningRegistry>().SaveAsync(new WarningInput(null, day, employee.Id, WarningLevels.Verbal, "มาสาย", null))));
        Ok(await AsHrAsync(s => s.Get<WarningRegistry>().SaveAsync(new WarningInput(null, other, employee.Id, WarningLevels.Verbal, "มาสาย", null))));

        Ok(await AsHrAsync(s => s.Get<WarningRegistry>().MoveAsync(new DateOnly(year, 1, 1), new DateOnly(year, 6, 30), true)));

        Assert.Equal([other], (await AsHrAsync(s => s.Get<WarningDirectory>().ListOfEmployeeAsync(employee.Id))).Select(w => w.Date));
        Assert.Equal(1, await AsHrAsync(s => s.Get<WarningDirectory>().CountMovedOfEmployeeAsync(employee.Id)));
        Assert.Equal(new MoveCount(0, 1), await AsHrAsync(s => s.Get<WarningDirectory>().CountForMoveAsync(new DateOnly(year, 1, 1), new DateOnly(year, 6, 30))));
        var report = await AsHrAsync(s => s.Get<WarningDirectory>().GetReportAsync(new DateOnly(year, 1, 1), new DateOnly(year, 12, 31), employee.Id));
        Assert.Equal(2, report.Lines.Count);
    }
}
