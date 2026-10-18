using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Lockte.Web.Accounts;

/// <summary>
/// An open Blazor circuit keeps its user until this says otherwise: every minute it checks the user is still
/// active and the security stamp unchanged (deactivated, or admin set a temporary password).
/// </summary>
internal sealed class RevalidatingAuthStateProvider(
    ILoggerFactory loggerFactory, IServiceScopeFactory scopeFactory, IOptions<IdentityOptions> options)
    : RevalidatingServerAuthenticationStateProvider(loggerFactory)
{
    protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(1);

    protected override async Task<bool> ValidateAuthenticationStateAsync(
        AuthenticationState authenticationState, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = await users.GetUserAsync(authenticationState.User);
        if (user is null || !user.IsActive) return false;
        var stamp = authenticationState.User.FindFirstValue(options.Value.ClaimsIdentity.SecurityStampClaimType);
        return stamp == await users.GetSecurityStampAsync(user);
    }
}
