using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Org.Data;

/// <summary>
/// module องค์กร (ADR 0002): ฝ่าย แผนก กะ หัวหน้า — writes only to schema <c>org</c>.
/// Entities are internal: other modules read through <see cref="OrgDirectory"/>, never through this DbContext.
/// </summary>
public sealed class OrgDbContext(DbContextOptions<OrgDbContext> options) : ModuleDbContext(options, Schema)
{
    public const string Schema = "org";

    internal DbSet<Section> Sections => Set<Section>();
    internal DbSet<Department> Departments => Set<Department>();
    internal DbSet<Shift> Shifts => Set<Shift>();
    internal DbSet<ShiftSupervisor> ShiftSupervisors => Set<ShiftSupervisor>();

    protected override void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Section>(s =>
        {
            s.HasAuditName("ฝ่าย");
            s.Property(x => x.Name).HasMaxLength(100).HasAuditName("ชื่อฝ่าย");
            s.Property(x => x.SortOrder).HasAuditName("ลำดับ");
            s.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<Department>(d =>
        {
            d.HasAuditName("แผนก");
            d.Property(x => x.Code).HasMaxLength(OrgRegistry.DepartmentCodeLength).HasAuditName("รหัสแผนก");
            d.Property(x => x.Name).HasMaxLength(100).HasAuditName("ชื่อแผนก");
            d.Property(x => x.EmployeeIdPrefix).HasMaxLength(1).IsFixedLength().HasAuditName("ตัวอักษรนำหน้ารหัสพนักงาน");
            d.Property(x => x.SortOrder).HasAuditName("ลำดับ");
            d.Property(x => x.SectionId).HasAuditName("ฝ่าย");
            d.Property(x => x.HeadEmployeeId).HasAuditName("หัวหน้าแผนก");
            d.Property(x => x.ManagerName).HasMaxLength(100).HasAuditName("ผู้จัดการ");
            d.Property(x => x.FactoryManagerName).HasMaxLength(100).HasAuditName("ผู้จัดการโรงงาน");
            d.HasIndex(x => x.Code).IsUnique();
            d.HasIndex(x => x.Name).IsUnique();
            d.HasOne(x => x.Section).WithMany().HasForeignKey(x => x.SectionId).OnDelete(DeleteBehavior.Restrict);
            d.HasMany(x => x.ShiftSupervisors).WithOne(x => x.Department).HasForeignKey(x => x.DepartmentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Shift>(s =>
        {
            s.HasAuditName("กะ");
            s.Property(x => x.Code).HasMaxLength(3).HasAuditName("รหัสกะ");
            s.Property(x => x.WorkIn).HasAuditName("เวลาเข้า");
            s.Property(x => x.WorkOut).HasAuditName("เวลาออก");
            s.Property(x => x.BreakStart).HasAuditName("เริ่มพัก");
            s.Property(x => x.BreakEnd).HasAuditName("เลิกพัก");
            s.Property(x => x.OvertimeIn).HasAuditName("เริ่ม OT");
            s.Property(x => x.OvertimeOut).HasAuditName("เลิก OT");
            s.Property(x => x.RestMinutes).HasAuditName("นาทีพัก");
            s.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<ShiftSupervisor>(h =>
        {
            h.HasAuditName("หัวหน้ากะ");
            h.Property(x => x.DepartmentId).HasAuditName("แผนก");
            h.Property(x => x.ShiftId).HasAuditName("กะ");
            h.Property(x => x.EmployeeId).HasAuditName("หัวหน้ากะ");
            h.HasIndex(x => new { x.DepartmentId, x.ShiftId }).IsUnique();
            h.HasOne(x => x.Shift).WithMany().HasForeignKey(x => x.ShiftId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
