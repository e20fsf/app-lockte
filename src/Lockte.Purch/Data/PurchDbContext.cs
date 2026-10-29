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
    internal DbSet<Supplier> Suppliers => Set<Supplier>();
    internal DbSet<SupplierCreditOption> SupplierCreditOptions => Set<SupplierCreditOption>();
    internal DbSet<SupplierBusinessType> SupplierBusinessTypes => Set<SupplierBusinessType>();
    internal DbSet<Material> Materials => Set<Material>();
    internal DbSet<MaterialGroup> MaterialGroups => Set<MaterialGroup>();

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

        modelBuilder.Entity<Supplier>(s =>
        {
            s.HasAuditName("ผู้ขาย");
            s.Property(x => x.Code).HasMaxLength(SupplierRegistry.CodeLength).HasAuditName("รหัสผู้ขาย");
            s.Property(x => x.Name).HasMaxLength(SupplierRegistry.TextLength).HasAuditName("ชื่อผู้ขาย");
            s.Property(x => x.Address1).HasMaxLength(SupplierRegistry.TextLength).HasAuditName("ที่อยู่");
            s.Property(x => x.Address2).HasMaxLength(SupplierRegistry.TextLength).HasAuditName("เขต(อำเภอ)และจังหวัด");
            s.Property(x => x.Tel1).HasMaxLength(SupplierRegistry.TextLength).HasAuditName("โทรศัพท์ 1");
            s.Property(x => x.Tel2).HasMaxLength(SupplierRegistry.TextLength).HasAuditName("โทรศัพท์ 2");
            s.Property(x => x.Fax).HasMaxLength(SupplierRegistry.TextLength).HasAuditName("แฟกซ์");
            s.Property(x => x.Contact).HasMaxLength(SupplierRegistry.TextLength).HasAuditName("ผู้ติดต่อ");
            s.Property(x => x.CreditDays).HasAuditName("เครดิต (วัน)");
            s.Property(x => x.Vat).HasAuditName("VAT");
            s.Property(x => x.ProductType).HasMaxLength(SupplierRegistry.TextLength).HasAuditName("ประเภทผลิตภัณฑ์");
            s.Property(x => x.BusinessType).HasMaxLength(SupplierRegistry.TextLength).HasAuditName("ประเภทธุรกิจ");
            s.Property(x => x.Certificate).HasAuditName("ใบรับรอง");
            s.Property(x => x.UsedInProduction).HasAuditName("ใช้ในการผลิต");
            s.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<SupplierCreditOption>(o =>
        {
            o.HasAuditName("ตัวเลือกเครดิตผู้ขาย");
            o.Property(x => x.Days).HasAuditName("เครดิต (วัน)");
            o.HasIndex(x => x.Days).IsUnique();
        });

        modelBuilder.Entity<SupplierBusinessType>(o =>
        {
            o.HasAuditName("ประเภทธุรกิจผู้ขาย");
            o.Property(x => x.Name).HasMaxLength(SupplierRegistry.TextLength).HasAuditName("ประเภทธุรกิจ");
            o.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<Material>(m =>
        {
            m.HasAuditName("วัตถุดิบ");
            m.Property(x => x.Code).HasMaxLength(MaterialRegistry.CodeLength).HasAuditName("รหัสวัตถุดิบ");
            m.Property(x => x.Name).HasMaxLength(MaterialRegistry.NameLength).HasAuditName("ชื่อวัตถุดิบ");
            m.Property(x => x.Unit).HasMaxLength(MaterialRegistry.UnitLength).HasAuditName("หน่วย");
            m.Property(x => x.MinimumQuantity).HasPrecision(18, 4).HasAuditName("จุดสั่งซื้อต่ำสุด");
            m.Property(x => x.Cost).HasPrecision(18, 4).HasAuditName("ราคา");
            m.Property(x => x.LatestSupplierId).HasAuditName("ผู้ขายล่าสุด");
            m.Property(x => x.IsMain).HasAuditName("วัตถุดิบหลัก");
            m.Property(x => x.BuyBackPrice).HasPrecision(18, 4).HasAuditName("ราคาซื้อคืน กก.ละ");
            m.Property(x => x.ProductId).HasAuditName("สินค้า");
            m.HasIndex(x => x.Code).IsUnique();
            m.HasIndex(x => x.LatestSupplierId);
            m.HasOne(x => x.LatestSupplier).WithMany().HasForeignKey(x => x.LatestSupplierId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MaterialGroup>(g =>
        {
            g.HasAuditName("กลุ่มรหัสวัตถุดิบ");
            g.Property(x => x.Prefix).HasMaxLength(MaterialRegistry.CodeLength).HasAuditName("รหัสขึ้นต้น");
            g.Property(x => x.Name).HasMaxLength(MaterialRegistry.NameLength).HasAuditName("ชื่อกลุ่ม");
            g.HasIndex(x => x.Prefix).IsUnique();
        });
    }
}
