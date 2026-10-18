using Lockte.SharedKernel.Persistence;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Web.Accounts;

/// <summary>
/// Login accounts, schema <c>auth</c>, owned by the web host (ADR 0002: one login for the whole app).
/// No roles (#39), so the role tables are not created. Must derive from Identity, so it repeats what
/// <see cref="ModuleDbContext"/> does.
/// </summary>
public sealed class AccountsDbContext(DbContextOptions<AccountsDbContext> options) : IdentityUserContext<AppUser>(options)
{
    public const string Schema = "auth";

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema(Schema);
        builder.Entity<AppUser>(user =>
        {
            user.HasAuditName("ผู้ใช้");
            user.Property(u => u.UserName).HasAuditName("ชื่อ user");
            user.Property(u => u.DisplayName).HasMaxLength(100).IsRequired().HasAuditName("ชื่อที่แสดง");
            user.Property(u => u.IsActive).HasAuditName("ใช้งาน");
            user.Property(u => u.IsAdmin).HasAuditName("admin");
            user.Property(u => u.MustChangePassword).HasAuditName("ต้องเปลี่ยนรหัส");
            user.Property(u => u.PasswordHash).HasAuditName("รหัสผ่าน").IsSecret();
            // Written by every login attempt, derived copies, or Identity features this system does not use.
            user.Property(u => u.AccessFailedCount).IsBookkeeping();
            user.Property(u => u.LockoutEnd).IsBookkeeping();
            user.Property(u => u.LockoutEnabled).IsBookkeeping();
            user.Property(u => u.SecurityStamp).IsBookkeeping();
            user.Property(u => u.NormalizedUserName).IsBookkeeping();
            user.Property(u => u.NormalizedEmail).IsBookkeeping();
            user.Property(u => u.EmailConfirmed).IsBookkeeping();
            user.Property(u => u.PhoneNumberConfirmed).IsBookkeeping();
            user.Property(u => u.TwoFactorEnabled).IsBookkeeping();
        });
        builder.ApplyLockteConventions();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
        => DbContextConcurrencyExtensions.SaveWithConflictCheck(() => base.SaveChanges(acceptAllChangesOnSuccess));

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        => DbContextConcurrencyExtensions.SaveWithConflictCheckAsync(() => base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken));
}
