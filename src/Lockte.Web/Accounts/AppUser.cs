using Lockte.SharedKernel.Persistence;
using Microsoft.AspNetCore.Identity;

namespace Lockte.Web.Accounts;

/// <summary>
/// ผู้ใช้ระบบ (#39): ชื่อ user ตั้งเอง + ชื่อที่แสดง ไม่ผูกทะเบียนพนักงาน ไม่มี role (มีเพียงธง "ดูข้อมูลค่าจ้างได้" ต่อ user, #277).
/// Never deleted — deactivated instead so the change history still names the person.
/// </summary>
public sealed class AppUser : IdentityUser, IAudited
{
    public const string AdminUserName = "admin";

    public string DisplayName { get; set; } = "";

    /// <summary>The single admin account: sees only the user-management page.</summary>
    public bool IsAdmin { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// #277 ดูข้อมูลค่าจ้างได้ — set per user by the admin. The admin account never sees ข้อมูลค่าจ้าง whatever this says
    /// (see <see cref="AccountClaims.CanViewPay"/>). New users start without it; users that existed before #277 got it.
    /// </summary>
    public bool CanViewPay { get; set; }

    /// <summary>Set by a temporary password; the user must choose a new one before using the system.</summary>
    public bool MustChangePassword { get; set; }

    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    string IHasBusinessKey.BusinessKey => UserName ?? "";
}
