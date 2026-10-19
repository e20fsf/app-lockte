using System.Globalization;
using Lockte.Hr;

namespace Lockte.Web.Tests;

/// <summary>ค่าเริ่มต้นวันที่งวดค่าแรง (#23 Q-04): 12–26 จ่ายวันที่ 1 / 27–11 จ่ายวันที่ 16 — no database.</summary>
public sealed class PayPeriodDatesTests
{
    private static DateOnly D(string iso) => DateOnly.ParseExact(iso, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    [Theory]
    [InlineData("2026-10-12", "2026-10-12", "2026-10-26", "2026-11-01")]
    [InlineData("2026-10-26", "2026-10-12", "2026-10-26", "2026-11-01")]
    [InlineData("2026-10-27", "2026-10-27", "2026-11-11", "2026-11-16")]
    [InlineData("2026-11-11", "2026-10-27", "2026-11-11", "2026-11-16")]
    [InlineData("2026-12-31", "2026-12-27", "2027-01-11", "2027-01-16")] // คร่อมปีใหม่
    [InlineData("2027-01-05", "2026-12-27", "2027-01-11", "2027-01-16")]
    [InlineData("2026-12-20", "2026-12-12", "2026-12-26", "2027-01-01")]
    public void The_period_containing_a_day_follows_the_12_26_and_27_11_pattern(string day, string from, string to, string pay)
        => Assert.Equal(new PayPeriodDates(D(from), D(to), D(pay)), PayPeriodDates.Containing(D(day)));

    [Fact]
    public void The_next_period_starts_the_day_after_the_previous_one_ends()
    {
        var first = PayPeriodDates.Containing(D("2026-11-12"));

        var second = PayPeriodDates.After(first);
        var third = PayPeriodDates.After(second);

        Assert.Equal(new PayPeriodDates(D("2026-11-27"), D("2026-12-11"), D("2026-12-16")), second);
        Assert.Equal(new PayPeriodDates(D("2026-12-12"), D("2026-12-26"), D("2027-01-01")), third);
    }

    [Fact] // งวดพิเศษ that HR typed: the next one still ends on the 26th or the 11th
    public void After_an_irregular_period_the_next_one_ends_on_the_usual_cut_day()
    {
        var special = new PayPeriodDates(D("2026-10-12"), D("2026-10-20"), D("2026-10-25"));

        Assert.Equal(new PayPeriodDates(D("2026-10-21"), D("2026-10-26"), D("2026-11-01")), PayPeriodDates.After(special));
    }

    [Theory] // no period yet: the last one that has ended — HR closes a period after its last day
    [InlineData("2026-10-27", "2026-10-12")]
    [InlineData("2026-10-26", "2026-10-12")]
    [InlineData("2026-10-19", "2026-09-27")]
    [InlineData("2026-11-12", "2026-10-27")]
    public void Without_a_previous_period_the_default_is_the_last_one_ended(string today, string from)
        => Assert.Equal(D(from), PayPeriodDates.LastEndedBy(D(today)).From);

    [Fact]
    public void Days_counts_both_ends()
        => Assert.Equal(16, new PayPeriodDates(D("2026-08-27"), D("2026-09-11"), D("2026-09-16")).Days);
}
