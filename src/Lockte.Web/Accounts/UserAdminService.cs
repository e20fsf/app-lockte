using Lockte.SharedKernel.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Web.Accounts;

/// <summary>
/// หน้าจัดการ user ของบัญชี admin (#39): เพิ่ม, ปิดใช้งาน (ไม่ลบ), ตั้งรหัสชั่วคราว — plus the server-side admin reset.
/// Changes that came from a list the admin loaded carry its rowversion; a newer save by someone else
/// throws <see cref="ConcurrencyConflictException"/>.
/// </summary>
public sealed class UserAdminService(UserManager<AppUser> users, AccountsDbContext db, TimeProvider clock)
{
    public const string AdminDisplayName = "ผู้ดูแลระบบ";

    public async Task<IReadOnlyList<UserSummary>> ListAsync()
    {
        var now = clock.GetUtcNow();
        var rows = await db.Users.AsNoTracking().OrderByDescending(u => u.IsAdmin).ThenBy(u => u.UserName).ToListAsync();
        return rows.Select(u => new UserSummary(
            u.Id, u.UserName!, u.DisplayName, u.IsAdmin, u.IsActive, u.MustChangePassword,
            u.LockoutEnd > now ? u.LockoutEnd : null,
            u.CreatedBy, u.CreatedAt, u.UpdatedBy, u.UpdatedAt, u.RowVersion)).ToList();
    }

    /// <summary>New user starts with a temporary password and must change it at first login.</summary>
    public async Task<AccountResult> CreateAsync(string userName, string displayName, string temporaryPassword)
    {
        userName = userName.Trim();
        displayName = displayName.Trim();
        if (userName.Length == 0) return AccountResult.Fail("กรุณากรอกชื่อ user");
        if (displayName.Length == 0) return AccountResult.Fail("กรุณากรอกชื่อที่แสดง");
        if (string.Equals(userName, AppUser.AdminUserName, StringComparison.OrdinalIgnoreCase))
            return AccountResult.Fail($"ชื่อ user '{AppUser.AdminUserName}' สงวนไว้สำหรับผู้ดูแลระบบ");

        var user = new AppUser { UserName = userName, DisplayName = displayName, MustChangePassword = true };
        return AccountResult.From(await users.CreateAsync(user, temporaryPassword));
    }

    /// <summary>ปิดใช้งาน / เปิดใช้งาน. A deactivated user can no longer log in and open sessions end at the next revalidation.</summary>
    public async Task<AccountResult> SetActiveAsync(string userId, bool active, byte[] loadedRowVersion)
    {
        var user = await users.FindByIdAsync(userId);
        if (user is null) return AccountResult.Fail("ไม่พบผู้ใช้");
        if (user.IsAdmin && !active) return AccountResult.Fail("ปิดใช้งานบัญชี admin ไม่ได้");

        db.ExpectRowVersion(user, loadedRowVersion);
        user.IsActive = active;
        return AccountResult.From(await users.UpdateSecurityStampAsync(user));
    }

    /// <summary>Sets a temporary password for a user who forgot theirs; also lifts a lockout.</summary>
    public async Task<AccountResult> SetTemporaryPasswordAsync(string userId, string temporaryPassword, byte[] loadedRowVersion)
    {
        var user = await users.FindByIdAsync(userId);
        if (user is null) return AccountResult.Fail("ไม่พบผู้ใช้");

        db.ExpectRowVersion(user, loadedRowVersion);
        return await ApplyTemporaryPasswordAsync(user, temporaryPassword);
    }

    /// <summary>
    /// The server command <c>Lockte.Web reset-admin</c>: creates the admin account if missing, otherwise
    /// re-enables and unlocks it. Either way the password is temporary.
    /// </summary>
    public async Task<AccountResult> ResetAdminAsync(string password)
    {
        var admin = await users.FindByNameAsync(AppUser.AdminUserName);
        if (admin is null)
        {
            admin = new AppUser
            {
                UserName = AppUser.AdminUserName, DisplayName = AdminDisplayName, IsAdmin = true, MustChangePassword = true,
            };
            return AccountResult.From(await users.CreateAsync(admin, password));
        }

        admin.IsAdmin = true;
        admin.IsActive = true;
        return await ApplyTemporaryPasswordAsync(admin, password);
    }

    private async Task<AccountResult> ApplyTemporaryPasswordAsync(AppUser user, string password)
    {
        var errors = new List<string>();
        foreach (var validator in users.PasswordValidators)
        {
            var check = await validator.ValidateAsync(users, user, password);
            errors.AddRange(check.Errors.Select(e => e.Description));
        }
        if (errors.Count > 0) return new AccountResult(false, errors);

        user.PasswordHash = users.PasswordHasher.HashPassword(user, password);
        user.MustChangePassword = true;
        user.LockoutEnd = null;
        user.AccessFailedCount = 0;
        // New stamp also ends the user's open sessions; saved in one update with the fields above.
        return AccountResult.From(await users.UpdateSecurityStampAsync(user));
    }
}
