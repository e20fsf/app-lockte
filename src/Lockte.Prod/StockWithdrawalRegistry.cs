using Lockte.Prod.Data;
using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Prod;

/// <summary>
/// A line of a withdrawal slip. <see cref="Id"/> null = a new line; <see cref="Quantity"/> blank drops the line (BR-018) — and deletes it when it exists.
/// The quantity is typed positive or negative: it is kept negative either way (ข้อสมมติ ข).
/// </summary>
public sealed record WithdrawalLineInput(int? Id, byte[]? RowVersion, int? LineNo, int ProductId, decimal? Quantity);

/// <summary>A slip's head is not a row of its own (as in the legacy): วันที่, เลขที่ (typed, no register) and พนักงาน are copied to every line.</summary>
public sealed record WithdrawalSlipInput(string DocumentNo, DateOnly? Date, int? EmployeeId, IReadOnlyList<WithdrawalLineInput> Lines);

public sealed record WithdrawalLineDto(int Id, int LineNo, int ProductId, string ProductCode, string ProductName, string? Unit, decimal Quantity, byte[] RowVersion);

public sealed record WithdrawalSlipDto(string DocumentNo, DateOnly? Date, int? EmployeeId, IReadOnlyList<WithdrawalLineDto> Lines);

/// <summary>A line of the RPT-18 รายงานใบเบิก: <see cref="Quantity"/> is what was taken out (positive).</summary>
public sealed record WithdrawalReportRow(DateOnly? Date, string DocumentNo, int LineNo, string ProductCode, string ProductName, string? Unit, decimal Quantity, int? EmployeeId);

/// <summary>
/// เบิกเพื่อผลิต (SCR-13, BR-018): a slip is entries of kind <see cref="StockEntryKind.Withdrawal"/> in the stock ledger, one per line, taking the stock down.
/// The balance is not checked (⚠ kept) and there is no register of slip numbers.
/// </summary>
public sealed class StockWithdrawalRegistry(ProdDbContext db, IProductionEmployeeLookup? employees = null)
{
    public async Task<ServiceResult> SaveAsync(WithdrawalSlipInput input)
    {
        var errors = new List<string>();
        var number = input.DocumentNo?.Trim() ?? "";
        if (number.Length == 0) errors.Add("กรุณาใส่เลขที่ใบเบิก");
        else if (number.Length > StockRegistry.DocumentNoLength) errors.Add($"เลขที่ยาวได้ไม่เกิน {StockRegistry.DocumentNoLength} ตัว");
        if (input.EmployeeId is { } employeeId && (employees is null || await employees.FindAsync(employeeId) is null)) errors.Add("เลือกพนักงานจากทะเบียนพนักงานเท่านั้น");

        var existing = new Dictionary<int, StockEntry>();
        foreach (var id in input.Lines.Where(l => l.Id is not null).Select(l => l.Id!.Value))
        {
            var entry = await db.StockEntries.FirstOrDefaultAsync(e => e.Id == id && e.Kind == StockEntryKind.Withdrawal);
            if (entry is null) errors.Add("ไม่พบบรรทัดที่แก้ อาจถูกลบไปแล้ว");
            else existing[id] = entry;
        }
        var productIds = input.Lines.Where(l => l.Quantity is not null).Select(l => l.ProductId).Distinct().ToList();
        var known = await db.Products.Where(p => productIds.Contains(p.Id)).Select(p => p.Id).ToListAsync();
        if (productIds.Except(known).Any()) errors.Add("กรุณาเลือกสินค้าที่มีในทะเบียนสินค้าทุกบรรทัด");
        if (errors.Count > 0) return new ServiceResult(false, errors.Distinct().ToList());

        var nextLine = existing.Values.Select(e => e.LineNo ?? 0).DefaultIfEmpty(0).Max();
        foreach (var line in input.Lines)
        {
            existing.TryGetValue(line.Id ?? 0, out var entry);
            if (line.Quantity is not { } quantity)
            {
                if (entry is not null)
                {
                    if (line.RowVersion is not null) entry.ExpectRowVersion(line.RowVersion);
                    db.StockEntries.Remove(entry); // a line whose quantity was cleared
                }
                continue;
            }
            if (entry is null)
            {
                entry = new StockEntry { Kind = StockEntryKind.Withdrawal };
                db.StockEntries.Add(entry);
                entry.LineNo = line.LineNo ?? ++nextLine;
            }
            else
            {
                if (line.RowVersion is not null) entry.ExpectRowVersion(line.RowVersion);
                if (line.LineNo is { } lineNo) entry.LineNo = lineNo;
            }
            entry.Date = input.Date;
            entry.DocumentNo = number;
            entry.EmployeeId = input.EmployeeId;
            entry.ProductId = line.ProductId;
            entry.Quantity = -Math.Abs(quantity); // kept negative whatever sign was typed (ข้อสมมติ ข)
        }
        await db.SaveChangesAsync();
        return ServiceResult.Ok();
    }
}

public sealed class StockWithdrawalDirectory(ProdDbContext db)
{
    /// <summary>A slip by its number: the lines in order. Null when no line has the number. The head is that of the first line.</summary>
    public async Task<WithdrawalSlipDto?> GetSlipAsync(string documentNo)
    {
        var number = documentNo.Trim();
        var lines = await db.StockEntries.AsNoTracking().Include(e => e.Product)
            .Where(e => e.Kind == StockEntryKind.Withdrawal && e.ClosedOn == null && e.DocumentNo == number).OrderBy(e => e.LineNo).ThenBy(e => e.Id).ToListAsync();
        return lines.Count == 0 ? null : new WithdrawalSlipDto(number, lines[0].Date, lines[0].EmployeeId,
            lines.Select(e => new WithdrawalLineDto(e.Id, e.LineNo ?? 0, e.ProductId, e.Product.Code, e.Product.Name, e.Product.Unit, e.Quantity, e.RowVersion)).ToList());
    }

    /// <summary>The numbers of the slips (to choose the one to change), the latest date first.</summary>
    public async Task<IReadOnlyList<string>> GetSlipNumbersAsync(string? text = null, int max = 100)
    {
        var query = db.StockEntries.AsNoTracking().Where(e => e.Kind == StockEntryKind.Withdrawal && e.ClosedOn == null && e.DocumentNo != null);
        if (!string.IsNullOrWhiteSpace(text)) query = query.Where(e => e.DocumentNo!.Contains(text.Trim()));
        return await query.GroupBy(e => e.DocumentNo!).OrderByDescending(g => g.Max(e => e.Date)).ThenByDescending(g => g.Key).Select(g => g.Key).Take(max).ToListAsync();
    }

    /// <summary>The withdrawal lines whose date is in the range (RPT-18), by date, number and line.</summary>
    public async Task<IReadOnlyList<WithdrawalReportRow>> GetReportRowsAsync(DateOnly from, DateOnly to)
        => (await db.StockEntries.AsNoTracking().Include(e => e.Product)
                .Where(e => e.Kind == StockEntryKind.Withdrawal && e.Date >= from && e.Date <= to).ToListAsync())
            .OrderBy(e => e.Date).ThenBy(e => e.DocumentNo, StringComparer.Ordinal).ThenBy(e => e.LineNo)
            .Select(e => new WithdrawalReportRow(e.Date, e.DocumentNo ?? "", e.LineNo ?? 0, e.Product.Code, e.Product.Name, e.Product.Unit, -e.Quantity, e.EmployeeId)).ToList();
}
