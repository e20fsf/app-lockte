using System.Globalization;

namespace Lockte.Web.Components.Pages.Hr;

/// <summary>เวลาเป็นข้อความ ชั่วโมง:นาที on the เวลาทำงาน pages (SCR-12/13).</summary>
internal static class TimeText
{
    public static string Format(TimeOnly? time) => time?.ToString("HH:mm", CultureInfo.InvariantCulture) ?? "";

    /// <summary>Blank = no time; otherwise H:mm, HH:mm, H.mm or HHmm.</summary>
    public static bool TryParse(string? text, out TimeOnly? time)
    {
        time = null;
        if (string.IsNullOrWhiteSpace(text)) return true;
        if (!TimeOnly.TryParseExact(text.Trim(), ["H:mm", "HH:mm", "H.mm", "HH.mm", "HHmm"], CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var t))
            return false;
        time = t;
        return true;
    }

    /// <summary>WORK_T or ชั่วโมง as shown in the tables: 2 decimals, blank without both times.</summary>
    public static string Number(double? value) => value?.ToString("0.##", CultureInfo.InvariantCulture) ?? "";
}
