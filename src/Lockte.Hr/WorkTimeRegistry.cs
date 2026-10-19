using System.Globalization;
using Lockte.Hr.Data;
using Lockte.Org;
using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Hr;

/// <summary>
/// สร้างเวลาปกติให้ทุกคน (SCR-11, BR-010). A batch job: one history event, no row-by-row history (#41).
/// Row-by-row entry and edits of เวลาทำงาน (SCR-12/13) come with their own tickets.
/// </summary>
public sealed class WorkTimeRegistry(HrDbContext db, OrgDirectory org, AuditScope audit)
{
    public const string WorkTimeTable = "เวลาทำงาน";
    public const string GenerateAction = "สร้างเวลาปกติ";

    /// <summary>
    /// มาโคร แมโคร2 for one day, as the legacy (#41, ⚠ kept): every employee with RESIGN blank and a shift gets a row
    /// TYPE W with the shift's W_IN, W_OUT and REST_T; rows of the day with TYPE blank become W; then every row of the
    /// day of an employee recorded ไม่มาทำงาน that day is deleted (also rows made before the absence was recorded).
    /// The legacy deletes such rows on every date of the open period, not only this day: kept to this day until the pay
    /// period exists (Q-51). No E_WORK_A backup copy (one table, #41). ⚠ It does not check whether the day was already made (a second press adds the rows again, BR-007 removes the
    /// repeats), nor the start date, nor วันอาทิตย์/วันหยุด (Q-24).
    /// </summary>
    public async Task<ServiceResult> GenerateNormalTimeAsync(DateOnly date)
    {
        var shifts = (await org.GetShiftsAsync()).ToDictionary(s => s.Id);
        var absent = (await db.Absences.Where(a => a.Date == date).Select(a => a.EmployeeId).ToListAsync()).ToHashSet();
        var working = await db.Employees.Where(e => e.ResignDate == null && e.ShiftId != null)
            .Select(e => new { e.Id, ShiftId = e.ShiftId!.Value })
            .ToListAsync();
        var added = working
            .Where(e => shifts.ContainsKey(e.ShiftId) && !absent.Contains(e.Id))
            .Select(e =>
            {
                var shift = shifts[e.ShiftId];
                return new WorkTime
                {
                    Date = date, EmployeeId = e.Id, Type = WorkTypes.Normal,
                    WorkIn = shift.WorkIn, WorkOut = shift.WorkOut, RestMinutes = shift.RestMinutes,
                };
            })
            .ToList();

        using (audit.SuppressRowChanges())
        {
            var untyped = await db.WorkTimes.Where(w => w.Date == date && w.Type == null).ToListAsync();
            foreach (var row in untyped) row.Type = WorkTypes.Normal;
            var removed = await db.WorkTimes.Where(w => w.Date == date && absent.Contains(w.EmployeeId)).ToListAsync();
            db.WorkTimes.RemoveRange(removed);
            db.WorkTimes.AddRange(added);
            db.AddHistoryEvent(GenerateAction,
                new { วันที่ = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), ไม่มาทำงาน = absent.Count, ลบเวลาคนไม่มา = removed.Count },
                added.Count, WorkTimeTable);
            await db.SaveChangesAsync();
        }
        return ServiceResult.Ok(added.Count);
    }
}
