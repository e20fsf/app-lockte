using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Lockte.Web.Accounts;

/// <summary>
/// An open Blazor circuit keeps its user until this says otherwise. Every minute it signs the circuit out when
/// the user was idle longer than Auth:IdleTimeoutMinutes, was deactivated, or got a new security stamp
/// (admin set a temporary password). The page then sends the user to login.
/// </summary>
internal sealed class RevalidatingAuthStateProvider(
    ILoggerFactory loggerFactory,
    IServiceScopeFactory scopeFactory,
    IOptions<IdentityOptions> identityOptions,
    IOptions<AuthOptions> authOptions,
    CircuitActivity activity,
    TimeProvider clock)
    : RevalidatingServerAuthenticationStateProvider(loggerFactory)
{
    protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(1);

    protected override async Task<bool> ValidateAuthenticationStateAsync(
        AuthenticationState authenticationState, CancellationToken cancellationToken)
    {
        if (clock.GetUtcNow() - activity.LastActivity >= TimeSpan.FromMinutes(authOptions.Value.IdleTimeoutMinutes))
            return false;

        await using var scope = scopeFactory.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = await users.GetUserAsync(authenticationState.User);
        if (user is null || !user.IsActive) return false;
        var stamp = authenticationState.User.FindFirstValue(identityOptions.Value.ClaimsIdentity.SecurityStampClaimType);
        return stamp == await users.GetSecurityStampAsync(user);
    }
}
