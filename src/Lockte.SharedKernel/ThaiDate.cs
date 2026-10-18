using System.Globalization;

namespace Lockte.SharedKernel;

/// <summary>วันที่แบบ พ.ศ. as users read and type them (ADR 0002): d/M/yyyy in the Thai Buddhist calendar.</summary>
public static class ThaiDate
{
    public const string Format = "d/M/yyyy";

    /// <summary>th-TH: Buddhist years, Thai day and month names.</summary>
    public static CultureInfo Culture { get; } = CultureInfo.ReadOnly(new CultureInfo("th-TH"));

    public static string Text(DateOnly date) => date.ToString(Format, Culture);

    /// <summary>ปี พ.ศ. of a Gregorian year.</summary>
    public static int BuddhistYear(int year) => year + 543;

    /// <summary>ชื่อวัน, e.g. "วันอาทิตย์".</summary>
    public static string DayName(DateOnly date) => date.ToString("dddd", Culture);
}
