using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lockte.SharedKernel.Persistence;

/// <summary>Fills CreatedBy/At and UpdatedBy/At on every <see cref="IAudited"/> row being saved.</summary>
public sealed class AuditStampInterceptor(ICurrentUser currentUser, TimeProvider clock) : SaveChangesInterceptor
{
    public const string BookkeepingAnnotation = "Lockte:Bookkeeping";

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
            else if (entry.State == EntityState.Modified && ChangesWhatPeopleEdit(entry))
            {
                entry.Entity.UpdatedBy = currentUser.UserName;
                entry.Entity.UpdatedAt = now;
                // Created* never changes after insert, even if a caller re-attached the row with stale values.
                entry.Property(e => e.CreatedBy).IsModified = false;
                entry.Property(e => e.CreatedAt).IsModified = false;
            }
        }
    }

    /// <summary>
    /// Compares values, not IsModified (some callers mark every column modified): a save that only touches
    /// bookkeeping columns, e.g. a failed-login counter, is not "last edited by".
    /// </summary>
    private static bool ChangesWhatPeopleEdit(EntityEntry<IAudited> entry)
        => entry.Properties.Any(p =>
            p.IsModified
            && p.Metadata.FindAnnotation(BookkeepingAnnotation) is null
            && !p.Metadata.IsConcurrencyToken
            && !Equals(p.OriginalValue, p.CurrentValue));
}

public static class BookkeepingPropertyExtensions
{
    /// <summary>A column the system updates on its own (login counters and the like) — does not count as an edit.</summary>
    public static PropertyBuilder<T> IsBookkeeping<T>(this PropertyBuilder<T> property)
        => property.HasAnnotation(AuditStampInterceptor.BookkeepingAnnotation, true);
}
