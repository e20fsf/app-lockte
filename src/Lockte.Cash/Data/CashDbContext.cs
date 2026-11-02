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
    internal DbSet<ScrapBuyer> ScrapBuyers => Set<ScrapBuyer>();
    internal DbSet<ScrapItem> ScrapItems => Set<ScrapItem>();
    internal DbSet<ExpenseLine> ExpenseLines => Set<ExpenseLine>();
    internal DbSet<ScrapReceipt> ScrapReceipts => Set<ScrapReceipt>();
    internal DbSet<ScrapReceiptLine> ScrapReceiptLines => Set<ScrapReceiptLine>();

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

        modelBuilder.Entity<ExpenseLine>(l =>
        {
            l.HasAuditName("ค่าใช้จ่ายเงินสดย่อย");
            l.ToTable("ExpenseLines");
            l.Property(x => x.Date).HasAuditName("วันที่");
            l.Property(x => x.VoucherNo).HasMaxLength(ExpenseLineRegistry.VoucherNoLength).HasAuditName("เลขที่");
            l.Property(x => x.Name).HasMaxLength(ExpenseLineRegistry.NameLength).HasAuditName("รายการ");
            l.Property(x => x.Qty).HasPrecision(18, 3).HasAuditName("จำนวน");
            l.Property(x => x.Unit).HasMaxLength(ExpenseLineRegistry.UnitLength).HasAuditName("หน่วย");
            l.Property(x => x.UnitPrice).HasPrecision(18, 4).HasAuditName("ราคา");
            l.Property(x => x.Amount).HasPrecision(18, 2).HasAuditName("จำนวนเงิน");
            l.Property(x => x.ExpenseTypeId).HasAuditName("ประเภท (รหัสภายใน)");
            l.HasOne(x => x.ExpenseType).WithMany().HasForeignKey(x => x.ExpenseTypeId).OnDelete(DeleteBehavior.Restrict);
            l.HasIndex(x => new { x.Date, x.VoucherNo });
            l.HasIndex(x => x.ExpenseTypeId);
        });

        modelBuilder.Entity<ScrapReceipt>(r =>
        {
            r.HasAuditName("ใบเสร็จขายของเก่า");
            r.ToTable("ScrapReceipts");
            r.Property(x => x.No).HasMaxLength(ScrapReceiptMath.MaxNumberLength).HasAuditName("เลขที่");
            r.Property(x => x.Date).HasAuditName("วันที่");
            r.Property(x => x.BuyerName).HasMaxLength(ScrapReceiptRegistry.BuyerNameLength).HasAuditName("ชื่อผู้ซื้อ");
            r.Property(x => x.BuyerAddress).HasMaxLength(ScrapReceiptRegistry.AddressLength).HasAuditName("ที่อยู่");
            r.Property(x => x.BuyerTel).HasMaxLength(ScrapReceiptRegistry.TelLength).HasAuditName("เบอร์โทรศัพท์");
            r.Property(x => x.ScrapBuyerId).HasAuditName("ผู้ซื้อในทะเบียน (รหัสภายใน)");
            r.Property(x => x.IsCancelled).HasAuditName("ยกเลิก");
            r.Property(x => x.CancelReason).HasMaxLength(500).HasAuditName("เหตุผลที่ยกเลิก");
            r.Property(x => x.CancelledBy).HasMaxLength(256).HasAuditName("ผู้ยกเลิก");
            r.Property(x => x.CancelledAt).HasAuditName("ยกเลิกเมื่อ");
            r.HasIndex(x => x.No).IsUnique();
            r.HasIndex(x => x.Date);
            r.HasOne<ScrapBuyer>().WithMany().HasForeignKey(x => x.ScrapBuyerId).OnDelete(DeleteBehavior.SetNull);
            r.HasMany(x => x.Lines).WithOne(x => x.Receipt).HasForeignKey(x => x.ReceiptId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ScrapReceiptLine>(l =>
        {
            l.HasAuditName("บรรทัดใบเสร็จขายของเก่า");
            l.ToTable("ScrapReceiptLines");
            l.Property(x => x.Seq).HasAuditName("ลำดับที่");
            l.Property(x => x.ItemName).HasMaxLength(ScrapReceiptRegistry.ItemNameLength).HasAuditName("รายการ");
            l.Property(x => x.ScrapItemId).HasAuditName("ชนิดในทะเบียน (รหัสภายใน)");
            l.Property(x => x.Unit).HasMaxLength(ScrapReceiptRegistry.UnitLength).HasAuditName("หน่วย");
            l.Property(x => x.Qty).HasPrecision(18, 3).HasAuditName("จำนวน");
            l.Property(x => x.UnitPrice).HasPrecision(18, 4).HasAuditName("ราคา");
            l.Property(x => x.Amount).HasPrecision(18, 2).HasAuditName("จำนวนเงิน");
            l.HasIndex(x => new { x.ReceiptId, x.Seq }).IsUnique();
            l.HasOne<ScrapItem>().WithMany().HasForeignKey(x => x.ScrapItemId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ScrapBuyer>(b =>
        {
            b.HasAuditName("ผู้ซื้อของเก่า");
            b.ToTable("ScrapBuyers");
            b.Property(x => x.Name).HasMaxLength(ScrapBuyerRegistry.NameLength).HasAuditName("ชื่อผู้ซื้อ");
            b.Property(x => x.Address).HasMaxLength(ScrapBuyerRegistry.AddressLength).HasAuditName("ที่อยู่");
            b.Property(x => x.Tel).HasMaxLength(ScrapBuyerRegistry.TelLength).HasAuditName("เบอร์โทรศัพท์");
            b.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<ScrapItem>(i =>
        {
            i.HasAuditName("ชนิดของเก่า");
            i.ToTable("ScrapItems");
            i.Property(x => x.Name).HasMaxLength(ScrapItemRegistry.NameLength).HasAuditName("ชื่อของเก่า");
            i.Property(x => x.Unit).HasMaxLength(ScrapItemRegistry.UnitLength).HasAuditName("หน่วย");
            i.HasIndex(x => x.Name).IsUnique();
        });
    }
}
