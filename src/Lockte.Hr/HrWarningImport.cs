using Lockte.Hr.Data;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Hr;

/// <summary>A migrated ใบเตือน row (ใบเตือน / ใบเตือนADD), already checked by the migration tool.</summary>
public sealed record WarningImport(DateOnly Date, int EmployeeId, int? DepartmentId, string? Level, string? Remark);

/// <summary>
/// ย้ายข้อมูลใบเตือน (#51, #59) into the one ใบเตือน table. For the data-migration tool only, never registered in the web
/// app: the rows are taken as the legacy kept them (ระดับ blank allowed, DQ-9), with no row-by-row history (#41).
/// </summary>
public sealed class HrWarningImport(HrDbContext db, AuditScope audit)
{
    public async Task AddWarningsAsync(IEnumerable<WarningImport> rows)
    {
        using (audit.SuppressRowChanges())
        {
            db.Warnings.AddRange(rows.Select(r => new Warning
            {
                Date = r.Date, EmployeeId = r.EmployeeId, DepartmentId = r.DepartmentId, Level = r.Level, Remark = r.Remark,
            }));
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
        }
    }

    public Task<int> CountWarningsAsync() => db.Warnings.CountAsync();
}
