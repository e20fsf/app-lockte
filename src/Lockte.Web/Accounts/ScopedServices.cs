using Microsoft.AspNetCore.Components.Authorization;

namespace Lockte.Web.Accounts;

/// <summary>
/// For interactive pages: runs each action in a fresh DI scope (its own DbContext — a circuit lives long and must
/// not reuse tracked rows) acting as the circuit's signed-in user, so CreatedBy/UpdatedBy and history name them.
/// </summary>
public sealed class ScopedServices(IServiceScopeFactory scopeFactory, AuthenticationStateProvider authState)
{
    public async Task<TResult> RunAsync<TService, TResult>(Func<TService, Task<TResult>> action) where TService : notnull
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<CurrentUser>().CircuitUser = (await authState.GetAuthenticationStateAsync()).User;
        return await action(scope.ServiceProvider.GetRequiredService<TService>());
    }
}
