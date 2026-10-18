using Microsoft.EntityFrameworkCore;

namespace Lockte.SharedKernel.Persistence;

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
        modelBuilder.ApplyLockteConventions(OwnsHistoryTable);
    }

    /// <summary>True only for the audit context, which creates <c>audit.History</c> in its migrations.</summary>
    protected virtual bool OwnsHistoryTable => false;

    /// <summary>The module's own entity mappings.</summary>
    protected virtual void ConfigureModel(ModelBuilder modelBuilder) { }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
        => DbContextConcurrencyExtensions.SaveWithConflictCheck(() => base.SaveChanges(acceptAllChangesOnSuccess));

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        => DbContextConcurrencyExtensions.SaveWithConflictCheckAsync(() => base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken));
}
