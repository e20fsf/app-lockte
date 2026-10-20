using Lockte.SharedKernel;

namespace Lockte.Web.Reports.Hr;

/// <summary>Number text of the employee lists (RPT-21).</summary>
public static class EmployeeListText
{
    /// <summary>A whole number as Access's Fixed / Standard with 0 decimals: half away from zero, thousands separated.</summary>
    public static string Whole(double value) => Math.Round((decimal)value, 0, MidpointRounding.AwayFromZero).ToString("#,##0", ThaiDate.Culture);
}
