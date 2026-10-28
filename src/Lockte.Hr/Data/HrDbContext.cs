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
    internal DbSet<Absence> Absences => Set<Absence>();
    internal DbSet<WorkTime> WorkTimes => Set<WorkTime>();
    internal DbSet<Leave> Leaves => Set<Leave>();
    internal DbSet<PayPeriod> PayPeriods => Set<PayPeriod>();
    internal DbSet<PayPeriodTimeTotal> PayPeriodTimeTotals => Set<PayPeriodTimeTotal>();
    internal DbSet<PayResult> PayResults => Set<PayResult>();
    internal DbSet<Warning> Warnings => Set<Warning>();
    internal DbSet<SlipSigner> SlipSigners => Set<SlipSigner>();

    protected override void ConfigureModel(ModelBuilder modelBuilder)
    {
        // Column sizes as in T-EMPLO, so the data migration never truncates.
        modelBuilder.Entity<Employee>(e =>
        {
            e.HasAuditName("พนักงาน");
            e.Property(x => x.Code).HasMaxLength(EmployeeRegistry.CodeLength).HasAuditName("รหัสพนักงาน");
            e.Property(x => x.Name).HasMaxLength(255).HasAuditName("ชื่อ-สกุล");
            e.Property(x => x.IsMigrationStub).HasAuditName("พนักงานตัวแทนจากการย้ายข้อมูล");
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
            h.Property(x => x.Name).HasMaxLength(CalendarRegistry.PublicHolidayNameLength).HasAuditName("ชื่อวันหยุด");
            h.HasIndex(x => x.Date).IsUnique();
        });

        // Not unique on (Date, Employee): the legacy data may repeat a pair; AbsenceRegistry stops new repeats.
        modelBuilder.Entity<Absence>(a =>
        {
            a.HasAuditName("ไม่มาทำงาน");
            a.Property(x => x.Date).HasAuditName("วันที่");
            a.Property(x => x.EmployeeId).HasAuditName("พนักงาน");
            a.Property(x => x.WorkIn).HasAuditName("เวลาเข้า");
            a.Property(x => x.Remark).HasMaxLength(AbsenceRegistry.RemarkLength).HasAuditName("หมายเหตุ");
            a.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            a.HasIndex(x => new { x.Date, x.EmployeeId });
        });

        // One table for every year (#41); rows may repeat until BR-007 removes them.
        modelBuilder.Entity<WorkTime>(w =>
        {
            w.HasAuditName(WorkTimeRegistry.WorkTimeTable);
            w.Property(x => x.Date).HasAuditName("วันที่");
            w.Property(x => x.EmployeeId).HasAuditName("พนักงาน");
            w.Property(x => x.Type).HasMaxLength(WorkTypes.Length).HasAuditName("ประเภท");
            w.Property(x => x.WorkIn).HasAuditName("เวลาเข้า");
            w.Property(x => x.WorkOut).HasAuditName("เวลาออก");
            w.Property(x => x.Late).HasAuditName("เวลาเข้าจริง (สาย)");
            w.Property(x => x.RestMinutes).HasAuditName("นาทีพัก");
            w.Property(x => x.Mark).HasMaxLength(2).HasAuditName("ลูกค้างานรับจ้าง");
            w.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            w.HasIndex(x => new { x.Date, x.EmployeeId });
            w.HasIndex(x => new { x.EmployeeId, x.Date });
        });

        // One table for every year (#41); not unique on (employee, FROM_D, TO_D, FROM_T): the legacy data may repeat.
        modelBuilder.Entity<Leave>(l =>
        {
            l.HasAuditName(LeaveRegistry.LeaveTable);
            l.Property(x => x.RecordedOn).HasAuditName("วันที่บันทึก");
            l.Property(x => x.EmployeeId).HasAuditName("พนักงาน");
            l.Property(x => x.FromDate).HasAuditName("ตั้งแต่วันที่");
            l.Property(x => x.ToDate).HasAuditName("ถึงวันที่");
            l.Property(x => x.FromTime).HasAuditName("ตั้งแต่เวลา");
            l.Property(x => x.ToTime).HasAuditName("ถึงเวลา");
            l.Property(x => x.Days).HasAuditName("จำนวนวัน");
            l.Property(x => x.Type).HasMaxLength(LeaveTypes.Length).HasAuditName("ประเภท");
            l.Property(x => x.Paid).HasAuditName("ได้ค่าแรง");
            l.Property(x => x.Remark).HasMaxLength(LeaveRegistry.RemarkLength).HasAuditName("เหตุผล");
            l.Property(x => x.DailyWage).HasAuditName("ค่าแรง/วัน");
            l.Property(x => x.Pay).HasAuditName("จ่ายค่าแรง");
            l.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            l.HasIndex(x => new { x.EmployeeId, x.FromDate });
            l.HasIndex(x => x.FromDate);
        });

        modelBuilder.Entity<PayPeriod>(p =>
        {
            p.HasAuditName(PayPeriodRegistry.PayPeriodTable);
            p.Property(x => x.FromDate).HasAuditName("จากวันที่");
            p.Property(x => x.ToDate).HasAuditName("ถึงวันที่");
            p.Property(x => x.PayDate).HasAuditName("วันที่จ่าย");
            p.Property(x => x.PartDays).HasAuditName("จำนวนวันที่จ่าย (ช่วงคร่อมปีใหม่)");
            p.Property(x => x.CompletedStep).HasAuditName("ขั้นที่ทำแล้ว");
            p.Property(x => x.TimeCalculatedAt).HasAuditName("คำนวณเวลาเมื่อ");
            p.Property(x => x.TimeCalculatedBy).HasMaxLength(ModelBuilderExtensions.UserNameLength).HasAuditName("คำนวณเวลาโดย");
            p.Property(x => x.PayCalculatedAt).HasAuditName("คำนวณค่าแรงเมื่อ");
            p.Property(x => x.PayCalculatedBy).HasMaxLength(ModelBuilderExtensions.UserNameLength).HasAuditName("คำนวณค่าแรงโดย");
            p.Property(x => x.Migrated).HasAuditName("ย้ายมาจากระบบเดิม");
            p.HasIndex(x => x.FromDate).IsUnique();
        });

        // Written only by คำนวณค่าแรง (one history event) and the data migration; one row per employee per period.
        modelBuilder.Entity<PayResult>(r =>
        {
            r.HasAuditName("ค่าแรงของงวด");
            r.Property(x => x.PayNormal).HasPrecision(18, 2);
            r.Property(x => x.PayOvertime).HasPrecision(18, 2);
            r.Property(x => x.OvertimePay).HasPrecision(18, 2);
            r.Property(x => x.SundayPay).HasPrecision(18, 2);
            r.Property(x => x.SundayOvertimePay).HasPrecision(18, 2);
            r.Property(x => x.HolidayPay).HasPrecision(18, 2);
            r.Property(x => x.HolidayOvertimePay).HasPrecision(18, 2);
            r.Property(x => x.Hot).HasPrecision(18, 2);
            r.HasOne(x => x.PayPeriod).WithMany().HasForeignKey(x => x.PayPeriodId).OnDelete(DeleteBehavior.Cascade);
            r.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            r.HasIndex(x => new { x.PayPeriodId, x.EmployeeId }).IsUnique();
            r.HasIndex(x => x.EmployeeId);
        });

        modelBuilder.Entity<SlipSigner>(s =>
        {
            s.HasAuditName("ผู้ลงนามบนสลิป");
            s.Property(x => x.Role).HasAuditName("ตำแหน่งผู้ลงนาม");
            s.Property(x => x.Name).HasMaxLength(SlipSignerRegistry.NameLength).HasAuditName("ชื่อผู้ลงนาม");
            s.Property(x => x.Signature).HasAuditName("รูปลายเซ็น");
            s.Property(x => x.SignatureContentType).HasMaxLength(50).HasAuditName("ชนิดรูปลายเซ็น");
            s.HasIndex(x => x.Role).IsUnique();
        });

        // Written only by the calculation (one history event); one row per employee per period.
        modelBuilder.Entity<PayPeriodTimeTotal>(t =>
        {
            t.HasAuditName("เวลาทำงานรวมของงวด");
            t.HasOne(x => x.PayPeriod).WithMany().HasForeignKey(x => x.PayPeriodId).OnDelete(DeleteBehavior.Cascade);
            t.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            t.HasIndex(x => new { x.PayPeriodId, x.EmployeeId }).IsUnique();
            t.HasIndex(x => x.EmployeeId);
        });

        // One table for every year (#41).
        modelBuilder.Entity<Warning>(w =>
        {
            w.HasAuditName(WarningRegistry.WarningTable);
            w.Property(x => x.Date).HasAuditName("วันที่");
            w.Property(x => x.EmployeeId).HasAuditName("พนักงาน");
            w.Property(x => x.DepartmentId).HasAuditName("แผนก");
            w.Property(x => x.Level).HasMaxLength(WarningLevels.Length).HasAuditName("ระดับ");
            w.Property(x => x.Remark).HasMaxLength(WarningRegistry.RemarkLength).HasAuditName("รายละเอียด");
            w.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            w.HasIndex(x => new { x.EmployeeId, x.Date });
            w.HasIndex(x => x.Date);
            w.HasIndex(x => x.DepartmentId);
        });
    }
}
