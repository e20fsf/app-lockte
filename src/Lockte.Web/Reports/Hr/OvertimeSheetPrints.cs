using Lockte.Hr;
using Microsoft.Extensions.Caching.Memory;

namespace Lockte.Web.Reports.Hr;

/// <summary>
/// Hands a printed ใบขออนุมัติทำ OT from the screen to the report's GET. The sheet is not a table (#41, T-OT was a
/// staging table), so SCR-14 keeps the sheet it just saved here under a random key and opens
/// <c>/reports/hr/overtime-sheets/{key}</c> in a new tab. Only the user who printed it can open it, for
/// <see cref="Lifetime"/> (as the legacy, reprinting is possible until the next sheet is started — here until it expires).
/// </summary>
public sealed class OvertimeSheetPrints(IMemoryCache cache)
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(8);

    public Guid Keep(OvertimeSheet sheet, string userName)
    {
        var key = Guid.NewGuid();
        cache.Set(CacheKey(key), (sheet, userName), Lifetime);
        return key;
    }

    public OvertimeSheet? Find(Guid key, string? userName)
        => cache.TryGetValue(CacheKey(key), out (OvertimeSheet Sheet, string UserName) kept) && kept.UserName == userName ? kept.Sheet : null;

    public static string Href(Guid key) => $"reports/hr/overtime-sheets/{key:D}";

    private static string CacheKey(Guid key) => "overtime-sheet:" + key.ToString("N");
}
