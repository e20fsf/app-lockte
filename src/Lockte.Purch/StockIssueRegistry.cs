using Lockte.Purch.Data;
using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Purch;

/// <summary>A line of a ใบเบิกวัตถุดิบ. The quantity is typed positive (or negative); it is stored negative either way (BR-040, AC-41).</summary>
public sealed record StockIssueLineInput(int MaterialId, decimal? Quantity, string? LineNo = null);

/// <summary>
/// หัวใบเบิกและรายการ (SCR-14). <see cref="EmployeeCode"/> is the code of the พนักงานผู้เบิก in module พนักงาน; a code that is not there is refused. The document
/// number is free text (<c>002/0098</c>, <c>87/4350</c> — Q-09) and is not checked for repeats, as before.
/// </summary>
public sealed record StockIssueInput(string DocumentNo, DateOnly Date, string? EmployeeCode, IReadOnlyList<StockIssueLineInput> Lines);

/// <summary>The outcome of a save: like <see cref="ServiceResult"/>, plus the warnings that do not stop it (a balance that went below zero, a material that is not a วัตถุดิบหลัก).</summary>
public sealed record StockIssueResult(bool Succeeded, IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings, int Saved = 0);

/// <summary>A line of a saved ใบเบิก as SCR-15 edits it; <see cref="Quantity"/> is typed as the quantity issued (positive).</summary>
public sealed record StockIssueLineUpdate(int Id, byte[] RowVersion, DateOnly Date, string? LineNo, decimal Quantity, string? EmployeeCode, string? Note);

/// <summary>เบิกวัตถุดิบ (SCR-14, BR-040) และแก้ใบเบิก (SCR-15): rows of kind เบิก in the stock ledger.</summary>
public sealed class StockIssueRegistry(PurchDbContext db, StockLedger ledger, IPurchEmployeeLookup employees)
{
    /// <summary>
    /// บันทึกใบเบิกเป็นหนึ่ง transaction. Every line with a quantity becomes a row of kind เบิก with the quantity made negative; a line with no quantity is left
    /// out ("ใบต่อไป" leaves the blank line, BR-040 ข้อ 3). The balance is not checked and the material need not be a วัตถุดิบหลัก (ข้อสมมติ ข) — the result only warns.
    /// </summary>
    public async Task<StockIssueResult> SaveAsync(StockIssueInput input)
    {
        var errors = new List<string>();
        var documentNo = Clean(input.DocumentNo);
        if (documentNo is null) errors.Add("กรุณากรอกเลขที่ใบเบิก");
        else if (documentNo.Length > StockLedger.TextLength) errors.Add($"เลขที่ใบเบิกยาวได้ไม่เกิน {StockLedger.TextLength} ตัว");
        if (input.Date == default) errors.Add("กรุณากรอกวันที่");
        var (employeeId, employeeError) = await FindEmployeeAsync(input.EmployeeCode);
        if (employeeError is not null) errors.Add(employeeError);
        var kept = input.Lines.Where(l => l.Quantity is { } q && q != 0).ToList();
        var materialIds = kept.Select(l => l.MaterialId).Distinct().ToList();
        var materials = await db.Materials.AsNoTracking().Where(m => materialIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id);
        if (kept.Any(l => !materials.ContainsKey(l.MaterialId))) errors.Add("กรุณาเลือกรหัสวัตถุดิบที่มีในทะเบียน");
        if (kept.Any(l => Clean(l.LineNo)?.Length > StockLedger.TextLength)) errors.Add($"ลำดับยาวได้ไม่เกิน {StockLedger.TextLength} ตัว");
        if (kept.Count == 0 && errors.Count == 0) return new StockIssueResult(true, [], []); // nothing to issue: no slip
        if (errors.Count > 0) return new StockIssueResult(false, errors, []);

        for (var i = 0; i < kept.Count; i++)
            ledger.Add(StockEntryKind.Issue, input.Date, kept[i].MaterialId, -Math.Abs(kept[i].Quantity!.Value), documentNo, Clean(kept[i].LineNo) ?? (i + 1).ToString(), employeeId: employeeId);
        await db.SaveChangesAsync();

        var warnings = new List<string>();
        foreach (var group in kept.GroupBy(l => l.MaterialId))
        {
            var material = materials[group.Key];
            var balance = await db.StockEntries.AsNoTracking().Where(e => e.MaterialId == group.Key && !e.IsClosed).SumAsync(e => (decimal?)e.Quantity) ?? 0;
            if (balance < 0) warnings.Add($"ยอดคงเหลือของ {material.Code} ติดลบ ({balance:#,0.####})");
            if (!material.IsMain) warnings.Add($"{material.Code} ไม่ใช่วัตถุดิบหลัก ไม่เคยเข้าสต็อกตอนรับของ");
        }
        return new StockIssueResult(true, [], warnings, kept.Count);
    }

