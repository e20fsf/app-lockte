using System.Globalization;
using Lockte.Hr.Data;
using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Hr;

/// <summary>
/// เพิ่ม/แก้ ปฏิทินวันหยุด: SCR-15 (วันหยุดประจำสัปดาห์) and SCR-16 (วันหยุดประเพณี). Each save carries the rowversion
/// the screen loaded; if the row changed since, it throws <see cref="ConcurrencyConflictException"/>.
/// The date is required (as decided for SCR-05 instead of BR-051's "blank row is deleted"): a blank new row is not
/// saved and clearing a date is an error, never a delete. A date appears once per register, so payroll never counts
/// a day twice.
/// </summary>
public sealed class HolidayRegistry(HrDbContext db)
{
    public const int WeeklyLabelLength = 10;
    public const int PublicHolidayNameLength = 46;

    /// <summary>ป้ายเริ่มต้น of T-SUNDAY.ITEM.</summary>
    public const string DefaultWeeklyLabel = "วันอาทิตย์";

    public async Task<ServiceResult> SaveWeeklyHolidayAsync(WeeklyHolidayInput input)
    {
        var label = Clean(input.Label) ?? DefaultWeeklyLabel;
        var errors = new List<string>();
        if (input.Date is not { } date) errors.Add("กรุณากรอกวันที่");
        else if (await db.WeeklyHolidays.AnyAsync(w => w.Date == date && w.Id != input.Id))
            errors.Add($"มีวันหยุดประจำสัปดาห์วันที่ {ThaiDate(date)} อยู่แล้ว");
        if (label.Length > WeeklyLabelLength) errors.Add($"ป้ายยาวได้ไม่เกิน {WeeklyLabelLength} ตัว");
        if (errors.Count > 0) return new ServiceResult(false, errors);

        var holiday = input.Id is { } id ? await LoadAsync(db.WeeklyHolidays, id, input.RowVersion) : db.WeeklyHolidays.Add(new WeeklyHoliday()).Entity;
        if (holiday is null) return ServiceResult.Fail("ไม่พบวันหยุดนี้ อาจถูกลบไปแล้ว");
        holiday.Date = input.Date!.Value;
        holiday.Label = label;
        await db.SaveChangesAsync();
        return ServiceResult.Ok(holiday.Id);
    }

    /// <summary>Weeks added per press of "เพิ่ม" (BR-014: macro SUNDAY runs four rounds).</summary>
    public const int WeeksPerAdd = 4;

    /// <summary>
    /// SCR-15 "เพิ่ม" (BR-014): for each label, four more dates a week apart after its last date — so a series that was
    /// moved to another weekday continues on that day. A date already in the register is left as it is.
    /// </summary>
    public async Task<ServiceResult> AddNextWeeklyHolidaysAsync()
    {
        var lastByLabel = await db.WeeklyHolidays.GroupBy(w => w.Label)
            .Select(g => new { Label = g.Key, Last = g.Max(w => w.Date) }).ToListAsync();
        if (lastByLabel.Count == 0) return ServiceResult.Fail("ยังไม่มีวันหยุดประจำสัปดาห์ กรุณากรอกวันแรกก่อน");

        var wanted = lastByLabel.OrderBy(s => s.Last)
            .SelectMany(s => Enumerable.Range(1, WeeksPerAdd).Select(week => (Date: s.Last.AddDays(7 * week), s.Label)))
            .DistinctBy(w => w.Date).ToList();
        var dates = wanted.Select(w => w.Date).ToList();
        var taken = (await db.WeeklyHolidays.Where(w => dates.Contains(w.Date)).Select(w => w.Date).ToListAsync()).ToHashSet();
        db.WeeklyHolidays.AddRange(wanted.Where(w => !taken.Contains(w.Date))
            .Select(w => new WeeklyHoliday { Date = w.Date, Label = w.Label }));
        await db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    /// <summary>SCR-15 "ลบ" (form ลบวันอาทิตย์ → macro ลบวันอาทิตย์1): every weekly holiday from <paramref name="from"/> to <paramref name="to"/>.</summary>
    public async Task<ServiceResult> DeleteWeeklyHolidaysAsync(DateOnly from, DateOnly to)
    {
        if (from > to) return ServiceResult.Fail("วันที่เริ่มต้องไม่หลังวันที่สิ้นสุด");
        db.WeeklyHolidays.RemoveRange(await db.WeeklyHolidays.Where(w => w.Date >= from && w.Date <= to).ToListAsync());
        await db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    /// <summary>SCR-16: deletes one row, as the legacy datasheet did.</summary>
    public async Task<ServiceResult> DeletePublicHolidayAsync(int id, byte[] loadedRowVersion)
    {
        var holiday = await LoadAsync(db.PublicHolidays, id, loadedRowVersion);
        if (holiday is null) return ServiceResult.Fail("ไม่พบวันหยุดนี้ อาจถูกลบไปแล้ว");
        db.PublicHolidays.Remove(holiday);
        await db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> SavePublicHolidayAsync(PublicHolidayInput input)
    {
        var name = Clean(input.Name);
        var errors = new List<string>();
        if (input.Date is not { } date) errors.Add("กรุณากรอกวันที่");
        else if (await db.PublicHolidays.AnyAsync(h => h.Date == date && h.Id != input.Id))
            errors.Add($"มีวันหยุดประเพณีวันที่ {ThaiDate(date)} อยู่แล้ว");
        if (name?.Length > PublicHolidayNameLength) errors.Add($"ชื่อวันหยุดยาวได้ไม่เกิน {PublicHolidayNameLength} ตัว");
        if (errors.Count > 0) return new ServiceResult(false, errors);

        var holiday = input.Id is { } id ? await LoadAsync(db.PublicHolidays, id, input.RowVersion) : db.PublicHolidays.Add(new PublicHoliday()).Entity;
        if (holiday is null) return ServiceResult.Fail("ไม่พบวันหยุดนี้ อาจถูกลบไปแล้ว");
        holiday.Date = input.Date!.Value;
        holiday.Name = name;
        await db.SaveChangesAsync();
        return ServiceResult.Ok(holiday.Id);
    }

    /// <summary>The row to change, or null if it is gone. Throws a conflict if it changed since the screen loaded it.</summary>
    private static async Task<T?> LoadAsync<T>(IQueryable<T> query, int id, byte[]? loadedRowVersion) where T : class, IAudited
    {
        var row = await query.FirstOrDefaultAsync(e => EF.Property<int>(e, "Id") == id);
        if (row is not null && loadedRowVersion is not null) row.ExpectRowVersion(loadedRowVersion);
        return row;
    }

    /// <summary>d/M/yyyy in พ.ศ., as the screens show dates.</summary>
    internal static string ThaiDate(DateOnly date) => date.ToString("d/M/yyyy", ThaiCulture);

    private static readonly CultureInfo ThaiCulture = new("th-TH");

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
