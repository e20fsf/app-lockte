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
            user.Property(u => u.DisplayName).HasMaxLength(100).IsRequired();
            // Written by every login attempt; not an edit by a person.
            user.Property(u => u.AccessFailedCount).IsBookkeeping();
            user.Property(u => u.LockoutEnd).IsBookkeeping();
        });
        builder.ApplyLockteConventions();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
        => DbContextConcurrencyExtensions.SaveWithConflictCheck(() => base.SaveChanges(acceptAllChangesOnSuccess));

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        => DbContextConcurrencyExtensions.SaveWithConflictCheckAsync(() => base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken));
}
