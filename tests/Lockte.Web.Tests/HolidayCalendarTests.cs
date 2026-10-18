using Lockte.Hr;
using Lockte.SharedKernel.Persistence;
using Lockte.Web.Audit;

namespace Lockte.Web.Tests;

/// <summary>วันหยุดประจำสัปดาห์ / วันหยุดประเพณี (#46, SCR-15/16) through the public services of module พนักงานและค่าแรง.</summary>
[Collection(LocalDbCollection.Name)]
public sealed class HolidayCalendarTests(LocalDbFixture db)
{
    // Both registers are company-wide and a date may appear once, so each test works in a year of its own
    // (ten apart: "เพิ่ม" extends every series by four weeks each time any test presses it).
    private static int lastYear = 2100;
    private static int OwnYear() => Interlocked.Add(ref lastYear, 10);

    private async Task<T> AsHrAsync<T>(Func<HolidayRegistry, HolidayCalendar, Task<T>> action)
    {
        await using var scope = db.Scope("hr1");
        return await action(scope.Get<HolidayRegistry>(), scope.Get<HolidayCalendar>());
    }

    private Task<DaysOff> DaysOffAsync(DateOnly from, DateOnly to)
        => AsHrAsync((_, calendar) => calendar.GetDaysOffAsync(from, to));

    [Fact]
    public async Task Public_holiday_is_a_day_off_for_payroll_in_its_range_only()
    {
        var year = OwnYear();
        var newYear = new DateOnly(year, 1, 1);

        var saved = await AsHrAsync((registry, _) => registry.SavePublicHolidayAsync(new PublicHolidayInput(null, newYear, " วันขึ้นปีใหม่ ", null)));

        Assert.True(saved.Succeeded);
        var january = await DaysOffAsync(newYear, new DateOnly(year, 1, 31));
        Assert.Equal([newYear], january.PublicHolidays);
        Assert.Empty(january.WeeklyHolidays);
        Assert.True(january.IsPublicHoliday(newYear));
        Assert.True(january.IsDayOff(newYear));
        Assert.False(january.IsDayOff(newYear.AddDays(1)));
        Assert.Empty((await DaysOffAsync(newYear.AddDays(1), new DateOnly(year, 12, 31))).PublicHolidays);
        var listed = Assert.Single(await AsHrAsync((_, calendar) => calendar.GetPublicHolidaysAsync()), h => h.Date == newYear);
        Assert.Equal("วันขึ้นปีใหม่", listed.Name);
    }

    [Fact]
    public async Task Weekly_holiday_is_a_day_off_and_is_labelled_วันอาทิตย์_unless_given_another_label()
    {
        var sunday = new DateOnly(OwnYear(), 3, 7);
        while (sunday.DayOfWeek != DayOfWeek.Sunday) sunday = sunday.AddDays(1);
        var movedToMonday = sunday.AddDays(8);

        var plain = await AsHrAsync((registry, _) => registry.SaveWeeklyHolidayAsync(new WeeklyHolidayInput(null, sunday, " ", null)));
        var moved = await AsHrAsync((registry, _) => registry.SaveWeeklyHolidayAsync(new WeeklyHolidayInput(null, movedToMonday, "เลื่อน", null)));

        Assert.True(plain.Succeeded && moved.Succeeded);
        var daysOff = await DaysOffAsync(sunday, sunday.AddDays(13));
        Assert.Equal([sunday, movedToMonday], daysOff.WeeklyHolidays.Order());
        Assert.True(daysOff.IsWeeklyHoliday(movedToMonday)); // a weekly holiday need not be a Sunday (Q-26)
        Assert.False(daysOff.IsPublicHoliday(sunday));
        var listed = (await AsHrAsync((_, calendar) => calendar.GetWeeklyHolidaysAsync())).Where(w => w.Date >= sunday && w.Date <= movedToMonday);
        Assert.Equal([(sunday, "วันอาทิตย์"), (movedToMonday, "เลื่อน")], listed.Select(w => (w.Date, w.Label)));
    }

