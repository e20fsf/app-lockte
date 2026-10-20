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

    /// <summary>Format "mmmm": the Thai month name.</summary>
    public static string Month(DateOnly date) => date.ToString("MMMM", ThaiDate.Culture);

    /// <summary>Format "yyyy" on a Thai Windows: the Buddhist year.</summary>
    public static string Year(DateOnly date) => date.ToString("yyyy", ThaiDate.Culture);
}
