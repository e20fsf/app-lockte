using Lockte.Purch.Data;
using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Purch;

/// <summary>ชนิดรายการของสมุดสต็อก (ข้อสมมติ ช): set by the system, kept apart from the note the user types.</summary>
public enum StockEntryKind
{
    /// <summary>รับ — from a goods receipt of a วัตถุดิบหลัก (BR-031).</summary>
    Receipt = 1,
    /// <summary>เบิก — from a ใบเบิก (BR-040), the quantity negative.</summary>
    Issue = 2,
    /// <summary>ปรับ — typed by hand on SCR-16 (BR-043), the sign as typed.</summary>
    Adjustment = 3,
    /// <summary>ยกมา — the balance carried forward by ตัดสต็อก (BR-042), negative when the stock was overdrawn.</summary>
    OpeningBalance = 4,
}

/// <summary>An employee of module พนักงาน as the stock ledger needs it; the register itself is read through <see cref="IPurchEmployeeLookup"/> (ADR 0002).</summary>
public sealed record PurchEmployeeRef(int Id, string Code, string Name);

/// <summary>Implemented by module พนักงานและค่าแรง; this module does not refer to it.</summary>
public interface IPurchEmployeeLookup
{
    Task<PurchEmployeeRef?> FindAsync(int employeeId);
    Task<PurchEmployeeRef?> FindByCodeAsync(string code);
}

/// <summary>A row of the stock ledger as the screens and other modules read it.</summary>
public sealed record StockEntryDto(
    int Id,
    DateOnly EntryDate,
    string? DocumentNo,
    string? DocumentLineNo,
    int MaterialId,
    string MaterialCode,
    string MaterialName,
    decimal Quantity,
    StockEntryKind Kind,
    int? SupplierId,
    int? EmployeeId,
    decimal? UnitPrice,
    string? Note,
    bool IsClosed,
    byte[] RowVersion);

/// <summary>The balance of a material: the sum of its open rows (BR-041).</summary>
public sealed record StockBalanceDto(int MaterialId, string MaterialCode, string MaterialName, string? Unit, decimal Balance, decimal Cost);

/// <summary>The fields of SCR-16 for a manual row. <see cref="Id"/> null = a new row (kind ปรับ); editing needs <see cref="RowVersion"/> as loaded.</summary>
public sealed record StockEntryInput(
    int? Id,
    DateOnly EntryDate,
    int MaterialId,
    decimal Quantity,
    byte[]? RowVersion,
    string? DocumentNo = null,
    string? DocumentLineNo = null,
    int? EmployeeId = null,
    string? Note = null);

/// <summary>
/// สมุดสต็อกวัตถุดิบ (SCR-16, BR-041, BR-043): the rows the store types by hand. Receiving, issuing and ตัดสต็อก write their rows through the internal
/// <see cref="AddAsync"/> with their own <see cref="StockEntryKind"/>. A material must be one of the register's (DQ-07: no typo'd code); nothing
/// checks that the balance is enough (ข้อสมมติ ข); a manual row keeps the sign it was typed with (AC-40).
/// </summary>
public sealed class StockLedger(PurchDbContext db, IPurchEmployeeLookup employees)
{
    public const int TextLength = 100;

    /// <summary>เพิ่มหรือแก้รายการสต็อกด้วยมือ (kind ปรับ for a new row; an edited row keeps its kind). A closed row cannot be edited.</summary>
    public async Task<ServiceResult> SaveAsync(StockEntryInput input)
    {
        var errors = new List<string>();
        if (input.EntryDate == default) errors.Add("กรุณากรอกวันที่");
        if (input.Quantity == 0) errors.Add("กรุณากรอกจำนวน (บวก = เข้า, ลบ = ออก)");
        var documentNo = Clean(input.DocumentNo);
        var lineNo = Clean(input.DocumentLineNo);
        var note = Clean(input.Note);
        if (documentNo?.Length > TextLength || lineNo?.Length > TextLength || note?.Length > TextLength) errors.Add($"เลขที่ ลำดับ และหมายเหตุยาวได้ไม่เกิน {TextLength} ตัว");
        if (!await db.Materials.AnyAsync(m => m.Id == input.MaterialId)) errors.Add("กรุณาเลือกรหัสวัตถุดิบที่มีในทะเบียน");
        if (input.EmployeeId is { } employeeId && await employees.FindAsync(employeeId) is null) errors.Add("ไม่พบพนักงานที่เลือก");

        StockEntry? entry = null;
        if (input.Id is { } id)
        {
            entry = await db.StockEntries.FirstOrDefaultAsync(e => e.Id == id);
            if (entry is null) errors.Add("ไม่พบรายการนี้ (อาจถูกลบไปแล้ว)");
            else
            {
                entry.ExpectRowVersion(input.RowVersion ?? throw new ArgumentException("Editing needs the rowversion the screen loaded.", nameof(input)));
                if (entry.IsClosed) errors.Add("รายการนี้ปิดงวดแล้ว แก้ไม่ได้");
            }
        }
        if (errors.Count > 0) return new ServiceResult(false, errors);

        entry ??= db.StockEntries.Add(new StockEntry { Kind = StockEntryKind.Adjustment }).Entity;
        entry.EntryDate = input.EntryDate;
        entry.DocumentNo = documentNo;
        entry.DocumentLineNo = lineNo;
        entry.MaterialId = input.MaterialId;
        entry.Quantity = input.Quantity;
        entry.EmployeeId = input.EmployeeId;
        entry.Note = note;
        await db.SaveChangesAsync();
        return ServiceResult.Ok(entry.Id);
    }

