using Lockte.Hr.Data;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Hr;

/// <summary>
/// The public, read-only face of วันหยุดประจำสัปดาห์ and วันหยุดประเพณี: how work time, payroll and screens ask which days are
/// วันหยุดประจำสัปดาห์ or วันหยุดประเพณี. Writes go through <see cref="HolidayRegistry"/>.
/// </summary>
public sealed class HolidayCalendar(HrDbContext db)
{
    public async Task<DaysOff> GetDaysOffAsync(DateOnly from, DateOnly to)
    {
        var weekly = await db.WeeklyHolidays.AsNoTracking().Where(w => w.Date >= from && w.Date <= to).Select(w => w.Date).ToListAsync();
        var holidays = await db.PublicHolidays.AsNoTracking().Where(h => h.Date >= from && h.Date <= to).Select(h => h.Date).ToListAsync();
        return new DaysOff(from, to, weekly.ToHashSet(), holidays.ToHashSet());
    }

    /// <summary>SCR-15, in date order.</summary>
    public async Task<IReadOnlyList<WeeklyHolidayDto>> GetWeeklyHolidaysAsync()
        => await db.WeeklyHolidays.AsNoTracking().OrderBy(w => w.Date)
            .Select(w => new WeeklyHolidayDto(w.Id, w.Date, w.Label, w.RowVersion)).ToListAsync();

    /// <summary>SCR-16, in date order.</summary>
    public async Task<IReadOnlyList<PublicHolidayDto>> GetPublicHolidaysAsync()
        => await db.PublicHolidays.AsNoTracking().OrderBy(h => h.Date)
            .Select(h => new PublicHolidayDto(h.Id, h.Date, h.Name, h.RowVersion)).ToListAsync();
}
