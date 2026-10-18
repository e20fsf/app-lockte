using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Web.Audit;

/// <summary>
/// ประวัติการแก้ไข, schema <c>audit</c>: owns (creates) <c>audit.History</c>. Every module also maps that table and
/// writes its own changes into it within its own save; this context reads it and records logins.
/// </summary>
public sealed class AuditDbContext(DbContextOptions<AuditDbContext> options) : ModuleDbContext(options, Schema)
{
    public const string Schema = HistoryModel.Schema;

    public DbSet<HistoryRecord> History => Set<HistoryRecord>();

    protected override bool OwnsHistoryTable => true;
}
