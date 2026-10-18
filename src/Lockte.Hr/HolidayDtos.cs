namespace Lockte.Hr;

public sealed record WeeklyHolidayDto(int Id, DateOnly Date, string Label, byte[] RowVersion);

public sealed record PublicHolidayDto(int Id, DateOnly Date, string? Name, byte[] RowVersion);

/// <summary>
/// A row of SCR-15 to save. <see cref="Id"/> null = new row; <see cref="RowVersion"/> = as loaded.
/// Date is required: a row is never deleted by clearing it. A blank label becomes "วันอาทิตย์".
/// </summary>
public sealed record WeeklyHolidayInput(int? Id, DateOnly? Date, string? Label, byte[]? RowVersion);

/// <summary>A row of SCR-16 to save; date required, name optional.</summary>
public sealed record PublicHolidayInput(int? Id, DateOnly? Date, string? Name, byte[]? RowVersion);

/// <summary>
/// The days off between <see cref="From"/> and <see cref="To"/> (inclusive), for generating work time and payroll
/// (SUN / HOLI, BR-002). A date can be both, as in the legacy tables, which payroll counts separately.
/// </summary>
public sealed record DaysOff(
    DateOnly From,
    DateOnly To,
    IReadOnlySet<DateOnly> WeeklyHolidays,
    IReadOnlySet<DateOnly> PublicHolidays)
{
    public bool IsWeeklyHoliday(DateOnly date) => WeeklyHolidays.Contains(date);

    public bool IsPublicHoliday(DateOnly date) => PublicHolidays.Contains(date);

    public bool IsDayOff(DateOnly date) => IsWeeklyHoliday(date) || IsPublicHoliday(date);
}
