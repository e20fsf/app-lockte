using Microsoft.EntityFrameworkCore;

namespace Lockte.Cash;

/// <summary>
/// The delete-and-reload side of the migration of <c>เงินสดย่อย.accdb</c> (#231): registered for <c>Lockte.Migration</c> only, never in the web app. It deletes the master data of
/// <c>cash</c> a run loads — the expense types, the scrap buyers and the scrap kinds. Rows are written through <see cref="ExpenseTypeRegistry"/>, <see cref="ScrapBuyerRegistry"/> and
/// <see cref="ScrapItemRegistry"/>, so a loaded row passes the same checks as one keyed on screen. The tickets that load the lines and the receipts (#236, #241) delete those first here.
/// </summary>
public sealed class CashMasterImport(Data.CashDbContext db)
{
    public async Task DeleteMasterAsync()
    {
        await db.ExpenseTypes.ExecuteDeleteAsync();
        await db.ScrapBuyers.ExecuteDeleteAsync();
        await db.ScrapItems.ExecuteDeleteAsync();
    }
}
