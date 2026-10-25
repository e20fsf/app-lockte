using Lockte.Hr.Data;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Hr;

/// <summary>A stand-in for an orphan code of the legacy pay tables (see <see cref="HrStubEmployeeImport"/>).</summary>
public sealed record StubEmployeeImport(string Code, string Name, int? DepartmentId, int? SectionId, int EmployeeType, DateOnly ResignDate, string? Memo);

/// <summary>
/// พนักงานตัวแทน (#51, DQ-6) of a code that has ผลค่าแรง in the legacy but no row in EMPLO. For the data-migration tool only,
/// never registered in the web app: the employee is saved already resigned and marked <c>IsMigrationStub</c>, so no list, search,
/// screen or government register shows it (the legacy never had the row) while the pay results can still point to it.
/// No row-by-row history (#41).
/// </summary>
public sealed class HrStubEmployeeImport(HrDbContext db, AuditScope audit)
{
    /// <summary>Every employee code in the table, the stand-ins too, by id.</summary>
    public async Task<IReadOnlyDictionary<string, int>> GetEmployeeIdsAsync()
        => await db.Employees.AsNoTracking().ToDictionaryAsync(e => e.Code, e => e.Id, StringComparer.OrdinalIgnoreCase);

    public async Task AddAsync(IEnumerable<StubEmployeeImport> stubs)
    {
        using (audit.SuppressRowChanges())
        {
            db.Employees.AddRange(stubs.Select(s => new Employee
            {
                Code = s.Code, Name = s.Name, DepartmentId = s.DepartmentId, SectionId = s.SectionId, EmployeeType = s.EmployeeType,
                ResignDate = s.ResignDate, Memo = s.Memo, Nationality = EmployeeInput.DefaultNationality, IsMigrationStub = true,
            }));
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
        }
    }

    public Task<int> CountAsync() => db.Employees.CountAsync(e => e.IsMigrationStub);
}
