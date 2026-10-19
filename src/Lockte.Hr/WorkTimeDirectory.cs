using Lockte.Hr.Data;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Hr;

/// <summary>The read side of เวลาทำงาน. Writes go through <see cref="WorkTimeRegistry"/>.</summary>
public sealed class WorkTimeDirectory(HrDbContext db)
{
    /// <summary>Every row of one day, by employee code then เวลาเข้า.</summary>
    public async Task<IReadOnlyList<WorkTimeDto>> ListAsync(DateOnly date)
        => await db.WorkTimes.AsNoTracking().Where(w => w.Date == date)
            .OrderBy(w => w.Employee.Code).ThenBy(w => w.WorkIn).ThenBy(w => w.Id)
            .Select(w => new WorkTimeDto(w.Id, w.Date, w.EmployeeId, w.Employee.Code, w.Employee.Name, w.Type,
                w.WorkIn, w.WorkOut, w.Late, w.RestMinutes, w.Mark, w.RowVersion))
            .ToListAsync();
}
