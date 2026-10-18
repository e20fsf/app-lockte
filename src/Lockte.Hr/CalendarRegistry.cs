using System.Globalization;
using Lockte.Hr.Data;
using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Hr;

/// <summary>
/// เพิ่ม/แก้/ลบ ปฏิทินวันหยุด: SCR-15 (วันอาทิตย์, BR-014) and SCR-16 (วันหยุด). A date is required — a blank row is
/// never saved (instead of BR-051's delete on close) — and appears once per calendar. Edits and deletes carry the
/// rowversion the screen loaded; a newer save by someone else throws <see cref="ConcurrencyConflictException"/>.
/// </summary>
public sealed class CalendarRegistry(HrDbContext db)
{
    /// <summary>T-SUNDAY.ITEM default and size.</summary>
    public const string DefaultWeeklyLabel = "วันอาทิตย์";
    public const int WeeklyLabelLength = 10;
    /// <summary>T-HOLIDAY.ITEM size.</summary>
    public const int HolidayNameLength = 46;

    public async Task<ServiceResult> SaveWeeklyHolidayAsync(WeeklyHolidayInput input)
    {
        var label = Clean(input.Label) ?? DefaultWeeklyLabel;
        if (input.Date is not { } date) return ServiceResult.Fail("กรุณากรอกวันที่");
        if (label.Length > WeeklyLabelLength) return ServiceResult.Fail($"ป้ายยาวได้ไม่เกิน {WeeklyLabelLength} ตัว");
        if (await db.WeeklyHolidays.AnyAsync(h => h.Date == date && h.Id != input.Id))
            return ServiceResult.Fail($"มีวันหยุดประจำสัปดาห์วันที่ {Thai(date)} อยู่แล้ว");

        var row = input.Id is { } id ? await LoadAsync(db.WeeklyHolidays, id, input.RowVersion) : db.WeeklyHolidays.Add(new WeeklyHoliday()).Entity;
        if (row is null) return ServiceResult.Fail("ไม่พบวันหยุดนี้ อาจถูกลบไปแล้ว");
        row.Date = date;
        row.Label = label;
        await db.SaveChangesAsync();
        return ServiceResult.Ok(row.Id);
    }

    public async Task<ServiceResult> SavePublicHolidayAsync(PublicHolidayInput input)
    {
        var name = Clean(input.Name);
        if (input.Date is not { } date) return ServiceResult.Fail("กรุณากรอกวันที่");
        if (name?.Length > HolidayNameLength) return ServiceResult.Fail($"ชื่อวันหยุดยาวได้ไม่เกิน {HolidayNameLength} ตัว");
        if (await db.PublicHolidays.AnyAsync(h => h.Date == date && h.Id != input.Id))
            return ServiceResult.Fail($"มีวันหยุดประเพณีวันที่ {Thai(date)} อยู่แล้ว");

        var row = input.Id is { } id ? await LoadAsync(db.PublicHolidays, id, input.RowVersion) : db.PublicHolidays.Add(new PublicHoliday()).Entity;
        if (row is null) return ServiceResult.Fail("ไม่พบวันหยุดนี้ อาจถูกลบไปแล้ว");
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
        foreach (var (label, date) in wanted.Where(w => taken.Add(w.Date)))
            db.WeeklyHolidays.Add(new WeeklyHoliday { Date = date, Label = label });
        await db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    public Task<ServiceResult> DeleteWeeklyHolidayAsync(int id, byte[] loadedRowVersion) => DeleteAsync(db.WeeklyHolidays, id, loadedRowVersion);

    public Task<ServiceResult> DeletePublicHolidayAsync(int id, byte[] loadedRowVersion) => DeleteAsync(db.PublicHolidays, id, loadedRowVersion);

    /// <summary>ปุ่ม "ลบ" of SCR-15 (มาโคร ลบวันอาทิตย์1): every วันหยุดประจำสัปดาห์ from one date to another.</summary>
    public async Task<ServiceResult> DeleteWeeklyHolidaysAsync(DateOnly from, DateOnly to)
    {
        if (from > to) return ServiceResult.Fail("วันที่เริ่มต้องไม่เกินวันที่สิ้นสุด");
        db.WeeklyHolidays.RemoveRange(await db.WeeklyHolidays.Where(h => h.Date >= from && h.Date <= to).ToListAsync());
        await db.SaveChangesAsync();
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

    private static string Thai(DateOnly date) => date.ToString("d/M/yyyy", new CultureInfo("th-TH"));

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
