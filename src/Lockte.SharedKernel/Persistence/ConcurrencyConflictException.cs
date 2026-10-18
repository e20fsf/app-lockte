using Microsoft.EntityFrameworkCore;

namespace Lockte.SharedKernel.Persistence;

/// <summary>
/// Someone else saved the row after this user loaded it (rowversion mismatch). The message is shown to the user as is.
/// </summary>
public sealed class ConcurrencyConflictException(Exception? inner = null)
    : Exception("รายการนี้ถูกผู้ใช้อื่นบันทึกไปก่อนแล้ว กรุณาโหลดข้อมูลใหม่แล้วแก้อีกครั้ง", inner);

public static class DbContextConcurrencyExtensions
{
    /// <summary>
    /// SaveChanges that reports optimistic-concurrency conflicts as <see cref="ConcurrencyConflictException"/>.
    /// Module DbContexts call this from their SaveChanges overrides.
    /// </summary>
    public static async Task<int> SaveWithConflictCheckAsync(this DbContext context, Func<Task<int>> save)
    {
        try
        {
            return await save();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException(ex);
        }
    }

    public static int SaveWithConflictCheck(this DbContext context, Func<int> save)
    {
        try
        {
            return save();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException(ex);
        }
    }

    /// <summary>
    /// Makes the next save check the row against the rowversion the user loaded, not the one just read from the database.
    /// </summary>
    public static void ExpectRowVersion<T>(this DbContext context, T entity, byte[] loadedRowVersion) where T : class, IAudited
        => context.Entry(entity).Property(e => e.RowVersion).OriginalValue = loadedRowVersion;
}
