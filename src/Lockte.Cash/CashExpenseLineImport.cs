using Lockte.Cash.Data;

namespace Lockte.Cash;

/// <summary>A line of <c>ซื้อของเงินสด</c> as the migration hands it over: the values of the legacy row, kept as they were (the amount is never worked out again).</summary>
public sealed record ImportedExpenseLine(DateOnly Date, string VoucherNo, string Name, decimal Qty, string? Unit, decimal UnitPrice, decimal Amount, int ExpenseTypeId);

/// <summary>
/// The bulk-load side of the migration of <c>เงินสดย่อย.accdb</c> (#236): registered for <c>Lockte.Migration</c> only, never in the web app. A line moved from the legacy is not keyed on
/// screen, so it is written as it is — a quantity of 0 (fuel typed by amount), a price of 0 — instead of through <see cref="ExpenseLineRegistry"/>, whose checks are those of a line being
/// keyed. The rows go in the order given, so their internal numbers follow the order of the legacy table.
/// </summary>
public sealed class CashExpenseLineImport(Data.CashDbContext db)
{
    private const int BatchSize = 1000;

    public async Task LoadAsync(IReadOnlyList<ImportedExpenseLine> lines)
    {
        foreach (var batch in lines.Chunk(BatchSize))
        {
            db.ExpenseLines.AddRange(batch.Select(l => new ExpenseLine
            {
                Date = l.Date, VoucherNo = l.VoucherNo, Name = l.Name, Qty = l.Qty, Unit = l.Unit, UnitPrice = l.UnitPrice, Amount = l.Amount, ExpenseTypeId = l.ExpenseTypeId,
            }));
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
        }
    }
}
