using Lockte.Web.Menus;

namespace Lockte.Web.Accounts;

/// <summary>
/// สิทธิ์เข้าโปรแกรม (ADR 0005): which of the five programs (<see cref="ProgramMenus.All"/>) a user may open, set per
/// user by the admin. Stored on <see cref="AppUser.Programs"/> as the program keys joined by commas, always in the
/// order of the program chooser, so the change history reads the same for the same choice.
/// </summary>
public static class ProgramAccess
{
    /// <summary>Every program key, in the order of the program chooser.</summary>
    public static IReadOnlyList<string> AllKeys { get; } = ProgramMenus.All.Select(p => p.Key).ToList();

    /// <summary>All programs — what the users that existed before ADR 0005 got.</summary>
    public static string All { get; } = string.Join(",", AllKeys);

    public static IReadOnlyList<string> Parse(string? stored)
        => (stored ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(k => AllKeys.Contains(k)).Distinct().ToList();

    /// <summary>Known keys only, in chooser order, no duplicates.</summary>
    public static string Format(IEnumerable<string>? keys)
    {
        var chosen = (keys ?? []).ToHashSet(StringComparer.Ordinal);
        return string.Join(",", AllKeys.Where(chosen.Contains));
    }

    /// <summary>
    /// The program a report URL belongs to: <c>/reports/{program key}/…</c>. Null for anything else.
    /// </summary>
    public static string? OfReportRoute(string? route)
    {
        const string prefix = "/reports/";
        if (route is null || !route.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return null;
        var key = route[prefix.Length..].Split('/')[0];
        return AllKeys.FirstOrDefault(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));
    }
}
