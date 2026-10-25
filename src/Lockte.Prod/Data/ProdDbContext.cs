using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Prod.Data;

/// <summary>
/// module ผลิต (ADR 0002): สินค้า แม่พิมพ์ ใบรายงานผลผลิต Lot สต็อกสินค้า QA — writes only to schema <c>prod</c>.
/// So far only the minimal ทะเบียนแม่พิมพ์ (#103). Entities are internal: other modules read through the directories.
/// </summary>
public sealed class ProdDbContext(DbContextOptions<ProdDbContext> options) : ModuleDbContext(options, Schema)
{
    public const string Schema = "prod";

    internal DbSet<Mold> Molds => Set<Mold>();

    protected override void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Mold>(m =>
        {
            m.HasAuditName("แม่พิมพ์");
            m.Property(x => x.Code).HasMaxLength(MoldRegistry.CodeLength).HasAuditName("รหัสแม่พิมพ์");
            m.Property(x => x.Name).HasMaxLength(MoldRegistry.NameLength).HasAuditName("ชื่อแม่พิมพ์");
            m.HasIndex(x => x.Code).IsUnique();
        });
    }
}
