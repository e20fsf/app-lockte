using Lockte.Org;
using Lockte.Purch.Data;
using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Purch;

/// <summary>A line of a purchase request that still has something to order (ยอดค้างสั่ง = ขอ − สั่งไปแล้ว).</summary>
public sealed record OutstandingRequestLine(
    int RequestLineId,
    string RequestNo,
    DateOnly RequestDate,
    int LineNo,
    int MaterialId,
    string MaterialCode,
    string MaterialName,
    string? Unit,
    decimal Requested,
    decimal Ordered,
    decimal Outstanding,
    decimal LatestPrice,
    int? LatestSupplierId,
    DateOnly? DesiredDate,
    int DepartmentId,
    string? Reason,
    string? Note,
    string? PurchasingLocation);

public sealed record PurchaseOrderLineDto(
    int Id,
    int LineNo,
    int PurchaseRequestLineId,
    string RequestNo,
    int MaterialId,
    string MaterialCode,
    string MaterialName,
    string? Unit,
    decimal RequestedQuantity,
    decimal Quantity,
    decimal UnitPrice,
    decimal NetAmount,
    decimal TaxIncludedAmount,
    DateOnly? DesiredDate,
    int DepartmentId,
    byte[] RowVersion);

public sealed record PurchaseOrderDto(
    int Id,
    string OrderNo,
    DateOnly OrderDate,
    int SupplierId,
    string SupplierName,
    string? SupplierAddress1,
    string? SupplierAddress2,
    string? SupplierTel,
    string? SupplierFax,
    int? CreditDays,
    bool ChargesVat,
    string? PurchasingLocation,
    string? Reason,
    string? Note,
    decimal Subtotal,
    decimal Tax,
    decimal Total,
    string TotalInWords,
    IReadOnlyList<PurchaseOrderLineDto> Lines,
    byte[] RowVersion);

/// <summary>A line to order. <see cref="UnitPrice"/> null = the register's latest price; <see cref="MaterialId"/> null = the request line's material.</summary>
public sealed record PurchaseOrderLineInput(
    int RequestLineId,
    decimal? Quantity,
    decimal? UnitPrice = null,
    int? LineNo = null,
    int? MaterialId = null,
    string? MaterialName = null,
    string? Unit = null);

/// <summary>
/// The fields of SCR-05: one supplier per order. The supplier's name, address, phone, fax, credit and VAT are given as the document will
/// print them (the screen fills them from the register — <see cref="PurchaseOrderRegistry"/> only checks the supplier exists).
/// </summary>
public sealed record PurchaseOrderInput(
    string OrderNo,
    DateOnly OrderDate,
    int SupplierId,
    string SupplierName,
    string? SupplierAddress1,
    string? SupplierAddress2,
    string? SupplierTel,
    string? SupplierFax,
    int? CreditDays,
    bool ChargesVat,
    string PurchasingLocation,
    string? Reason,
    string? Note,
    IReadOnlyList<PurchaseOrderLineInput> Lines);

/// <summary>
/// A line of an order being edited (SCR-07): quantity, price and the document's own name/unit. <see cref="RowVersion"/> is the one it was loaded with.
/// <see cref="UnitPrice"/> null keeps the price it has.
/// </summary>
public sealed record PurchaseOrderLineUpdate(
    int Id,
    byte[] RowVersion,
    decimal? Quantity,
    decimal? UnitPrice = null,
    int? LineNo = null,
    string? MaterialName = null,
    string? Unit = null);

/// <summary>The head of an order as edited, and its lines. Lines of the order that are not listed stay as they are; a line is removed with <see cref="PurchaseOrderRegistry.DeleteLineAsync"/>.</summary>
public sealed record PurchaseOrderUpdate(
    int Id,
    byte[] RowVersion,
    string OrderNo,
    DateOnly OrderDate,
    int SupplierId,
    string SupplierName,
    string? SupplierAddress1,
    string? SupplierAddress2,
    string? SupplierTel,
    string? SupplierFax,
    int? CreditDays,
    bool ChargesVat,
    string PurchasingLocation,
    string? Reason,
    string? Note,
    IReadOnlyList<PurchaseOrderLineUpdate> Lines);

/// <summary>An order in a search list (SCR-07/09).</summary>
public sealed record PurchaseOrderSummary(int Id, string OrderNo, DateOnly OrderDate, string SupplierName, int LineCount, decimal Total);

