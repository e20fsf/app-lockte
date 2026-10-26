using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Sales.Data;

/// <summary>
/// module ขายและส่งของ (ADR 0002) — writes only to schema <c>sales</c>: ทะเบียนลูกค้า (#41, #106) and เขตการขาย (#106).
/// Entities are internal: other modules read through <see cref="CustomerDirectory"/> and <see cref="SalesTerritoryDirectory"/>.
/// </summary>
public sealed class SalesDbContext(DbContextOptions<SalesDbContext> options) : ModuleDbContext(options, Schema)
{
    public const string Schema = "sales";

    internal DbSet<Customer> Customers => Set<Customer>();
    internal DbSet<SalesTerritory> SalesTerritories => Set<SalesTerritory>();
    internal DbSet<SalesOrder> SalesOrders => Set<SalesOrder>();
    internal DbSet<SalesOrderLine> SalesOrderLines => Set<SalesOrderLine>();
    internal DbSet<Delivery> Deliveries => Set<Delivery>();

    protected override void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SalesOrder>(o =>
        {
            o.HasAuditName("ORDER");
            o.Property(x => x.OrderNo).HasMaxLength(SalesOrderRegistry.OrderNoLength).HasAuditName("เลขที่ ORDER");
            o.Property(x => x.OriginalNo).HasMaxLength(SalesOrderRegistry.OrderNoLength).HasAuditName("เลขเดิม");
            o.Property(x => x.OrderDate).HasAuditName("วันที่");
            o.Property(x => x.CustomerId).HasAuditName("ลูกค้า");
            o.Property(x => x.CustomerName).HasMaxLength(CustomerRegistry.NameLength).HasAuditName("ชื่อลูกค้า");
            o.Property(x => x.SalesTerritoryId).HasAuditName("เขตการขาย");
            o.Property(x => x.DeliveryDate).HasAuditName("กำหนดส่ง");
            o.Property(x => x.PaymentTerms).HasMaxLength(SalesOrderRegistry.TextLength).HasAuditName("เงื่อนไขการชำระเงิน");
            o.Property(x => x.Remark).HasMaxLength(SalesOrderRegistry.TextLength).HasAuditName("หมายเหตุ");
            o.Property(x => x.RecordedAt).HasAuditName("เข้าประวัติเมื่อ");
            o.HasIndex(x => x.OrderNo).IsUnique();
            o.HasIndex(x => x.OriginalNo);
            o.HasIndex(x => x.CustomerId);
            o.HasIndex(x => x.OrderDate);
            o.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
            o.HasOne(x => x.SalesTerritory).WithMany().HasForeignKey(x => x.SalesTerritoryId).OnDelete(DeleteBehavior.Restrict);
            o.HasMany(x => x.Lines).WithOne(x => x.SalesOrder).HasForeignKey(x => x.SalesOrderId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Delivery>(d =>
        {
            d.HasAuditName("การส่งของ");
            d.Property(x => x.OrderNo).HasMaxLength(SalesOrderRegistry.OrderNoLength).HasAuditName("เลขที่ ORDER");
            d.Property(x => x.OrderDate).HasAuditName("วันที่ ORDER");
            d.Property(x => x.LineNo).HasAuditName("ลำดับ");
            d.Property(x => x.CustomerId).HasAuditName("ลูกค้า");
            d.Property(x => x.CustomerName).HasMaxLength(CustomerRegistry.NameLength).HasAuditName("ชื่อลูกค้า");
            d.Property(x => x.SalesTerritoryId).HasAuditName("เขตการขาย");
            d.Property(x => x.ProductId).HasAuditName("สินค้า");
            d.Property(x => x.ProductName).HasMaxLength(SalesOrderRegistry.TextLength).HasAuditName("ชื่อสินค้า");
            d.Property(x => x.Unit).HasMaxLength(SalesOrderRegistry.TextLength).HasAuditName("หน่วย");
            d.Property(x => x.UnitPrice).HasPrecision(18, 4).HasAuditName("ราคา");
            d.Property(x => x.DueDate).HasAuditName("กำหนดส่ง");
            d.Property(x => x.DeliveryDate).HasAuditName("วันที่ส่ง");
            d.Property(x => x.InvoiceNo).HasMaxLength(SalesDeliveryRegistry.InvoiceNoLength).HasAuditName("เลขที่บิล");
            d.Property(x => x.InvoiceLine).HasAuditName("ลำดับในบิล");
            d.Property(x => x.Lot).HasMaxLength(SalesDeliveryRegistry.LotLength).HasAuditName("Lot");
            d.Property(x => x.Quantity).HasPrecision(18, 4).HasAuditName("จำนวนส่ง");
            d.Property(x => x.Amount).HasPrecision(24, 8).HasAuditName("มูลค่าส่ง");
            d.Property(x => x.Boxes).HasPrecision(18, 4).HasAuditName("จำนวนกล่อง");
            d.Property(x => x.Complete).HasAuditName("ส่งครบ");
            d.HasIndex(x => x.OrderNo);
            d.HasIndex(x => x.InvoiceNo);
            d.HasIndex(x => x.DeliveryDate);
            d.HasIndex(x => x.CustomerId);
            d.HasIndex(x => x.ProductId);
            d.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SalesOrderLine>(l =>
        {
            l.HasAuditName("บรรทัด ORDER");
            l.Property(x => x.LineNo).HasAuditName("ลำดับ");
            l.Property(x => x.ProductId).HasAuditName("สินค้า");
            l.Property(x => x.ProductName).HasMaxLength(SalesOrderRegistry.TextLength).HasAuditName("ชื่อสินค้า");
            l.Property(x => x.Unit).HasMaxLength(SalesOrderRegistry.TextLength).HasAuditName("หน่วย");
            l.Property(x => x.UnitPrice).HasPrecision(18, 4).HasAuditName("ราคา");
            l.Property(x => x.ReceivedQuantity).HasPrecision(18, 4).HasAuditName("จำนวนที่รับ");
            l.Property(x => x.ReceivedAmount).HasPrecision(24, 8).HasAuditName("มูลค่าที่รับ");
            l.Property(x => x.OutstandingQuantity).HasPrecision(18, 4).HasAuditName("ยอดค้างส่ง");
            l.Property(x => x.OutstandingAmount).HasPrecision(24, 8).HasAuditName("มูลค่าค้างส่ง");
            l.Property(x => x.PendingDeliveryDate).HasAuditName("วันที่ส่ง (ยังไม่บันทึก)");
            l.Property(x => x.PendingInvoiceNo).HasMaxLength(SalesDeliveryRegistry.InvoiceNoLength).HasAuditName("เลขที่บิล (ยังไม่บันทึก)");
            l.Property(x => x.PendingInvoiceLine).HasAuditName("ลำดับในบิล (ยังไม่บันทึก)");
            l.Property(x => x.PendingQuantity).HasPrecision(18, 4).HasAuditName("จำนวนส่ง (ยังไม่บันทึก)");
            l.Property(x => x.PendingAmount).HasPrecision(24, 8).HasAuditName("มูลค่าส่ง (ยังไม่บันทึก)");
            l.Property(x => x.PendingLot).HasMaxLength(SalesDeliveryRegistry.LotLength).HasAuditName("Lot (ยังไม่บันทึก)");
            l.Property(x => x.PendingBoxes).HasPrecision(18, 4).HasAuditName("จำนวนกล่อง (ยังไม่บันทึก)");
            l.HasIndex(x => new { x.SalesOrderId, x.LineNo }).IsUnique();
            l.HasIndex(x => x.ProductId);
        });

        modelBuilder.Entity<SalesTerritory>(t =>
        {
            t.HasAuditName("เขตการขาย");
            t.Property(x => x.Code).HasMaxLength(SalesTerritoryRegistry.CodeLength).HasAuditName("รหัสเขตการขาย");
            t.Property(x => x.Name).HasMaxLength(SalesTerritoryRegistry.NameLength).HasAuditName("ชื่อเขตการขาย");
            t.Property(x => x.EmployeeId).HasAuditName("พนักงานขาย");
            t.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<Customer>(c =>
        {
            c.HasAuditName("ลูกค้า");
            c.Property(x => x.Code).HasMaxLength(CustomerRegistry.CodeLength).HasAuditName("รหัสลูกค้า");
            c.Property(x => x.Name).HasMaxLength(CustomerRegistry.NameLength).HasAuditName("ชื่อลูกค้า");
            c.Property(x => x.CreditDays).HasAuditName("เครดิต (วัน)");
            c.Property(x => x.Address).HasMaxLength(CustomerRegistry.TextLength).HasAuditName("ที่อยู่");
            c.Property(x => x.District).HasMaxLength(CustomerRegistry.TextLength).HasAuditName("อำเภอ");
            c.Property(x => x.Province).HasMaxLength(CustomerRegistry.TextLength).HasAuditName("จังหวัด");
            c.Property(x => x.PostalCode).HasMaxLength(CustomerRegistry.PostalCodeLength).HasAuditName("รหัสไปรษณีย์");
            c.Property(x => x.TransportName).HasMaxLength(CustomerRegistry.TextLength).HasAuditName("ชื่อขนส่ง");
            c.Property(x => x.TransportAddress).HasMaxLength(CustomerRegistry.TextLength).HasAuditName("ที่อยู่ขนส่ง");
            c.Property(x => x.TransportPhone).HasMaxLength(CustomerRegistry.TextLength).HasAuditName("โทรขนส่ง");
            c.Property(x => x.Phone1).HasMaxLength(CustomerRegistry.TextLength).HasAuditName("โทร 1");
            c.Property(x => x.Phone2).HasMaxLength(CustomerRegistry.TextLength).HasAuditName("โทร 2");
            c.Property(x => x.SalesTerritoryId).HasAuditName("เขตการขาย");
            c.Property(x => x.Remark).HasMaxLength(CustomerRegistry.TextLength).HasAuditName("หมายเหตุ");
            c.HasIndex(x => x.Code).IsUnique();
            c.HasIndex(x => x.PostalCode);
            c.HasOne(x => x.SalesTerritory).WithMany().HasForeignKey(x => x.SalesTerritoryId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
