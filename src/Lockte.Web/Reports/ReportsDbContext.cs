using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Web.Reports;

/// <summary>Report infrastructure of the host (ADR 0002 table: schema <c>report</c>), like <c>auth</c> and <c>audit</c>.</summary>
public sealed class ReportsDbContext(DbContextOptions<ReportsDbContext> options) : ModuleDbContext(options, Schema)
{
    public const string Schema = "report";

    internal DbSet<DocumentNumber> DocumentNumbers => Set<DocumentNumber>();

    protected override void ConfigureModel(ModelBuilder modelBuilder)
        => modelBuilder.Entity<DocumentNumber>(d =>
        {
            d.HasAuditName(DocumentNumberService.Table);
            d.Property(x => x.Key).HasMaxLength(20).HasAuditName("เอกสาร");
            d.Property(x => x.Name).HasMaxLength(200).HasAuditName("ชื่อเอกสาร");
            d.Property(x => x.Code).HasMaxLength(DocumentNumberService.TextLength).HasAuditName("รหัสเอกสาร");
            d.Property(x => x.Edition).HasMaxLength(DocumentNumberService.TextLength).HasAuditName("ฉบับที่");
            d.HasIndex(x => x.Key).IsUnique();
        });
}
