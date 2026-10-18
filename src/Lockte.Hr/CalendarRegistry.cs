using Lockte.Hr.Data;
using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Hr;

/// <summary>
/// เพิ่ม/แก้/ลบ ปฏิทินวันหยุด: วันหยุดประจำสัปดาห์ (SCR-15, BR-014) and วันหยุดประเพณี (SCR-16). A date is required — a
/// blank row is never saved (instead of BR-051's delete on close) — and appears once per calendar (the legacy tables have
/// no duplicates, checked 2026-10-18). Edits and deletes carry the rowversion the screen loaded; a newer save by someone
/// else throws <see cref="ConcurrencyConflictException"/>. Deleting a row that is already gone succeeds. The two
/// many-row operations of SCR-15 are batch jobs: one history event each, no row-by-row history.
/// </summary>
public sealed class CalendarRegistry(HrDbContext db, AuditScope audit)
{
    /// <summary>T-SUNDAY.ITEM default and size.</summary>
    public const string DefaultWeeklyLabel = "วันอาทิตย์";
    public const int WeeklyLabelLength = 10;
    /// <summary>T-HOLIDAY.ITEM size.</summary>
    public const int PublicHolidayNameLength = 46;

    public const string WeeklyHolidayTable = "วันหยุดประจำสัปดาห์";
    public const string AddFourWeeksAction = "เพิ่มวันหยุดประจำสัปดาห์ 4 สัปดาห์";
    public const string DeleteRangeAction = "ลบวันหยุดประจำสัปดาห์ตามช่วงวันที่";

    public async Task<ServiceResult> SaveWeeklyHolidayAsync(WeeklyHolidayInput input)
    {
        var label = Clean(input.Label) ?? DefaultWeeklyLabel;
        if (input.Date is not { } date) return ServiceResult.Fail("กรุณากรอกวันที่");
        if (label.Length > WeeklyLabelLength) return ServiceResult.Fail($"ป้ายยาวได้ไม่เกิน {WeeklyLabelLength} ตัว");
        if (await db.WeeklyHolidays.AnyAsync(h => h.Date == date && h.Id != input.Id))
            return ServiceResult.Fail($"มีวันหยุดประจำสัปดาห์วันที่ {Thai(date)} อยู่แล้ว");

        var row = input.Id is { } id ? await LoadAsync(db.WeeklyHolidays, id, input.RowVersion) : db.WeeklyHolidays.Add(new WeeklyHoliday()).Entity;
        if (row is null) return ServiceResult.Fail("ไม่พบวันหยุดประจำสัปดาห์นี้ อาจถูกลบไปแล้ว");
        row.Date = date;
        row.Label = label;
        await db.SaveChangesAsync();
        return ServiceResult.Ok(row.Id);
    }

    public async Task<ServiceResult> SavePublicHolidayAsync(PublicHolidayInput input)
    {
        var name = Clean(input.Name);
        if (input.Date is not { } date) return ServiceResult.Fail("กรุณากรอกวันที่");
        if (name?.Length > PublicHolidayNameLength) return ServiceResult.Fail($"ชื่อวันหยุดยาวได้ไม่เกิน {PublicHolidayNameLength} ตัว");
        if (await db.PublicHolidays.AnyAsync(h => h.Date == date && h.Id != input.Id))
            return ServiceResult.Fail($"มีวันหยุดประเพณีวันที่ {Thai(date)} อยู่แล้ว");

        var row = input.Id is { } id ? await LoadAsync(db.PublicHolidays, id, input.RowVersion) : db.PublicHolidays.Add(new PublicHoliday()).Entity;
        if (row is null) return ServiceResult.Fail("ไม่พบวันหยุดประเพณีนี้ อาจถูกลบไปแล้ว");
        row.Date = date;
        row.Name = name;
        await db.SaveChangesAsync();
        return ServiceResult.Ok(row.Id);
    }

