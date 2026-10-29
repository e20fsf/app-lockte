using Lockte.Purch.Data;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Purch;

/// <summary>
/// The delete-everything and bulk-load service of the master data of module วัตถุดิบ, for the migration tool only (#217): registered in that tool, never in the web app (the same as
/// the other modules' <c>…DataReset</c>). It clears ผู้ขาย, วัตถุดิบ and the lists they use; a database that already holds documents (ใบสั่งซื้อ, การรับของ, สต็อก) refuses,
/// because those refer to the master data by id and are loaded again after it (#266, #267).
/// </summary>
public sealed class PurchMasterImport(PurchDbContext db)
{
    public async Task DeleteMasterAsync()
    {
        if (await db.PurchaseRequests.AnyAsync() || await db.PurchaseOrders.AnyAsync() || await db.GoodsReceipts.AnyAsync() || await db.StockEntries.AnyAsync()
            || await db.StockPeriodBalances.AnyAsync() || await db.SupplierEvaluations.AnyAsync())
            throw new InvalidOperationException("ฐานข้อมูลนี้มีเอกสารจัดซื้อหรือสต็อกแล้ว — ย้ายข้อมูลหลักใหม่ไม่ได้ (เอกสารอ้างถึงผู้ขายและวัตถุดิบ) ใช้ฐานข้อมูลที่ยังไม่มีเอกสาร");
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.Materials.ExecuteDeleteAsync();
        await db.Suppliers.ExecuteDeleteAsync();
        await db.SupplierCreditOptions.ExecuteDeleteAsync();
        await db.SupplierBusinessTypes.ExecuteDeleteAsync();
        await db.MaterialGroups.ExecuteDeleteAsync();
        await db.DepartmentPrefixes.ExecuteDeleteAsync();
        await db.PurchasingLocations.ExecuteDeleteAsync();
        await db.RequestReasons.ExecuteDeleteAsync();
        await db.RequestNotes.ExecuteDeleteAsync();
        await transaction.CommitAsync();
    }

    /// <summary>เหตุผลในการขอซื้อ and หมายเหตุ the user picks from: texts only, repeats left out.</summary>
    public async Task AddReasonsAndNotesAsync(IEnumerable<string> reasons, IEnumerable<string> notes)
    {
        foreach (var text in reasons.Select(r => r.Trim()).Where(r => r.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase)) db.RequestReasons.Add(new RequestReason { Text = text });
        foreach (var text in notes.Select(r => r.Trim()).Where(r => r.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase)) db.RequestNotes.Add(new RequestNote { Text = text });
        await db.SaveChangesAsync();
    }

    public async Task<int> CountReasonsAsync() => await db.RequestReasons.CountAsync();

    public async Task<int> CountNotesAsync() => await db.RequestNotes.CountAsync();
}
