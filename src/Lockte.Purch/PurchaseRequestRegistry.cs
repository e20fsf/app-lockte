using Lockte.Org;
using Lockte.Purch.Data;
using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Purch;

/// <summary>
/// เลขเอกสารที่ระบบออกให้ไม่ซ้ำแม้หลายคนขอพร้อมกัน (AC-34, Q-40): each series has a counter moved under a database lock.
/// A number taken and never used stays a gap — the legacy proposed the same number to two people instead (BR-010 ⚠ #7).
/// </summary>
public sealed class DocumentNumbers(PurchDbContext db)
{
    private const string Lock = "purch.DocumentNumbers";

    /// <summary>The next number of <paramref name="key"/>: one more than both the counter and <paramref name="floor"/> (the highest number already in use).</summary>
    public async Task<int> NextAsync(string key, int floor)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        var granted = (await db.Database.SqlQuery<int>($"DECLARE @r int; EXEC @r = sp_getapplock @Resource = {Lock}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000; SELECT @r AS Value")
            .ToListAsync()).Single();
        if (granted < 0) throw new InvalidOperationException("ออกเลขเอกสารไม่ได้ในขณะนี้ กรุณาลองใหม่");
        var counter = await db.DocumentCounters.FirstOrDefaultAsync(c => c.Key == key) ?? db.DocumentCounters.Add(new DocumentCounter { Key = key }).Entity;
        counter.Last = Math.Max(counter.Last, floor) + 1;
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return counter.Last;
    }
}

/// <summary>ใบขอซื้อที่อ่านออกไป (ADR 0002): หัวใบและรายการ.</summary>
public sealed record PurchaseRequestDto(
    int Id,
    string RequestNo,
    DateOnly RequestDate,
    int DepartmentId,
    DateOnly? DesiredDate,
    string? PurchasingLocation,
    string? Reason,
    string? Note,
    IReadOnlyList<PurchaseRequestLineDto> Lines,
    byte[] RowVersion);

public sealed record PurchaseRequestLineDto(
    int Id,
    int LineNo,
    int MaterialId,
    string MaterialCode,
    string MaterialName,
    string? Unit,
    decimal Quantity,
    decimal UnitPrice,
    decimal NetAmount,
    decimal? TankWeight);

/// <summary>
/// A line to save. <see cref="MaterialName"/>, <see cref="Unit"/> and <see cref="UnitPrice"/> left null are filled from the register
/// (name, unit, ราคาล่าสุด — BR-011); filled in they are the document's own text.
/// </summary>
public sealed record PurchaseRequestLineInput(
    int MaterialId,
    decimal? Quantity,
    int? LineNo = null,
    string? MaterialName = null,
    string? Unit = null,
    decimal? UnitPrice = null,
    decimal? TankWeight = null);

/// <summary>The fields of SCR-04.</summary>
public sealed record PurchaseRequestInput(
    string RequestNo,
    DateOnly RequestDate,
    int DepartmentId,
    DateOnly? DesiredDate,
    string PurchasingLocation,
    string? Reason,
    string? Note,
    IReadOnlyList<PurchaseRequestLineInput> Lines);

/// <summary>ออกเลขและบันทึกใบขอซื้อ (SCR-03, SCR-04, BR-010, BR-011, BR-014).</summary>
public sealed class PurchaseRequestRegistry(PurchDbContext db, OrgDirectory org, DocumentNumbers numbers, RequestSetupDirectory setup)
{
    public const int NumberLength = 30;

    /// <summary>เลขที่ถัดไปของแผนก: <c>&lt;prefix&gt;&lt;YY พ.ศ.&gt;-&lt;NNNN&gt;</c>, null when the แผนก has no prefix (the user types the number — BR-010 #4).</summary>
    public async Task<string?> ReserveNumberAsync(int departmentId, DateOnly date)
    {
        if (await setup.GetPrefixAsync(departmentId) is not { } prefix) return null;
        var head = Head(prefix, date);
        var used = (await db.PurchaseRequests.AsNoTracking().Where(r => r.RequestNo.StartsWith(head)).Select(r => r.RequestNo).ToListAsync())
            .Select(no => int.TryParse(no[head.Length..], out var n) ? n : 0).DefaultIfEmpty(0).Max();
        var next = await numbers.NextAsync("PR:" + head.TrimEnd('-'), used);
        return head + next.ToString("0000");
    }

    /// <summary>ปี พ.ศ. 2 หลัก of the date (2026 → 69), not read from the machine's regional setting.</summary>
    public static int ThaiYear2(DateOnly date) => (date.Year + 543) % 100;

    private static string Head(string prefix, DateOnly date) => $"{prefix}{ThaiYear2(date):00}-";