/// <summary>ออกใบสั่งซื้อจากรายการขอซื้อ เต็มหรือบางส่วน (SCR-05, BR-020, BR-021, BR-022, BR-023).</summary>
public sealed class PurchaseOrderRegistry(PurchDbContext db, SupplierDirectory suppliers, PurchSettingsDirectory settings, RequestSetupDirectory setup)
{
    public const int NumberLength = 20;

    /// <summary>
    /// ออกใบสั่งซื้อเป็นหนึ่ง transaction (BR-021): one supplier, VAT at the head, totals by BR-022. Each line orders a quantity of a request line;
    /// what is left of the request is its ยอดค้างสั่ง (a request line can be ordered again, from another supplier too, and over-ordered — AC-10). An order
    /// missing its date, number, a quantity or a price is not saved (BR-023). The number must not repeat.
    /// </summary>
    public async Task<ServiceResult> IssueAsync(PurchaseOrderInput input)
    {
        var no = Clean(input.OrderNo);
        var errors = new List<string>();
        if (no is null) errors.Add("กรุณากรอกเลขที่ใบสั่งซื้อ");
        else if (no.Length > NumberLength) errors.Add($"เลขที่ใบสั่งซื้อยาวได้ไม่เกิน {NumberLength} ตัว");
        else if (await db.PurchaseOrders.AnyAsync(o => o.OrderNo == no)) errors.Add($"มีเลขที่ใบสั่งซื้อ '{no}' อยู่แล้ว");
        if (input.OrderDate == default) errors.Add("กรุณากรอกวันที่สั่งซื้อ");
        var supplier = await suppliers.GetAsync(input.SupplierId);
        if (supplier is null) errors.Add("กรุณาเลือกผู้ขาย");
        var supplierName = Clean(input.SupplierName) ?? supplier?.Name;
        var location = Clean(input.PurchasingLocation);
        if (location is null) errors.Add("กรุณาเลือกผู้จัดซื้อ");
        else if ((await setup.GetLocationsAsync()).All(l => l.Code != location)) errors.Add($"ไม่พบผู้จัดซื้อ '{location}'");
        if (input.CreditDays < 0) errors.Add("เครดิตต้องไม่ติดลบ (เงินสด = 0)");
        var texts = new (string? Value, string Label)[]
        {
            (supplierName, "ชื่อผู้ขาย"), (input.SupplierAddress1, "ที่อยู่"), (input.SupplierAddress2, "เขต(อำเภอ)และจังหวัด"), (input.SupplierTel, "โทรศัพท์"), (input.SupplierFax, "แฟกซ์"),
        };
        errors.AddRange(texts.Where(t => t.Value?.Trim().Length > SupplierRegistry.TextLength).Select(t => $"{t.Label}ยาวได้ไม่เกิน {SupplierRegistry.TextLength} ตัว"));
        var reason = Clean(input.Reason);
        var note = Clean(input.Note);
        if (reason?.Length > RequestSetupRegistry.TextLength || note?.Length > RequestSetupRegistry.TextLength)
            errors.Add($"เหตุผลและหมายเหตุยาวได้ไม่เกิน {RequestSetupRegistry.TextLength} ตัว");

        if (input.Lines.Count == 0) errors.Add("ไม่มีรายการที่จะสั่งซื้อ");
        if (input.Lines.Any(l => l.Quantity is not > 0)) errors.Add("ทุกรายการต้องกรอกจำนวนสั่งซื้อ (มากกว่า 0)");
        if (input.Lines.Any(l => l.UnitPrice < 0)) errors.Add("ราคาต้องไม่ติดลบ");
        var requestLineIds = input.Lines.Select(l => l.RequestLineId).ToList();
        var requestLines = await db.PurchaseRequestLines.Include(l => l.PurchaseRequest).Include(l => l.Material)
            .Where(l => requestLineIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id);
        if (requestLineIds.Any(id => !requestLines.ContainsKey(id))) errors.Add("ไม่พบรายการขอซื้อที่เลือก (อาจถูกลบไปแล้ว)");
        var materialIds = input.Lines.Where(l => l.MaterialId is not null).Select(l => l.MaterialId!.Value).Distinct().ToList();
        var materials = await db.Materials.AsNoTracking().Where(m => materialIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id);
        if (materialIds.Any(id => !materials.ContainsKey(id))) errors.Add("ไม่พบวัตถุดิบที่เลือก");
        if (errors.Count > 0) return new ServiceResult(false, errors);

        var order = db.PurchaseOrders.Add(new PurchaseOrder
        {
            OrderNo = no!, OrderDate = input.OrderDate, SupplierId = input.SupplierId, SupplierName = supplierName!,
            SupplierAddress1 = Clean(input.SupplierAddress1), SupplierAddress2 = Clean(input.SupplierAddress2), SupplierTel = Clean(input.SupplierTel),
            SupplierFax = Clean(input.SupplierFax), CreditDays = input.CreditDays, ChargesVat = input.ChargesVat, PurchasingLocation = location,
            Reason = reason, Note = note,
        }).Entity;
        for (var i = 0; i < input.Lines.Count; i++)
        {
            var line = input.Lines[i];
            var source = requestLines[line.RequestLineId];
            var material = line.MaterialId is { } m ? materials[m] : source.Material!;
            var changed = material.Id != source.MaterialId;
            var price = line.UnitPrice ?? (changed ? material.Cost : (source.Material!.Cost));
            order.Lines.Add(new PurchaseOrderLine
            {
                LineNo = line.LineNo is > 0 ? line.LineNo.Value : i + 1,
                PurchaseRequestLineId = source.Id,
                MaterialId = material.Id,
                MaterialName = Clean(line.MaterialName) ?? (changed ? material.Name : source.MaterialName),
                Unit = Clean(line.Unit) ?? (changed ? material.Unit : source.Unit),
                RequestedQuantity = source.Quantity,
                Quantity = line.Quantity!.Value,
                UnitPrice = price,
                NetAmount = PurchaseOrderMath.LineNet(price, line.Quantity.Value),
                DesiredDate = source.PurchaseRequest!.DesiredDate,
                DepartmentId = source.PurchaseRequest.DepartmentId,
            });
        }
        await RecalculateAsync(order);
        await db.SaveChangesAsync();
        return ServiceResult.Ok(order.Id);
    }

