using System.Security.Claims;
using Lockte.SharedKernel.Persistence;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;

namespace Lockte.Web.Accounts;

/// <summary>
/// The signed-in user for CreatedBy/UpdatedBy. Inside a Blazor circuit there is no HttpContext, so
/// <see cref="CurrentUserCircuitHandler"/> hands over the circuit's user.
/// </summary>
public sealed class CurrentUser(IHttpContextAccessor http) : ICurrentUser
{
    public const string Anonymous = "anonymous";

    internal ClaimsPrincipal? CircuitUser { get; set; }

    /// <summary>Set by work that runs outside any user, e.g. the reset-admin command.</summary>
    internal string? OverrideUserName { get; set; }

    public string UserName
        => OverrideUserName ?? ((CircuitUser ?? http.HttpContext?.User)?.Identity?.Name is { Length: > 0 } name ? name : Anonymous);
}

internal sealed class CurrentUserCircuitHandler(AuthenticationStateProvider authState, CurrentUser currentUser)
    : CircuitHandler, IDisposable
{
    public override async Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        authState.AuthenticationStateChanged += OnChanged;
        currentUser.CircuitUser = (await authState.GetAuthenticationStateAsync()).User;
    }

    private async void OnChanged(Task<AuthenticationState> state)
    {
        try { currentUser.CircuitUser = (await state).User; }
        catch { currentUser.CircuitUser = null; }
    }

    public void Dispose() => authState.AuthenticationStateChanged -= OnChanged;
}
