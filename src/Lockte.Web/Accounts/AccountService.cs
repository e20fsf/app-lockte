using Lockte.Web.Audit;
using Microsoft.AspNetCore.Identity;

namespace Lockte.Web.Accounts;

/// <summary>What a user does with their own account: log in and change password.</summary>
public sealed class AccountService(UserManager<AppUser> users, SignInManager<AppUser> signIn, HistoryService history)
{
    /// <summary>
    /// Checks the password and counts failures toward lockout (5 wrong → locked 15 minutes).
    /// Does not write the login cookie — the login page does that when <see cref="LoginResult.CanSignIn"/>.
    /// Every attempt goes into the history.
    /// </summary>
    public async Task<LoginResult> CheckLoginAsync(string userName, string password)
    {
        var (result, detail) = await CheckAsync(userName.Trim(), password);
        await history.RecordLoginAsync(userName.Trim(), result.CanSignIn, detail);
        return result;
    }

    private async Task<(LoginResult, string Detail)> CheckAsync(string userName, string password)
    {
        var user = await users.FindByNameAsync(userName);
        if (user is null) return (new(LoginStatus.Invalid), "ไม่พบชื่อ user");

        var result = await signIn.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
        if (result.IsLockedOut)
            return (new(LoginStatus.LockedOut, user.Id, await users.GetLockoutEndDateAsync(user)), "บัญชีถูกล็อก");
        if (result.IsNotAllowed)
            // Say "disabled" only to someone who knows the password.
            return await users.CheckPasswordAsync(user, password)
                ? (new(LoginStatus.Disabled, user.Id), "บัญชีปิดใช้งาน")
                : (new(LoginStatus.Invalid), "รหัสผ่านผิด (บัญชีปิดใช้งาน)");
        if (!result.Succeeded) return (new(LoginStatus.Invalid), "รหัสผ่านผิด");

        return user.MustChangePassword
            ? (new(LoginStatus.MustChangePassword, user.Id), "รหัสชั่วคราว ต้องเปลี่ยนรหัส")
            : (new(LoginStatus.Succeeded, user.Id), "");
    }

    /// <summary>Changes the user's own password; also clears the must-change flag left by a temporary password.</summary>
    public async Task<AccountResult> ChangePasswordAsync(string userId, string currentPassword, string newPassword)
    {
        var user = await users.FindByIdAsync(userId);
        if (user is null || !user.IsActive) return AccountResult.Fail("ไม่พบผู้ใช้");

        var mustChange = user.MustChangePassword;
        user.MustChangePassword = false; // saved together with the new password hash
        var result = await users.ChangePasswordAsync(user, currentPassword, newPassword);
        if (!result.Succeeded) user.MustChangePassword = mustChange;
        return AccountResult.From(result);
    }

    public Task<AppUser?> FindAsync(string userId) => users.FindByIdAsync(userId);
}
