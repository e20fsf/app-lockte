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
    public const int DisplayNameLength = 100;

    public async Task<IReadOnlyList<UserSummary>> ListAsync()
    {
        var now = clock.GetUtcNow();
        var rows = await db.Users.AsNoTracking().OrderByDescending(u => u.IsAdmin).ThenBy(u => u.UserName).ToListAsync();
        return rows.Select(u => new UserSummary(
            u.Id, u.UserName!, u.DisplayName, u.IsAdmin, u.IsActive, u.MustChangePassword, u.CanViewPay,
            u.IsAdmin ? [] : ProgramAccess.Parse(u.Programs),
            u.LockoutEnd > now ? u.LockoutEnd : null,
            u.CreatedBy, u.CreatedAt, u.UpdatedBy, u.UpdatedAt, u.RowVersion)).ToList();
    }

    /// <summary>
    /// New user starts with a temporary password and must change it at first login; it may open only the
    /// <paramref name="programs"/> given (none when omitted, ADR 0005).
    /// </summary>
    public async Task<AccountResult> CreateAsync(string userName, string displayName, string temporaryPassword, bool canViewPay = false,
        IEnumerable<string>? programs = null)
    {
        userName = userName.Trim();
        displayName = displayName.Trim();
        if (userName.Length == 0) return AccountResult.Fail("กรุณากรอกชื่อ user");
        if (displayName.Length == 0) return AccountResult.Fail("กรุณากรอกชื่อที่แสดง");
        if (string.Equals(userName, AppUser.AdminUserName, StringComparison.OrdinalIgnoreCase))
            return AccountResult.Fail($"ชื่อ user '{AppUser.AdminUserName}' สงวนไว้สำหรับผู้ดูแลระบบ");

        var user = new AppUser
        {
            UserName = userName, DisplayName = displayName, MustChangePassword = true, CanViewPay = canViewPay,
            Programs = ProgramAccess.Format(programs),
        };
        return AccountResult.From(await users.CreateAsync(user, temporaryPassword));
    }

    /// <summary>
    /// <c>seed-users</c>: creates each user of <paramref name="sheet"/> that does not exist yet, all with the same temporary
    /// password. A user that exists (by user name) is left exactly as it is, so the command can run again safely.
    /// </summary>
    public async Task<SeedUsersResult> CreateMissingAsync(IEnumerable<InitialUser> sheet, string temporaryPassword)
    {
        var created = new List<string>();
        var skipped = new List<string>();
        var errors = new List<string>();
        foreach (var entry in sheet)
        {
            if (await users.FindByNameAsync(entry.UserName) is not null)
            {
                skipped.Add(entry.UserName);
                continue;
            }
            var result = await CreateAsync(entry.UserName, entry.DisplayName, temporaryPassword, canViewPay: false, entry.Programs);
            if (result.Succeeded) created.Add(entry.UserName);
            else errors.AddRange(result.Errors.Select(e => $"{entry.UserName}: {e}"));
        }
        return new SeedUsersResult(created, skipped, errors);
    }

    /// <summary>ปิดใช้งาน / เปิดใช้งาน. A deactivated user can no longer log in and open sessions end at the next revalidation.</summary>
    public async Task<AccountResult> SetActiveAsync(string userId, bool active, byte[] loadedRowVersion)
    {
        var user = await users.FindByIdAsync(userId);
        if (user is null) return AccountResult.Fail("ไม่พบผู้ใช้");
        if (user.IsAdmin && !active) return AccountResult.Fail("ปิดใช้งานบัญชี admin ไม่ได้");

        user.ExpectRowVersion(loadedRowVersion);
        user.IsActive = active;
        return AccountResult.From(await users.UpdateSecurityStampAsync(user));
    }

    /// <summary>
    /// #277 ดูข้อมูลค่าจ้างได้. The change is in the change history like any other edit; the new security stamp ends the
    /// user's open sessions so the next login carries the new right (the login cookie holds it as a claim).
    /// The admin account has no such right whatever is stored.
    /// </summary>
    /// <summary>
    /// แก้ชื่อที่แสดง (UAT 2026-11-24, e.g. the spare users สำรอง given to a person). In the change history; the user's open
    /// sessions are not ended — the app bar shows the new name from the next login.
    /// </summary>
    public async Task<AccountResult> SetDisplayNameAsync(string userId, string displayName, byte[] loadedRowVersion)
    {
        displayName = displayName.Trim();
        if (displayName.Length == 0) return AccountResult.Fail("กรุณากรอกชื่อที่แสดง");
        if (displayName.Length > DisplayNameLength) return AccountResult.Fail($"ชื่อที่แสดงยาวได้ไม่เกิน {DisplayNameLength} ตัว");
        var user = await users.FindByIdAsync(userId);
        if (user is null) return AccountResult.Fail("ไม่พบผู้ใช้");

        user.ExpectRowVersion(loadedRowVersion);
        if (user.DisplayName == displayName) return AccountResult.Ok;
        user.DisplayName = displayName;
        return AccountResult.From(await users.UpdateAsync(user));
    }

    public async Task<AccountResult> SetCanViewPayAsync(string userId, bool canViewPay, byte[] loadedRowVersion)
    {
        var user = await users.FindByIdAsync(userId);
        if (user is null) return AccountResult.Fail("ไม่พบผู้ใช้");
        if (user.IsAdmin) return AccountResult.Fail("บัญชี admin ดูข้อมูลค่าจ้างไม่ได้");

        user.ExpectRowVersion(loadedRowVersion);
        user.CanViewPay = canViewPay;
        return AccountResult.From(await users.UpdateSecurityStampAsync(user));
    }

    /// <summary>
    /// ADR 0005 เข้าโปรแกรมได้. Like <see cref="SetCanViewPayAsync"/>: in the change history, and the new security stamp
    /// ends the user's open sessions so the next login carries the new programs.
    /// </summary>
    public async Task<AccountResult> SetProgramsAsync(string userId, IEnumerable<string> programs, byte[] loadedRowVersion)
    {
        var user = await users.FindByIdAsync(userId);
        if (user is null) return AccountResult.Fail("ไม่พบผู้ใช้");
        if (user.IsAdmin) return AccountResult.Fail("บัญชี admin เข้าโปรแกรมงานไม่ได้");

        user.ExpectRowVersion(loadedRowVersion);
        user.Programs = ProgramAccess.Format(programs);
        return AccountResult.From(await users.UpdateSecurityStampAsync(user));
    }

    /// <summary>Sets a temporary password for a user who forgot theirs; also lifts a lockout.</summary>
    public async Task<AccountResult> SetTemporaryPasswordAsync(string userId, string temporaryPassword, byte[] loadedRowVersion)
    {
        var user = await users.FindByIdAsync(userId);
        if (user is null) return AccountResult.Fail("ไม่พบผู้ใช้");

        user.ExpectRowVersion(loadedRowVersion);
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