    /// <summary>
    /// บันทึกใบขอซื้อเป็นหนึ่ง transaction. Lines with no quantity (blank or 0) are not kept (AC-05) and a request left with no line is not
    /// created at all (<see cref="ServiceResult.Id"/> null). The number may be edited but must not repeat.
    /// </summary>
    public async Task<ServiceResult> SaveAsync(PurchaseRequestInput input)
    {
        var no = Clean(input.RequestNo);
        var errors = new List<string>();
        if (no is null) errors.Add("กรุณากรอกเลขที่ใบขอซื้อ");
        else if (no.Length > NumberLength) errors.Add($"เลขที่ใบขอซื้อยาวได้ไม่เกิน {NumberLength} ตัว");
        else if (await db.PurchaseRequests.AnyAsync(r => r.RequestNo == no)) errors.Add($"มีเลขที่ใบขอซื้อ '{no}' อยู่แล้ว");
        if (await org.GetDepartmentAsync(input.DepartmentId) is null) errors.Add("ไม่พบแผนกที่เลือก");
        var location = Clean(input.PurchasingLocation);
        if (location is null) errors.Add("กรุณาเลือกผู้จัดซื้อ");
        else if ((await setup.GetLocationsAsync()).All(l => l.Code != location)) errors.Add($"ไม่พบผู้จัดซื้อ '{location}'");
        var reason = Clean(input.Reason);
        var note = Clean(input.Note);
        if (reason?.Length > RequestSetupRegistry.TextLength || note?.Length > RequestSetupRegistry.TextLength)
            errors.Add($"เหตุผลและหมายเหตุยาวได้ไม่เกิน {RequestSetupRegistry.TextLength} ตัว");

        var kept = input.Lines.Where(l => l.Quantity is > 0).ToList();
        if (kept.Any(l => l.UnitPrice < 0 || l.TankWeight < 0)) errors.Add("ราคาและนน.ถังต้องไม่ติดลบ");
        var materials = await db.Materials.AsNoTracking().Where(m => kept.Select(l => l.MaterialId).Contains(m.Id)).ToDictionaryAsync(m => m.Id);
        if (kept.Any(l => !materials.ContainsKey(l.MaterialId))) errors.Add("ไม่พบวัตถุดิบที่เลือก");
        if (errors.Count > 0) return new ServiceResult(false, errors);
        if (kept.Count == 0) return ServiceResult.Ok(); // BR-014: a request with no item is not created

        var request = db.PurchaseRequests.Add(new PurchaseRequest
        {
            RequestNo = no!, RequestDate = input.RequestDate, DepartmentId = input.DepartmentId, DesiredDate = input.DesiredDate,
            PurchasingLocation = location, Reason = reason, Note = note,
        }).Entity;
        for (var i = 0; i < kept.Count; i++)
        {
            var line = kept[i];
            var material = materials[line.MaterialId];
            var price = line.UnitPrice ?? material.Cost;
            request.Lines.Add(new PurchaseRequestLine
            {
                LineNo = line.LineNo is > 0 ? line.LineNo.Value : i + 1,
                MaterialId = material.Id,
                MaterialName = Clean(line.MaterialName) ?? material.Name,
                Unit = Clean(line.Unit) ?? material.Unit,
                Quantity = line.Quantity!.Value,
                UnitPrice = price,
                NetAmount = LineNet(price, line.Quantity.Value),
                TankWeight = line.TankWeight,
            });
        }
        await db.SaveChangesAsync();
        return ServiceResult.Ok(request.Id);
    }

    /// <summary>RND-B: price × quantity as the legacy's doubles, not rounded (the same as <c>PurchaseOrderMath.LineNet</c> of #221).</summary>
    private static decimal LineNet(decimal unitPrice, decimal quantity) => (decimal)((double)unitPrice * (double)quantity);

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}

/// <summary>The public, read-only face of ใบขอซื้อ (ADR 0002). Writes go through <see cref="PurchaseRequestRegistry"/>.</summary>
public sealed class PurchaseRequestDirectory(PurchDbContext db)
{
    public async Task<PurchaseRequestDto?> GetAsync(int id) => await Query().FirstOrDefaultAsync(r => r.Id == id) is { } r ? ToDto(r) : null;

    public async Task<PurchaseRequestDto?> FindByNoAsync(string requestNo)
    {
        var no = requestNo.Trim();
        return await Query().FirstOrDefaultAsync(r => r.RequestNo == no) is { } r ? ToDto(r) : null;
    }

    /// <summary>Whether any request was issued for the แผนก.</summary>
    public Task<bool> HasRequestsAsync(int departmentId) => db.PurchaseRequests.AnyAsync(r => r.DepartmentId == departmentId);

    private IQueryable<PurchaseRequest> Query()
        => db.PurchaseRequests.AsNoTracking().Include(r => r.Lines).ThenInclude(l => l.Material);

    private static PurchaseRequestDto ToDto(PurchaseRequest r) => new(
        r.Id, r.RequestNo, r.RequestDate, r.DepartmentId, r.DesiredDate, r.PurchasingLocation, r.Reason, r.Note,
        r.Lines.OrderBy(l => l.LineNo).ThenBy(l => l.Id).Select(l => new PurchaseRequestLineDto(
            l.Id, l.LineNo, l.MaterialId, l.Material!.Code, l.MaterialName, l.Unit, l.Quantity, l.UnitPrice, l.NetAmount, l.TankWeight)).ToList(),
        r.RowVersion);
}
