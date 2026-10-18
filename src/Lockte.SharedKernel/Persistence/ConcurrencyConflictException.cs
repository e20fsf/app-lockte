using Microsoft.EntityFrameworkCore;

namespace Lockte.SharedKernel.Persistence;

/// <summary>
/// Someone else saved the row after this user loaded it (rowversion mismatch). The message is shown to the user as is.
/// </summary>
public sealed class ConcurrencyConflictException(Exception? inner = null) : Exception(UserMessage, inner)
{
    public const string UserMessage = "รายการนี้ถูกผู้ใช้อื่นบันทึกไปก่อนแล้ว กรุณาโหลดข้อมูลใหม่แล้วแก้อีกครั้ง";
}

public static class DbContextConcurrencyExtensions
{
    /// <summary>
    /// Runs a SaveChanges, reporting optimistic-concurrency conflicts as <see cref="ConcurrencyConflictException"/>.
    /// For DbContexts that cannot derive from <see cref="ModuleDbContext"/> (Identity).
    /// </summary>
    public static async Task<int> SaveWithConflictCheckAsync(Func<Task<int>> save)
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

    public static int SaveWithConflictCheck(Func<int> save)
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
    /// Throws a conflict if the row just read from the database is no longer the one the screen loaded.
    /// A save made between this read and SaveChanges is still caught by the rowversion check of the UPDATE.
    /// Compared here rather than by overwriting the tracked original value: Identity's UserStore calls
    /// <c>DbContext.Update</c>, which resets original values to the current ones.
    /// </summary>
    public static void ExpectRowVersion(this IAudited entity, byte[] loadedRowVersion)
    {
        if (!entity.RowVersion.AsSpan().SequenceEqual(loadedRowVersion)) throw new ConcurrencyConflictException();
    }
}