    /// <summary>
    /// แก้ใบสั่งซื้อ (SCR-07, BR-024): the supplier (name, address, credit — and VAT, which the legacy did not refill), the price, the quantity ordered, the
    /// purchasing location, the number. Unlike the legacy the stored totals and each line's amount are recomputed after every edit (BR-024 #1). The number
    /// may change but must not repeat. A stale head or line throws <see cref="ConcurrencyConflictException"/>.
    /// </summary>
    public async Task<ServiceResult> UpdateAsync(PurchaseOrderUpdate input)
    {
        var order = await db.PurchaseOrders.Include(o => o.Lines).FirstOrDefaultAsync(o => o.Id == input.Id);
        if (order is null) return ServiceResult.Fail("ไม่พบใบสั่งซื้อนี้");
        order.ExpectRowVersion(input.RowVersion);

        var no = Clean(input.OrderNo);
        var errors = new List<string>();
        if (no is null) errors.Add("กรุณากรอกเลขที่ใบสั่งซื้อ");
        else if (no.Length > NumberLength) errors.Add($"เลขที่ใบสั่งซื้อยาวได้ไม่เกิน {NumberLength} ตัว");
        else if (await db.PurchaseOrders.AnyAsync(o => o.OrderNo == no && o.Id != input.Id)) errors.Add($"มีเลขที่ใบสั่งซื้อ '{no}' อยู่แล้ว");
        if (input.OrderDate == default) errors.Add("กรุณากรอกวันที่สั่งซื้อ");
        var supplier = await suppliers.GetAsync(input.SupplierId);
        if (supplier is null) errors.Add("กรุณาเลือกผู้ขาย");
        var supplierName = Clean(input.SupplierName) ?? supplier?.Name;
        var location = Clean(input.PurchasingLocation);
        if (location is null) errors.Add("กรุณาเลือกผู้จัดซื้อ");
        else if ((await setup.GetLocationsAsync()).All(l => l.Code != location)) errors.Add($"ไม่พบผู้จัดซื้อ '{location}'");
        if (input.CreditDays < 0) errors.Add("เครดิตต้องไม่ติดลบ (เงินสด = 0)");
        var texts = new (string? Value, string Label)[]
        {
            (supplierName, "ชื่อผู้ขาย"), (input.SupplierAddress1, "ที่อยู่"), (input.SupplierAddress2, "เขต(อำเภอ)และจังหวัด"), (input.SupplierTel, "โทรศัพท์"), (input.SupplierFax, "แฟกซ์"),
        };
        errors.AddRange(texts.Where(t => t.Value?.Trim().Length > SupplierRegistry.TextLength).Select(t => $"{t.Label}ยาวได้ไม่เกิน {SupplierRegistry.TextLength} ตัว"));
        var reason = Clean(input.Reason);
        var note = Clean(input.Note);
        if (reason?.Length > RequestSetupRegistry.TextLength || note?.Length > RequestSetupRegistry.TextLength)
            errors.Add($"เหตุผลและหมายเหตุยาวได้ไม่เกิน {RequestSetupRegistry.TextLength} ตัว");
        var existing = order.Lines.ToDictionary(l => l.Id);
        if (input.Lines.Any(l => !existing.ContainsKey(l.Id))) errors.Add("ไม่พบรายการที่แก้ (อาจถูกลบไปแล้ว)");
        if (input.Lines.Any(l => l.Quantity is not > 0)) errors.Add("ทุกรายการต้องกรอกจำนวนสั่งซื้อ (มากกว่า 0) — ถ้าไม่ต้องการรายการนี้ ใช้ปุ่มลบรายการ");
        if (input.Lines.Any(l => l.UnitPrice < 0)) errors.Add("ราคาต้องไม่ติดลบ");
        if (errors.Count > 0) return new ServiceResult(false, errors);

        order.OrderNo = no!;
        order.OrderDate = input.OrderDate;
        order.SupplierId = input.SupplierId;
        order.SupplierName = supplierName!;
        order.SupplierAddress1 = Clean(input.SupplierAddress1);
        order.SupplierAddress2 = Clean(input.SupplierAddress2);
        order.SupplierTel = Clean(input.SupplierTel);
        order.SupplierFax = Clean(input.SupplierFax);
        order.CreditDays = input.CreditDays;
        order.ChargesVat = input.ChargesVat;
        order.PurchasingLocation = location;
        order.Reason = reason;
        order.Note = note;
        foreach (var edit in input.Lines)
        {
            var line = existing[edit.Id];
            line.ExpectRowVersion(edit.RowVersion);
            if (edit.LineNo is > 0) line.LineNo = edit.LineNo.Value;
            line.MaterialName = Clean(edit.MaterialName) ?? line.MaterialName;
            line.Unit = Clean(edit.Unit) ?? line.Unit;
            line.Quantity = edit.Quantity!.Value;
            line.UnitPrice = edit.UnitPrice ?? line.UnitPrice;
            line.NetAmount = PurchaseOrderMath.LineNet(line.UnitPrice, line.Quantity);
        }
        await RecalculateAsync(order);
        await db.SaveChangesAsync();
        return ServiceResult.Ok(order.Id);
    }

