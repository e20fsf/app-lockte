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
    internal DbSet<DepartmentPrefix> DepartmentPrefixes => Set<DepartmentPrefix>();
    internal DbSet<PurchasingLocation> PurchasingLocations => Set<PurchasingLocation>();
    internal DbSet<RequestReason> RequestReasons => Set<RequestReason>();
    internal DbSet<RequestNote> RequestNotes => Set<RequestNote>();
    internal DbSet<DocumentCounter> DocumentCounters => Set<DocumentCounter>();
    internal DbSet<PurchaseRequest> PurchaseRequests => Set<PurchaseRequest>();
    internal DbSet<PurchaseRequestLine> PurchaseRequestLines => Set<PurchaseRequestLine>();
    internal DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    internal DbSet<PurchaseOrderLine> PurchaseOrderLines => Set<PurchaseOrderLine>();
    internal DbSet<StockEntry> StockEntries => Set<StockEntry>();
    internal DbSet<GoodsReceipt> GoodsReceipts => Set<GoodsReceipt>();

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

        modelBuilder.Entity<DepartmentPrefix>(d =>
        {
            d.HasAuditName("prefix เลขที่ใบขอซื้อของแผนก");
            d.Property(x => x.DepartmentId).HasAuditName("แผนก");
            d.Property(x => x.Prefix).HasMaxLength(RequestSetupRegistry.PrefixLength).HasAuditName("prefix เลขที่ใบขอซื้อ");
            d.HasIndex(x => x.DepartmentId).IsUnique();
            d.HasIndex(x => x.Prefix).IsUnique();
        });

        modelBuilder.Entity<PurchasingLocation>(l =>
        {
            l.HasAuditName("ผู้จัดซื้อ");
            l.Property(x => x.Code).HasMaxLength(RequestSetupRegistry.PrefixLength).HasAuditName("รหัสผู้จัดซื้อ");
            l.Property(x => x.Name).HasMaxLength(RequestSetupRegistry.NameLength).HasAuditName("ชื่อผู้จัดซื้อ");
            l.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<RequestReason>(r =>
        {
            r.HasAuditName("เหตุผลในการขอซื้อ");
            r.Property(x => x.Text).HasMaxLength(RequestSetupRegistry.TextLength).HasAuditName("ข้อความเหตุผล");
            r.HasIndex(x => x.Text).IsUnique();
        });

        modelBuilder.Entity<RequestNote>(r =>
        {
            r.HasAuditName("หมายเหตุใบขอซื้อ");
            r.Property(x => x.Text).HasMaxLength(RequestSetupRegistry.TextLength).HasAuditName("ข้อความหมายเหตุ");
            r.HasIndex(x => x.Text).IsUnique();
        });

        modelBuilder.Entity<DocumentCounter>(c =>
        {
            c.Property(x => x.Key).HasMaxLength(40);
            c.HasIndex(x => x.Key).IsUnique();
        });

        modelBuilder.Entity<PurchaseRequest>(r =>
        {
            r.HasAuditName("ใบขออนุมัติสั่งซื้อ");
            r.Property(x => x.RequestNo).HasMaxLength(PurchaseRequestRegistry.NumberLength).HasAuditName("เลขที่ใบขอซื้อ");
            r.Property(x => x.RequestDate).HasAuditName("วันที่ขอซื้อ");
            r.Property(x => x.DepartmentId).HasAuditName("แผนก");
            r.Property(x => x.DesiredDate).HasAuditName("วันที่ต้องการ");
            r.Property(x => x.PurchasingLocation).HasMaxLength(RequestSetupRegistry.PrefixLength).HasAuditName("ผู้จัดซื้อ");
            r.Property(x => x.Reason).HasMaxLength(RequestSetupRegistry.TextLength).HasAuditName("เหตุผลในการขอซื้อ");
            r.Property(x => x.Note).HasMaxLength(RequestSetupRegistry.TextLength).HasAuditName("หมายเหตุ");
            r.HasIndex(x => x.RequestNo).IsUnique();
            r.HasIndex(x => x.DepartmentId);
            r.HasMany(x => x.Lines).WithOne(x => x.PurchaseRequest).HasForeignKey(x => x.PurchaseRequestId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PurchaseRequestLine>(l =>
        {
            l.HasAuditName("รายการใบขอซื้อ");
            l.Property(x => x.LineNo).HasAuditName("ลำดับ");
            l.Property(x => x.MaterialId).HasAuditName("วัตถุดิบ");
            l.Property(x => x.MaterialName).HasMaxLength(MaterialRegistry.NameLength).HasAuditName("ชื่อวัตถุดิบ");
            l.Property(x => x.Unit).HasMaxLength(MaterialRegistry.UnitLength).HasAuditName("หน่วย");
            l.Property(x => x.Quantity).HasPrecision(18, 4).HasAuditName("จำนวน");
            l.Property(x => x.UnitPrice).HasPrecision(18, 4).HasAuditName("ราคา");
            l.Property(x => x.NetAmount).HasPrecision(18, 4).HasAuditName("จำนวนเงิน");
            l.Property(x => x.TankWeight).HasPrecision(18, 4).HasAuditName("นน.ถัง");
            l.Property(x => x.AbandonedQuantity).HasPrecision(18, 4).HasAuditName("จำนวนที่สั่งแล้วลบใบ");
            l.HasIndex(x => x.MaterialId);
            // A material that a request uses cannot be deleted (ข้อสมมติ ง).
            l.HasOne(x => x.Material).WithMany().HasForeignKey(x => x.MaterialId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PurchaseOrder>(o =>
        {
            o.HasAuditName("ใบสั่งซื้อ");
            o.Property(x => x.OrderNo).HasMaxLength(PurchaseOrderRegistry.NumberLength).HasAuditName("เลขที่ใบสั่งซื้อ");
            o.Property(x => x.OrderDate).HasAuditName("วันที่สั่งซื้อ");
            o.Property(x => x.SupplierId).HasAuditName("ผู้ขาย");
            o.Property(x => x.SupplierName).HasMaxLength(SupplierRegistry.TextLength).HasAuditName("ชื่อผู้ขาย");
            o.Property(x => x.SupplierAddress1).HasMaxLength(SupplierRegistry.TextLength).HasAuditName("ที่อยู่ผู้ขาย");
            o.Property(x => x.SupplierAddress2).HasMaxLength(SupplierRegistry.TextLength).HasAuditName("เขต(อำเภอ)และจังหวัดผู้ขาย");
            o.Property(x => x.SupplierTel).HasMaxLength(SupplierRegistry.TextLength).HasAuditName("โทรศัพท์ผู้ขาย");
            o.Property(x => x.SupplierFax).HasMaxLength(SupplierRegistry.TextLength).HasAuditName("แฟกซ์ผู้ขาย");
            o.Property(x => x.CreditDays).HasAuditName("เครดิต (วัน)");
            o.Property(x => x.ChargesVat).HasAuditName("คิด VAT");
            o.Property(x => x.PurchasingLocation).HasMaxLength(RequestSetupRegistry.PrefixLength).HasAuditName("ผู้จัดซื้อ");
            o.Property(x => x.Reason).HasMaxLength(RequestSetupRegistry.TextLength).HasAuditName("เหตุผลในการซื้อ");
            o.Property(x => x.Note).HasMaxLength(RequestSetupRegistry.TextLength).HasAuditName("หมายเหตุ");
            o.Property(x => x.Subtotal).HasPrecision(18, 2).HasAuditName("ยอดก่อนภาษี");
            o.Property(x => x.Tax).HasPrecision(18, 2).HasAuditName("ภาษีมูลค่าเพิ่ม");
            o.Property(x => x.Total).HasPrecision(18, 2).HasAuditName("ยอดรวมทั้งสิ้น");
            o.HasIndex(x => x.OrderNo).IsUnique();
            o.HasIndex(x => x.SupplierId);
            o.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
            o.HasMany(x => x.Lines).WithOne(x => x.PurchaseOrder).HasForeignKey(x => x.PurchaseOrderId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PurchaseOrderLine>(l =>
        {
            l.HasAuditName("รายการใบสั่งซื้อ");
            l.Property(x => x.LineNo).HasAuditName("ลำดับ");
            l.Property(x => x.PurchaseRequestLineId).HasAuditName("รายการใบขอซื้อ");
            l.Property(x => x.MaterialId).HasAuditName("วัตถุดิบ");
            l.Property(x => x.MaterialName).HasMaxLength(MaterialRegistry.NameLength).HasAuditName("ชื่อวัตถุดิบ");
            l.Property(x => x.Unit).HasMaxLength(MaterialRegistry.UnitLength).HasAuditName("หน่วย");
            l.Property(x => x.RequestedQuantity).HasPrecision(18, 4).HasAuditName("จำนวนที่ขอ");
            l.Property(x => x.Quantity).HasPrecision(18, 4).HasAuditName("จำนวนสั่งซื้อ");
            l.Property(x => x.UnitPrice).HasPrecision(18, 4).HasAuditName("ราคา");
            l.Property(x => x.NetAmount).HasPrecision(18, 4).HasAuditName("จำนวนเงิน");
            l.Property(x => x.TaxIncludedAmount).HasPrecision(18, 2).HasAuditName("ยอดรวม VAT");
            l.Property(x => x.DesiredDate).HasAuditName("วันที่ต้องการ");
            l.Property(x => x.DepartmentId).HasAuditName("แผนก");
            l.Property(x => x.ReceivingCancelled).HasAuditName("ยกเลิกการรับ");
            l.HasIndex(x => x.PurchaseRequestLineId);
            l.HasIndex(x => x.MaterialId);
            // A request line, or a material, that an order uses cannot be deleted (ข้อสมมติ ง).
            l.HasOne(x => x.PurchaseRequestLine).WithMany().HasForeignKey(x => x.PurchaseRequestLineId).OnDelete(DeleteBehavior.Restrict);
            l.HasOne(x => x.Material).WithMany().HasForeignKey(x => x.MaterialId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StockEntry>(e =>
        {
            e.HasAuditName("สมุดสต็อกวัตถุดิบ");
            e.Property(x => x.EntryDate).HasAuditName("วันที่");
            e.Property(x => x.DocumentNo).HasMaxLength(StockLedger.TextLength).HasAuditName("เลขที่");
            e.Property(x => x.DocumentLineNo).HasMaxLength(StockLedger.TextLength).HasAuditName("ลำดับ");
            e.Property(x => x.MaterialId).HasAuditName("วัตถุดิบ");
            e.Property(x => x.Quantity).HasPrecision(18, 4).HasAuditName("จำนวน");
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20).HasAuditName("ชนิดรายการ");
            e.Property(x => x.SupplierId).HasAuditName("ผู้ขาย");
            e.Property(x => x.EmployeeId).HasAuditName("พนักงาน");
            e.Property(x => x.UnitPrice).HasPrecision(18, 4).HasAuditName("ราคา");
            e.Property(x => x.Note).HasMaxLength(StockLedger.TextLength).HasAuditName("หมายเหตุ");
            e.Property(x => x.IsClosed).HasAuditName("ปิดงวดแล้ว");
            e.HasIndex(x => new { x.MaterialId, x.IsClosed });
            e.HasIndex(x => x.DocumentNo);
            e.HasOne(x => x.Material).WithMany().HasForeignKey(x => x.MaterialId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<GoodsReceipt>(r =>
        {
            r.HasAuditName("การรับของ");
            r.Property(x => x.PurchaseOrderLineId).HasAuditName("รายการใบสั่งซื้อ");
            r.Property(x => x.ReceiveDate).HasAuditName("วันที่รับ");
            r.Property(x => x.Quantity).HasPrecision(18, 4).HasAuditName("จำนวนรับ");
            r.Property(x => x.InvoiceNo).HasMaxLength(StockLedger.TextLength).HasAuditName("เลขที่ใบส่งสินค้า");
            r.Property(x => x.InvoiceLineNo).HasMaxLength(StockLedger.TextLength).HasAuditName("ลำดับใบส่งสินค้า");
            r.Property(x => x.UnitPrice).HasPrecision(18, 4).HasAuditName("ราคา");
            r.Property(x => x.NetAmount).HasPrecision(18, 4).HasAuditName("จำนวนเงิน");
            r.Property(x => x.SupplierId).HasAuditName("ผู้ขาย");
            r.Property(x => x.ReturnNote).HasMaxLength(StockLedger.TextLength).HasAuditName("อื่น ๆ (ส่งคืน)");
            r.Property(x => x.MaterialId).HasAuditName("วัตถุดิบ");
            r.Property(x => x.MaterialName).HasMaxLength(MaterialRegistry.NameLength).HasAuditName("ชื่อวัตถุดิบ");
            r.Property(x => x.Unit).HasMaxLength(MaterialRegistry.UnitLength).HasAuditName("หน่วย");
            r.Property(x => x.Failed).HasAuditName("ผลตรวจไม่ผ่าน");
            r.HasIndex(x => x.MaterialId);
            r.HasIndex(x => x.PurchaseOrderLineId);
            r.HasIndex(x => x.ReceiveDate);
            // An order line that was received cannot be deleted (ข้อสมมติ ง).
            r.HasOne(x => x.PurchaseOrderLine).WithMany().HasForeignKey(x => x.PurchaseOrderLineId).OnDelete(DeleteBehavior.Restrict);
            r.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
            r.HasOne(x => x.Material).WithMany().HasForeignKey(x => x.MaterialId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
