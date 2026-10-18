using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Hr.Data;

/// <summary>
/// module พนักงานและค่าแรง (ADR 0002): พนักงาน เวลาทำงาน การลา ใบเตือน งวดค่าแรง — writes only to schema <c>hr</c>.
/// Screens and tests go through this module's public services, never through this DbContext.
/// </summary>
public sealed class HrDbContext(DbContextOptions<HrDbContext> options) : ModuleDbContext(options, Schema)
{
    public const string Schema = "hr";

    internal DbSet<Employee> Employees => Set<Employee>();
    internal DbSet<WeeklyHoliday> WeeklyHolidays => Set<WeeklyHoliday>();
    internal DbSet<PublicHoliday> PublicHolidays => Set<PublicHoliday>();

    protected override void ConfigureModel(ModelBuilder modelBuilder)
    {
        // Column sizes as in T-EMPLO, so the data migration never truncates.
        modelBuilder.Entity<Employee>(e =>
        {
            e.HasAuditName("พนักงาน");
            e.Property(x => x.Code).HasMaxLength(EmployeeRegistry.CodeLength).HasAuditName("รหัสพนักงาน");
            e.Property(x => x.Name).HasMaxLength(255).HasAuditName("ชื่อ-สกุล");
            e.Property(x => x.EnglishTitle).HasMaxLength(255).HasAuditName("คำนำหน้า (อังกฤษ)");
            e.Property(x => x.EnglishName).HasMaxLength(255).HasAuditName("ชื่ออังกฤษ");
            e.Property(x => x.Sex).HasMaxLength(1).IsFixedLength().HasAuditName("เพศ");
            e.Property(x => x.BirthDate).HasAuditName("วันเกิด");
            e.Property(x => x.NationalId).HasMaxLength(15).HasAuditName("เลขบัตรประชาชน");
            e.Property(x => x.SocialSecurityNo).HasMaxLength(15).HasAuditName("เลขประกันสังคม");
            e.Property(x => x.IdCardPlace).HasMaxLength(255).HasAuditName("ที่ออกบัตร");
            e.Property(x => x.IdCardIssued).HasAuditName("วันออกบัตร");
            e.Property(x => x.RegisteredAddress).HasMaxLength(255).HasAuditName("ที่อยู่ทะเบียนบ้าน");
            e.Property(x => x.CurrentAddress).HasMaxLength(255).HasAuditName("ที่อยู่ปัจจุบัน");
            e.Property(x => x.Telephone).HasMaxLength(255).HasAuditName("โทรศัพท์");
            e.Property(x => x.Nationality).HasMaxLength(50).HasAuditName("สัญชาติ");
            e.Property(x => x.StartDate).HasAuditName("วันเริ่มงาน");
            e.Property(x => x.ListNo).HasMaxLength(4).HasAuditName("ลำดับที่");
            e.Property(x => x.Education).HasMaxLength(12).HasAuditName("วุฒิ");
            e.Property(x => x.DepartmentId).HasAuditName("แผนก");
            e.Property(x => x.SectionId).HasAuditName("ฝ่าย");
            e.Property(x => x.Position).HasMaxLength(18).HasAuditName("ตำแหน่ง");
            e.Property(x => x.Duty).HasMaxLength(255).HasAuditName("หน้าที่");
            e.Property(x => x.EmployeeType).HasAuditName("ประเภท");
            e.Property(x => x.Salary).HasAuditName("เงินเดือน");
            e.Property(x => x.DailyWage).HasAuditName("ค่าแรง/วัน");
            e.Property(x => x.ShiftId).HasAuditName("กะ");
            e.Property(x => x.HotAllowance).HasAuditName("ได้ HOT");
            e.Property(x => x.BankAccountTtb).HasMaxLength(10).HasAuditName("บัญชีทหารไทย");
            e.Property(x => x.BankAccountKbank).HasMaxLength(255).HasAuditName("บัญชีกสิกร");
            e.Property(x => x.Hospital).HasMaxLength(255).HasAuditName("โรงพยาบาล");
            e.Property(x => x.ResignDate).HasAuditName("วันลาออก");
            e.Property(x => x.Memo).HasMaxLength(255).HasAuditName("หมายเหตุ");
            e.Property(x => x.Evaluation1Date).HasAuditName("วันประเมินครั้งที่ 1");
            e.Property(x => x.Evaluation1Score).HasAuditName("คะแนนประเมินครั้งที่ 1");
            e.Property(x => x.Evaluation1Grade).HasMaxLength(50).HasAuditName("เกรดประเมินครั้งที่ 1");
            e.Property(x => x.Evaluation2Date).HasAuditName("วันประเมินครั้งที่ 2");
            e.Property(x => x.Evaluation2Score).HasAuditName("คะแนนประเมินครั้งที่ 2");
            e.Property(x => x.Evaluation2Grade).HasMaxLength(50).HasAuditName("เกรดประเมินครั้งที่ 2");
            e.Property(x => x.ProbationPassedDate).HasAuditName("วันผ่านทดลองงาน");
            e.HasIndex(x => x.Code).IsUnique();
            e.HasIndex(x => x.Name);
            e.HasIndex(x => x.DepartmentId);
            e.HasIndex(x => x.SectionId);
            e.HasIndex(x => x.ShiftId);
        });

        modelBuilder.Entity<WeeklyHoliday>(h =>
        {
            h.HasAuditName("วันหยุดประจำสัปดาห์");
            h.Property(x => x.Date).HasAuditName("วันที่");
            h.Property(x => x.Label).HasMaxLength(CalendarRegistry.WeeklyLabelLength).HasAuditName("ป้าย");
            h.HasIndex(x => x.Date).IsUnique();
        });

        modelBuilder.Entity<PublicHoliday>(h =>
        {
            h.HasAuditName("วันหยุดประเพณี");
            h.Property(x => x.Date).HasAuditName("วันที่");
            h.Property(x => x.Name).HasMaxLength(CalendarRegistry.HolidayNameLength).HasAuditName("ชื่อวันหยุด");
            h.HasIndex(x => x.Date).IsUnique();
        });
    }
}