    /// <summary>
    /// แก้บรรทัดของใบเบิก (SCR-15): date, line number, quantity (typed as issued, stored negative), employee and note of a row of kind เบิก that is not closed.
    /// A stale row throws <see cref="ConcurrencyConflictException"/>.
    /// </summary>
    public async Task<ServiceResult> UpdateLineAsync(StockIssueLineUpdate input)
    {
        var errors = new List<string>();
        if (input.Date == default) errors.Add("กรุณากรอกวันที่");
        if (input.Quantity == 0) errors.Add("กรุณากรอกจำนวนที่เบิก (ถ้าไม่ต้องการบรรทัดนี้ ใช้ปุ่มลบ)");
        if (Clean(input.LineNo)?.Length > StockLedger.TextLength || Clean(input.Note)?.Length > StockLedger.TextLength) errors.Add($"ลำดับและหมายเหตุยาวได้ไม่เกิน {StockLedger.TextLength} ตัว");
        var (employeeId, employeeError) = await FindEmployeeAsync(input.EmployeeCode);
        if (employeeError is not null) errors.Add(employeeError);
        var entry = await db.StockEntries.FirstOrDefaultAsync(e => e.Id == input.Id);
        if (entry is null) errors.Add("ไม่พบรายการนี้ (อาจถูกลบไปแล้ว)");
        else
        {
            entry.ExpectRowVersion(input.RowVersion);
            if (entry.Kind != StockEntryKind.Issue) errors.Add("แก้ได้เฉพาะรายการชนิดเบิก");
            else if (entry.IsClosed) errors.Add("รายการนี้ปิดงวดแล้ว แก้ไม่ได้");
        }
        if (errors.Count > 0) return new ServiceResult(false, errors);

        entry!.EntryDate = input.Date;
        entry.DocumentLineNo = Clean(input.LineNo);
        entry.Quantity = -Math.Abs(input.Quantity);
        entry.EmployeeId = employeeId;
        entry.Note = Clean(input.Note);
        await db.SaveChangesAsync();
        return ServiceResult.Ok(entry.Id);
    }

    private async Task<(int? Id, string? Error)> FindEmployeeAsync(string? code)
    {
        var trimmed = Clean(code);
        if (trimmed is null) return (null, null);
        return await employees.FindByCodeAsync(trimmed) is { } e ? (e.Id, null) : (null, $"ไม่พบรหัสพนักงาน '{trimmed}' ในทะเบียนพนักงาน");
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}

/// <summary>The public, read-only face of ใบเบิก (ADR 0002).</summary>
public sealed class StockIssueDirectory(PurchDbContext db)
{
    /// <summary>เลขที่ใบเบิก that still have open rows of kind เบิก, newest first (what SCR-15's "เลือกเลขที่เอกสาร" lists).</summary>
    public async Task<IReadOnlyList<string>> GetDocumentsAsync()
        => await db.StockEntries.AsNoTracking().Where(e => e.Kind == StockEntryKind.Issue && !e.IsClosed && e.DocumentNo != null)
            .GroupBy(e => e.DocumentNo!).Select(g => new { No = g.Key, Last = g.Max(e => e.EntryDate) })
            .OrderByDescending(g => g.Last).ThenByDescending(g => g.No).Select(g => g.No).ToListAsync();

    /// <summary>The rows of a ใบเบิก by line.</summary>
    public async Task<IReadOnlyList<StockEntryDto>> GetDocumentAsync(string documentNo)
    {
        var no = documentNo.Trim();
        return (await db.StockEntries.AsNoTracking().Include(e => e.Material)
                .Where(e => e.Kind == StockEntryKind.Issue && !e.IsClosed && e.DocumentNo == no).OrderBy(e => e.DocumentLineNo).ThenBy(e => e.Id).ToListAsync())
            .Select(e => new StockEntryDto(e.Id, e.EntryDate, e.DocumentNo, e.DocumentLineNo, e.MaterialId, e.Material!.Code, e.Material.Name, e.Quantity, e.Kind, e.SupplierId, e.EmployeeId,
                e.UnitPrice, e.Note, e.IsClosed, e.RowVersion)).ToList();
    }
}
