using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Lockte.SharedKernel.Persistence;

public static class ReferenceViolation
{
    /// <summary>SQL Server error 547: a FOREIGN KEY / CHECK constraint stopped the statement.</summary>
    private const int ConstraintConflict = 547;

    /// <summary>
    /// Saves a delete of a master-data row (ข้อสมมติ ง: a row other tables refer to cannot be deleted). Returns false —
    /// and forgets the pending delete, which also rolls its history rows back — when a foreign key refers to the row.
    /// Any other failure is thrown as usual.
    /// </summary>
    public static async Task<bool> TrySaveDeleteAsync(this DbContext db)
    {
        try
        {
            await db.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: ConstraintConflict })
        {
            db.ChangeTracker.Clear();
            return false;
        }
    }
}
