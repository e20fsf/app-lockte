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
    internal DbSet<Product> Products => Set<Product>();
    internal DbSet<ProductType> ProductTypes => Set<ProductType>();
    internal DbSet<BrakeType> BrakeTypes => Set<BrakeType>();
    internal DbSet<ProductModel> ProductModels => Set<ProductModel>();
    internal DbSet<ProductGroup> ProductGroups => Set<ProductGroup>();

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

        modelBuilder.Entity<ProductType>(t =>
        {
            t.HasAuditName("ประเภทสินค้า");
            t.Property(x => x.Name).HasMaxLength(ProductRegistry.TextLength).HasAuditName("ชื่อประเภท");
            t.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<BrakeType>(t =>
        {
            t.HasAuditName("ชนิดเบรก");
            t.Property(x => x.Name).HasMaxLength(ProductRegistry.TextLength).HasAuditName("ชื่อชนิด");
            t.Property(x => x.Code).HasMaxLength(ProductRegistry.BrakeTypeCodeLength).HasAuditName("รหัสชนิด");
            t.HasIndex(x => x.Code).IsUnique();
            t.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<ProductModel>(t =>
        {
            t.HasAuditName("รุ่น");
            t.Property(x => x.Name).HasMaxLength(ProductRegistry.TextLength).HasAuditName("ชื่อรุ่น");
            t.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<ProductGroup>(t =>
        {
            t.HasAuditName("กลุ่มสินค้า");
            t.Property(x => x.Pattern).HasMaxLength(ProductRegistry.GroupPatternLength).HasAuditName("รหัสกลุ่ม");
            t.Property(x => x.Name).HasMaxLength(ProductRegistry.TextLength).HasAuditName("ชื่อกลุ่ม");
            t.HasIndex(x => x.Pattern).IsUnique();
        });

        modelBuilder.Entity<Product>(p =>
        {
            p.HasAuditName("สินค้า");
            p.Property(x => x.Code).HasMaxLength(ProductRegistry.CodeLength).HasAuditName("รหัสสินค้า");
            p.Property(x => x.Name).HasMaxLength(ProductRegistry.TextLength).HasAuditName("ชื่อสินค้า");
            p.Property(x => x.Unit).HasMaxLength(ProductRegistry.TextLength).HasAuditName("หน่วย");
            p.Property(x => x.Pack).HasMaxLength(ProductRegistry.TextLength).HasAuditName("หน่วยบรรจุ");
            p.Property(x => x.AluminumWeight).HasPrecision(18, 4).HasAuditName("นน.อลูมิเนียม");
            p.Property(x => x.BrakePadWeight).HasPrecision(18, 4).HasAuditName("นน.ผ้าเบรก");
            p.Property(x => x.CorkWeight).HasPrecision(18, 4).HasAuditName("นน.ไม้ก๊อก");
            p.Property(x => x.SteelCost).HasPrecision(18, 4).HasAuditName("ค่าเหล็ก");
            p.Property(x => x.PairWeight).HasPrecision(18, 4).HasAuditName("นน.รวม 1 คู่");
            p.Property(x => x.SellingPrice).HasPrecision(18, 4).HasAuditName("ราคาขาย");
            p.Property(x => x.UnitCost).HasPrecision(18, 4).HasAuditName("ต้นทุนต่อหน่วย");
            p.Property(x => x.ProductTypeId).HasAuditName("ประเภท");
            p.Property(x => x.MoldId).HasAuditName("แม่พิมพ์");
            p.Property(x => x.CustomerId).HasAuditName("ลูกค้า");
            p.Property(x => x.ProducedOn).HasAuditName("วันที่ผลิต");
            p.Property(x => x.ModelId).HasAuditName("รุ่น");
            p.Property(x => x.Barcode).HasMaxLength(ProductRegistry.BarcodeLength).HasAuditName("บาร์โค้ด");
            p.Property(x => x.StandardNumber).HasMaxLength(ProductRegistry.TextLength).HasAuditName("เลข มอก.");
            p.Property(x => x.BrakeTypeId).HasAuditName("ชนิดเบรก");
            p.Property(x => x.Size).HasMaxLength(ProductRegistry.TextLength).HasAuditName("ขนาด");
            p.Property(x => x.Formula).HasMaxLength(ProductRegistry.TextLength).HasAuditName("สูตร");
            p.Property(x => x.StandardModel).HasMaxLength(ProductRegistry.TextLength).HasAuditName("รุ่น มอก.");
            p.HasIndex(x => x.Code).IsUnique();
            p.HasIndex(x => x.Barcode);
            p.HasIndex(x => x.CustomerId);
            // A type, brake type, model or mold that products use cannot be deleted (ข้อสมมติ ง).
            p.HasOne(x => x.ProductType).WithMany().HasForeignKey(x => x.ProductTypeId).OnDelete(DeleteBehavior.Restrict);
            p.HasOne(x => x.Mold).WithMany().HasForeignKey(x => x.MoldId).OnDelete(DeleteBehavior.Restrict);
            p.HasOne(x => x.Model).WithMany().HasForeignKey(x => x.ModelId).OnDelete(DeleteBehavior.Restrict);
            p.HasOne(x => x.BrakeType).WithMany().HasForeignKey(x => x.BrakeTypeId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