    /// <summary>
    /// ลบรายการของใบสั่งซื้อ (BR-025, ปุ่มลบพร้อมหน้ายืนยัน): the order number stays while it has another line and is free again once the last one is removed
    /// (AC-35). The request line is not given back to be ordered again — as before, a new request is needed (BR-025 ⚠). The totals are recomputed.
    /// Deleting a line that is already gone succeeds.
    /// </summary>
    public async Task<ServiceResult> DeleteLineAsync(int lineId, byte[] loadedRowVersion)
    {
        var line = await db.PurchaseOrderLines.Include(l => l.PurchaseOrder).ThenInclude(o => o!.Lines).FirstOrDefaultAsync(l => l.Id == lineId);
        if (line is null) return ServiceResult.Ok();
        line.ExpectRowVersion(loadedRowVersion);
        if (await db.GoodsReceipts.AnyAsync(r => r.PurchaseOrderLineId == lineId)) return ServiceResult.Fail("รายการนี้มีการรับของแล้ว ลบไม่ได้");
        var order = line.PurchaseOrder!;
        var removed = order.Lines.Count == 1 ? order.Lines : [line];
        var requestLineIds = removed.Select(l => l.PurchaseRequestLineId).ToList();
        var requestLines = await db.PurchaseRequestLines.Where(r => requestLineIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id);
        foreach (var gone in removed) requestLines[gone.PurchaseRequestLineId].AbandonedQuantity += gone.Quantity; // not handed back to be ordered again
        if (order.Lines.Count == 1) db.PurchaseOrders.Remove(order); // the line goes with it
        else
        {
            order.Lines.Remove(line);
            db.PurchaseOrderLines.Remove(line);
            await RecalculateAsync(order);
        }
        return await db.TrySaveDeleteAsync()
            ? ServiceResult.Ok()
            : ServiceResult.Fail("ลบรายการนี้ไม่ได้ เพราะมีรายการอื่นอ้างถึงอยู่ (เช่น การรับของ)");
    }

