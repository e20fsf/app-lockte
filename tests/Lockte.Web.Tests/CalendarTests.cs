using Lockte.Hr;
using Lockte.SharedKernel.Persistence;
using Lockte.Web.Audit;

namespace Lockte.Web.Tests;

/// <summary>ปฏิทินวันหยุด (#46, SCR-15/16, BR-014, BR-051) through the public services of module พนักงานและค่าแรง.</summary>
[Collection(LocalDbCollection.Name)]
public sealed class CalendarTests(LocalDbFixture db)
{
    /// <summary>Tests share one database: each test works in its own far-away year.</summary>
    private static DateOnly NewSunday()
    {
        var day = new DateOnly(Random.Shared.Next(3000, 9000), 1, 1).AddDays(Random.Shared.Next(0, 300));
        return day.AddDays(((int)DayOfWeek.Sunday - (int)day.DayOfWeek + 7) % 7);
    }

    /// <summary>ITEM is Text(10); a label of its own keeps BR-014's "per label" runs apart.</summary>
    private static string NewLabel() => "ท" + Guid.NewGuid().ToString("N")[..6];

    private async Task<T> AsHrAsync<T>(Func<CalendarRegistry, CalendarDirectory, Task<T>> action)
    {
        await using var scope = db.Scope("hr1");
        return await action(scope.Get<CalendarRegistry>(), scope.Get<CalendarDirectory>());
    }

    [Fact]
    public async Task Weekly_and_public_holidays_are_answered_for_a_date_range()
    {
        var sunday = NewSunday();
        await AsHrAsync((r, _) => r.SaveWeeklyHolidayAsync(new WeeklyHolidayInput(null, sunday, null, null)));
        await AsHrAsync((r, _) => r.SaveWeeklyHolidayAsync(new WeeklyHolidayInput(null, sunday.AddDays(7), null, null)));
        await AsHrAsync((r, _) => r.SavePublicHolidayAsync(new PublicHolidayInput(null, sunday.AddDays(3), " วันพิเศษ ", null)));

        var calendar = await AsHrAsync((_, c) => c.GetAsync(sunday, sunday.AddDays(6)));

        Assert.Equal([sunday], calendar.WeeklyHolidays.Select(h => h.Date));
        Assert.Equal("วันอาทิตย์", calendar.WeeklyHolidays[0].Label); // the legacy default ITEM
        Assert.Equal([(sunday.AddDays(3), "วันพิเศษ")], calendar.PublicHolidays.Select(h => (h.Date, h.Name)));
        Assert.True(calendar.IsWeeklyHoliday(sunday));
        Assert.True(calendar.IsPublicHoliday(sunday.AddDays(3)));
        Assert.False(calendar.IsWeeklyHoliday(sunday.AddDays(1)) || calendar.IsPublicHoliday(sunday.AddDays(1)));
    }

    [Fact] // BR-014: each run adds Max(DATE) + 7 for every ITEM, four runs → four more weeks
    public async Task Adding_continues_every_label_four_weeks_after_its_last_date()
    {
        var (first, second) = (NewSunday(), NewSunday());
        var (labelA, labelB) = (NewLabel(), NewLabel());
        await AsHrAsync((r, _) => r.SaveWeeklyHolidayAsync(new WeeklyHolidayInput(null, first, labelA, null)));
        await AsHrAsync((r, _) => r.SaveWeeklyHolidayAsync(new WeeklyHolidayInput(null, first.AddDays(7), labelA, null)));
        await AsHrAsync((r, _) => r.SaveWeeklyHolidayAsync(new WeeklyHolidayInput(null, second.AddDays(-1), labelB, null))); // a Saturday

        var added = await AsHrAsync((r, _) => r.AddNextFourWeeksAsync());

        Assert.True(added.Succeeded);
        var a = await AsHrAsync((_, c) => c.GetAsync(first, first.AddDays(70)));
        Assert.Equal(Enumerable.Range(0, 6).Select(w => first.AddDays(7 * w)),
            a.WeeklyHolidays.Where(h => h.Label == labelA).Select(h => h.Date));
        var b = await AsHrAsync((_, c) => c.GetAsync(second.AddDays(-1), second.AddDays(70)));
        Assert.Equal(Enumerable.Range(0, 5).Select(w => second.AddDays(-1 + 7 * w)),
            b.WeeklyHolidays.Where(h => h.Label == labelB).Select(h => h.Date));
    }

