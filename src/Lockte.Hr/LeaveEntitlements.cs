using Lockte.Hr.Data;
using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Hr;

/// <summary>One range of สิทธิ์วันลา: <see cref="EndDate"/> null = เป็นต้นไป.</summary>
public sealed record LeaveEntitlementDto(int Id, DateOnly StartDate, DateOnly? EndDate, LeaveEntitlementDays Days, byte[] RowVersion)
{
    public bool Covers(DateOnly date) => StartDate <= date && (EndDate is not { } end || date <= end);
}

/// <summary>What HR saves: <see cref="Id"/> null adds a range; <see cref="RowVersion"/> is the one the page loaded when editing.</summary>
public sealed record LeaveEntitlementInput(int? Id, DateOnly? StartDate, DateOnly? EndDate, LeaveEntitlementDays? Days, byte[]? RowVersion);

/// <summary>
/// สิทธิ์วันลาได้ค่าแรง (UAT 10, แก้ครั้งที่ 10 "สำหรับแก้ไขและเพิ่มจำนวนลา อนาคตกฎหมายอาจมีการเปลี่ยนแปลงจำนวนวันลา"): the ranges HR
/// keeps on the settings page, read by the quota of การลา (BR-020) for the date of each leave. A date no range covers takes
/// <see cref="LeaveEntitlementDays.Default"/>, so the program works as before until HR sets one. A range that overlaps another is
/// refused; a newer save by someone else throws <see cref="ConcurrencyConflictException"/>.
/// </summary>
public sealed class LeaveEntitlementRegistry(HrDbContext db)
{
    public const string Title = "ตั้งค่าสิทธิ์วันลา";
    public const double MaxDays = 365;

    /// <summary>The fields of the page in the order of the quota table on SCR-30, with ลากิจ split by อายุงาน.</summary>
    public static IReadOnlyList<(string Name, Func<LeaveEntitlementDays, double> Get)> Fields { get; } =
    [
        ("ลากิจ (ทำงานไม่ถึง 1 ปี)", d => d.PersonalFirstYear),
        ("ลากิจ (ทำงานครบ 1 ปี)", d => d.Personal),
        ("ลาป่วย", d => d.Sick),
        ("ลาพักร้อน (ต่อปี)", d => d.Vacation),
        ("ลาคลอด", d => d.Maternity),
        ("ลาเพื่อระดมพล", d => d.Military),
        ("ลาคลอดบุตรกรณีพิเศษ", d => d.SpecialMaternity),
        ("ลาช่วยเหลือคู่สมรสคลอดบุตร", d => d.SpouseMaternity),
    ];

    /// <summary>Every range by วันเริ่ม.</summary>
    public async Task<IReadOnlyList<LeaveEntitlementDto>> ListAsync()
        => (await db.LeaveEntitlements.AsNoTracking().ToListAsync())
            .OrderBy(r => r.StartDate)
            .Select(r => new LeaveEntitlementDto(r.Id, r.StartDate, r.EndDate,
                new(r.PersonalFirstYear, r.Personal, r.Sick, r.Vacation, r.Maternity, r.Military, r.SpecialMaternity, r.SpouseMaternity),
                r.RowVersion))
            .ToList();

    /// <summary>The สิทธิ์วันลา in force on <paramref name="date"/>: the range covering it, else <see cref="LeaveEntitlementDays.Default"/>.</summary>
    public async Task<LeaveEntitlementDays> OnAsync(DateOnly date) => On(await ListAsync(), date);

    public static LeaveEntitlementDays On(IEnumerable<LeaveEntitlementDto> ranges, DateOnly date)
        => ranges.FirstOrDefault(r => r.Covers(date))?.Days ?? LeaveEntitlementDays.Default;

    public async Task<ServiceResult> SaveAsync(LeaveEntitlementInput input)
    {
        if (input.StartDate is not { } start) return ServiceResult.Fail("กรุณากรอกวันเริ่มมีผล");
        if (input.EndDate is { } end && end < start) return ServiceResult.Fail("วันสิ้นสุดต้องไม่ก่อนวันเริ่มมีผล");
        if (input.Days is not { } days) return ServiceResult.Fail("กรุณากรอกจำนวนวัน");
        foreach (var (name, get) in Fields)
        {
            var value = get(days);
            if (double.IsNaN(value) || value < 0 || value > MaxDays) return ServiceResult.Fail($"{name}: จำนวนวันต้องอยู่ระหว่าง 0 ถึง {MaxDays:0}");
            if (Math.Round(value * 8) != value * 8) return ServiceResult.Fail($"{name}: จำนวนวันใส่ได้ทีละ 1/8 วัน (0.125 = 1 ชม.)");
        }

        var others = (await ListAsync()).Where(r => r.Id != input.Id).OrderBy(r => r.StartDate);
        if (others.FirstOrDefault(r => (input.EndDate is not { } e || r.StartDate <= e) && (r.EndDate is not { } re || start <= re)) is { } clash)
            return ServiceResult.Fail($"ช่วงวันที่ทับกับช่วง {RangeText(clash.StartDate, clash.EndDate)} — แก้ช่วงเดิมก่อน");

        LeaveEntitlement? row;
        if (input.Id is { } id)
        {
            row = await db.LeaveEntitlements.FindAsync(id);
            if (row is null) return ServiceResult.Fail("ไม่พบช่วงนี้ อาจถูกลบไปแล้ว");
            row.ExpectRowVersion(input.RowVersion ?? throw new ConcurrencyConflictException());
        }
        else row = db.LeaveEntitlements.Add(new LeaveEntitlement()).Entity;
        row.StartDate = start;
        row.EndDate = input.EndDate;
        row.PersonalFirstYear = days.PersonalFirstYear;
        row.Personal = days.Personal;
        row.Sick = days.Sick;
        row.Vacation = days.Vacation;
        row.Maternity = days.Maternity;
        row.Military = days.Military;
        row.SpecialMaternity = days.SpecialMaternity;
        row.SpouseMaternity = days.SpouseMaternity;
        await db.SaveChangesAsync();
        return ServiceResult.Ok(row.Id);
    }

    public async Task<ServiceResult> DeleteAsync(int id, byte[] loadedRowVersion)
    {
        var row = await db.LeaveEntitlements.FindAsync(id);
        if (row is null) return ServiceResult.Ok(); // already gone
        row.ExpectRowVersion(loadedRowVersion);
        db.LeaveEntitlements.Remove(row);
        await db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    /// <summary>"1 ม.ค. 2570 – 31 ธ.ค. 2570" / "1 ม.ค. 2571 เป็นต้นไป".</summary>
    public static string RangeText(DateOnly start, DateOnly? end) => WelfareRateTable.RangeText(start, end);
}