    /// <summary>BR-022: the totals of the order and each line's amount with VAT, by the rate in the settings.</summary>
    private async Task RecalculateAsync(PurchaseOrder order)
    {
        var totals = PurchaseOrderMath.Calculate(order.Lines.Select(l => l.NetAmount).ToList(), order.ChargesVat, (await settings.GetAsync()).VatRate);
        order.Subtotal = totals.Subtotal;
        order.Tax = totals.Tax;
        order.Total = totals.Total;
        for (var i = 0; i < order.Lines.Count; i++) order.Lines[i].TaxIncludedAmount = totals.Lines[i].WithTax;
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}

/// <summary>The public, read-only face of ใบสั่งซื้อ (ADR 0002). Writes go through <see cref="PurchaseOrderRegistry"/>.</summary>
public sealed class PurchaseOrderDirectory(PurchDbContext db)
{
    /// <summary>
    /// เลขที่ใบสั่งซื้อถัดไปที่เสนอให้ (BR-020): <c>&lt;YY พ.ศ.&gt;&lt;NNNN&gt;</c>, one more than the highest of the year (the numbers run through the whole
    /// company). It only proposes: the user may change it and a repeated number is refused when the order is issued.
    /// </summary>
    public async Task<string> SuggestNumberAsync(DateOnly date)
    {
        var year = PurchaseRequestRegistry.ThaiYear2(date).ToString("00");
        var highest = await HighestNumberAsync(date);
        return highest is null ? year + "0001" : year + (int.Parse(highest[2..]) + 1).ToString("0000");
    }

    /// <summary>เลขที่ใบสั่งซื้อสูงสุดของปีของวันนั้น ("เลขที่ใบสั่งซื้อสูงสุดในทะเบียน"), null when the year has none.</summary>
    public async Task<string?> HighestNumberAsync(DateOnly date)
    {
        var year = PurchaseRequestRegistry.ThaiYear2(date).ToString("00");
        return (await db.PurchaseOrders.AsNoTracking().Where(o => o.OrderNo.StartsWith(year)).Select(o => o.OrderNo).ToListAsync())
            .Where(n => n.Length > 2 && n[2..].All(char.IsDigit)).OrderByDescending(n => n.Length).ThenByDescending(n => n).FirstOrDefault();
    }

    /// <summary>ใบขอซื้อที่ยังมียอดค้างสั่ง, newest first: what the order screen offers to choose from.</summary>
    public async Task<IReadOnlyList<PurchaseRequestSummary>> GetRequestsWithOutstandingAsync()
    {
        var requests = await db.PurchaseRequests.AsNoTracking().Include(r => r.Lines).OrderByDescending(r => r.RequestDate).ThenByDescending(r => r.RequestNo).ToListAsync();
        var ordered = await OrderedByRequestLineAsync(requests.SelectMany(r => r.Lines.Select(l => l.Id)).ToList());
        return requests.Where(r => r.Lines.Any(l => l.Quantity - l.AbandonedQuantity - ordered.GetValueOrDefault(l.Id) > 0))
            .Select(r => new PurchaseRequestSummary(r.Id, r.RequestNo, r.RequestDate, r.DepartmentId, r.Lines.Count(l => l.Quantity - l.AbandonedQuantity - ordered.GetValueOrDefault(l.Id) > 0))).ToList();
    }

