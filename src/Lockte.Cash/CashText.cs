using System.Text.RegularExpressions;

namespace Lockte.Cash;

/// <summary>How the module keeps the free text of its registers: the blanks at either end gone, any run of blanks or line breaks one space (the legacy kept them as typed).</summary>
internal static class CashText
{
    private static readonly Regex Blanks = new(@"\s+", RegexOptions.Compiled);

    /// <summary>A name or a short text; null when nothing is left.</summary>
    internal static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : Blanks.Replace(text.Trim(), " ");
}
