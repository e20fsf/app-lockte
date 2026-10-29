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
        return requests.Where(r => r.Lines.Any(l => l.Quantity - ordered.GetValueOrDefault(l.Id) > 0))
            .Select(r => new PurchaseRequestSummary(r.Id, r.RequestNo, r.RequestDate, r.DepartmentId, r.Lines.Count(l => l.Quantity - ordered.GetValueOrDefault(l.Id) > 0))).ToList();
    }

    /// <summary>The lines of a request that still have ยอดค้างสั่ง (ขอ − สั่งไปแล้ว &gt; 0), by ลำดับ.</summary>
    public async Task<IReadOnlyList<OutstandingRequestLine>> GetOutstandingLinesAsync(int requestId)
    {
        var request = await db.PurchaseRequests.AsNoTracking().Include(r => r.Lines).ThenInclude(l => l.Material).FirstOrDefaultAsync(r => r.Id == requestId);
        if (request is null) return [];
        var ordered = await OrderedByRequestLineAsync(request.Lines.Select(l => l.Id).ToList());
        return request.Lines.OrderBy(l => l.LineNo).ThenBy(l => l.Id)
            .Select(l => (Line: l, Ordered: ordered.GetValueOrDefault(l.Id)))
            .Where(x => x.Line.Quantity - x.Ordered > 0)
            .Select(x => new OutstandingRequestLine(x.Line.Id, request.RequestNo, request.RequestDate, x.Line.LineNo, x.Line.MaterialId, x.Line.Material!.Code, x.Line.MaterialName,
                x.Line.Unit, x.Line.Quantity, x.Ordered, x.Line.Quantity - x.Ordered, x.Line.Material.Cost, x.Line.Material.LatestSupplierId, request.DesiredDate, request.DepartmentId,
                request.Reason, request.Note, request.PurchasingLocation)).ToList();
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

    private IQueryable<PurchaseOrder> Query() => db.PurchaseOrders.AsNoTracking().Include(o => o.Lines).ThenInclude(l => l.Material);

    private static PurchaseOrderDto ToDto(PurchaseOrder o) => new(
        o.Id, o.OrderNo, o.OrderDate, o.SupplierId, o.SupplierName, o.SupplierAddress1, o.SupplierAddress2, o.SupplierTel, o.SupplierFax, o.CreditDays, o.ChargesVat,
        o.PurchasingLocation, o.Reason, o.Note, o.Subtotal, o.Tax, o.Total, PurchaseOrderMath.InWords(o.Total),
        o.Lines.OrderBy(l => l.LineNo).ThenBy(l => l.Id).Select(l => new PurchaseOrderLineDto(
            l.Id, l.LineNo, l.PurchaseRequestLineId, l.MaterialId, l.Material!.Code, l.MaterialName, l.Unit, l.RequestedQuantity, l.Quantity, l.UnitPrice, l.NetAmount,
            l.TaxIncludedAmount, l.DesiredDate, l.DepartmentId, l.RowVersion)).ToList(),
        o.RowVersion);
}
