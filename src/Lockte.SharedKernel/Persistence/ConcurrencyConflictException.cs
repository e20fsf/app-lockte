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
    /// Makes the next save check the row against the rowversion the user loaded, not the one just read from the database.
    /// </summary>
    public static void ExpectRowVersion<T>(this DbContext context, T entity, byte[] loadedRowVersion) where T : class, IAudited
        => context.Entry(entity).Property(e => e.RowVersion).OriginalValue = loadedRowVersion;
}

/// <summary>
/// Base of every module's DbContext (ADR 0002): its own schema, the <see cref="IAudited"/> conventions,
/// and rowversion conflicts as <see cref="ConcurrencyConflictException"/>.
/// </summary>
public abstract class ModuleDbContext(DbContextOptions options, string schema) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(schema);
        ConfigureModel(modelBuilder);
        modelBuilder.ApplyLockteConventions();
    }

    /// <summary>The module's own entity mappings.</summary>
    protected virtual void ConfigureModel(ModelBuilder modelBuilder) { }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
        => DbContextConcurrencyExtensions.SaveWithConflictCheck(() => base.SaveChanges(acceptAllChangesOnSuccess));

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        => DbContextConcurrencyExtensions.SaveWithConflictCheckAsync(() => base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken));
}