    [Fact]
    public async Task A_date_is_required_and_appears_once_per_calendar()
    {
        var sunday = NewSunday();
        await AsHrAsync((r, _) => r.SaveWeeklyHolidayAsync(new WeeklyHolidayInput(null, sunday, null, null)));
        await AsHrAsync((r, _) => r.SavePublicHolidayAsync(new PublicHolidayInput(null, sunday.AddDays(1), "ปีใหม่", null)));

        var blankWeekly = await AsHrAsync((r, _) => r.SaveWeeklyHolidayAsync(new WeeklyHolidayInput(null, null, null, null)));
        var blankPublic = await AsHrAsync((r, _) => r.SavePublicHolidayAsync(new PublicHolidayInput(null, null, "ไม่มีวันที่", null)));
        var sameWeekly = await AsHrAsync((r, _) => r.SaveWeeklyHolidayAsync(new WeeklyHolidayInput(null, sunday, null, null)));
        var samePublic = await AsHrAsync((r, _) => r.SavePublicHolidayAsync(new PublicHolidayInput(null, sunday.AddDays(1), "ซ้ำ", null)));

        Assert.Equal(["กรุณากรอกวันที่"], blankWeekly.Errors);
        Assert.Equal(["กรุณากรอกวันที่"], blankPublic.Errors);
        Assert.Single(sameWeekly.Errors, e => e.StartsWith("มีวันหยุดประจำสัปดาห์วันที่"));
        Assert.Single(samePublic.Errors, e => e.StartsWith("มีวันหยุดประเพณีวันที่"));
    }

    [Fact]
    public async Task Editing_and_deleting_go_through_the_rowversion_the_screen_loaded()
    {
        var day = NewSunday();
        var id = (await AsHrAsync((r, _) => r.SavePublicHolidayAsync(new PublicHolidayInput(null, day, "เดิม", null)))).Id!.Value;
        var loaded = (await AsHrAsync((_, c) => c.GetAsync(day, day))).PublicHolidays.Single();

        await AsHrAsync((r, _) => r.SavePublicHolidayAsync(new PublicHolidayInput(id, day.AddDays(1), "เลื่อน", loaded.RowVersion)));

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            AsHrAsync((r, _) => r.DeletePublicHolidayAsync(id, loaded.RowVersion)));
        var moved = (await AsHrAsync((_, c) => c.GetAsync(day, day.AddDays(1)))).PublicHolidays.Single();
        Assert.Equal((day.AddDays(1), "เลื่อน"), (moved.Date, moved.Name));

