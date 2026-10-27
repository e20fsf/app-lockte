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
    internal DbSet<ProductionReport> ProductionReports => Set<ProductionReport>();
    internal DbSet<ProductionReportLine> ProductionReportLines => Set<ProductionReportLine>();
    internal DbSet<MoldUsage> MoldUsages => Set<MoldUsage>();
    internal DbSet<MoldCutoff> MoldCutoffs => Set<MoldCutoff>();
    internal DbSet<StockEntry> StockEntries => Set<StockEntry>();
    internal DbSet<QaDocumentType> QaDocumentTypes => Set<QaDocumentType>();
    internal DbSet<QaModelSpec> QaModelSpecs => Set<QaModelSpec>();
    internal DbSet<QaMeasurePoint> QaMeasurePoints => Set<QaMeasurePoint>();
    internal DbSet<QaDefect> QaDefects => Set<QaDefect>();
    internal DbSet<QaSampleSize> QaSampleSizes => Set<QaSampleSize>();

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

        modelBuilder.Entity<ProductionReport>(r =>
        {
            r.HasAuditName("ใบรายงานผลผลิต");
            r.Property(x => x.DepartmentCode).HasMaxLength(1).HasAuditName("แผนก");
            r.Property(x => x.ReportDate).HasAuditName("วันที่");
            r.Property(x => x.ReportNo).HasMaxLength(ProductionReportRegistry.ReportNoLength).HasAuditName("เลขที่ใบรายงาน");
            r.Property(x => x.MachineId).HasAuditName("เครื่องจักร");
            r.Property(x => x.StockedAt).HasAuditName("เข้าสต็อกเมื่อ");
            r.HasIndex(x => x.ReportNo).IsUnique();
            r.HasIndex(x => new { x.DepartmentCode, x.ReportDate });
            r.HasMany(x => x.Lines).WithOne(x => x.ProductionReport).HasForeignKey(x => x.ProductionReportId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProductionReportLine>(l =>
        {
            l.HasAuditName("บรรทัดใบรายงานผลผลิต");
            l.Property(x => x.LineNo).HasAuditName("ลำดับ");
            l.Property(x => x.ShiftId).HasAuditName("กะ");
            l.Property(x => x.Hours).HasPrecision(9, 2).HasAuditName("ชั่วโมง");
            l.Property(x => x.MaterialLot1).HasMaxLength(ProductionReportRegistry.TextLength).HasAuditName("Lot วัตถุดิบ 1");
            l.Property(x => x.MaterialLot2).HasMaxLength(ProductionReportRegistry.TextLength).HasAuditName("Lot วัตถุดิบ 2");
            l.Property(x => x.ProductId).HasAuditName("สินค้า");
            l.Property(x => x.ProductName).HasMaxLength(ProductionReportRegistry.TextLength).HasAuditName("ชื่อสินค้า");
            l.Property(x => x.Unit).HasMaxLength(ProductionReportRegistry.TextLength).HasAuditName("หน่วย");
            l.Property(x => x.GoodQuantity).HasPrecision(18, 4).HasAuditName("จำนวนดี");
            l.Property(x => x.RejectQuantity).HasPrecision(18, 4).HasAuditName("เสีย");
            l.Property(x => x.TotalQuantity).HasPrecision(18, 4).HasAuditName("จำนวนรวม");
            l.Property(x => x.EmployeeId).HasAuditName("พนักงาน");
            l.Property(x => x.MoldId).HasAuditName("แม่พิมพ์");
            l.Property(x => x.Lot).HasMaxLength(ProductionReportRegistry.TextLength).HasAuditName("Lot ผลผลิต");
            l.HasIndex(x => new { x.ProductionReportId, x.LineNo }).IsUnique();
            l.HasIndex(x => x.ProductId);
            l.HasIndex(x => x.EmployeeId);
            // A product or mold that a report uses cannot be deleted (ข้อสมมติ ง).
            l.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
            l.HasOne(x => x.Mold).WithMany().HasForeignKey(x => x.MoldId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MoldUsage>(u =>
        {
            u.HasAuditName("การใช้งานแม่พิมพ์");
            u.Property(x => x.MoldId).HasAuditName("แม่พิมพ์");
            u.Property(x => x.Day).HasAuditName("วันที่");
            u.Property(x => x.Shots).HasAuditName("จำนวนครั้งที่ใช้");
            u.Property(x => x.IsOpening).HasAuditName("ยอดยกมา");
            u.HasIndex(x => new { x.MoldId, x.Day });
            u.HasIndex(x => x.Day);
            u.HasOne(x => x.Mold).WithMany().HasForeignKey(x => x.MoldId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MoldCutoff>(c =>
        {
            c.HasAuditName("ยอดสะสมการใช้งานแม่พิมพ์");
            c.Property(x => x.MoldId).HasAuditName("แม่พิมพ์");
            c.Property(x => x.CutDate).HasAuditName("วันตัดยอด");
            c.Property(x => x.Cumulative).HasAuditName("ยอดสะสม");
            c.HasIndex(x => new { x.CutDate, x.MoldId }).IsUnique();
            c.HasOne(x => x.Mold).WithMany().HasForeignKey(x => x.MoldId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<QaDocumentType>(t =>
        {
            t.HasAuditName("ชนิดเอกสารใบตรวจ QA");
            t.Property(x => x.Code).HasMaxLength(QaSpecRegistry.CodeLength).HasAuditName("รหัสเอกสาร");
            t.Property(x => x.Revision).HasMaxLength(QaSpecRegistry.CodeLength).HasAuditName("ฉบับที่");
            t.Property(x => x.Name).HasMaxLength(QaSpecRegistry.TextLength).HasAuditName("ชื่อใบตรวจ");
            t.Property(x => x.DepartmentStep).HasMaxLength(QaSpecRegistry.TextLength).HasAuditName("แผนก/ขั้นตอน");
            t.Property(x => x.SampleUnit).HasMaxLength(QaSpecRegistry.CodeLength).HasAuditName("หน่วยสุ่ม");
            t.Property(x => x.ResultColumns).HasConversion<byte>().HasAuditName("ชนิดคอลัมน์ผล");
            t.Property(x => x.AttributeRanges).HasMaxLength(QaSpecRegistry.LongTextLength).HasAuditName("ช่วงคอลัมน์ตำหนิ");
            t.Property(x => x.PageLayout).HasConversion<byte>().HasAuditName("โครงหน้า");
            t.Property(x => x.Paper).HasMaxLength(QaSpecRegistry.CodeLength).HasAuditName("ขนาดกระดาษ");
            t.HasIndex(x => new { x.Code, x.Revision, x.Name }).IsUnique(); // sheets of different names may share one control number (the seven packing sheets)
        });

        modelBuilder.Entity<QaModelSpec>(s =>
        {
            s.HasAuditName("สเปคตรวจรายรุ่น");
            s.Property(x => x.Name).HasMaxLength(QaSpecRegistry.TextLength).HasAuditName("ชื่อสเปค");
            s.Property(x => x.Models).HasMaxLength(QaSpecRegistry.LongTextLength).HasAuditName("รุ่นที่ใช้ร่วม");
            s.Property(x => x.DocumentSequence).HasAuditName("ลำดับเลขเอกสาร");
            s.Property(x => x.ImageData).HasAuditName("รูปชิ้นงาน");
            s.Property(x => x.ImageContentType).HasMaxLength(QaSpecRegistry.CodeLength).HasAuditName("ชนิดไฟล์รูป");
            s.HasIndex(x => new { x.DocumentTypeId, x.Name }).IsUnique();
            s.HasOne(x => x.DocumentType).WithMany().HasForeignKey(x => x.DocumentTypeId).OnDelete(DeleteBehavior.Restrict);
            s.HasMany(x => x.Points).WithOne(p => p.QaModelSpec).HasForeignKey(p => p.QaModelSpecId).OnDelete(DeleteBehavior.Cascade);
            s.HasMany(x => x.Defects).WithOne(d => d.QaModelSpec).HasForeignKey(d => d.QaModelSpecId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<QaMeasurePoint>(p =>
        {
            p.HasAuditName("จุดวัดของสเปค");
            p.Property(x => x.Sequence).HasAuditName("ลำดับ");
            p.Property(x => x.PointNo).HasMaxLength(QaSpecRegistry.CodeLength).HasAuditName("เลขจุดวัด");
            p.Property(x => x.Description).HasMaxLength(QaSpecRegistry.LongTextLength).HasAuditName("ข้อความค่าที่กำหนด");
            p.Property(x => x.Standard).HasMaxLength(QaSpecRegistry.LongTextLength).HasAuditName("ค่ามาตรฐาน");
            p.HasIndex(x => new { x.QaModelSpecId, x.Sequence }).IsUnique();
        });

        modelBuilder.Entity<QaDefect>(d =>
        {
            d.HasAuditName("รายการตำหนิของสเปค");
            d.Property(x => x.Sequence).HasAuditName("ลำดับ");
            d.Property(x => x.Name).HasMaxLength(QaSpecRegistry.TextLength).HasAuditName("รายการตำหนิ");
            d.HasIndex(x => new { x.QaModelSpecId, x.Sequence }).IsUnique();
        });

        modelBuilder.Entity<QaSampleSize>(s =>
        {
            s.HasAuditName("จำนวนสุ่มตามขนาด Lot");
            s.Property(x => x.UpTo).HasAuditName("จำนวนดีสูงสุดของช่วง");
            s.Property(x => x.SampleSize).HasAuditName("จำนวนสุ่ม");
        });

        modelBuilder.Entity<StockEntry>(e =>
        {
            e.HasAuditName("รายการสต็อก");
            e.Property(x => x.Date).HasAuditName("วันที่");
            e.Property(x => x.DocumentNo).HasMaxLength(StockRegistry.DocumentNoLength).HasAuditName("เลขที่เอกสาร");
            e.Property(x => x.LineNo).HasAuditName("ลำดับ");
            e.Property(x => x.ProductId).HasAuditName("สินค้า");
            e.Property(x => x.Quantity).HasPrecision(18, 4).HasAuditName("จำนวน");
            e.Property(x => x.EmployeeId).HasAuditName("ผู้ทำรายการ");
            e.Property(x => x.Kind).HasConversion<byte>().HasAuditName("ชนิดรายการ");
            e.Property(x => x.Remark).HasMaxLength(StockRegistry.RemarkLength).HasAuditName("หมายเหตุ");
            e.Property(x => x.ClosedOn).HasAuditName("ปิดงวดเมื่อ");
            e.HasIndex(x => new { x.ProductId, x.ClosedOn });
            e.HasIndex(x => new { x.Kind, x.Date });
            e.HasIndex(x => x.DocumentNo);
            // A product that has stock entries cannot be deleted (ข้อสมมติ ง).
            e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
