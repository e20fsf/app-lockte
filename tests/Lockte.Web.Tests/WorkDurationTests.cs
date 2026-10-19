using Lockte.Hr;

namespace Lockte.Web.Tests;

/// <summary>BR-001 คำนวณเวลาทำงานต่อรายการ (WORK_T): the Input/Expected table of specs/employee/business-rules.md.</summary>
public sealed class WorkDurationTests
{
    [Theory]
    [InlineData("W", "08:00", "17:00", 60, 1.0)]        // 1
    [InlineData("W", "08:00", "16:45", 60, 1.0)]        // 2: 7.75 ชม. ถึงเกณฑ์พอดี
    [InlineData("W", "08:00", "16:30", 60, 0.9375)]     // 3
    [InlineData("O1", "17:20", "20:10", 0, 17.0 / 6)]   // 4: 2.8333…
    [InlineData("W", "20:10", "05:10", 60, 1.0)]        // 5: กะ B, W_OUT is the next day
    public void WorkT_follows_the_BR_001_table(string type, string workIn, string workOut, int rest, double expected)
        => Assert.Equal(expected, WorkDuration.WorkT(type, TimeOnly.Parse(workIn), TimeOnly.Parse(workOut), rest), 12);

    [Theory]
    [InlineData("08:00", "17:00", 60, 8.0)]
    [InlineData("20:10", "05:10", 60, 8.0)]   // ข้ามคืน
    [InlineData("17:20", "20:10", 0, 17.0 / 6)]
    [InlineData("08:00", "08:00", 0, 0.0)]
    public void Hours_are_out_minus_in_minus_the_rest_and_wrap_past_midnight(string workIn, string workOut, int rest, double expected)
        => Assert.Equal(expected, WorkDuration.Hours(TimeOnly.Parse(workIn), TimeOnly.Parse(workOut), rest), 12);

    [Fact] // TYPE อื่น = ชั่วโมง, also for a long OT that is no W
    public void Other_types_count_hours_not_days()
    {
        Assert.Equal(8.0, WorkDuration.WorkT("S", new TimeOnly(8, 0), new TimeOnly(17, 0), 60), 12);
        Assert.Equal(8.0, WorkDuration.WorkT("H", new TimeOnly(8, 0), new TimeOnly(17, 0), 60), 12);
    }

    [Fact]
    public void A_row_dto_shows_its_WORK_T_and_none_without_both_times()
    {
        var night = new WorkTimeDto(1, new DateOnly(2026, 9, 1), 1, "K001", "ทดสอบ", "W", new TimeOnly(20, 10), new TimeOnly(5, 10),
            null, 60, null, []);
        var open = night with { WorkOut = null };

        Assert.Equal((8.0, 1.0), (night.Hours, night.WorkT));
        Assert.Equal((null, null), (open.Hours, open.WorkT));
    }
}