        await AsHrAsync((r, _) => r.DeletePublicHolidayAsync(id, moved.RowVersion));
        Assert.Empty((await AsHrAsync((_, c) => c.GetAsync(day, day.AddDays(1)))).PublicHolidays);
    }

    [Fact]
    public async Task Weekly_holidays_can_be_deleted_for_a_date_range()
    {
        var sunday = NewSunday();
        foreach (var week in Enumerable.Range(0, 4))
            await AsHrAsync((r, _) => r.SaveWeeklyHolidayAsync(new WeeklyHolidayInput(null, sunday.AddDays(7 * week), null, null)));

        var deleted = await AsHrAsync((r, _) => r.DeleteWeeklyHolidaysAsync(sunday.AddDays(7), sunday.AddDays(14)));
        var reversed = await AsHrAsync((r, _) => r.DeleteWeeklyHolidaysAsync(sunday.AddDays(14), sunday));

        Assert.True(deleted.Succeeded);
        Assert.Equal(["วันที่เริ่มต้องไม่เกินวันที่สิ้นสุด"], reversed.Errors);
        Assert.Equal([sunday, sunday.AddDays(21)],
            (await AsHrAsync((_, c) => c.GetAsync(sunday, sunday.AddDays(21)))).WeeklyHolidays.Select(h => h.Date));
    }
    [Fact]
    public async Task Adding_four_weeks_is_one_history_event_not_a_row_per_day()
    {
        var sunday = NewSunday();
        var label = NewLabel();
        await AsHrAsync((r, _) => r.SaveWeeklyHolidayAsync(new WeeklyHolidayInput(null, sunday, label, null)));

        await AsHrAsync((r, _) => r.AddNextFourWeeksAsync());

        var history = await HistoryAsync(new(Table: "วันหยุดประจำสัปดาห์", UserName: "hr1"));
        Assert.DoesNotContain(history, h => h.RowKey == Iso(sunday.AddDays(7)));
        var job = Assert.Single(history, h => h.Kind == HistoryKind.Event && h.Detail?.Contains(Iso(sunday.AddDays(28))) == true);
        Assert.Equal(CalendarRegistry.AddFourWeeksAction, job.Action);
        Assert.True(job.RowCount >= 4);
    }

    [Fact]
    public async Task Deleting_a_date_range_is_one_history_event()
    {
        var sunday = NewSunday();
        foreach (var week in Enumerable.Range(0, 3))
            await AsHrAsync((r, _) => r.SaveWeeklyHolidayAsync(new WeeklyHolidayInput(null, sunday.AddDays(7 * week), null, null)));

        await AsHrAsync((r, _) => r.DeleteWeeklyHolidaysAsync(sunday, sunday.AddDays(14)));

        var history = await HistoryAsync(new(Table: "วันหยุดประจำสัปดาห์", RowKey: Iso(sunday.AddDays(7))));
        Assert.DoesNotContain(history, h => h.Action == HistoryActions.Deleted);
        var job = Assert.Single(await HistoryAsync(new(Table: "วันหยุดประจำสัปดาห์")),
            h => h.Kind == HistoryKind.Event && h.Detail?.Contains(Iso(sunday)) == true);
        Assert.Equal((CalendarRegistry.DeleteRangeAction, (int?)3), (job.Action, job.RowCount));
    }

    [Fact]
    public async Task Adding_four_weeks_skips_a_date_another_label_already_has()
    {
        var sunday = NewSunday();
        var (labelA, labelB) = (NewLabel(), NewLabel());
        await AsHrAsync((r, _) => r.SaveWeeklyHolidayAsync(new WeeklyHolidayInput(null, sunday, labelA, null)));
        await AsHrAsync((r, _) => r.SaveWeeklyHolidayAsync(new WeeklyHolidayInput(null, sunday.AddDays(21), labelB, null)));

        await AsHrAsync((r, _) => r.AddNextFourWeeksAsync());

        var days = (await AsHrAsync((_, c) => c.GetAsync(sunday, sunday.AddDays(49)))).WeeklyHolidays;
        Assert.Equal(Enumerable.Range(0, 8).Select(w => sunday.AddDays(7 * w)), days.Select(d => d.Date)); // each date once
        Assert.Equal(labelB, days.Single(d => d.Date == sunday.AddDays(21)).Label); // A's fourth week was already B's
    }

    [Fact]
    public async Task The_calendar_only_answers_for_the_period_it_read()
    {
        var sunday = NewSunday();
        var calendar = await AsHrAsync((_, c) => c.GetAsync(sunday, sunday.AddDays(6)));

        Assert.Throws<ArgumentOutOfRangeException>(() => calendar.IsWeeklyHoliday(sunday.AddDays(7)));
        Assert.Throws<ArgumentOutOfRangeException>(() => calendar.IsPublicHoliday(sunday.AddDays(-1)));
    }

    /// <summary>Row keys and event parameters use culture-independent dates.</summary>
    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    private async Task<IReadOnlyList<HistoryRow>> HistoryAsync(HistoryFilter filter)
    {
        await using var scope = db.Scope();
        return await scope.Get<HistoryService>().SearchAsync(filter);
    }
}

