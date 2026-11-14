using System.Globalization;

namespace Lockte.SharedKernel;

/// <summary>
/// ลำดับของชื่อในรายการ (UAT วัตถุดิบ รอบ 2): A–Z ก่อน แล้วต่อด้วย ก–ฮ, ไม่สนตัวพิมพ์เล็ก/ใหญ่ — the Thai collation puts Latin before Thai and
/// reads the Thai in dictionary order. The one comparer for every list of suppliers and materials by name, on screen and printed.
/// </summary>
public static class NameOrder
{
    private static readonly StringComparer Thai = StringComparer.Create(CultureInfo.ReadOnly(new CultureInfo("th-TH")), ignoreCase: true);

    /// <summary>A–Z (and digits, symbols) first, then Thai; each half in its own collation. The Thai culture alone puts Thai first, so the halves are split by hand.</summary>
    public static IComparer<string> Comparer { get; } = Comparer<string>.Create(Compare);

    private static bool IsThai(string text)
    {
        var first = text.TrimStart().FirstOrDefault();
        return first is >= '฀' and <= '๿';
    }

    private static int Compare(string? a, string? b)
    {
        a ??= "";
        b ??= "";
        var (thaiA, thaiB) = (IsThai(a), IsThai(b));
        if (thaiA != thaiB) return thaiA ? 1 : -1;
        return thaiA ? Thai.Compare(a, b) : StringComparer.InvariantCultureIgnoreCase.Compare(a, b);
    }

    /// <summary>Order by name, then by code (ordinal) for equal names.</summary>
    public static IEnumerable<T> ByName<T>(IEnumerable<T> items, Func<T, string?> name, Func<T, string?> code)
        => items.OrderBy(i => name(i) ?? "", Comparer).ThenBy(i => code(i) ?? "", StringComparer.Ordinal);
}
