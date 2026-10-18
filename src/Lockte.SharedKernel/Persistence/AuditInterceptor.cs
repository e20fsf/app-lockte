using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Lockte.SharedKernel.Persistence;

/// <summary>
/// The one place that audits saves (#39): on every <see cref="IAudited"/> row it fills CreatedBy/At and
/// UpdatedBy/At, and adds one <see cref="HistoryRecord"/> per column added / changed / deleted, in the same save.
/// Also fills who/when on history rows added by others (events, logins).
/// </summary>
public sealed class AuditInterceptor(ICurrentUser currentUser, TimeProvider clock, AuditScope scope) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Audit(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Audit(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private void Audit(DbContext? context)
    {
        if (context is null) return;
        // Entities changed by plain property sets are still "Unchanged" until changes are detected.
        context.ChangeTracker.DetectChanges();

        var now = clock.GetUtcNow();
        var userName = currentUser.UserName;
        var changeSet = Guid.NewGuid();
        var history = new List<HistoryRecord>();

        foreach (var entry in context.ChangeTracker.Entries().ToList())
        {
            if (entry is { Entity: HistoryRecord added, State: EntityState.Added })
            {
                if (added.UserName.Length == 0) added.UserName = userName;
                if (added.At == default) added.At = now;
                continue;
            }
            if (entry.Entity is not IAudited audited) continue;

            var changes = PeopleEditedColumns(entry).ToList();
            switch (entry.State)
            {
                case EntityState.Added:
                    audited.CreatedBy = audited.UpdatedBy = userName;
                    audited.CreatedAt = audited.UpdatedAt = now;
                    break;
                case EntityState.Modified when changes.Count > 0:
                    audited.UpdatedBy = userName;
                    audited.UpdatedAt = now;
                    // Created* never changes after insert, even if a caller re-attached the row with stale values.
                    entry.Property(nameof(IAudited.CreatedBy)).IsModified = false;
                    entry.Property(nameof(IAudited.CreatedAt)).IsModified = false;
                    break;
                case EntityState.Deleted:
                    break;
                default:
                    continue;
            }

            if (scope.RowChangesSuppressed) continue;
            var action = entry.State switch
            {
                EntityState.Added => HistoryActions.Added,
                EntityState.Deleted => HistoryActions.Deleted,
                _ => HistoryActions.Modified,
            };
            var table = entry.Metadata.AuditName();
            var rowKey = RowKey(entry);
            foreach (var p in changes)
            {
                var secret = p.Metadata.IsSecret();
                history.Add(new HistoryRecord
                {
                    At = now,
                    UserName = userName,
                    Kind = HistoryKind.Change,
                    Table = table,
                    RowKey = rowKey,
                    Action = action,
                    Column = p.Metadata.AuditName(),
                    OldValue = entry.State == EntityState.Added ? null : secret ? "***" : Format(p.OriginalValue),
                    NewValue = entry.State == EntityState.Deleted ? null : secret ? "***" : Format(p.CurrentValue),
                    ChangeSet = changeSet,
                });
            }
        }

        if (history.Count > 0) context.AddRange(history);
    }

    /// <summary>
    /// Columns a person can edit, with a value worth recording: added rows → every non-empty value,
    /// deleted rows → every value, modified rows → values that really changed (some callers mark every column
    /// modified). Never the key, the audit stamps, rowversion/concurrency tokens or bookkeeping columns.
    /// </summary>
    private static IEnumerable<PropertyEntry> PeopleEditedColumns(EntityEntry entry)
        => entry.Properties.Where(p =>
            p.Metadata.Name is not (nameof(IAudited.CreatedBy) or nameof(IAudited.CreatedAt)
                or nameof(IAudited.UpdatedBy) or nameof(IAudited.UpdatedAt))
            && !p.Metadata.IsPrimaryKey() // shown as the row key
            && !p.Metadata.IsConcurrencyToken
            && !p.Metadata.IsBookkeeping()
            && entry.State switch
            {
                EntityState.Added => p.CurrentValue is not null && !p.IsTemporary,
                EntityState.Deleted => true,
                _ => p.IsModified && !Equals(p.OriginalValue, p.CurrentValue),
            });

    private static string RowKey(EntityEntry entry)
    {
        if (entry.Entity is IHasBusinessKey keyed) return keyed.BusinessKey;
        var key = entry.Metadata.FindPrimaryKey();
        if (key is null) return "";
        var parts = key.Properties.Select(p => entry.Property(p.Name)).ToList();
        return parts.Any(p => p.IsTemporary) ? "(ใหม่)" : string.Join(", ", parts.Select(p => Format(p.CurrentValue)));
    }

    /// <summary>Culture-independent text, so history reads the same whoever saved it.</summary>
    private static string? Format(object? value) => value switch
    {
        null => null,
        DateTimeOffset d => d.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture),
        DateTime d => d.TimeOfDay == TimeSpan.Zero
            ? d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : d.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        bool b => b ? "ใช่" : "ไม่ใช่",
        byte[] => "(binary)",
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString(),
    };
}