    /// <summary>
    /// ปุ่ม "เพิ่ม" of SCR-15 (BR-014, มาโคร SUNDAY): four runs, each adding Max(DATE) + 7 for every label.
    /// A date already in the calendar is left as it is.
    /// </summary>
    public async Task<ServiceResult> AddNextFourWeeksAsync()
    {
        var lastByLabel = await db.WeeklyHolidays.GroupBy(h => h.Label)
            .Select(g => new { Label = g.Key, Last = g.Max(h => h.Date) })
            .ToListAsync();
        if (lastByLabel.Count == 0) return ServiceResult.Fail("ยังไม่มีวันหยุดประจำสัปดาห์ — กรอกวันแรกก่อน");

        var wanted = lastByLabel
            .SelectMany(l => Enumerable.Range(1, 4).Select(week => (l.Label, Date: l.Last.AddDays(7 * week))))
            .ToList();
        var dates = wanted.Select(w => w.Date).ToList();
        var taken = (await db.WeeklyHolidays.Where(h => dates.Contains(h.Date)).Select(h => h.Date).ToListAsync()).ToHashSet();
        var added = wanted.Where(w => taken.Add(w.Date)).Select(w => new WeeklyHoliday { Date = w.Date, Label = w.Label }).ToList();
        using (audit.SuppressRowChanges())
        {
            db.WeeklyHolidays.AddRange(added);
            var perLabel = added.GroupBy(h => h.Label)
                .Select(g => new { ป้าย = g.Key, ตั้งแต่ = IsoDate(g.Min(h => h.Date)), ถึง = IsoDate(g.Max(h => h.Date)) })
                .ToList();
            db.AddHistoryEvent(AddFourWeeksAction, perLabel, added.Count, WeeklyHolidayTable);
            await db.SaveChangesAsync();
        }
        return ServiceResult.Ok();
    }

    public Task<ServiceResult> DeleteWeeklyHolidayAsync(int id, byte[] loadedRowVersion) => DeleteAsync(db.WeeklyHolidays, id, loadedRowVersion);

    public Task<ServiceResult> DeletePublicHolidayAsync(int id, byte[] loadedRowVersion) => DeleteAsync(db.PublicHolidays, id, loadedRowVersion);

    /// <summary>ปุ่ม "ลบ" of SCR-15 (มาโคร ลบวันอาทิตย์1): every วันหยุดประจำสัปดาห์ from one date to another.</summary>
    public async Task<ServiceResult> DeleteWeeklyHolidaysAsync(DateOnly from, DateOnly to)
    {
        if (from > to) return ServiceResult.Fail("วันที่เริ่มต้องไม่เกินวันที่สิ้นสุด");
        var rows = await db.WeeklyHolidays.Where(h => h.Date >= from && h.Date <= to).ToListAsync();
        using (audit.SuppressRowChanges())
        {
            db.WeeklyHolidays.RemoveRange(rows);
            db.AddHistoryEvent(DeleteRangeAction, new { ตั้งแต่ = IsoDate(from), ถึง = IsoDate(to) }, rows.Count, WeeklyHolidayTable);
            await db.SaveChangesAsync();
        }
        return ServiceResult.Ok();
    }

    private async Task<ServiceResult> DeleteAsync<T>(DbSet<T> rows, int id, byte[] loadedRowVersion) where T : class, IAudited
    {
        if (await LoadAsync(rows, id, loadedRowVersion) is not { } row) return ServiceResult.Ok(); // already gone
        rows.Remove(row);
        await db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    private static async Task<T?> LoadAsync<T>(DbSet<T> rows, int id, byte[]? loadedRowVersion) where T : class, IAudited
    {
        var row = await rows.FindAsync(id);
        row?.ExpectRowVersion(loadedRowVersion
            ?? throw new ArgumentException("Editing needs the rowversion the screen loaded.", nameof(loadedRowVersion)));
        return row;
    }

    private static string Thai(DateOnly date) => ThaiDate.Format(date);

    /// <summary>Dates in an event's parameters: culture-independent, like the history's own values.</summary>
    private static string IsoDate(DateOnly date) => date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
