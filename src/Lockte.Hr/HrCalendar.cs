using Lockte.Hr.Data;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Hr;

/// <summary>
/// The read side of ปฏิทินวันหยุด: SCR-15/16 lists and the questions time entry and payroll ask
/// ("is this a วันหยุดประจำสัปดาห์ / วันหยุดประเพณี?"). Writes go through <see cref="CalendarRegistry"/>.
/// </summary>
public sealed class HrCalendar(HrDbContext db)
{
    public async Task<HolidayCalendar> GetAsync(DateOnly from, DateOnly to)
    {
        var weekly = await db.WeeklyHolidays.AsNoTracking()
            .Where(h => h.Date >= from && h.Date <= to).OrderBy(h => h.Date)
            .Select(h => new WeeklyHolidayDto(h.Id, h.Date, h.Label, h.RowVersion))
            .ToListAsync();
        var holidays = await db.PublicHolidays.AsNoTracking()
            .Where(h => h.Date >= from && h.Date <= to).OrderBy(h => h.Date)
            .Select(h => new PublicHolidayDto(h.Id, h.Date, h.Name, h.RowVersion))
            .ToListAsync();
        return new HolidayCalendar(from, to, weekly, holidays);
    }
}
