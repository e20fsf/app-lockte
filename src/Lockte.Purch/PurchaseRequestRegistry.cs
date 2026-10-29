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
    decimal? TankWeight,
    byte[] RowVersion);

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

/// <summary>
/// A line of a request being edited (SCR-06). <see cref="Id"/> null = a new line (dropped when it has no quantity); an existing one keeps the
/// <see cref="RowVersion"/> it was loaded with. <see cref="UnitPrice"/> null = the register's latest price (give it only when the material is unchanged).
/// </summary>
public sealed record PurchaseRequestLineUpdate(
    int? Id,
    byte[]? RowVersion,
    int MaterialId,
    decimal? Quantity,
    int? LineNo = null,
    string? MaterialName = null,
    string? Unit = null,
    decimal? UnitPrice = null,
    decimal? TankWeight = null);

/// <summary>
/// The head and lines of a request as edited. Lines of the request that are not listed stay as they are: a line is removed with
/// <see cref="PurchaseRequestRegistry.DeleteLineAsync"/> (a button with a confirmation, BR-013).
/// </summary>
public sealed record PurchaseRequestUpdate(
    int Id,
    byte[] RowVersion,
    string RequestNo,
    DateOnly RequestDate,
    int DepartmentId,
    DateOnly? DesiredDate,
    string PurchasingLocation,
    string? Reason,
    string? Note,
    IReadOnlyList<PurchaseRequestLineUpdate> Lines);

