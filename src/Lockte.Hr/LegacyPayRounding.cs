using Lockte.Hr.Data;
using Lockte.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Lockte.Hr;

/// <summary>One column of stored pay figures and how many of its rows are not whole baht.</summary>
public sealed record PayRoundingCount(string Column, int Rows);

/// <summary>
/// One-off rounding of the เงินเดือน stored before #392 (UAT-21): it is whole baht, half up (<see cref="Rounding.RndA"/>), but a
/// row written earlier keeps its satang until it is saved again. ค่าแรง/วัน is not touched: it keeps its satang (decided 2026-11-03). Done in SQL on the stored values because the model rounds whatever it reads and cannot
/// see them. Like the migration, it writes nothing to the change history. A dry run only counts.
/// </summary>
public sealed class LegacyPayRounding(HrDbContext db)
{
    private static readonly (Type Entity, string Property, string Label)[] Columns =
    [
        (typeof(Employee), nameof(Employee.Salary), "พนักงาน เงินเดือน"),
    ];

    /// <summary>Counts the rows <see cref="ApplyAsync"/> would change, changing nothing.</summary>
    public async Task<IReadOnlyList<PayRoundingCount>> DryRunAsync(CancellationToken cancellationToken = default)
    {
        var counts = new List<PayRoundingCount>();
        foreach (var (entity, property, label) in Columns)
        {
            var (table, column) = Locate(entity, property);
            var rows = await db.Database.SqlQueryRaw<int>($"SELECT COUNT(*) AS [Value] FROM {table} WHERE {Fractional(column)}").SingleAsync(cancellationToken);
            counts.Add(new(label, rows));
        }
        return counts;
    }

    /// <summary>Rounds every stored value, all columns in one transaction; returns the rows changed per column.</summary>
    public async Task<IReadOnlyList<PayRoundingCount>> ApplyAsync(CancellationToken cancellationToken = default)
    {
        var counts = new List<PayRoundingCount>();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        foreach (var (entity, property, label) in Columns)
        {
            var (table, column) = Locate(entity, property);
            var rows = await db.Database.ExecuteSqlRawAsync($"UPDATE {table} SET {column} = {Whole(column)} WHERE {Fractional(column)}", cancellationToken);
            counts.Add(new(label, rows));
        }
        await transaction.CommitAsync(cancellationToken);
        return counts;
    }

    // Half away from zero on the value read to 6 decimals, as RndA reads a Double to 15 significant digits.
    private static string Whole(string column) => $"ROUND(CAST({column} AS decimal(38, 6)), 0)";

    private static string Fractional(string column) => $"{column} IS NOT NULL AND {column} <> {Whole(column)}";

    private (string Table, string Column) Locate(Type entity, string property)
    {
        var type = db.Model.FindEntityType(entity)!;
        var id = StoreObjectIdentifier.Table(type.GetTableName()!, type.GetSchema());
        return ($"[{type.GetSchema()}].[{type.GetTableName()}]", $"[{type.FindProperty(property)!.GetColumnName(id)}]");
    }
}
