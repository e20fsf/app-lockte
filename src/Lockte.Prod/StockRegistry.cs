using Lockte.Prod.Data;
using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Prod;

/// <summary><see cref="Id"/> null = a new entry added by hand (เพิ่มSTOCK); editing needs <see cref="RowVersion"/> as loaded.</summary>
public sealed record StockEntryInput(
    int? Id,
    byte[]? RowVersion,
    DateOnly? Date,
    string? DocumentNo,
    int? LineNo,
    int ProductId,
    decimal Quantity,
    int? EmployeeId,
    string? Remark);

public sealed record StockEntryDto(
    int Id,
    DateOnly? Date,
    string? DocumentNo,
    int? LineNo,
    int ProductId,
    string ProductCode,
    string ProductName,
    decimal Quantity,
    int? EmployeeId,
    StockEntryKind Kind,
    string? Remark,
    DateOnly? ClosedOn,
    byte[] RowVersion);

/// <summary>A product of the stock with its balance (Σ of the entries not closed).</summary>
public sealed record StockBalance(int ProductId, string ProductCode, string ProductName, string? Unit, decimal Quantity);

/// <summary>
/// What a transfer did: reports marked as in the stock, entries made, lines left out (no product) and whether the range touches a period
/// that was already closed — a warning, never a stop (⚠ BR-005, ข้อสมมติ ซ).
/// </summary>
public sealed record StockTransferResult(int Reports, int Entries, int LinesWithoutProduct, int ClosedEntriesInRange, bool Busy = false)
{
    public bool OverlapsClosedPeriod => ClosedEntriesInRange > 0;
}

/// <summary>The stock ledger (T-P_ST): entries added and changed by hand (SCR-14, BR-006) and the transfer of the production (SCR-16, BR-005).</summary>
public sealed class StockRegistry(ProdDbContext db, AuditScope audit, IProductionEmployeeLookup? employees = null)
{
    public const int DocumentNoLength = 50;
    public const int RemarkLength = 100;

    private const string TransferLock = "lockte:prod:stock-transfer";

    /// <summary>เพิ่มSTOCK / แก้ไขSTOCK: a free entry (kind <see cref="StockEntryKind.Manual"/>), the sign typed by the user.</summary>
    public async Task<ServiceResult> SaveAsync(StockEntryInput input)
    {
        var errors = new List<string>();
        var documentNo = Clean(input.DocumentNo);
        var remark = Clean(input.Remark);
        if (documentNo?.Length > DocumentNoLength) errors.Add($"เลขที่ยาวได้ไม่เกิน {DocumentNoLength} ตัว");
        if (remark?.Length > RemarkLength) errors.Add($"หมายเหตุยาวได้ไม่เกิน {RemarkLength} ตัว");
        if (!await db.Products.AnyAsync(p => p.Id == input.ProductId)) errors.Add("กรุณาเลือกสินค้าที่มีในทะเบียนสินค้า");
        if (input.EmployeeId is { } employeeId && (employees is null || await employees.FindAsync(employeeId) is null)) errors.Add("เลือกพนักงานจากทะเบียนพนักงานเท่านั้น");
        if (errors.Count > 0) return new ServiceResult(false, errors);

        StockEntry entry;
        if (input.Id is { } id)
        {
            if (await db.StockEntries.FirstOrDefaultAsync(e => e.Id == id) is not { } found) return ServiceResult.Fail("ไม่พบรายการสต็อกนี้ อาจถูกลบไปแล้ว");
            entry = found;
            entry.ExpectRowVersion(input.RowVersion ?? []);
        }
        else
        {
            entry = new StockEntry { Kind = StockEntryKind.Manual };
            db.StockEntries.Add(entry);
        }
        entry.Date = input.Date;
        entry.DocumentNo = documentNo;
        entry.LineNo = input.LineNo;
        entry.ProductId = input.ProductId;
        entry.Quantity = input.Quantity;
        entry.EmployeeId = input.EmployeeId;
        entry.Remark = remark;
        await db.SaveChangesAsync();
        return ServiceResult.Ok(entry.Id);
    }

