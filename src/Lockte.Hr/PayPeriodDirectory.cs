using Lockte.Hr.Data;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Hr;

/// <summary>
/// The read side of งวดค่าแรง: the periods, the default dates of the next one (#23 Q-04), the checks before calculating
/// (#22), the calculated time totals (BR-002) and the rows of RPT-01. Writes go through <see cref="PayPeriodRegistry"/>.
/// </summary>
public sealed class PayPeriodDirectory(HrDbContext db, TimeProvider clock)
{
    /// <summary>Newest first.</summary>
    public async Task<IReadOnlyList<PayPeriodDto>> ListAsync()
        => await db.PayPeriods.AsNoTracking().OrderByDescending(p => p.FromDate).Select(ToDto).ToListAsync();

    public async Task<PayPeriodDto?> GetAsync(int id)
        => await db.PayPeriods.AsNoTracking().Where(p => p.Id == id).Select(ToDto).FirstOrDefaultAsync();

    /// <summary>
    /// วันที่ที่ระบบเติมให้ตอนตั้งงวดใหม่: the period after the newest one, or — with none yet — the newest usual
    /// period that has ended by today.
    /// </summary>
    public async Task<PayPeriodDates> DefaultDatesAsync()
    {
        var latest = await db.PayPeriods.AsNoTracking().OrderByDescending(p => p.FromDate)
            .Select(p => new { p.FromDate, p.ToDate, p.PayDate }).FirstOrDefaultAsync();
        return latest is null
            ? PayPeriodDates.LastEndedBy(DateOnly.FromDateTime(clock.GetLocalNow().DateTime))
            : PayPeriodDates.After(new PayPeriodDates(latest.FromDate, latest.ToDate, latest.PayDate));
    }

    /// <summary>
    /// ตรวจเวลาทำงานก่อนคำนวณ (#22): working days of the period (not วันหยุดประจำสัปดาห์/ประเพณี) that nobody has
    /// a เวลาปกติ (W) row for yet, and the มาสาย rows (LATE set), by date then code.
    /// </summary>
    public async Task<PayPeriodCheck> CheckAsync(int periodId)
    {
        var period = await db.PayPeriods.AsNoTracking().FirstAsync(p => p.Id == periodId);
        var (from, to) = (period.FromDate, period.ToDate);
        var daysOff = (await db.WeeklyHolidays.Where(h => h.Date >= from && h.Date <= to).Select(h => h.Date).ToListAsync())
            .Concat(await db.PublicHolidays.Where(h => h.Date >= from && h.Date <= to).Select(h => h.Date).ToListAsync())
            .ToHashSet();
        var withTime = (await db.WorkTimes.Where(w => w.Date >= from && w.Date <= to && w.Type == WorkTypes.Normal)
            .Select(w => w.Date).Distinct().ToListAsync()).ToHashSet();
        var missing = Enumerable.Range(0, to.DayNumber - from.DayNumber + 1).Select(from.AddDays)
            .Where(d => !daysOff.Contains(d) && !withTime.Contains(d)).ToList();
        var late = (await db.WorkTimes.AsNoTracking().Where(w => w.Date >= from && w.Date <= to && w.Late != null)
                .Select(w => new { w.Date, w.Employee.Code, w.Employee.Name, w.WorkIn, Late = w.Late!.Value }).ToListAsync())
            .OrderBy(w => w.Date).ThenBy(w => w.Code, StringComparer.Ordinal)
            .Select(w => new LateArrival(w.Date, w.Code, w.Name, w.WorkIn, w.Late)).ToList();
        return new PayPeriodCheck(missing, late);
    }

    /// <summary>The time totals of the last calculation (BR-002), by รหัส ↑; empty before the first one.</summary>
    public async Task<IReadOnlyList<EmployeeTimeTotal>> GetTimeTotalsAsync(int periodId)
        => (await db.PayPeriodTimeTotals.AsNoTracking().Where(t => t.PayPeriodId == periodId)
                .Select(t => new EmployeeTimeTotal(t.EmployeeId, t.Employee.Code, t.Employee.Name, t.Normal, t.PaidLeave, t.UnpaidLeave,
                    t.Overtime, t.Sunday, t.SundayOvertime, t.Holiday, t.HolidayOvertime, t.PublicHolidays, t.WeeklyHolidays,
                    t.WeeklyHolidaysToResign))
                .ToListAsync())
            .OrderBy(t => t.Code, StringComparer.Ordinal).ToList();

    /// <summary>RPT-01 เวลาทำงานรายคน; null until the period's time is calculated.</summary>
    public async Task<TimeReport?> GetTimeReportAsync(int periodId)
    {
        var period = await GetAsync(periodId);
        if (period is null || period.CompletedStep < PayPeriodStep.TimeCalculated) return null;
        var weekly = await db.WeeklyHolidays.CountAsync(h => h.Date >= period.From && h.Date <= period.To);
        var holidays = await db.PublicHolidays.Where(h => h.Date >= period.From && h.Date <= period.To)
            .Select(h => h.Date).OrderBy(d => d).ToListAsync();
        return new TimeReport(period, weekly, holidays.Count, holidays.Count > 0 ? holidays[0] : null, await GetTimeTotalsAsync(periodId));
    }

    private static readonly System.Linq.Expressions.Expression<Func<PayPeriod, PayPeriodDto>> ToDto = p =>
        new PayPeriodDto(p.Id, p.FromDate, p.ToDate, p.PayDate, p.CompletedStep, p.TimeCalculatedAt, p.TimeCalculatedBy, p.RowVersion);
}