    [Fact]
    public async Task Date_is_required_and_appears_once_so_a_blank_row_is_never_saved_and_a_day_is_never_counted_twice()
    {
        var day = new DateOnly(OwnYear(), 4, 13);
        await AsHrAsync((registry, _) => registry.SavePublicHolidayAsync(new PublicHolidayInput(null, day, "สงกรานต์", null)));
        await AsHrAsync((registry, _) => registry.SaveWeeklyHolidayAsync(new WeeklyHolidayInput(null, day, null, null)));
        var holidaysBefore = (await AsHrAsync((_, calendar) => calendar.GetPublicHolidaysAsync())).Count;
        var weeklyBefore = (await AsHrAsync((_, calendar) => calendar.GetWeeklyHolidaysAsync())).Count;

        var blankHoliday = await AsHrAsync((registry, _) => registry.SavePublicHolidayAsync(new PublicHolidayInput(null, null, "ไม่มีวันที่", null)));
        var blankWeekly = await AsHrAsync((registry, _) => registry.SaveWeeklyHolidayAsync(new WeeklyHolidayInput(null, null, null, null)));
        var sameHoliday = await AsHrAsync((registry, _) => registry.SavePublicHolidayAsync(new PublicHolidayInput(null, day, "ซ้ำ", null)));
        var sameWeekly = await AsHrAsync((registry, _) => registry.SaveWeeklyHolidayAsync(new WeeklyHolidayInput(null, day, null, null)));

        Assert.Equal(["กรุณากรอกวันที่"], blankHoliday.Errors);
        Assert.Equal(["กรุณากรอกวันที่"], blankWeekly.Errors);
        Assert.Equal([$"มีวันหยุดประเพณีวันที่ {ThaiDate(day)} อยู่แล้ว"], sameHoliday.Errors);
        Assert.Equal([$"มีวันหยุดประจำสัปดาห์วันที่ {ThaiDate(day)} อยู่แล้ว"], sameWeekly.Errors);
        Assert.Equal(holidaysBefore, (await AsHrAsync((_, calendar) => calendar.GetPublicHolidaysAsync())).Count);
        Assert.Equal(weeklyBefore, (await AsHrAsync((_, calendar) => calendar.GetWeeklyHolidaysAsync())).Count);
        // A weekly holiday that falls on a public holiday is both, as in the legacy tables.
        Assert.True((await DaysOffAsync(day, day)) is { } both && both.IsWeeklyHoliday(day) && both.IsPublicHoliday(day));
    }

    [Fact]
    public async Task Moving_a_holiday_keeps_the_row_and_records_the_old_and_new_date()
    {
        var year = OwnYear();
        var planned = new DateOnly(year, 5, 4);
        var moved = new DateOnly(year, 5, 5);
        var id = (await AsHrAsync((registry, _) => registry.SavePublicHolidayAsync(new PublicHolidayInput(null, planned, "ฉัตรมงคล", null)))).Id!.Value;
        var loaded = (await AsHrAsync((_, calendar) => calendar.GetPublicHolidaysAsync())).Single(h => h.Id == id);

        var result = await AsHrAsync((registry, _) => registry.SavePublicHolidayAsync(new PublicHolidayInput(id, moved, "ฉัตรมงคล (ชดเชย)", loaded.RowVersion)));

        Assert.True(result.Succeeded);
        var daysOff = await DaysOffAsync(planned, moved);
        Assert.Equal([moved], daysOff.PublicHolidays);
        await using var scope = db.Scope();
        var changes = (await scope.Get<HistoryService>().SearchAsync(new(Table: "วันหยุดประเพณี", RowKey: Key(moved))))
            .Where(r => r.Action == HistoryActions.Modified).ToList();
        Assert.Contains(changes, c => (c.Column, c.OldValue, c.NewValue, c.UserName) == ("วันที่", Key(planned), Key(moved), "hr1"));
        Assert.Contains(changes, c => (c.Column, c.OldValue, c.NewValue) == ("ชื่อวันหยุด", "ฉัตรมงคล", "ฉัตรมงคล (ชดเชย)"));
    }

