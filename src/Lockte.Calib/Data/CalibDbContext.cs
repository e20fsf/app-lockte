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
    internal DbSet<Instrument> Instruments => Set<Instrument>();
    internal DbSet<InstrumentMachine> InstrumentMachines => Set<InstrumentMachine>();

    protected override void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CalibSettings>(s =>
        {
            s.HasAuditName("ค่าตั้งค่าสอบเทียบ");
            s.ToTable("Settings");
            s.Property(x => x.StickerWidthCm).HasPrecision(5, 2).HasAuditName("ความกว้างสติกเกอร์ (ซม.)");
            s.Property(x => x.StickerHeightCm).HasPrecision(5, 2).HasAuditName("ความสูงสติกเกอร์ (ซม.)");
        });

        modelBuilder.Entity<Instrument>(i =>
        {
            i.HasAuditName("เครื่องมือวัด");
            i.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20).HasAuditName("ชนิด");
            i.Property(x => x.Code).HasMaxLength(InstrumentRegistry.CodeLength).HasAuditName("หมายเลขเครื่อง");
            i.Property(x => x.Name).HasMaxLength(InstrumentRegistry.TextLength).HasAuditName("ชื่อเครื่องมือวัด");
            i.Property(x => x.RegisteredOn).HasAuditName("วันที่ขึ้นบัญชี");
            i.Property(x => x.Maker).HasMaxLength(InstrumentRegistry.TextLength).HasAuditName("ยี่ห้อ/ผู้ผลิต");
            i.Property(x => x.DepartmentId).HasAuditName("แผนกที่ใช้งาน");
            i.Property(x => x.Range).HasMaxLength(InstrumentRegistry.TextLength).HasAuditName("พิกัดใช้งาน");
            i.Property(x => x.Resolution).HasMaxLength(InstrumentRegistry.ShortTextLength).HasAuditName("ค่าความละเอียด");
            i.Property(x => x.Limit).HasMaxLength(InstrumentRegistry.ShortTextLength).HasAuditName("ขีดจำกัดที่ยอมรับได้");
            i.Property(x => x.Unit).HasMaxLength(InstrumentRegistry.ShortTextLength).HasAuditName("หน่วย");
            i.Property(x => x.StandardUsed).HasMaxLength(InstrumentRegistry.TextLength).HasAuditName("หมายเลขเครื่องมือที่ใช้");
            i.Property(x => x.CalibratorEmployeeId).HasAuditName("ผู้สอบเทียบ");
            i.Property(x => x.Axis).HasMaxLength(InstrumentRegistry.TextLength).HasAuditName("แกน (mm)");
            i.Property(x => x.ManualNo).HasMaxLength(InstrumentRegistry.ShortTextLength).HasAuditName("คู่มือวิธีการที่ใช้เลขที่");
            i.Property(x => x.Note).HasMaxLength(InstrumentRegistry.TextLength).HasAuditName("หมายเหตุ");
            i.HasIndex(x => x.Code).IsUnique();
            i.HasIndex(x => x.Kind);
            i.HasMany(x => x.Machines).WithOne(x => x.Instrument).HasForeignKey(x => x.InstrumentId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<InstrumentMachine>(m =>
        {
            m.HasAuditName("เครื่องจักรของเครื่องมือวัด");
            m.Property(x => x.MachineId).HasAuditName("เครื่องจักร");
            m.HasIndex(x => new { x.InstrumentId, x.MachineId }).IsUnique();
        });
    }
}
