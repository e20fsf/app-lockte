namespace Lockte.Hr;

public sealed record WeeklyHolidayDto(int Id, DateOnly Date, string Label, byte[] RowVersion);

public sealed record PublicHolidayDto(int Id, DateOnly Date, string? Name, byte[] RowVersion);

/// <summary>A row of SCR-15 to save. <see cref="Id"/> null = new; a blank label takes the legacy default "วันอาทิตย์".</summary>
public sealed record WeeklyHolidayInput(int? Id, DateOnly? Date, string? Label, byte[]? RowVersion);

/// <summary>A row of SCR-16 (วันหยุดประเพณี) to save. <see cref="Id"/> null = new.</summary>
public sealed record PublicHolidayInput(int? Id, DateOnly? Date, string? Name, byte[]? RowVersion);

/// <summary>
/// วันหยุดประจำสัปดาห์ and วันหยุดประเพณี between two dates (inclusive) — what time entry and payroll ask the
/// calendar (SUN / HOLI / DAT of BR-002 count these).
/// </summary>
public sealed record HolidayCalendar(DateOnly From, DateOnly To, IReadOnlyList<WeeklyHolidayDto> WeeklyHolidays,
    IReadOnlyList<PublicHolidayDto> PublicHolidays)
{
    private readonly HashSet<DateOnly> weekly = WeeklyHolidays.Select(h => h.Date).ToHashSet();
    private readonly HashSet<DateOnly> holidays = PublicHolidays.Select(h => h.Date).ToHashSet();

    /// <summary>วันหยุดประจำสัปดาห์ (ตาราง SUNDAY) — only dates between <see cref="From"/> and <see cref="To"/>.</summary>
    public bool IsWeeklyHoliday(DateOnly date) => InRange(date) && weekly.Contains(date);

    /// <summary>วันหยุดประเพณี (ตาราง HOLIDAY) — only dates between <see cref="From"/> and <see cref="To"/>.</summary>
    public bool IsPublicHoliday(DateOnly date) => InRange(date) && holidays.Contains(date);

    private bool InRange(DateOnly date)
        => date >= From && date <= To ? true : throw new ArgumentOutOfRangeException(nameof(date), "Outside the calendar that was read.");
}
