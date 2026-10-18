using Lockte.SharedKernel.Persistence;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Web.Accounts;

/// <summary>Login accounts, schema <c>auth</c>. No roles (#39), so the role tables are not created.</summary>
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
        });
        builder.ApplyLockteConventions();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
        => this.SaveWithConflictCheck(() => base.SaveChanges(acceptAllChangesOnSuccess));

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        => this.SaveWithConflictCheckAsync(() => base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken));
}