    /// <summary>ลบรายการสต็อก (ปุ่มลบพร้อมหน้ายืนยัน). A closed row cannot be deleted; deleting a row that is already gone succeeds.</summary>
    public async Task<ServiceResult> DeleteAsync(int id, byte[] loadedRowVersion)
    {
        if (await db.StockEntries.FirstOrDefaultAsync(e => e.Id == id) is not { } entry) return ServiceResult.Ok();
        entry.ExpectRowVersion(loadedRowVersion);
        if (entry.IsClosed) return ServiceResult.Fail("รายการนี้ปิดงวดแล้ว ลบไม่ได้");
        db.StockEntries.Remove(entry);
        await db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    /// <summary>A row written by receiving, issuing or ตัดสต็อก (same assembly): the sign is the caller's, the kind is the system's. Not saved — the caller saves with its own change.</summary>
    internal StockEntry Add(StockEntryKind kind, DateOnly date, int materialId, decimal quantity, string? documentNo = null, string? documentLineNo = null,
        int? supplierId = null, int? employeeId = null, decimal? unitPrice = null, string? note = null)
        => db.StockEntries.Add(new StockEntry
        {
            Kind = kind, EntryDate = date, MaterialId = materialId, Quantity = quantity, DocumentNo = Clean(documentNo), DocumentLineNo = Clean(documentLineNo),
            SupplierId = supplierId, EmployeeId = employeeId, UnitPrice = unitPrice, Note = Clean(note),
        }).Entity;

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}

/// <summary>The public, read-only face of สมุดสต็อกวัตถุดิบ (ADR 0002). Writes go through <see cref="StockLedger"/>.</summary>
public sealed class StockDirectory(PurchDbContext db)
{
    /// <summary>The open rows of a material, by date → document number → line, as SCR-16 and the stock card list them.</summary>
    public async Task<IReadOnlyList<StockEntryDto>> GetEntriesAsync(int materialId)
        => (await db.StockEntries.AsNoTracking().Include(e => e.Material).Where(e => e.MaterialId == materialId && !e.IsClosed)
                .OrderBy(e => e.EntryDate).ThenBy(e => e.DocumentNo).ThenBy(e => e.DocumentLineNo).ThenBy(e => e.Id).ToListAsync())
            .Select(ToDto).ToList();

    public async Task<StockEntryDto?> GetAsync(int id)
        => await db.StockEntries.AsNoTracking().Include(e => e.Material).FirstOrDefaultAsync(e => e.Id == id) is { } e ? ToDto(e) : null;

    /// <summary>ยอดคงเหลือ of a material = Σ จำนวน of its open rows (BR-041); negative is shown as it is.</summary>
    public async Task<decimal> GetBalanceAsync(int materialId)
        => await db.StockEntries.AsNoTracking().Where(e => e.MaterialId == materialId && !e.IsClosed).SumAsync(e => (decimal?)e.Quantity) ?? 0;

    /// <summary>The materials that have open rows, with their balance — what "เลือกรหัสที่ต้องการแก้ไข" lists.</summary>
    public async Task<IReadOnlyList<StockBalanceDto>> GetBalancesAsync()
        => (await db.StockEntries.AsNoTracking().Where(e => !e.IsClosed)
                .GroupBy(e => e.MaterialId).Select(g => new { MaterialId = g.Key, Balance = g.Sum(e => e.Quantity) }).ToListAsync())
            .Join(await db.Materials.AsNoTracking().ToListAsync(), g => g.MaterialId, m => m.Id, (g, m) => new StockBalanceDto(m.Id, m.Code, m.Name, m.Unit, g.Balance, m.Cost))
            .OrderBy(b => b.MaterialCode).ToList();

    private static StockEntryDto ToDto(StockEntry e) => new(
        e.Id, e.EntryDate, e.DocumentNo, e.DocumentLineNo, e.MaterialId, e.Material!.Code, e.Material.Name, e.Quantity, e.Kind, e.SupplierId, e.EmployeeId, e.UnitPrice, e.Note,
        e.IsClosed, e.RowVersion);
}