    [Fact]
    public async Task Saving_over_someone_elses_newer_save_reports_a_conflict()
    {
        var sunday = new DateOnly(OwnYear(), 6, 6);
        var id = (await AsHrAsync((registry, _) => registry.SaveWeeklyHolidayAsync(new WeeklyHolidayInput(null, sunday, null, null)))).Id!.Value;
        var loaded = (await AsHrAsync((_, calendar) => calendar.GetWeeklyHolidaysAsync())).Single(w => w.Id == id);
        await AsHrAsync((registry, _) => registry.SaveWeeklyHolidayAsync(new WeeklyHolidayInput(id, sunday.AddDays(1), null, loaded.RowVersion)));

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            AsHrAsync((registry, _) => registry.SaveWeeklyHolidayAsync(new WeeklyHolidayInput(id, sunday.AddDays(2), null, loaded.RowVersion))));
    }

    [Fact]
    public async Task Pressing_เพิ่ม_adds_four_more_weeks_after_the_last_date_of_each_label()
    {
        var year = OwnYear();
        string weekly = "ส" + Guid.NewGuid().ToString("N")[..6], otherDay = "ส" + Guid.NewGuid().ToString("N")[..6];
        var lastWeekly = new DateOnly(year, 8, 2);
        var lastOtherDay = new DateOnly(year, 9, 5);
        foreach (var (date, label) in new[] { (lastWeekly.AddDays(-7), weekly), (lastWeekly, weekly), (lastOtherDay, otherDay) })
            await AsHrAsync((registry, _) => registry.SaveWeeklyHolidayAsync(new WeeklyHolidayInput(null, date, label, null)));

        var result = await AsHrAsync((registry, _) => registry.AddNextWeeklyHolidaysAsync());

        Assert.True(result.Succeeded);
        var listed = (await AsHrAsync((_, calendar) => calendar.GetWeeklyHolidaysAsync())).Where(w => w.Date.Year == year).ToList();
        Assert.Equal(
            [new DateOnly(year, 8, 9), new(year, 8, 16), new(year, 8, 23), new(year, 8, 30)],
            listed.Where(w => w.Label == weekly && w.Date > lastWeekly).Select(w => w.Date));
        Assert.Equal(
            [new DateOnly(year, 9, 12), new(year, 9, 19), new(year, 9, 26), new(year, 10, 3)],
            listed.Where(w => w.Label == otherDay && w.Date > lastOtherDay).Select(w => w.Date));
        Assert.True((await DaysOffAsync(new(year, 10, 3), new(year, 10, 3))).IsWeeklyHoliday(new(year, 10, 3)));
    }

    [Fact]
    public async Task Weekly_holidays_are_deleted_by_date_range_or_by_row_and_a_public_holiday_by_row()
    {
        var year = OwnYear();
        var weeks = Enumerable.Range(0, 4).Select(i => new DateOnly(year, 11, 1).AddDays(7 * i)).ToList();
        foreach (var date in weeks)
            await AsHrAsync((registry, _) => registry.SaveWeeklyHolidayAsync(new WeeklyHolidayInput(null, date, null, null)));
        var holidayId = (await AsHrAsync((registry, _) => registry.SavePublicHolidayAsync(new PublicHolidayInput(null, weeks[1], "ลอยกระทง", null)))).Id!.Value;
        var holiday = (await AsHrAsync((_, calendar) => calendar.GetPublicHolidaysAsync())).Single(h => h.Id == holidayId);

        var lastWeek = (await AsHrAsync((_, calendar) => calendar.GetWeeklyHolidaysAsync())).Single(w => w.Date == weeks[3]);
        var oneRow = await AsHrAsync((registry, _) => registry.DeleteWeeklyHolidayAsync(lastWeek.Id, lastWeek.RowVersion));
        var range = await AsHrAsync((registry, _) => registry.DeleteWeeklyHolidaysAsync(weeks[1], weeks[2]));
        var row = await AsHrAsync((registry, _) => registry.DeletePublicHolidayAsync(holidayId, holiday.RowVersion));
        var backwards = await AsHrAsync((registry, _) => registry.DeleteWeeklyHolidaysAsync(weeks[3], weeks[0]));

        Assert.True(oneRow.Succeeded && range.Succeeded && row.Succeeded);
        Assert.Equal(["วันที่เริ่มต้องไม่หลังวันที่สิ้นสุด"], backwards.Errors);
        var daysOff = await DaysOffAsync(weeks[0], weeks[3]);
        Assert.Equal([weeks[0]], daysOff.WeeklyHolidays);
        Assert.Empty(daysOff.PublicHolidays);
        await using var scope = db.Scope();
        Assert.Contains(await scope.Get<HistoryService>().SearchAsync(new(Table: "วันหยุดประจำสัปดาห์", RowKey: Key(weeks[2]))),
            r => r.Action == HistoryActions.Deleted && r.UserName == "hr1");
    }

    /// <summary>รหัสรายการ in history, and dates as history writes them.</summary>
    private static string Key(DateOnly date) => date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>d/M/yyyy in พ.ศ., as the screens show dates.</summary>
    private static string ThaiDate(DateOnly date) => $"{date.Day}/{date.Month}/{date.Year + 543}";
}
