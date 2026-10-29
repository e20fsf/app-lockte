using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Purch.Data;

/// <summary>
/// module จัดซื้อและวัตถุดิบ (ADR 0002): ผู้ขาย วัตถุดิบ ใบขอซื้อ ใบสั่งซื้อ การรับของ ผลประเมินผู้ขาย สต็อกวัตถุดิบ —
/// writes only to schema <c>purch</c>. Entities are internal: other modules read through the directories.
/// </summary>
public sealed class PurchDbContext(DbContextOptions<PurchDbContext> options) : ModuleDbContext(options, Schema)
{
    public const string Schema = "purch";

    internal DbSet<PurchSettings> Settings => Set<PurchSettings>();
    internal DbSet<ResaleGroup> ResaleGroups => Set<ResaleGroup>();

    protected override void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PurchSettings>(s =>
        {
            s.HasAuditName("ค่าตั้งค่าจัดซื้อ");
            s.ToTable("Settings");
            s.Property(x => x.VatRate).HasPrecision(6, 4).HasAuditName("อัตรา VAT");
            s.Property(x => x.SmallOrderLimit).HasPrecision(18, 2).HasAuditName("เกณฑ์ใบสั่งซื้อเล็ก");
            s.Property(x => x.SmallOrderApprover).HasMaxLength(PurchSettingsRegistry.NameLength).HasAuditName("ผู้อนุมัติใบสั่งซื้อเล็ก");
            s.Property(x => x.NoCarryPrefixes).HasMaxLength(PurchSettingsRegistry.PrefixListLength).HasAuditName("prefix ที่ไม่ยกยอดตอนตัดสต็อก");
            s.Property(x => x.GradeA).HasPrecision(5, 2).HasAuditName("เกณฑ์เกรด A");
            s.Property(x => x.GradeB).HasPrecision(5, 2).HasAuditName("เกณฑ์เกรด B");
            s.Property(x => x.GradeC).HasPrecision(5, 2).HasAuditName("เกณฑ์เกรด C");
        });

        modelBuilder.Entity<ResaleGroup>(g =>
        {
            g.HasAuditName("กลุ่มสินค้าซื้อมาขาย");
            g.Property(x => x.Prefix).HasMaxLength(PurchSettingsRegistry.PrefixLength).HasAuditName("รหัสขึ้นต้น");
            g.Property(x => x.Name).HasMaxLength(PurchSettingsRegistry.NameLength).HasAuditName("ชื่อกลุ่ม");
            g.Property(x => x.SortOrder).HasAuditName("ลำดับ");
            g.HasIndex(x => x.Prefix).IsUnique();
        });
    }
}
