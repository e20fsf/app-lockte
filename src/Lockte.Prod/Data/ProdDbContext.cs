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
            m.Property(x => x.PiecesPerShot).HasAuditName("ชิ้น/พิมพ์");
            m.Property(x => x.MaintenanceShots).HasAuditName("เกณฑ์บำรุงรักษา จำนวนครั้งการฉีด");
            m.Property(x => x.MaintenancePieces).HasAuditName("เกณฑ์บำรุงรักษา จำนวนชิ้น");
            m.Property(x => x.MaintenanceYears).HasAuditName("เกณฑ์บำรุงรักษา จำนวนปี");
            m.Property(x => x.DepartmentId).HasAuditName("แผนก");
            m.Property(x => x.Maker).HasMaxLength(MoldRegistry.PersonLength).HasAuditName("ผู้ทำแม่พิมพ์");
            m.Property(x => x.StartDate).HasAuditName("วันที่เริ่มใช้");
            m.Property(x => x.EndDate).HasAuditName("วันที่เลิกใช้");
            m.Property(x => x.Cost).HasPrecision(18, 2).HasAuditName("ราคา");
            m.Property(x => x.Owner).HasMaxLength(MoldRegistry.PersonLength).HasAuditName("เจ้าของแม่พิมพ์");
            m.Property(x => x.AluminumWeight).HasPrecision(12, 3).HasAuditName("น้ำหนักอลูมิเนียม");
            m.HasIndex(x => x.Code).IsUnique();
            m.HasIndex(x => x.DepartmentId);
        });
    }
}
