using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Sales.Data;

/// <summary>
/// module ขายและส่งของ (ADR 0002) — writes only to schema <c>sales</c>. So far only the minimal ทะเบียนลูกค้า (#41).
/// Entities are internal: other modules read through <see cref="CustomerDirectory"/>.
/// </summary>
public sealed class SalesDbContext(DbContextOptions<SalesDbContext> options) : ModuleDbContext(options, Schema)
{
    public const string Schema = "sales";

    internal DbSet<Customer> Customers => Set<Customer>();

    protected override void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>(c =>
        {
            c.HasAuditName("ลูกค้า");
            c.Property(x => x.Code).HasMaxLength(CustomerRegistry.CodeLength).HasAuditName("รหัสลูกค้า");
            c.Property(x => x.Name).HasMaxLength(CustomerRegistry.NameLength).HasAuditName("ชื่อลูกค้า");
            c.HasIndex(x => x.Code).IsUnique();
        });
    }
}
