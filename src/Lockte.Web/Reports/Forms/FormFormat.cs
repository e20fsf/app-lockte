using System.Globalization;
using Lockte.SharedKernel;

namespace Lockte.Web.Reports.Forms;

/// <summary>The Access formats the legacy forms used, for ระดับ A text.</summary>
public static class FormFormat
{
    /// <summary>Format "Standard" with <paramref name="decimals"/> places: thousands separators, half away from zero.</summary>
    public static string Standard(double value, int decimals)
        => Math.Round((decimal)value, decimals, MidpointRounding.AwayFromZero).ToString("#,##0" + (decimals > 0 ? "." + new string('0', decimals) : ""), CultureInfo.InvariantCulture);

    /// <summary>Format "Long Date" on a Thai Windows: "15 ตุลาคม 2569".</summary>
    public static string LongDate(DateOnly date) => date.ToString("d MMMM yyyy", ThaiDate.Culture);

    /// <summary>
    /// A Thai telephone number with its dashes (UAT-29): <c>024204554</c> → <c>02-4204554</c>, <c>0223360023</c> → <c>02-2336002-3</c>
    /// (a number after the 7 digits is the next line or extension), 10-digit mobile <c>0812345678</c> → <c>081-234-5678</c>, other
    /// 9-digit area codes <c>0X-XXXXXXX</c>. Anything that is not only digits, spaces and dashes (two numbers, "ต่อ 12") is left as typed.
    /// </summary>
    public static string Phone(string? value)
    {
        var text = value?.Trim() ?? "";
        if (text.Length == 0 || !text.All(c => char.IsAsciiDigit(c) || c is '-' or ' ')) return text;
        var d = new string(text.Where(char.IsAsciiDigit).ToArray());
        if (d.Length < 9 || d.Length > 11 || d[0] != '0') return text;
        if (d.Length == 10 && d[1] is '6' or '8' or '9') return $"{d[..3]}-{d[3..6]}-{d[6..]}";
        return d.Length == 9 ? $"{d[..2]}-{d[2..]}" : $"{d[..2]}-{d[2..9]}-{d[9..]}";
    }

    /// <summary>
    /// A ถนน value without a leading "ถนน" / "ถ.", for forms that print the word themselves: the server's setting may hold either
    /// <c>ตัวอย่างสาย 5</c> or <c>ถนนตัวอย่างสาย 5</c>, and the form must not read "ถนน ถนนตัวอย่างสาย 5".
    /// </summary>
    public static string Road(string? value)
    {
        var text = value?.Trim() ?? "";
        foreach (var prefix in new[] { "ถนน", "ถ." })
            if (text.StartsWith(prefix, StringComparison.Ordinal) && text.Length > prefix.Length) return text[prefix.Length..].TrimStart();
        return text;
    }

    /// <summary>Format "mmmm": the Thai month name.</summary>
    public static string Month(DateOnly date) => date.ToString("MMMM", ThaiDate.Culture);

    /// <summary>Format "yyyy" on a Thai Windows: the Buddhist year.</summary>
    public static string Year(DateOnly date) => date.ToString("yyyy", ThaiDate.Culture);
}
