using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Lockte.SharedKernel.Persistence;

/// <summary>Fills CreatedBy/At and UpdatedBy/At on every <see cref="IAudited"/> row being saved.</summary>
public sealed class AuditStampInterceptor(ICurrentUser currentUser, TimeProvider clock) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Stamp(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Stamp(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private void Stamp(DbContext? context)
    {
        if (context is null) return;
        var now = clock.GetUtcNow();
        foreach (var entry in context.ChangeTracker.Entries<IAudited>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedBy = entry.Entity.UpdatedBy = currentUser.UserName;
                entry.Entity.CreatedAt = entry.Entity.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedBy = currentUser.UserName;
                entry.Entity.UpdatedAt = now;
                // Created* never changes after insert, even if a caller re-attached the row with stale values.
                entry.Property(e => e.CreatedBy).IsModified = false;
                entry.Property(e => e.CreatedAt).IsModified = false;
            }
        }
    }
}
