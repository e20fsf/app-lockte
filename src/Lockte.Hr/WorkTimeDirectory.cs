using Lockte.Hr.Data;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Hr;

/// <summary>The read side of เวลาทำงาน. Writes go through <see cref="WorkTimeRegistry"/>.</summary>
public sealed class WorkTimeDirectory(HrDbContext db)
{
    /// <summary>Every row of one day, by employee code then เวลาเข้า.</summary>
    public async Task<IReadOnlyList<WorkTimeDto>> ListAsync(DateOnly date)
        => await ToDtos(db.WorkTimes.AsNoTracking().Where(w => w.Date == date)
            .OrderBy(w => w.Employee.Code).ThenBy(w => w.WorkIn).ThenBy(w => w.Id))
            .ToListAsync();

    /// <summary>
    /// One employee's rows from <paramref name="from"/> to <paramref name="to"/> (both included), by date then เวลาเข้า —
    /// the subform E_WORK1 of SCR-13, which showed the open period (one table for every year, #41).
    /// </summary>
    public async Task<IReadOnlyList<WorkTimeDto>> ListOfEmployeeAsync(int employeeId, DateOnly from, DateOnly to)
        => await ToDtos(db.WorkTimes.AsNoTracking().Where(w => w.EmployeeId == employeeId && w.Date >= from && w.Date <= to)
            .OrderBy(w => w.Date).ThenBy(w => w.WorkIn).ThenBy(w => w.Id))
            .ToListAsync();

    private static IQueryable<WorkTimeDto> ToDtos(IQueryable<WorkTime> rows)
        => rows.Select(w => new WorkTimeDto(w.Id, w.Date, w.EmployeeId, w.Employee.Code, w.Employee.Name, w.Type,
            w.WorkIn, w.WorkOut, w.Late, w.RestMinutes, w.Mark, w.RowVersion));
}
