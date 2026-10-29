using System.Globalization;

namespace Lockte.Web.Reports.Purch;

/// <summary>A date in the form the report endpoints and the file names take, <c>yyyy-MM-dd</c> in the Gregorian calendar whatever the culture of the thread (th-TH would give the Buddhist year).</summary>
public static class UrlDate
{
    public static string Of(DateTime? date) => date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "";

    public static string Of(DateOnly? date) => date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "";
}
