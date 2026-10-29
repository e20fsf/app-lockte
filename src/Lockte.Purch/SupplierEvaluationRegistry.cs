using Lockte.Purch.Data;
using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Purch;

/// <summary>The counts of an evaluation (BR-050 ข้อ 2).</summary>
public sealed record SupplierCounts(int Passed, int Failed, int WithCertificate, int WithoutCertificate, int OnTime, int Late)
{
    public bool IsEmpty => Passed + Failed + WithCertificate + WithoutCertificate + OnTime + Late == 0;
}

/// <summary>
/// คะแนนประเมินผู้ขาย (BR-050, worked out and not stored): five topics of 1–5, the total of 25, the percentage not rounded, the grade by the thresholds of the settings
/// and its wording. Cleared up from the legacy: NCR, certificate and delivery are 0→5, 1→4, 2→3, 3→2, 4 or more→1; a supplier with no certificate gets 1 for it.
/// </summary>
public sealed record SupplierScore(int Quality, int Certificate, int Delivery, int Service1, int Service2, decimal Percent, string Grade, string Result)
{
    public int Total => Quality + Certificate + Delivery + Service1 + Service2;

    public const int Maximum = 25;

    /// <summary>0→5, 1→4, 2→3, 3→2, ≥4→1.</summary>
    public static int Points(int count) => count switch { <= 0 => 5, 1 => 4, 2 => 3, 3 => 2, _ => 1 };

    public static string ResultText(string grade) => grade switch
    {
        "A" => "ผู้ขายชั้นดี",
        "B" => "ควรสอบถามข้อมูลให้แน่ชัดก่อนสั่งซื้อ",
        "C" => "ระมัดระวังในการสั่งซื้อ,สั่งซื้อเท่าที่จำเป็น",
        _ => "ห้ามสั่งซื้อ นอกจากได้รับคำสั่งจาก ผจก.จัดซื้อ",
    };

    /// <summary>คำอธิบายคะแนนการบริการ 1 (การรับฟัง-ตอบปัญหา): 5 สม่ำเสมอและแก้ไขปัญหา, 4 สม่ำเสมอ, 3 บางครั้ง, 2 น้อยครั้ง, อื่น ไม่สามารถ.</summary>
    public static string Service1Text(int point) => point switch { 5 => "สม่ำเสมอและแก้ไขปัญหา", 4 => "สม่ำเสมอ", 3 => "บางครั้ง", 2 => "น้อยครั้ง", _ => "ไม่สามารถ" };

    /// <summary>คำอธิบายคะแนนการบริการ 2 (การตอบสนองข้อร้องเรียน): 5 ดีมาก, 4 ดี, 3 ปานกลาง, 2 พอใช้, อื่น น้อย.</summary>
    public static string Service2Text(int point) => point switch { 5 => "ดีมาก", 4 => "ดี", 3 => "ปานกลาง", 2 => "พอใช้", _ => "น้อย" };

    public static SupplierScore Calculate(SupplierCounts counts, bool supplierHasCertificate, int point1, int point2, PurchSettingsDto settings)
    {
        var quality = Points(counts.Failed);
        var certificate = supplierHasCertificate ? Points(counts.WithoutCertificate) : 1;
        var delivery = Points(counts.Late);
        var total = quality + certificate + delivery + point1 + point2;
        var percent = total * 100m / Maximum;
        var grade = settings.GradeOf(percent);
        return new SupplierScore(quality, certificate, delivery, point1, point2, percent, grade, ResultText(grade));
    }
}

/// <summary>An evaluation as read: the period, the counts the user cannot edit, the points and the score.</summary>
public sealed record SupplierEvaluationDto(
    int Id,
    int SupplierId,
    string SupplierCode,
    string SupplierName,
    DateOnly FromDate,
    DateOnly ToDate,
    int Quarter,
    SupplierCounts Counts,
    int Point1,
    int Point2,
    SupplierScore Score,
    byte[] RowVersion);

/// <summary>What to evaluate: one supplier over a period, the quarter (1–4) and the two service points the user gives.</summary>
public sealed record SupplierEvaluationInput(int SupplierId, DateOnly From, DateOnly To, int Quarter, int Point1, int Point2);

/// <summary>The outcome: <see cref="Evaluation"/> is null when the supplier had no receipt with a LOT in the period — nothing is kept (BR-050 #4).</summary>
public sealed record SupplierEvaluationResult(bool Succeeded, IReadOnlyList<string> Errors, SupplierEvaluationDto? Evaluation, SupplierCounts Counts);

