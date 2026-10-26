using Lockte.SharedKernel.Persistence;

namespace Lockte.Prod.Data;

/// <summary>
/// ชนิดรายการสต็อก — a field of its own, apart from the remark (ข้อสมมติ ช): the legacy kept it in the same column as the free text
/// (<c>MARK</c>), so a remark "pd" typed by hand was deleted by the next transfer.
/// </summary>
public enum StockEntryKind : byte
{
    /// <summary>เพิ่มด้วยมือ (SCR-14): the user types the sign.</summary>
    Manual = 0,
    /// <summary>ผลผลิต <c>pd</c>: from the production reports by โอนผลผลิตเข้าสต็อก (BR-005), +.</summary>
    Production = 1,
    /// <summary>เบิก <c>tr</c>: เบิกเพื่อผลิต (BR-018), −.</summary>
    Withdrawal = 2,
    /// <summary>ยอดยกมา: carried over by ตัดสต็อกสิ้นงวด (BR-007).</summary>
    OpeningBalance = 3,
}

/// <summary>
/// รายการสมุดสต็อก (T-P_ST / T-P_ADDST, #119): one row = one movement, + in, − out; the balance of a product is the sum of its entries
/// that are not closed (BR-006) — the opening balance is an entry too. One table for every year (ข้อตัดสิน #27): ตัดสต็อกสิ้นงวด sets
/// <see cref="ClosedOn"/> on what it carries over (#123). Internal to the module (ADR 0002); the public services are in <see cref="StockDirectory"/>.
/// </summary>
internal sealed class StockEntry : IAudited
{
    public int Id { get; set; }
    public DateOnly? Date { get; set; }
    /// <summary>เลขที่เอกสารอ้างอิง (REF_NO): ใบรายงานผลผลิต / ใบเบิก / what the user typed.</summary>
    public string? DocumentNo { get; set; }
    /// <summary>ลำดับบรรทัด (SUF_NO).</summary>
    public int? LineNo { get; set; }
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    /// <summary>จำนวน: + รับเข้า, − จ่ายออก; may leave the balance below 0 (BR-006).</summary>
    public decimal Quantity { get; set; }
    /// <summary>ผู้ทำรายการ (ID): an employee of module บุคคล, checked on save.</summary>
    public int? EmployeeId { get; set; }
    public StockEntryKind Kind { get; set; }
    /// <summary>หมายเหตุ (REM).</summary>
    public string? Remark { get; set; }
    /// <summary>The ตัดสต็อก date this entry was closed at; null = in the current period, counted in the balance.</summary>
    public DateOnly? ClosedOn { get; set; }

    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    string IHasBusinessKey.BusinessKey => $"{DocumentNo} #{LineNo}".Trim();
}
