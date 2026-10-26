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

    protected override void ConfigureModel(ModelBuilder modelBuilder)
    {
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