/// <summary>
/// ประเมินผู้ขายรายไตรมาส (BR-050): the receipts of the supplier in the period **that have a LOT** (QA has looked at them) are counted — ผ่าน/ไม่ผ่าน, มี/ไม่มีใบรับรอง, ทัน/ไม่ทัน —
/// the counts replace those kept for the same supplier and period, and a supplier with nothing counted keeps no row. The supplier is the id on the receipt, not a name.
/// </summary>
public sealed class SupplierEvaluationRegistry(PurchDbContext db, PurchSettingsDirectory settings)
{
    public async Task<SupplierEvaluationResult> EvaluateAsync(SupplierEvaluationInput input)
    {
        var errors = new List<string>();
        if (input.From == default || input.To == default) errors.Add("กรุณากรอกช่วงวันที่");
        else if (input.From > input.To) errors.Add("ตั้งแต่วันที่ต้องไม่หลังถึงวันที่");
        if (input.Quarter is < 1 or > 4) errors.Add("ไตรมาสต้องเป็น 1–4");
        if (input.Point1 is < 1 or > 5 || input.Point2 is < 1 or > 5) errors.Add("คะแนนบริการต้องอยู่ระหว่าง 1 ถึง 5");
        var supplier = await db.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == input.SupplierId);
        if (supplier is null) errors.Add("ไม่พบผู้ขายที่เลือก");
        if (errors.Count > 0) return new SupplierEvaluationResult(false, errors, null, new SupplierCounts(0, 0, 0, 0, 0, 0));

        var receipts = await db.GoodsReceipts.AsNoTracking().Include(r => r.PurchaseOrderLine)
            .Where(r => r.SupplierId == input.SupplierId && r.ReceiveDate >= input.From && r.ReceiveDate <= input.To && r.Lot != null && r.Lot != "").ToListAsync();
        var counts = new SupplierCounts(
            receipts.Count(r => !r.Failed), receipts.Count(r => r.Failed),
            receipts.Count(r => r.HasCertificate == true), receipts.Count(r => r.HasCertificate == false),
            receipts.Count(r => GoodsReceiptMath.OnTime(r.PurchaseOrderLine!.DesiredDate, r.ReceiveDate)), receipts.Count(r => !GoodsReceiptMath.OnTime(r.PurchaseOrderLine!.DesiredDate, r.ReceiveDate)));

        var existing = await db.SupplierEvaluations.FirstOrDefaultAsync(e => e.SupplierId == input.SupplierId && e.FromDate == input.From && e.ToDate == input.To);
        if (counts.IsEmpty)
        {
            if (existing is not null) { db.SupplierEvaluations.Remove(existing); await db.SaveChangesAsync(); }
            return new SupplierEvaluationResult(true, [], null, counts);
        }

        existing ??= db.SupplierEvaluations.Add(new SupplierEvaluation { SupplierId = input.SupplierId, FromDate = input.From, ToDate = input.To }).Entity;
        existing.Quarter = input.Quarter;
        existing.Passed = counts.Passed;
        existing.Failed = counts.Failed;
        existing.WithCertificate = counts.WithCertificate;
        existing.WithoutCertificate = counts.WithoutCertificate;
        existing.OnTime = counts.OnTime;
        existing.Late = counts.Late;
        existing.Point1 = input.Point1;
        existing.Point2 = input.Point2;
        await db.SaveChangesAsync();
        var score = SupplierScore.Calculate(counts, supplier!.Certificate, input.Point1, input.Point2, await settings.GetAsync());
        return new SupplierEvaluationResult(true, [], new SupplierEvaluationDto(existing.Id, supplier.Id, supplier.Code, supplier.Name, input.From, input.To, input.Quarter, counts, input.Point1, input.Point2, score, existing.RowVersion), counts);
    }
}

/// <summary>The public, read-only face of ผลประเมินผู้ขาย (ADR 0002).</summary>
public sealed class SupplierEvaluationDirectory(PurchDbContext db, PurchSettingsDirectory settings)
{
    /// <summary>The evaluations kept, optionally of one quarter (<paramref name="quarter"/> with its year by <paramref name="from"/>…<paramref name="to"/>), by supplier code.</summary>
    public async Task<IReadOnlyList<SupplierEvaluationDto>> ListAsync(DateOnly? from = null, DateOnly? to = null, int? quarter = null)
    {
        var rows = await db.SupplierEvaluations.AsNoTracking().Include(e => e.Supplier)
            .Where(e => (from == null || e.FromDate >= from) && (to == null || e.ToDate <= to) && (quarter == null || e.Quarter == quarter))
            .ToListAsync();
        var config = await settings.GetAsync();
        return rows.OrderBy(e => e.Supplier!.Code).ThenBy(e => e.FromDate).Select(e => ToDto(e, config)).ToList();
    }

    public async Task<SupplierEvaluationDto?> GetAsync(int supplierId, DateOnly from, DateOnly to)
        => await db.SupplierEvaluations.AsNoTracking().Include(e => e.Supplier).FirstOrDefaultAsync(e => e.SupplierId == supplierId && e.FromDate == from && e.ToDate == to) is { } e
            ? ToDto(e, await settings.GetAsync()) : null;

    private static SupplierEvaluationDto ToDto(SupplierEvaluation e, PurchSettingsDto config)
    {
        var counts = new SupplierCounts(e.Passed, e.Failed, e.WithCertificate, e.WithoutCertificate, e.OnTime, e.Late);
        return new SupplierEvaluationDto(e.Id, e.SupplierId, e.Supplier!.Code, e.Supplier.Name, e.FromDate, e.ToDate, e.Quarter, counts, e.Point1, e.Point2,
            SupplierScore.Calculate(counts, e.Supplier.Certificate, e.Point1, e.Point2, config), e.RowVersion);
    }
}
