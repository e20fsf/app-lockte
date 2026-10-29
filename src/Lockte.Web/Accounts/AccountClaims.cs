using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Lockte.Web.Accounts;

public static class AccountClaims
{
    public const string DisplayName = "lockte:display_name";
    public const string Admin = "lockte:admin";
    public const string MustChangePassword = "lockte:must_change_password";
    public const string ViewPay = "lockte:view_pay";

    public static string DisplayNameOf(ClaimsPrincipal user)
        => user.FindFirstValue(DisplayName) ?? user.Identity?.Name ?? "";

    public static bool IsAdmin(ClaimsPrincipal user) => user.HasClaim(c => c.Type == Admin);

    /// <summary>#277: the user may see ข้อมูลค่าจ้าง. The admin never does.</summary>
    public static bool CanViewPay(ClaimsPrincipal user) => user.HasClaim(c => c.Type == ViewPay) && !IsAdmin(user);

    public static bool MustChange(ClaimsPrincipal user) => user.HasClaim(c => c.Type == MustChangePassword);

    /// <summary>
    /// The one page this user may start from (base-relative): must change password → change-password page,
    /// admin → user management, everyone else → เมนูหลัก.
    /// </summary>
    public static string HomeHref(ClaimsPrincipal user)
        => MustChange(user) ? "Account/ChangePassword" : IsAdmin(user) ? "admin/users" : "";
}

/// <summary>Puts the display name and the admin / must-change-password flags into the login cookie.</summary>
public sealed class AppClaimsPrincipalFactory(UserManager<AppUser> userManager, IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<AppUser>(userManager, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(AppUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        identity.AddClaim(new Claim(AccountClaims.DisplayName, user.DisplayName));
        if (user.IsAdmin) identity.AddClaim(new Claim(AccountClaims.Admin, "1"));
        if (user.CanViewPay && !user.IsAdmin) identity.AddClaim(new Claim(AccountClaims.ViewPay, "1"));
        if (user.MustChangePassword) identity.AddClaim(new Claim(AccountClaims.MustChangePassword, "1"));
        return identity;
    }
}

/// <summary>
/// Identity asks this before every sign-in (SignIn.RequireConfirmedAccount = true):
/// a deactivated user cannot sign in, even with the right password.
/// </summary>
public sealed class ActiveUserConfirmation : IUserConfirmation<AppUser>
{
    public Task<bool> IsConfirmedAsync(UserManager<AppUser> manager, AppUser user) => Task.FromResult(user.IsActive);
}