    /// <summary>The lines of a request that still have ยอดค้างสั่ง (ขอ − สั่งไปแล้ว &gt; 0), by ลำดับ.</summary>
    public async Task<IReadOnlyList<OutstandingRequestLine>> GetOutstandingLinesAsync(int requestId)
    {
        var request = await db.PurchaseRequests.AsNoTracking().Include(r => r.Lines).ThenInclude(l => l.Material).FirstOrDefaultAsync(r => r.Id == requestId);
        if (request is null) return [];
        var ordered = await OrderedByRequestLineAsync(request.Lines.Select(l => l.Id).ToList());
        return request.Lines.OrderBy(l => l.LineNo).ThenBy(l => l.Id)
            .Select(l => (Line: l, Ordered: ordered.GetValueOrDefault(l.Id) + l.AbandonedQuantity))
            .Where(x => x.Line.Quantity - x.Ordered > 0)
            .Select(x => new OutstandingRequestLine(x.Line.Id, request.RequestNo, request.RequestDate, x.Line.LineNo, x.Line.MaterialId, x.Line.Material!.Code, x.Line.MaterialName,
                x.Line.Unit, x.Line.Quantity, x.Ordered, x.Line.Quantity - x.Ordered, x.Line.Material.Cost, x.Line.Material.LatestSupplierId, request.DesiredDate, request.DepartmentId,
                request.Reason, request.Note, request.PurchasingLocation)).ToList();
    }

    /// <summary>
    /// ใบสั่งซื้อที่เลขที่มี <paramref name="text"/> (ว่าง = ทั้งหมด), the highest number first (SCR-07/09: there is no number register), at most <paramref name="max"/>.
    /// </summary>
    public async Task<IReadOnlyList<PurchaseOrderSummary>> SearchAsync(string? text = null, int max = 100)
    {
        var term = text?.Trim();
        return await db.PurchaseOrders.AsNoTracking().Where(o => string.IsNullOrEmpty(term) || o.OrderNo.Contains(term))
            .OrderByDescending(o => o.OrderNo.Length).ThenByDescending(o => o.OrderNo).Take(max)
            .Select(o => new PurchaseOrderSummary(o.Id, o.OrderNo, o.OrderDate, o.SupplierName, o.Lines.Count, o.Total)).ToListAsync();
    }

    /// <summary>How much of a request line has been ordered, over all orders.</summary>
    public async Task<decimal> OrderedQuantityAsync(int requestLineId)
        => await db.PurchaseOrderLines.AsNoTracking().Where(l => l.PurchaseRequestLineId == requestLineId).SumAsync(l => (decimal?)l.Quantity) ?? 0;

    public async Task<PurchaseOrderDto?> GetAsync(int id)
        => await Query().FirstOrDefaultAsync(o => o.Id == id) is { } o ? ToDto(o) : null;

    public async Task<PurchaseOrderDto?> FindByNoAsync(string orderNo)
    {
        var no = orderNo.Trim();
        return await Query().FirstOrDefaultAsync(o => o.OrderNo == no) is { } o ? ToDto(o) : null;
    }

    private async Task<Dictionary<int, decimal>> OrderedByRequestLineAsync(IReadOnlyList<int> requestLineIds)
        => await db.PurchaseOrderLines.AsNoTracking().Where(l => requestLineIds.Contains(l.PurchaseRequestLineId))
            .GroupBy(l => l.PurchaseRequestLineId).Select(g => new { g.Key, Total = g.Sum(l => l.Quantity) }).ToDictionaryAsync(g => g.Key, g => g.Total);

    private IQueryable<PurchaseOrder> Query()
        => db.PurchaseOrders.AsNoTracking().Include(o => o.Lines).ThenInclude(l => l.Material)
            .Include(o => o.Lines).ThenInclude(l => l.PurchaseRequestLine).ThenInclude(r => r!.PurchaseRequest);

    private static PurchaseOrderDto ToDto(PurchaseOrder o) => new(
        o.Id, o.OrderNo, o.OrderDate, o.SupplierId, o.SupplierName, o.SupplierAddress1, o.SupplierAddress2, o.SupplierTel, o.SupplierFax, o.CreditDays, o.ChargesVat,
        o.PurchasingLocation, o.Reason, o.Note, o.Subtotal, o.Tax, o.Total, PurchaseOrderMath.InWords(o.Total),
        o.Lines.OrderBy(l => l.LineNo).ThenBy(l => l.Id).Select(l => new PurchaseOrderLineDto(
            l.Id, l.LineNo, l.PurchaseRequestLineId, l.PurchaseRequestLine!.PurchaseRequest!.RequestNo, l.MaterialId, l.Material!.Code, l.MaterialName, l.Unit, l.RequestedQuantity, l.Quantity, l.UnitPrice, l.NetAmount,
            l.TaxIncludedAmount, l.DesiredDate, l.DepartmentId, l.RowVersion)).ToList(),
        o.RowVersion);
}
