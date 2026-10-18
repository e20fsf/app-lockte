using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Hr.Data;

/// <summary>
/// module พนักงานและค่าแรง (ADR 0002): พนักงาน เวลาทำงาน การลา ใบเตือน งวดค่าแรง — writes only to schema <c>hr</c>.
/// Screens and tests go through this module's public services, never through this DbContext.
/// </summary>
public sealed class HrDbContext(DbContextOptions<HrDbContext> options) : DbContext(options)
{
    public const string Schema = "hr";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyLockteConventions();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
        => this.SaveWithConflictCheck(() => base.SaveChanges(acceptAllChangesOnSuccess));

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        => this.SaveWithConflictCheckAsync(() => base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken));
}
