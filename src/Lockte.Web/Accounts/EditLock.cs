using Microsoft.EntityFrameworkCore;

namespace Lockte.Web.Accounts;

/// <summary>
/// ล็อกการแก้ไข (#343, UAT-7): who has a record open for editing right now. A temporary owner per record (an ORDER, a delivery line, a
/// production report — <see cref="Key"/>), not per menu; it lapses by itself when its owner's page stops sending a heartbeat
/// (<see cref="EditLockService.Lifetime"/>), so a closed tab or a dropped connection does not keep anyone out. A working state, not business
/// data: not <see cref="Lockte.SharedKernel.Persistence.IAudited"/>, so it writes no history. The central service for any screen to use;
/// installed on the edit pages of ผลผลิตและORDER first (the user, 2026-11-02).
/// </summary>
public sealed class EditLock
{
    public const int KeyLength = 200;

    public int Id { get; set; }
    /// <summary>What is being edited, as <c>kind:id</c> — see <see cref="EditLockKeys"/>.</summary>
    public string Key { get; set; } = "";
    public string UserName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset HeartbeatAt { get; set; }
}

/// <summary>The keys of the things that can be locked, so the same record has the same key on every screen.</summary>
public static class EditLockKeys
{
    public static string Order(int id) => $"order:{id}";
    public static string Delivery(int id) => $"delivery:{id}";
    public static string ProductionReport(int id) => $"production-report:{id}";
}

public sealed record EditLockHolder(string UserName, string DisplayName, DateTimeOffset Since);

/// <summary>Granted, or who holds the record (null when the holder cannot be told, e.g. the lock lapsed a moment ago).</summary>
public sealed record EditLockResult(bool Granted, EditLockHolder? Holder);

public sealed class EditLockService(AccountsDbContext db, TimeProvider clock)
{
    /// <summary>A lock without a heartbeat for this long is free (the user's proposal: about 2 minutes).</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(2);

    /// <summary>How often an open page tells it is still there — well inside <see cref="Lifetime"/>.</summary>
    public static readonly TimeSpan HeartbeatEvery = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Take the lock of <paramref name="key"/> for <paramref name="userName"/>: free, lapsed, or already theirs (another tab of the same
    /// user is not kept out — the record's rowversion still guards the save). Every step is one statement, so two users asking together
    /// cannot both get it.
    /// </summary>
    public async Task<EditLockResult> AcquireAsync(string key, string userName, string displayName)
    {
        var now = clock.GetUtcNow();
        var lapsed = now - Lifetime;
        var taken = await db.EditLocks.Where(l => l.Key == key && (l.UserName == userName || l.HeartbeatAt < lapsed))
            .ExecuteUpdateAsync(s => s
                .SetProperty(l => l.StartedAt, l => l.UserName == userName ? l.StartedAt : now)
                .SetProperty(l => l.UserName, userName)
                .SetProperty(l => l.DisplayName, displayName)
                .SetProperty(l => l.HeartbeatAt, now));
        if (taken > 0) return new(true, null);

        if (!await db.EditLocks.AnyAsync(l => l.Key == key))
        {
            db.EditLocks.Add(new EditLock { Key = key, UserName = userName, DisplayName = displayName, StartedAt = now, HeartbeatAt = now });
            try
            {
                await db.SaveChangesAsync();
                return new(true, null);
            }
            catch (DbUpdateException)
            {
                // Someone inserted the same key a moment ago (unique key): they hold it.
                db.ChangeTracker.Clear();
            }
        }
        return new(false, await HolderAsync(key));
    }

    /// <summary>The page is still open. False when the lock is no longer this user's (an admin freed it, or it lapsed and someone took it).</summary>
    public async Task<bool> HeartbeatAsync(string key, string userName)
    {
        var now = clock.GetUtcNow();
        return await db.EditLocks.Where(l => l.Key == key && l.UserName == userName)
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.HeartbeatAt, now)) > 0;
    }

    /// <summary>The user left or saved: free the record (only their own lock).</summary>
    public async Task ReleaseAsync(string key, string userName)
        => await db.EditLocks.Where(l => l.Key == key && l.UserName == userName).ExecuteDeleteAsync();

    /// <summary>An admin frees a record whoever holds it. True when there was a lock to free.</summary>
    public async Task<bool> ForceReleaseAsync(string key)
        => await db.EditLocks.Where(l => l.Key == key).ExecuteDeleteAsync() > 0;

    public async Task<EditLockHolder?> HolderAsync(string key)
    {
        var lapsed = clock.GetUtcNow() - Lifetime;
        return await db.EditLocks.AsNoTracking().Where(l => l.Key == key && l.HeartbeatAt >= lapsed)
            .Select(l => new EditLockHolder(l.UserName, l.DisplayName, l.StartedAt)).FirstOrDefaultAsync();
    }
}
