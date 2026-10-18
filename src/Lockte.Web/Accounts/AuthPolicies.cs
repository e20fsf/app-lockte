using Microsoft.AspNetCore.Authorization;

namespace Lockte.Web.Accounts;

/// <summary>
/// No roles (#39). Three gates only:
/// <see cref="Staff"/> (default for every page) — signed in, not admin, password not temporary;
/// <see cref="Admin"/> — the admin account, which sees only user management;
/// <see cref="SignedIn"/> — any signed-in user (change password, log out).
/// </summary>
public static class AuthPolicies
{
    public const string Staff = "staff";
    public const string Admin = "admin";
    public const string SignedIn = "signed-in";

    public static void Configure(AuthorizationOptions options)
    {
        options.AddPolicy(SignedIn, p => p.RequireAuthenticatedUser());
        options.AddPolicy(Staff, p => p.RequireAuthenticatedUser().RequireAssertion(c =>
            !AccountClaims.MustChange(c.User) && !AccountClaims.IsAdmin(c.User)));
        options.AddPolicy(Admin, p => p.RequireAuthenticatedUser().RequireAssertion(c =>
            !AccountClaims.MustChange(c.User) && AccountClaims.IsAdmin(c.User)));
        options.DefaultPolicy = options.GetPolicy(Staff)!;
    }
}