/// <summary>A request in a search list (SCR-06/08): the number, the date and how many items it has.</summary>
public sealed record PurchaseRequestSummary(int Id, string RequestNo, DateOnly RequestDate, int DepartmentId, int LineCount);

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
                NetAmount = PurchaseOrderMath.LineNet(price, line.Quantity.Value),
                TankWeight = line.TankWeight,
            });
        }
        await db.SaveChangesAsync();
        return ServiceResult.Ok(request.Id);
    }

    /// <summary>
    /// แก้ใบขอซื้อ (SCR-06, BR-012): every field of the head and of the lines, in one transaction. Picking another material refills its name and
    /// unit, the amount is recomputed whenever quantity or price change (ข้อสมมติ ก). The number may change but must not repeat; a line
    /// kept in the request must have a quantity — remove it with <see cref="DeleteLineAsync"/>. A stale head or line throws
    /// <see cref="ConcurrencyConflictException"/>.
    /// </summary>
    public async Task<ServiceResult> UpdateAsync(PurchaseRequestUpdate input)
    {
        var request = await db.PurchaseRequests.Include(r => r.Lines).FirstOrDefaultAsync(r => r.Id == input.Id);
        if (request is null) return ServiceResult.Fail("ไม่พบใบขอซื้อนี้");
        request.ExpectRowVersion(input.RowVersion);

        var no = Clean(input.RequestNo);
        var errors = new List<string>();
        if (no is null) errors.Add("กรุณากรอกเลขที่ใบขอซื้อ");
        else if (no.Length > NumberLength) errors.Add($"เลขที่ใบขอซื้อยาวได้ไม่เกิน {NumberLength} ตัว");
        else if (await db.PurchaseRequests.AnyAsync(r => r.RequestNo == no && r.Id != input.Id)) errors.Add($"มีเลขที่ใบขอซื้อ '{no}' อยู่แล้ว");
        if (await org.GetDepartmentAsync(input.DepartmentId) is null) errors.Add("ไม่พบแผนกที่เลือก");
        var location = Clean(input.PurchasingLocation);
        if (location is null) errors.Add("กรุณาเลือกผู้จัดซื้อ");
        else if ((await setup.GetLocationsAsync()).All(l => l.Code != location)) errors.Add($"ไม่พบผู้จัดซื้อ '{location}'");
        var reason = Clean(input.Reason);
        var note = Clean(input.Note);
        if (reason?.Length > RequestSetupRegistry.TextLength || note?.Length > RequestSetupRegistry.TextLength)
            errors.Add($"เหตุผลและหมายเหตุยาวได้ไม่เกิน {RequestSetupRegistry.TextLength} ตัว");

        var existing = request.Lines.ToDictionary(l => l.Id);
        var edits = input.Lines.Where(l => l.Id is not null || l.Quantity is > 0).ToList();
        if (edits.Any(l => l.Id is { } id && !existing.ContainsKey(id))) errors.Add("ไม่พบรายการที่แก้ (อาจถูกลบไปแล้ว)");
        if (edits.Any(l => l.Id is not null && l.Quantity is not > 0)) errors.Add("จำนวนของรายการต้องมากกว่า 0 (ถ้าไม่ต้องการรายการนี้ ใช้ปุ่มลบรายการ)");
        if (edits.Any(l => l.UnitPrice < 0 || l.TankWeight < 0)) errors.Add("ราคาและนน.ถังต้องไม่ติดลบ");
        var materialIds = edits.Select(l => l.MaterialId).Distinct().ToList();
        var materials = await db.Materials.AsNoTracking().Where(m => materialIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id);
        if (edits.Any(l => !materials.ContainsKey(l.MaterialId))) errors.Add("ไม่พบวัตถุดิบที่เลือก");
        if (errors.Count > 0) return new ServiceResult(false, errors);

        request.RequestNo = no!;
        request.RequestDate = input.RequestDate;
        request.DepartmentId = input.DepartmentId;
        request.DesiredDate = input.DesiredDate;
        request.PurchasingLocation = location;
        request.Reason = reason;
        request.Note = note;

        foreach (var edit in edits)
        {
            var material = materials[edit.MaterialId];
            PurchaseRequestLine line;
            if (edit.Id is { } id)
            {
                line = existing[id];
                line.ExpectRowVersion(edit.RowVersion ?? throw new ArgumentException("Editing a line needs the rowversion the screen loaded.", nameof(input)));
            }
            else
            {
                line = new PurchaseRequestLine { LineNo = edit.LineNo is > 0 ? edit.LineNo.Value : request.Lines.Select(l => l.LineNo).DefaultIfEmpty(0).Max() + 1 };
                request.Lines.Add(line);
            }
            var materialChanged = line.MaterialId != material.Id;
            if (edit.LineNo is > 0) line.LineNo = edit.LineNo.Value;
            line.MaterialId = material.Id;
            line.MaterialName = Clean(edit.MaterialName) ?? (materialChanged || edit.Id is null ? material.Name : line.MaterialName);
            line.Unit = Clean(edit.Unit) ?? (materialChanged || edit.Id is null ? material.Unit : line.Unit);
            line.Quantity = edit.Quantity!.Value;
            line.UnitPrice = edit.UnitPrice ?? (materialChanged || edit.Id is null ? material.Cost : line.UnitPrice);
            line.NetAmount = PurchaseOrderMath.LineNet(line.UnitPrice, line.Quantity);
            line.TankWeight = edit.TankWeight;
        }
        await db.SaveChangesAsync();
        return ServiceResult.Ok(request.Id);
    }

    /// <summary>
    /// ลบรายการของใบขอซื้อ (BR-013, ปุ่มลบพร้อมหน้ายืนยัน): the number stays in use while the request has another line and is free again once
    /// the last line is removed (AC-35). Deleting a line that is already gone succeeds.
    /// </summary>
    public async Task<ServiceResult> DeleteLineAsync(int lineId, byte[] loadedRowVersion)
    {
        var line = await db.PurchaseRequestLines.Include(l => l.PurchaseRequest).ThenInclude(r => r!.Lines).FirstOrDefaultAsync(l => l.Id == lineId);
        if (line is null) return ServiceResult.Ok();
        line.ExpectRowVersion(loadedRowVersion);
        var request = line.PurchaseRequest!;
        if (request.Lines.Count == 1) db.PurchaseRequests.Remove(request); // the lines go with it
        else db.PurchaseRequestLines.Remove(line);
        return await db.TrySaveDeleteAsync()
            ? ServiceResult.Ok()
            : ServiceResult.Fail("ลบรายการนี้ไม่ได้ เพราะมีรายการอื่นอ้างถึงอยู่");
    }

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

    /// <summary>
    /// ใบขอซื้อที่เลขที่มี <paramref name="text"/> (ว่าง = ทั้งหมด), newest number first, at most <paramref name="max"/>: SCR-08 as a search
    /// (there is no number register). Requests that have been fully ordered are listed too — they can still be edited and printed again.
    /// </summary>
    public async Task<IReadOnlyList<PurchaseRequestSummary>> SearchAsync(string? text = null, int max = 100)
    {
        var term = text?.Trim();
        return await db.PurchaseRequests.AsNoTracking()
            .Where(r => string.IsNullOrEmpty(term) || r.RequestNo.Contains(term))
            .OrderByDescending(r => r.RequestDate).ThenByDescending(r => r.RequestNo).Take(max)
            .Select(r => new PurchaseRequestSummary(r.Id, r.RequestNo, r.RequestDate, r.DepartmentId, r.Lines.Count)).ToListAsync();
    }

    /// <summary>Whether any request was issued for the แผนก.</summary>
    public Task<bool> HasRequestsAsync(int departmentId) => db.PurchaseRequests.AnyAsync(r => r.DepartmentId == departmentId);

    private IQueryable<PurchaseRequest> Query()
        => db.PurchaseRequests.AsNoTracking().Include(r => r.Lines).ThenInclude(l => l.Material);

    private static PurchaseRequestDto ToDto(PurchaseRequest r) => new(
        r.Id, r.RequestNo, r.RequestDate, r.DepartmentId, r.DesiredDate, r.PurchasingLocation, r.Reason, r.Note,
        r.Lines.OrderBy(l => l.LineNo).ThenBy(l => l.Id).Select(l => new PurchaseRequestLineDto(
            l.Id, l.LineNo, l.MaterialId, l.Material!.Code, l.MaterialName, l.Unit, l.Quantity, l.UnitPrice, l.NetAmount, l.TankWeight, l.RowVersion)).ToList(),
        r.RowVersion);
}
