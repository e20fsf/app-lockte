using System.Globalization;

namespace Lockte.SharedKernel;

/// <summary>วันที่แบบ พ.ศ. as screens and messages show it: <c>d/M/yyyy</c> in the Thai Buddhist calendar (ADR 0002).</summary>
public static class ThaiDate
{
    public const string Pattern = "d/M/yyyy";

    /// <summary>th-TH: Buddhist years, Thai day and month names. Also the culture of date pickers.</summary>
    public static CultureInfo Culture { get; } = CultureInfo.ReadOnly(new CultureInfo("th-TH"));

    public static string Format(DateOnly date) => date.ToString(Pattern, Culture);
}
