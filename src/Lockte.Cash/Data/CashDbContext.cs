using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Cash.Data;

/// <summary>
/// module เงินสดย่อย (ADR 0002): เงินสดย่อย, ใบสำคัญจ่าย/ค่าใช้จ่าย, ผู้ซื้อของเก่า, ใบเสร็จขายของเก่า, การนำส่งสำนักงาน — writes only to schema <c>cash</c>.
/// Entities are internal: other modules read through the directories.
/// </summary>
public sealed class CashDbContext(DbContextOptions<CashDbContext> options) : ModuleDbContext(options, Schema)
{
    public const string Schema = "cash";

    internal DbSet<CashSettings> Settings => Set<CashSettings>();
    internal DbSet<ExpenseType> ExpenseTypes => Set<ExpenseType>();

    protected override void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CashSettings>(s =>
        {
            s.HasAuditName("ค่าตั้งค่าเงินสดย่อย");
            s.ToTable("Settings");
            s.Property(x => x.LastRemittedReceiptNo).HasMaxLength(CashSettings.ReceiptNoLength).HasAuditName("เลขที่ใบเสร็จสุดท้ายที่ตัดยอดนำส่งแล้ว");
        });

        modelBuilder.Entity<ExpenseType>(t =>
        {
            t.HasAuditName("ประเภทค่าใช้จ่าย");
            t.ToTable("ExpenseTypes");
            t.Property(x => x.Name).HasMaxLength(ExpenseTypeRegistry.NameLength).HasAuditName("ชื่อค่าใช้จ่าย");
            t.Property(x => x.IsActive).HasAuditName("ใช้งาน");
            t.HasIndex(x => x.Name).IsUnique();
        });
    }
}
