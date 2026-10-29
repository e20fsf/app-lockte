using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Calib.Data;

/// <summary>
/// module สอบเทียบ (ADR 0002): เครื่องมือวัด (6 ชนิด) และแบบตรวจ JIG — writes only to schema <c>calib</c>. Employees (ผู้สอบเทียบ),
/// แผนก and เครื่องจักร are read from <c>hr</c>/<c>org</c>, บริษัทที่สอบเทียบ from <c>purch</c>, through their public services.
/// Entities are internal: other modules read through the directories.
/// </summary>
public sealed class CalibDbContext(DbContextOptions<CalibDbContext> options) : ModuleDbContext(options, Schema)
{
    public const string Schema = "calib";

    internal DbSet<CalibSettings> Settings => Set<CalibSettings>();

    protected override void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CalibSettings>(s =>
        {
            s.HasAuditName("ค่าตั้งค่าสอบเทียบ");
            s.ToTable("Settings");
            s.Property(x => x.StickerWidthCm).HasPrecision(5, 2).HasAuditName("ความกว้างสติกเกอร์ (ซม.)");
            s.Property(x => x.StickerHeightCm).HasPrecision(5, 2).HasAuditName("ความสูงสติกเกอร์ (ซม.)");
        });
    }
}
