using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Web.Audit;

/// <summary>
/// Search filter of the history page; every part optional. Dates are days at the factory (server local time),
/// both included. <see cref="RowKey"/> matches part of the key.
/// </summary>
public sealed record HistoryFilter(
    DateOnly? From = null,
    DateOnly? To = null,
    string? UserName = null,
    string? Table = null,
    string? RowKey = null,
    int Max = HistoryService.DefaultMax);

public sealed record HistoryRow(
    long Id,
    DateTimeOffset At,
    string UserName,
    HistoryKind Kind,
    string? Table,
    string? RowKey,
    string Action,
    string? Column,
    string? OldValue,
    string? NewValue,
    string? Detail,
    int? RowCount);

/// <summary>One column of a change: its name and the value before and after (null when the row was added / deleted).</summary>
public sealed record HistoryChange(string? Column, string? OldValue, string? NewValue);

/// <summary>
/// One save of one row in the history of a record: who, when, เพิ่ม / แก้ / ลบ, which row (<see cref="Table"/> and
/// <see cref="RowKey"/>) and the columns it changed.
/// </summary>
public sealed record HistoryEntry(DateTimeOffset At, string UserName, string Action, string? Table, string? RowKey, IReadOnlyList<HistoryChange> Changes);

/// <summary>
/// หน้าค้นประวัติ (#43): read-only search over every change, event and login — there is no way to delete history.
/// Also records login attempts.
/// </summary>
public sealed class HistoryService(AuditDbContext db)
{
    public const int DefaultMax = 500;
    public const string LoginTable = "การเข้าระบบ";
    public const string LoginSucceeded = "login สำเร็จ";
    public const string LoginFailed = "login ไม่สำเร็จ";

    /// <summary>Newest first, at most <see cref="HistoryFilter.Max"/> rows.</summary>
    public async Task<IReadOnlyList<HistoryRow>> SearchAsync(HistoryFilter filter)
    {
        var query = db.History.AsNoTracking();
        if (filter.From is { } from)
        {
            var start = StartOfLocalDay(from);
            query = query.Where(h => h.At >= start);
        }
        if (filter.To is { } to)
        {
            var end = StartOfLocalDay(to.AddDays(1));
            query = query.Where(h => h.At < end);
        }
        if (!string.IsNullOrWhiteSpace(filter.UserName)) query = query.Where(h => h.UserName == filter.UserName.Trim());
        if (!string.IsNullOrWhiteSpace(filter.Table)) query = query.Where(h => h.Table == filter.Table);
        if (!string.IsNullOrWhiteSpace(filter.RowKey)) query = query.Where(h => h.RowKey!.Contains(filter.RowKey.Trim()));

        return await query
            .OrderByDescending(h => h.At).ThenByDescending(h => h.Id)
            .Take(filter.Max)
            .Select(h => new HistoryRow(h.Id, h.At, h.UserName, h.Kind, h.Table, h.RowKey, h.Action,
                h.Column, h.OldValue, h.NewValue, h.Detail, h.RowCount))
            .ToListAsync();
    }

    /// <summary>The audit names of an ORDER and of its lines (Lockte.Sales SalesDbContext); a line's key is "เลขที่ ORDER #ลำดับ".</summary>
    public const string OrderTable = "ORDER";
    public const string OrderLineTable = "บรรทัด ORDER";

    /// <summary>
    /// ประวัติใบ ORDER (UAT 2026-11-24): every change of the order <paramref name="orderNo"/> and of its lines, oldest first,
    /// one entry per save of one row — also after the order was deleted.
    /// </summary>
    public async Task<IReadOnlyList<HistoryEntry>> ForOrderAsync(string orderNo)
    {
        var number = orderNo.Trim();
        if (number.Length == 0) return [];
        var linePrefix = number + " #";
        var rows = await db.History.AsNoTracking()
            .Where(h => h.Kind == HistoryKind.Change
                && ((h.Table == OrderTable && h.RowKey == number) || (h.Table == OrderLineTable && h.RowKey!.StartsWith(linePrefix))))
            .OrderBy(h => h.At).ThenBy(h => h.Id)
            .ToListAsync();
        return rows
            .GroupBy(h => (h.ChangeSet, Set: h.ChangeSet is null ? h.Id : 0, h.Table, h.RowKey, h.Action))
            .Select(g => new HistoryEntry(g.First().At, g.First().UserName, g.Key.Action, g.Key.Table, g.Key.RowKey,
                g.Select(h => new HistoryChange(h.Column, h.OldValue, h.NewValue)).ToList()))
            .ToList();
    }

    private static DateTimeOffset StartOfLocalDay(DateOnly day)
    {
        var midnight = day.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(midnight, TimeZoneInfo.Local.GetUtcOffset(midnight));
    }

    /// <summary>Table names that appear in the history, for the filter list.</summary>
    public async Task<IReadOnlyList<string>> TablesAsync()
        => await db.History.Where(h => h.Table != null).Select(h => h.Table!).Distinct().OrderBy(t => t).ToListAsync();

    /// <summary>One row per login attempt; <paramref name="userName"/> is the name typed, which may not exist.</summary>
    public async Task RecordLoginAsync(string userName, bool succeeded, string detail)
    {
        db.History.Add(new HistoryRecord
        {
            Kind = HistoryKind.Login,
            UserName = userName,
            Table = LoginTable,
            RowKey = userName,
            Action = succeeded ? LoginSucceeded : LoginFailed,
            Detail = detail,
        });
        await db.SaveChangesAsync();
    }
}
