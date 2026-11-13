using Lockte.Hr.Data;
using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Hr;

/// <summary>One range of อัตราเงินสมทบกองทุนสงเคราะห์ลูกจ้าง: <see cref="EndDate"/> null = เป็นต้นไป, <see cref="Percent"/> 0.25 = 0.25% ฝ่ายละ.</summary>
public sealed record WelfareRateDto(int Id, DateOnly StartDate, DateOnly? EndDate, decimal Percent, byte[] RowVersion)
{
    public bool Covers(DateOnly date) => StartDate <= date && (EndDate is not { } end || date <= end);
}

/// <summary>What HR saves: <see cref="Id"/> null adds a range; <see cref="RowVersion"/> is the one the page loaded when editing.</summary>
public sealed record WelfareRateInput(int? Id, DateOnly? StartDate, DateOnly? EndDate, decimal? Percent, byte[]? RowVersion);

/// <summary>
/// The rules of the rate table, without the database: which range covers a date and whether a new range overlaps the others.
/// </summary>
public static class WelfareRateTable
{
    /// <summary>The range in force on <paramref name="date"/>, or null when none covers it (never a guess).</summary>
    public static WelfareRateDto? On(IEnumerable<WelfareRateDto> rates, DateOnly date) => rates.FirstOrDefault(r => r.Covers(date));

    /// <summary>The first of <paramref name="others"/> that shares a day with start–end (end null = เป็นต้นไป).</summary>
    public static WelfareRateDto? Overlapping(IEnumerable<WelfareRateDto> others, DateOnly start, DateOnly? end)
        => others.OrderBy(r => r.StartDate).FirstOrDefault(r => (end is not { } e || r.StartDate <= e) && (r.EndDate is not { } re || start <= re));

    /// <summary>"1 ม.ค. 2570 – 30 ก.ย. 2574" / "1 ต.ค. 2574 เป็นต้นไป".</summary>
    public static string RangeText(DateOnly start, DateOnly? end)
        => end is { } e ? $"{ThaiDate.Format(start)} – {ThaiDate.Format(e)}" : $"{ThaiDate.Format(start)} เป็นต้นไป";

    /// <summary>"0.25%" / "0.5%".</summary>
    public static string PercentText(decimal percent) => percent.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture) + "%";

    /// <summary>One portion (ลูกจ้าง or บริษัท) on a ค่าจ้าง: whole baht, half away from zero (as <see cref="Welfare"/>).</summary>
    public static decimal Contribution(decimal wage, decimal percent) => Math.Round(wage * percent / 100m, 0, MidpointRounding.AwayFromZero);

    /// <summary>The message when no range covers a งวด's ถึงวันที่.</summary>
    public static string Missing(DateOnly date)
        => $"ยังไม่ได้ตั้งอัตราเงินสมทบกองทุนสงเคราะห์ลูกจ้างที่ครอบวันที่ {ThaiDate.Format(date)} — ตั้งอัตราที่หน้า \"{WelfareRateRegistry.Title}\" ก่อน";
}

/// <summary>
/// อัตราเงินสมทบกองทุนสงเคราะห์ลูกจ้าง (UAT-43): the ranges HR keeps on the settings page, read by ยอดเงินส่งกองทุนสงเคราะห์ลูกจ้าง.
/// A range that overlaps another is refused; a newer save by someone else throws <see cref="ConcurrencyConflictException"/>.
/// </summary>
public sealed class WelfareRateRegistry(HrDbContext db)
{
    public const string Title = "อัตราเงินสมทบกองทุนสงเคราะห์ลูกจ้าง";
    public const decimal MaxPercent = 100m;

    /// <summary>Every range by วันเริ่ม.</summary>
    public async Task<IReadOnlyList<WelfareRateDto>> ListAsync()
        => (await db.WelfareRates.AsNoTracking().ToListAsync())
            .OrderBy(r => r.StartDate)
            .Select(r => new WelfareRateDto(r.Id, r.StartDate, r.EndDate, r.Percent, r.RowVersion))
            .ToList();

    /// <summary>The rate % in force on <paramref name="date"/>, or null when no range covers it.</summary>
    public async Task<decimal?> PercentOnAsync(DateOnly date) => WelfareRateTable.On(await ListAsync(), date)?.Percent;

    public async Task<ServiceResult> SaveAsync(WelfareRateInput input)
    {
        if (input.StartDate is not { } start) return ServiceResult.Fail("กรุณากรอกวันเริ่มมีผล");
        if (input.EndDate is { } end && end < start) return ServiceResult.Fail("วันสิ้นสุดต้องไม่ก่อนวันเริ่มมีผล");
        if (input.Percent is not { } percent) return ServiceResult.Fail("กรุณากรอกอัตรา %");
        if (percent <= 0 || percent > MaxPercent) return ServiceResult.Fail($"อัตราต้องมากกว่า 0 และไม่เกิน {MaxPercent}%");
        if (decimal.Round(percent, 4) != percent) return ServiceResult.Fail("อัตราใส่ทศนิยมได้ไม่เกิน 4 ตำแหน่ง");

        var others = (await ListAsync()).Where(r => r.Id != input.Id);
        if (WelfareRateTable.Overlapping(others, start, input.EndDate) is { } clash)
            return ServiceResult.Fail($"ช่วงวันที่ทับกับช่วง {WelfareRateTable.RangeText(clash.StartDate, clash.EndDate)} ({WelfareRateTable.PercentText(clash.Percent)}) — แก้ช่วงเดิมก่อน");

        WelfareRate? row;
        if (input.Id is { } id)
        {
            row = await db.WelfareRates.FindAsync(id);
            if (row is null) return ServiceResult.Fail("ไม่พบช่วงอัตรานี้ อาจถูกลบไปแล้ว");
            row.ExpectRowVersion(input.RowVersion ?? throw new ConcurrencyConflictException());
        }
        else row = db.WelfareRates.Add(new WelfareRate()).Entity;
        row.StartDate = start;
        row.EndDate = input.EndDate;
        row.Percent = percent;
        await db.SaveChangesAsync();
        return ServiceResult.Ok(row.Id);
    }

    public async Task<ServiceResult> DeleteAsync(int id, byte[] loadedRowVersion)
    {
        var row = await db.WelfareRates.FindAsync(id);
        if (row is null) return ServiceResult.Ok(); // already gone
        row.ExpectRowVersion(loadedRowVersion);
        db.WelfareRates.Remove(row);
        await db.SaveChangesAsync();
        return ServiceResult.Ok();
    }
}
