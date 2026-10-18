using Microsoft.AspNetCore.Components.Server.Circuits;

namespace Lockte.Web.Accounts;

/// <summary>When the user last did something in this circuit (click, typing, navigation…).</summary>
public sealed class CircuitActivity(TimeProvider clock)
{
    public DateTimeOffset LastActivity { get; private set; } = clock.GetUtcNow();

    public void Touch() => LastActivity = clock.GetUtcNow();
}

/// <summary>
/// Records every message from the browser as activity. The server-side half of the idle timeout:
/// <see cref="RevalidatingAuthStateProvider"/> signs the circuit out when there was none for the idle time,
/// even if idle.js in the browser did not run.
/// </summary>
internal sealed class CircuitActivityHandler(CircuitActivity activity) : CircuitHandler
{
    public override Func<CircuitInboundActivityContext, Task> CreateInboundActivityHandler(
        Func<CircuitInboundActivityContext, Task> next)
        => context =>
        {
            activity.Touch();
            return next(context);
        };
}
