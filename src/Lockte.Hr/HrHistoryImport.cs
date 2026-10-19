using Lockte.Hr.Data;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Hr;

/// <summary>A migrated เวลาทำงาน row (E_WORK / ADD_EW), already checked by the migration tool.</summary>
public sealed record WorkTimeImport(DateOnly Date, int EmployeeId, string? Type, TimeOnly? WorkIn, TimeOnly? WorkOut,
    TimeOnly? Late, int RestMinutes, string? Mark);

/// <summary>A migrated การลา row (LEAVE / LEAVE_ADD), already checked by the migration tool.</summary>
public sealed record LeaveImport(int EmployeeId, DateOnly RecordedOn, DateOnly FromDate, DateOnly ToDate, TimeOnly FromTime,
    TimeOnly ToTime, double Days, string Type, bool? Paid, string? Remark, double DailyWage, double Pay);

/// <summary>เวลาทำงานของหนึ่งเดือน: rows, and ชั่วโมงทำงาน (BR-001, not rounded) of the rows that have both times.</summary>
public sealed record WorkTimeMonth(int Year, int Month, int Rows, double Hours);

/// <summary>การลาของหนึ่งเดือนตาม FROM_D: rows, days and the days ได้ค่าแรง.</summary>
public sealed record LeaveMonth(int Year, int Month, int Rows, double Days, double PaidDays);

/// <summary>
/// ย้ายข้อมูลประวัติ (#51) into เวลาทำงาน and การลา. For the data-migration tool only, never registered in the web app:
/// the legacy rows are taken as they were (no BR-020 quota check, a blank TYPE allowed), in batches, with no row-by-row
/// history (#41). Also counts the rows back for the tool's รายงานตรวจยอด.
/// </summary>
public sealed class HrHistoryImport(HrDbContext db, AuditScope audit)
{
    private const int BatchSize = 2000;

    public Task AddWorkTimesAsync(IEnumerable<WorkTimeImport> rows) => AddAsync(rows, r => new WorkTime
    {
        Date = r.Date, EmployeeId = r.EmployeeId, Type = r.Type, WorkIn = r.WorkIn, WorkOut = r.WorkOut, Late = r.Late,
        RestMinutes = r.RestMinutes, Mark = r.Mark,
    });

    public Task AddLeavesAsync(IEnumerable<LeaveImport> rows) => AddAsync(rows, r => new Leave
    {
        EmployeeId = r.EmployeeId, RecordedOn = r.RecordedOn, FromDate = r.FromDate, ToDate = r.ToDate, FromTime = r.FromTime,
        ToTime = r.ToTime, Days = r.Days, Type = r.Type, Paid = r.Paid, Remark = r.Remark, DailyWage = r.DailyWage, Pay = r.Pay,
    });

    public Task<int> CountWorkTimesAsync() => db.WorkTimes.CountAsync();

    public Task<int> CountLeavesAsync() => db.Leaves.CountAsync();

    /// <summary>เวลาทำงาน by month of DATE, from <paramref name="from"/> to <paramref name="to"/> (both included).</summary>
    public async Task<IReadOnlyList<WorkTimeMonth>> GetWorkTimeMonthsAsync(DateOnly from, DateOnly to)
    {
        var rows = await db.WorkTimes.AsNoTracking().Where(w => w.Date >= from && w.Date <= to)
            .Select(w => new { w.Date, w.WorkIn, w.WorkOut, w.RestMinutes })
            .ToListAsync();
        return rows.GroupBy(r => (r.Date.Year, r.Date.Month)).OrderBy(g => g.Key)
            .Select(g => new WorkTimeMonth(g.Key.Year, g.Key.Month, g.Count(),
                g.Where(r => r.WorkIn is not null && r.WorkOut is not null)
                    .Sum(r => WorkDuration.Hours(r.WorkIn!.Value, r.WorkOut!.Value, r.RestMinutes))))
            .ToList();
    }

    /// <summary>การลา by month of FROM_D, from <paramref name="from"/> to <paramref name="to"/> (both included).</summary>
    public async Task<IReadOnlyList<LeaveMonth>> GetLeaveMonthsAsync(DateOnly from, DateOnly to)
    {
        var rows = await db.Leaves.AsNoTracking().Where(l => l.FromDate >= from && l.FromDate <= to)
            .Select(l => new { l.FromDate, l.Days, l.Paid })
            .ToListAsync();
        return rows.GroupBy(r => (r.FromDate.Year, r.FromDate.Month)).OrderBy(g => g.Key)
            .Select(g => new LeaveMonth(g.Key.Year, g.Key.Month, g.Count(), g.Sum(r => r.Days), g.Where(r => r.Paid == true).Sum(r => r.Days)))
            .ToList();
    }

    private async Task AddAsync<TImport, TRow>(IEnumerable<TImport> rows, Func<TImport, TRow> create) where TRow : class
    {
        using (audit.SuppressRowChanges())
        {
            foreach (var batch in rows.Chunk(BatchSize))
            {
                db.Set<TRow>().AddRange(batch.Select(create));
                await db.SaveChangesAsync();
                db.ChangeTracker.Clear();
            }
        }
    }
}
