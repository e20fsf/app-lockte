using Lockte.Hr;
using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;

namespace Lockte.Web.Tests;

/// <summary>
/// จำนวนวันลา from the dates and times (UAT-27, #405; the user's decision of 2026-11-04): days both counted, without วันหยุดประจำสัปดาห์
/// and นักขัตฤกษ์, 13:00 start / 12:00 end take off 0.5, 1 hour = 0.125. The pure rule first, then the service with the calendar.
/// </summary>
[Collection(LocalDbCollection.Name)]
public sealed class LeaveDayCounterTests(LocalDbFixture db)
{
    private static readonly IReadOnlySet<DateOnly> NoHolidays = new HashSet<DateOnly>();
    private static readonly TimeOnly T0800 = new(8, 0), T1200 = new(12, 0), T1300 = new(13, 0), T1700 = new(17, 0);

    private static DateOnly D(int day, int month = 3, int year = 2026) => new(year, month, day);

    [Fact]
    public void OneFullDay() => Assert.Equal(1, LeaveDayCounter.Count(D(2), D(2), T0800, T1700, NoHolidays));

    [Fact]
    public void BothDaysAreCounted() => Assert.Equal(3, LeaveDayCounter.Count(D(2), D(4), T0800, T1700, NoHolidays));

    [Theory]
    [InlineData(8, 0, 12, 0, 0.5)]
    [InlineData(13, 0, 17, 0, 0.5)]
    [InlineData(8, 0, 9, 0, 0.125)]
    [InlineData(9, 0, 17, 0, 0.875)]
    [InlineData(8, 0, 13, 0, 0.5)]
    public void HoursOfOneDay(int fromHour, int fromMinute, int toHour, int toMinute, double expected)
        => Assert.Equal(expected, LeaveDayCounter.Count(D(2), D(2), new(fromHour, fromMinute), new(toHour, toMinute), NoHolidays));

    [Fact]
    public void StartingAfternoonTakesOffHalfADay() => Assert.Equal(2.5, LeaveDayCounter.Count(D(2), D(4), T1300, T1700, NoHolidays));

    [Fact]
    public void EndingNoonTakesOffHalfADay() => Assert.Equal(2.5, LeaveDayCounter.Count(D(2), D(4), T0800, T1200, NoHolidays));

    [Fact]
    public void HalfDayBothEnds() => Assert.Equal(2, LeaveDayCounter.Count(D(2), D(4), T1300, T1200, NoHolidays));

    [Fact]
    public void WeeklyAndPublicHolidaysAreNotCounted()
    {
        // 2-6 March: the 4th is a วันหยุดประจำสัปดาห์, the 5th a นักขัตฤกษ์
        var off = new HashSet<DateOnly> { D(4), D(5) };
        Assert.Equal(3, LeaveDayCounter.Count(D(2), D(6), T0800, T1700, off));
    }

    [Fact]
    public void AHolidayAtTheEndTakesNothingOffForTheTimes()
        => Assert.Equal(2, LeaveDayCounter.Count(D(2), D(4), T0800, T1200, new HashSet<DateOnly> { D(4) }));

    [Fact]
    public void OnlyHolidaysIsZero() => Assert.Equal(0, LeaveDayCounter.Count(D(4), D(5), T0800, T1700, new HashSet<DateOnly> { D(4), D(5) }));

    [Fact]
    public void OneHolidayOneDayIsZero() => Assert.Equal(0, LeaveDayCounter.Count(D(4), D(4), T0800, T1700, new HashSet<DateOnly> { D(4) }));

    [Fact]
    public void NoAnswerWhileDatesAreMissingOrInWrongOrder()
    {
        Assert.Null(LeaveDayCounter.Count(null, D(2), T0800, T1700, NoHolidays));
        Assert.Null(LeaveDayCounter.Count(D(2), null, T0800, T1700, NoHolidays));
        Assert.Null(LeaveDayCounter.Count(D(3), D(2), T0800, T1700, NoHolidays));
    }

    [Fact]
    public void RuleOfTheDatePair()
    {
        Assert.True(DateRangeRule.IsInvalid(D(3), D(2)));
        Assert.False(DateRangeRule.IsInvalid(D(2), D(2)));
        Assert.False(DateRangeRule.IsInvalid((DateOnly?)null, D(2)));
        Assert.True(DateRangeRule.IsInvalid(new DateTime(2026, 3, 3, 12, 0, 0), new DateTime(2026, 3, 2)));
        Assert.False(DateRangeRule.IsInvalid(new DateTime(2026, 3, 2, 12, 0, 0), new DateTime(2026, 3, 2)));
        Assert.Equal(DateRangeRule.Message, DateRangeRule.Check(D(3), D(2)));
        Assert.Null(DateRangeRule.Check(D(2), D(3)));
    }

    [Fact]
    public async Task ServiceTakesTheCalendarsHolidaysOut()
    {
        // 2090-06-04 (Mon) .. 2090-06-10 (Sun): the 8th a วันหยุดประจำสัปดาห์, the 9th a นักขัตฤกษ์ — a year no other test uses
        await using var scope = db.Scope("hr1");
        var calendar = scope.Get<CalendarRegistry>();
        Ok(await calendar.SaveWeeklyHolidayAsync(new WeeklyHolidayInput(null, D(8, 6, 2090), "วันอาทิตย์", null)));
        Ok(await calendar.SavePublicHolidayAsync(new PublicHolidayInput(null, D(9, 6, 2090), "วันทดสอบ", null)));
        var leave = scope.Get<LeaveDirectory>();

        Assert.Equal(5, await leave.CalculateDaysAsync(D(4, 6, 2090), D(10, 6, 2090), T0800, T1700));
        Assert.Equal(4.5, await leave.CalculateDaysAsync(D(4, 6, 2090), D(10, 6, 2090), T1300, T1700));
        Assert.Equal(0.5, await leave.CalculateDaysAsync(D(4, 6, 2090), D(4, 6, 2090), T0800, T1200));
        Assert.Null(await leave.CalculateDaysAsync(D(5, 6, 2090), D(4, 6, 2090), T0800, T1700));
    }

    private static void Ok(ServiceResult result) => Assert.True(result.Succeeded, string.Join(", ", result.Errors));
}
