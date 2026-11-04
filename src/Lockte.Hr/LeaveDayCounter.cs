namespace Lockte.Hr;

/// <summary>
/// จำนวนวันลา ("SUM_D") worked out from the dates and times (UAT-27, #405; the user's decision of 2026-11-04 closing Q-13):
/// the days from FROM_D to TO_D both counted, without the วันหยุดประจำสัปดาห์ and วันหยุดนักขัตฤกษ์ of the calendar. A working day is
/// 08:00–12:00 and 13:00–17:00 (8 hours, 1 hour = 0.125 day): the first day loses what comes before the start time and the last day
/// what comes after the end time — start 13:00 loses 0.5, end 12:00 loses 0.5, 08:00–17:00 is a whole day. A start or end that falls on a
/// non-working day takes nothing off. The result is only the value offered on the screen: the user may type another.
/// </summary>
public static class LeaveDayCounter
{
    private const double HoursPerDay = 8;
    private static readonly TimeOnly DayStart = new(8, 0);
    private static readonly TimeOnly LunchStart = new(12, 0);
    private static readonly TimeOnly LunchEnd = new(13, 0);

    /// <summary>Null while the dates are missing or the start is after the end.</summary>
    public static double? Count(DateOnly? from, DateOnly? to, TimeOnly? fromTime, TimeOnly? toTime, IReadOnlySet<DateOnly> nonWorkingDays)
    {
        if (from is not { } start || to is not { } end || start > end) return null;
        var (startHours, endHours) = (WorkedHours(fromTime ?? DayStart), WorkedHours(toTime ?? new TimeOnly(17, 0)));

        if (start == end)
            return nonWorkingDays.Contains(start) ? 0 : ToEighths(Math.Max(0, endHours - startHours) / HoursPerDay);

        double days = 0;
        for (var d = start; d <= end; d = d.AddDays(1))
            if (!nonWorkingDays.Contains(d)) days++;
        if (!nonWorkingDays.Contains(start)) days -= startHours / HoursPerDay;
        if (!nonWorkingDays.Contains(end)) days -= (HoursPerDay - endHours) / HoursPerDay;
        return Math.Max(0, ToEighths(days));

        static double ToEighths(double days) => Math.Round(days * HoursPerDay, MidpointRounding.AwayFromZero) / HoursPerDay;
    }

    /// <summary>Hours of the working day (0..8) from 08:00 up to <paramref name="time"/>, the lunch hour not counted.</summary>
    internal static double WorkedHours(TimeOnly time)
    {
        var hours = (time - DayStart).TotalHours;
        if (time > LunchStart) hours -= Math.Min((time - LunchStart).TotalHours, (LunchEnd - LunchStart).TotalHours);
        return Math.Clamp(hours, 0, HoursPerDay);
    }
}
