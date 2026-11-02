using Lockte.Web.Accounts;

namespace Lockte.Web.Tests;

/// <summary>A clock the test moves by hand.</summary>
internal sealed class MovableClock(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset now = start;

    public void Advance(TimeSpan by) => now += by;

    public override DateTimeOffset GetUtcNow() => now;
}

/// <summary>#343 ล็อกการแก้ไข without a database: what the keys and times are.</summary>
public sealed class EditLockRulesTests
{
    [Fact] // one key per record, the same on every screen
    public void The_keys_name_the_record()
    {
        Assert.Equal("order:7", EditLockKeys.Order(7));
        Assert.Equal("delivery:7", EditLockKeys.Delivery(7));
        Assert.Equal("production-report:7", EditLockKeys.ProductionReport(7));
        Assert.InRange(EditLockKeys.ProductionReport(int.MaxValue).Length, 1, EditLock.KeyLength);
    }

    [Fact] // the heartbeat is well inside the life of a lock (a missed beat or two does not free it); the life is the proposed ~2 minutes
    public void The_heartbeat_is_well_inside_the_lifetime()
    {
        Assert.Equal(TimeSpan.FromMinutes(2), EditLockService.Lifetime);
        Assert.True(EditLockService.HeartbeatEvery * 3 < EditLockService.Lifetime);
    }
}

/// <summary>#343 ล็อกการแก้ไข: two users, one record — the second is told who has it; it lapses, is freed on leaving, and an admin can free it.</summary>
[Collection(LocalDbCollection.Name)]
public sealed class EditLockTests(LocalDbFixture db)
{
    private static readonly DateTimeOffset Start = new(2026, 11, 3, 9, 0, 0, TimeSpan.Zero);

    private static string NewKey() => EditLockKeys.Order(Random.Shared.Next(1_000_000, int.MaxValue));

    /// <summary>The service on its own context and the given clock (each call is its own scope, as each circuit action is).</summary>
    private async Task<T> UseAsync<T>(MovableClock clock, Func<EditLockService, Task<T>> action)
    {
        await using var scope = db.Scope();
        return await action(new EditLockService(scope.Get<AccountsDbContext>(), clock));
    }

    [Fact]
    public async Task The_second_user_is_refused_and_told_who_has_the_record()
    {
        var (key, clock) = (NewKey(), new MovableClock(Start));

        var first = await UseAsync(clock, s => s.AcquireAsync(key, "u-a", "ผู้ใช้ เอ"));
        var second = await UseAsync(clock, s => s.AcquireAsync(key, "u-b", "ผู้ใช้ บี"));

        Assert.True(first.Granted);
        Assert.False(second.Granted);
        Assert.Equal(("u-a", "ผู้ใช้ เอ", Start), (second.Holder!.UserName, second.Holder.DisplayName, second.Holder.Since));
        await UseAsync(clock, s => s.ReleaseAsync(key, "u-a").ContinueWith(_ => true));
    }

    [Fact] // the same user in another tab is not kept out, and the start of the lock stays
    public async Task The_same_user_gets_it_again_without_a_new_start()
    {
        var (key, clock) = (NewKey(), new MovableClock(Start));
        await UseAsync(clock, s => s.AcquireAsync(key, "u-a", "ผู้ใช้ เอ"));

        clock.Advance(TimeSpan.FromSeconds(45));
        var again = await UseAsync(clock, s => s.AcquireAsync(key, "u-a", "ผู้ใช้ เอ"));
        var holder = await UseAsync(clock, s => s.HolderAsync(key));

        Assert.True(again.Granted);
        Assert.Equal(Start, holder!.Since);
        await UseAsync(clock, s => s.ReleaseAsync(key, "u-a").ContinueWith(_ => true));
    }

    [Fact] // heartbeats keep it; without them it lapses after 2 minutes and the next user takes it
    public async Task A_lock_without_heartbeats_lapses()
    {
        var (key, clock) = (NewKey(), new MovableClock(Start));
        await UseAsync(clock, s => s.AcquireAsync(key, "u-a", "ผู้ใช้ เอ"));

        clock.Advance(TimeSpan.FromSeconds(90));
        Assert.True(await UseAsync(clock, s => s.HeartbeatAsync(key, "u-a")));
        clock.Advance(TimeSpan.FromSeconds(90)); // 3 minutes in, 90 s since the beat: still held
        Assert.False((await UseAsync(clock, s => s.AcquireAsync(key, "u-b", "ผู้ใช้ บี"))).Granted);

        clock.Advance(TimeSpan.FromSeconds(31)); // 121 s without a beat
        var taken = await UseAsync(clock, s => s.AcquireAsync(key, "u-b", "ผู้ใช้ บี"));

        Assert.True(taken.Granted);
        Assert.False(await UseAsync(clock, s => s.HeartbeatAsync(key, "u-a"))); // a is told it lost it
        await UseAsync(clock, s => s.ReleaseAsync(key, "u-b").ContinueWith(_ => true));
    }

    [Fact] // leaving frees it — only for its owner
    public async Task Release_frees_the_record_for_its_owner_only()
    {
        var (key, clock) = (NewKey(), new MovableClock(Start));
        await UseAsync(clock, s => s.AcquireAsync(key, "u-a", "ผู้ใช้ เอ"));

        await UseAsync(clock, s => s.ReleaseAsync(key, "u-b").ContinueWith(_ => true));
        Assert.False((await UseAsync(clock, s => s.AcquireAsync(key, "u-b", "ผู้ใช้ บี"))).Granted); // b's release did nothing

        await UseAsync(clock, s => s.ReleaseAsync(key, "u-a").ContinueWith(_ => true));
        Assert.True((await UseAsync(clock, s => s.AcquireAsync(key, "u-b", "ผู้ใช้ บี"))).Granted);
        await UseAsync(clock, s => s.ReleaseAsync(key, "u-b").ContinueWith(_ => true));
    }

    [Fact] // an admin frees it whoever holds it; the holder finds out at the next beat
    public async Task An_admin_can_free_the_record()
    {
        var (key, clock) = (NewKey(), new MovableClock(Start));
        await UseAsync(clock, s => s.AcquireAsync(key, "u-a", "ผู้ใช้ เอ"));

        Assert.True(await UseAsync(clock, s => s.ForceReleaseAsync(key)));
        Assert.False(await UseAsync(clock, s => s.ForceReleaseAsync(key))); // nothing left to free
        Assert.False(await UseAsync(clock, s => s.HeartbeatAsync(key, "u-a")));
        Assert.True((await UseAsync(clock, s => s.AcquireAsync(key, "u-b", "ผู้ใช้ บี"))).Granted);
        await UseAsync(clock, s => s.ReleaseAsync(key, "u-b").ContinueWith(_ => true));
    }

    [Fact] // many asking at once: exactly one gets it
    public async Task Only_one_of_many_simultaneous_askers_gets_the_record()
    {
        var (key, clock) = (NewKey(), new MovableClock(Start));

        var results = await Task.WhenAll(Enumerable.Range(1, 8).Select(i => UseAsync(clock, s => s.AcquireAsync(key, "u-" + i, "ผู้ใช้ " + i))));

        Assert.Equal(1, results.Count(r => r.Granted));
        Assert.All(results.Where(r => !r.Granted), r => Assert.NotNull(r.Holder));
        await UseAsync(clock, s => s.ForceReleaseAsync(key));
    }
}