    public async Task<ServiceResult> DeleteAsync(int id, byte[] loadedRowVersion)
    {
        if (await db.StockEntries.FirstOrDefaultAsync(e => e.Id == id) is not { } entry) return ServiceResult.Ok();
        entry.ExpectRowVersion(loadedRowVersion);
        db.StockEntries.Remove(entry);
        await db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    /// <summary>
    /// โอนผลผลิตเข้าสต็อก (BR-005): <b>delete and make again</b> for every แผนก in the range, so running the same range twice is the same as
    /// once — the reports of the range are marked "เข้าสต็อกแล้ว" and the production entries of the range (not closed) are replaced by one per
    /// line, quantity = จำนวนรวม (ดี + เสีย, ❓ Q-17 kept), dated as the report. One person at a time: a second one is told it is busy.
    /// What the range holds of a closed period is not touched and is reported as a warning.
    /// </summary>
    public async Task<StockTransferResult> TransferProductionAsync(DateOnly from, DateOnly to)
    {
        if (from > to) throw new ArgumentException("ตั้งแต่วันที่ต้องไม่หลังถึงวันที่");
        await using var transaction = await db.Database.BeginTransactionAsync();
        // sp_getapplock is not composable SQL: read its single row as it comes.
        var granted = (await db.Database.SqlQuery<int>($"DECLARE @r int; EXEC @r = sp_getapplock @Resource = {TransferLock}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 0; SELECT @r AS Value")
            .ToListAsync()).Single();
        if (granted < 0) return new StockTransferResult(0, 0, 0, 0, Busy: true);

        var closed = await db.StockEntries.CountAsync(e => e.Kind == StockEntryKind.Production && e.ClosedOn != null && e.Date >= from && e.Date <= to);
        await db.StockEntries.Where(e => e.Kind == StockEntryKind.Production && e.ClosedOn == null && e.Date >= from && e.Date <= to).ExecuteDeleteAsync();

        var stamp = DateTimeOffset.UtcNow;
        var reports = await db.ProductionReports.Include(r => r.Lines).Where(r => r.ReportDate >= from && r.ReportDate <= to).ToListAsync();
        var entries = 0;
        var withoutProduct = 0;
        using (audit.SuppressRowChanges()) // derived data, made again at will: no row-by-row history
        {
            foreach (var report in reports)
            {
                report.StockedAt = stamp;
                foreach (var line in report.Lines)
                {
                    if (line.ProductId is not { } productId) { withoutProduct++; continue; }
                    db.StockEntries.Add(new StockEntry
                    {
                        Date = report.ReportDate, DocumentNo = report.ReportNo, LineNo = line.LineNo, ProductId = productId, Quantity = line.TotalQuantity,
                        EmployeeId = line.EmployeeId, Kind = StockEntryKind.Production,
                    });
                    entries++;
                }
            }
            await db.SaveChangesAsync();
        }
        await transaction.CommitAsync();
        return new StockTransferResult(reports.Count, entries, withoutProduct, closed);
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}

/// <summary>Reads of the stock ledger for the screens and for the other tickets of module ผลิต (stock reports, withdrawals, closing).</summary>
public sealed class StockDirectory(ProdDbContext db)
{
    /// <summary>ยอดคงเหลือ of a product = Σ จำนวน of the entries not closed; 0 when it has none (BR-006).</summary>
    public async Task<decimal> GetBalanceAsync(int productId)
        => await db.StockEntries.Where(e => e.ProductId == productId && e.ClosedOn == null).SumAsync(e => (decimal?)e.Quantity) ?? 0;

    /// <summary>The products that have an entry in the current period, with their balance, by code. <paramref name="nonZeroOnly"/> as the reports of the legacy (RPT-11).</summary>
    public async Task<IReadOnlyList<StockBalance>> GetBalancesAsync(bool nonZeroOnly = false)
    {
        var rows = await db.StockEntries.AsNoTracking().Where(e => e.ClosedOn == null)
            .GroupBy(e => new { e.ProductId, e.Product.Code, e.Product.Name, e.Product.Unit })
            .Select(g => new StockBalance(g.Key.ProductId, g.Key.Code, g.Key.Name, g.Key.Unit, g.Sum(e => e.Quantity)))
            .ToListAsync();
        return rows.Where(r => !nonZeroOnly || r.Quantity != 0).OrderBy(r => r.ProductCode, StringComparer.Ordinal).ToList();
    }

    /// <summary>Every entry of the product in the current period, oldest first (แก้ไขSTOCK).</summary>
    public async Task<IReadOnlyList<StockEntryDto>> GetEntriesAsync(int productId, bool includeClosed = false)
        => (await db.StockEntries.AsNoTracking().Include(e => e.Product).Where(e => e.ProductId == productId && (includeClosed || e.ClosedOn == null))
            .OrderBy(e => e.Date).ThenBy(e => e.DocumentNo).ThenBy(e => e.LineNo).ThenBy(e => e.Id).ToListAsync())
            .Select(ToDto).ToList();

    public async Task<StockEntryDto?> GetAsync(int id)
        => await db.StockEntries.AsNoTracking().Include(e => e.Product).FirstOrDefaultAsync(e => e.Id == id) is { } e ? ToDto(e) : null;

    private static StockEntryDto ToDto(StockEntry e)
        => new(e.Id, e.Date, e.DocumentNo, e.LineNo, e.ProductId, e.Product.Code, e.Product.Name, e.Quantity, e.EmployeeId, e.Kind, e.Remark, e.ClosedOn, e.RowVersion);
}
