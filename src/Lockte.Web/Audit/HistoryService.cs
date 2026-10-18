using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Web.Audit;

/// <summary>Search filter of the history page; every part optional. <see cref="RowKey"/> matches part of the key.</summary>
public sealed record HistoryFilter(
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
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
        if (filter.From is { } from) query = query.Where(h => h.At >= from);
        if (filter.To is { } to) query = query.Where(h => h.At < to);
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
