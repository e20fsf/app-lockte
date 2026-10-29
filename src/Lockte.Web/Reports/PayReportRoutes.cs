using Lockte.Web.Accounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing.Patterns;

namespace Lockte.Web.Reports;

/// <summary>
/// #277: the one list of the reports, bank files and slips that carry ข้อมูลค่าจ้าง. A user without ดูข้อมูลค่าจ้างได้ gets
/// 403 from these (not a report with ***). Every endpoint under <c>/reports/hr</c> must be in exactly one of
/// <see cref="Pay"/>, <see cref="Mixed"/> or <see cref="NoPay"/>; a test fails for a new route that is in none, so
/// whoever adds a report has to decide.
/// </summary>
public static class PayReportRoutes
{
    /// <summary>Wholly or partly pay: ค่าแรง, OT, โบนัส, เงินสงเคราะห์, เลขบัญชี. Same route for PDF and Excel/bank file.</summary>
    public static readonly IReadOnlySet<string> Pay = new HashSet<string>(StringComparer.Ordinal)
    {
        "/reports/hr/pay-periods/{id:int}/pay",
        "/reports/hr/pay-periods/{id:int}/pay-summary",
        "/reports/hr/pay-periods/{id:int}/bank/{bank}",
        "/reports/hr/pay-periods/{id:int}/slips",
        "/reports/hr/pay-periods/{id:int}/bonus-bank/{bank}",
        "/reports/hr/pay-periods/{id:int}/slips-with-bonus",
        "/reports/hr/two-part-periods/{first:int}/{second:int}/pay-summary",
        "/reports/hr/two-part-periods/{first:int}/{second:int}/bank",
        "/reports/hr/pay/contract-labor",
        "/reports/hr/pay/month-end",
        "/reports/hr/pay/month-end/summary",
        "/reports/hr/pay/monthly",
        "/reports/hr/pay/totals",
        "/reports/hr/bonus",
        "/reports/hr/bonus/last",
        "/reports/hr/pay-adjustment",
        "/reports/hr/vacation-pay",
        "/reports/hr/leave/employee/{id:int}",
        "/reports/hr/leave/summary",
        "/reports/hr/employees/roster",
        "/reports/hr/employees/register",
        "/reports/hr/employees/{id:int}/contract",
        "/reports/hr/employees/{id:int}/hiring-form",
        "/reports/hr/employees/{id:int}/kor2",
        "/reports/hr/insured-persons",
        "/reports/hr/severance/{employeeId:int}",
    };

    /// <summary>Pay only for some inputs: the handler asks <see cref="PayReportAccess"/> itself.</summary>
    public static readonly IReadOnlySet<string> Mixed = new HashSet<string>(StringComparer.Ordinal)
    {
        "/reports/hr/certificate/{employeeId:int}", // ?kind=salary is หนังสือรับรองเงินเดือน
    };

    /// <summary>Checked: nothing in these is ข้อมูลค่าจ้าง (hours, dates, names, text).</summary>
    public static readonly IReadOnlySet<string> NoPay = new HashSet<string>(StringComparer.Ordinal)
    {
        "/reports/hr/warnings",
        "/reports/hr/leave/department",
        "/reports/hr/leave/list",
        "/reports/hr/leave/absent",
        "/reports/hr/time/late",
        "/reports/hr/time/hours",
        "/reports/hr/time/overtime",
        "/reports/hr/time/attendance",
        "/reports/hr/time/absences",
        "/reports/hr/pay-periods/{id:int}/time",
        "/reports/hr/overtime-sheets/{key:guid}",
        "/reports/hr/evaluations",
        "/reports/hr/yearly-evaluation",
        "/reports/hr/employees/on-probation",
        "/reports/hr/employees/working",
        "/reports/hr/employees/{id:int}/probation-approval",
        "/reports/hr/leave-form/{employeeId:int}",
        "/reports/hr/resignation-letter/{employeeId:int}",
        "/reports/hr/resigned",
        "/reports/hr/vacation-notice",
    };

    public static bool IsPay(RoutePattern pattern) => pattern.RawText is { } text && Pay.Contains(text);

    /// <summary>Adds the <see cref="AuthPolicies.ViewPay"/> gate to every listed route of a group (and of the groups inside it).</summary>
    public static TBuilder RequirePayWhereListed<TBuilder>(this TBuilder group) where TBuilder : IEndpointConventionBuilder
    {
        group.Add(endpoint =>
        {
            if (endpoint is RouteEndpointBuilder route && IsPay(route.RoutePattern))
                route.Metadata.Add(new AuthorizeAttribute(AuthPolicies.ViewPay));
        });
        return group;
    }
}

/// <summary>For a route that is pay only for some inputs: 403 unless the signed-in user may see ข้อมูลค่าจ้าง.</summary>
public static class PayReportAccess
{
    public static bool Allowed(HttpContext http) => AccountClaims.CanViewPay(http.User);

    public static IResult Forbidden() => Results.Text("ไม่มีสิทธิ์ดูข้อมูลค่าจ้าง", statusCode: StatusCodes.Status403Forbidden);
}
