using Lockte.Cash.Data;

namespace Lockte.Cash;

/// <summary>A line of a receipt as the migration hands it over, kept as the legacy had it (the amount is never worked out again).</summary>
public sealed record ImportedReceiptLine(int Seq, string ItemName, int? ScrapItemId, string? Unit, decimal Qty, decimal UnitPrice, decimal Amount);

/// <summary>A receipt of <c>ขายของเก่า</c> as the migration hands it over; <see cref="Remitted"/> = it was sent to the office before the migration.</summary>
public sealed record ImportedReceipt(string No, DateOnly Date, string BuyerName, string? BuyerAddress, string? BuyerTel, int? ScrapBuyerId, IReadOnlyList<ImportedReceiptLine> Lines, bool Remitted);

/// <summary>The one การนำส่ง the migration makes for every receipt already sent ("นำส่งก่อนย้ายข้อมูล").</summary>
public sealed record ImportedRemittance(DateOnly MadeOn, string FromNo, string ToNo, string MadeBy);

/// <summary>
/// The bulk-load side of the migration of the receipts (#241): registered for <c>Lockte.Migration</c> only, never in the web app. A receipt moved from the legacy is written as it
/// was — numbers, amounts and all — not through <see cref="ScrapReceiptRegistry"/>, whose checks are those of a receipt being made today. The rows go in the order given.
/// </summary>
public sealed class CashReceiptImport(Data.CashDbContext db)
{
    private const int BatchSize = 100;

    public async Task LoadAsync(IReadOnlyList<ImportedReceipt> receipts, ImportedRemittance? remittance)
    {
        int? remittanceId = null;
        if (remittance is not null)
        {
            var row = new ScrapRemittance { MadeOn = remittance.MadeOn, FromNo = remittance.FromNo, ToNo = remittance.ToNo, MadeBy = remittance.MadeBy };
            db.ScrapRemittances.Add(row);
            await db.SaveChangesAsync();
            remittanceId = row.Id;
            db.ChangeTracker.Clear();
        }
        foreach (var batch in receipts.Chunk(BatchSize))
        {
            foreach (var r in batch)
            {
                var receipt = new ScrapReceipt
                {
                    No = r.No, Date = r.Date, BuyerName = r.BuyerName, BuyerAddress = r.BuyerAddress, BuyerTel = r.BuyerTel, ScrapBuyerId = r.ScrapBuyerId,
                    RemittanceId = r.Remitted ? remittanceId : null,
                };
                receipt.Lines.AddRange(r.Lines.Select(l => new ScrapReceiptLine { Seq = l.Seq, ItemName = l.ItemName, ScrapItemId = l.ScrapItemId, Unit = l.Unit, Qty = l.Qty, UnitPrice = l.UnitPrice, Amount = l.Amount }));
                db.ScrapReceipts.Add(receipt);
            }
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
        }
    }
}
