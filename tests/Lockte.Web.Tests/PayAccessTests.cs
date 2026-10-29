using System.Security.Claims;
using Lockte.Hr;
using Lockte.Org;
using Lockte.Prod;
using Lockte.Sales;
using Lockte.SharedKernel.Persistence;
using Lockte.Web.Accounts;
using Lockte.Web.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Lockte.Web.Tests;

/// <summary>
/// ดูข้อมูลค่าจ้างได้ (#277) without a database: the claim, the policy, and that every report route is decided
/// (pay → ViewPay gate, otherwise none), so a new report cannot slip through unclassified.
/// </summary>
public sealed class PayAccessTests
{
    private static ClaimsPrincipal User(params string[] claimTypes)
        => new(new ClaimsIdentity(claimTypes.Select(t => new Claim(t, "1")).Append(new Claim(ClaimTypes.Name, "u")), "test"));

    private static async Task<bool> PolicyAllowsAsync(ClaimsPrincipal user, string policy)
    {
        var services = new ServiceCollection().AddLogging().AddAuthorization(AuthPolicies.Configure).BuildServiceProvider();
        var result = await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(user, null, policy);
        return result.Succeeded;
    }

    [Fact]
    public void Only_staff_with_the_flag_can_view_pay_and_the_admin_never_can()
    {
        Assert.True(AccountClaims.CanViewPay(User(AccountClaims.ViewPay)));
        Assert.False(AccountClaims.CanViewPay(User()));
        Assert.False(AccountClaims.CanViewPay(User(AccountClaims.ViewPay, AccountClaims.Admin)));
    }

    [Fact]
    public async Task View_pay_policy_follows_the_claim_and_not_a_temporary_password()
    {
        Assert.True(await PolicyAllowsAsync(User(AccountClaims.ViewPay), AuthPolicies.ViewPay));
        Assert.False(await PolicyAllowsAsync(User(), AuthPolicies.ViewPay));
        Assert.False(await PolicyAllowsAsync(User(AccountClaims.ViewPay, AccountClaims.Admin), AuthPolicies.ViewPay));
        Assert.False(await PolicyAllowsAsync(User(AccountClaims.ViewPay, AccountClaims.MustChangePassword), AuthPolicies.ViewPay));
        // The other gates are as before: a user without the flag is still ordinary staff.
        Assert.True(await PolicyAllowsAsync(User(), AuthPolicies.Staff));
    }

    /// <summary>The report endpoints as the host maps them (nothing is opened: no database, no Chromium).</summary>
    private static List<RouteEndpoint> ReportEndpoints()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"ConnectionStrings:{AccountsRegistration.ConnectionStringName}"] = @"Server=(localdb)\none;Database=none;Integrated Security=true",
        });
        var cs = AccountsRegistration.ConnectionString(builder.Configuration);
        builder.Services.AddLockteAccounts(builder.Configuration);
        builder.Services.AddOrgModule(cs);
        builder.Services.AddHrModule(cs);
        builder.Services.AddSalesModule(cs);
        builder.Services.AddProdModule(cs);
        builder.Services.AddReportData(cs);
        builder.Services.AddReportPrinting(builder.Configuration);
        builder.Services.AddScoped<ICurrentUser, SystemUser>();
        builder.Services.AddScoped<Reports.Hr.FormPrinter>();
        var app = builder.Build();
        app.MapReports();
        Reports.Hr.HrDocumentEndpoints.MapHrDocuments(app);
        return ((IEndpointRouteBuilder)app).DataSources.SelectMany(d => d.Endpoints).OfType<RouteEndpoint>().ToList();
    }

    private static bool Gated(Endpoint endpoint)
        => endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Any(a => a.Policy == AuthPolicies.ViewPay);

    [Fact]
    public void Every_hr_report_route_is_classified_and_exactly_the_pay_ones_are_gated()
    {
        var hr = ReportEndpoints().Where(e => e.RoutePattern.RawText!.StartsWith("/reports/hr", StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(hr);

        var unclassified = hr.Select(e => e.RoutePattern.RawText!)
            .Where(t => !PayReportRoutes.Pay.Contains(t) && !PayReportRoutes.Mixed.Contains(t) && !PayReportRoutes.NoPay.Contains(t))
            .ToList();
        Assert.True(unclassified.Count == 0,
            "New report route(s) not decided in PayReportRoutes (pay → Pay, no pay → NoPay): " + string.Join(", ", unclassified));

        Assert.All(hr.Where(e => PayReportRoutes.Pay.Contains(e.RoutePattern.RawText!)), e => Assert.True(Gated(e), e.RoutePattern.RawText));
        Assert.All(hr.Where(e => !PayReportRoutes.Pay.Contains(e.RoutePattern.RawText!)), e => Assert.False(Gated(e), e.RoutePattern.RawText));
    }

    [Fact]
    public void Every_listed_route_exists()
    {
        var mapped = ReportEndpoints().Select(e => e.RoutePattern.RawText!).ToHashSet();
        var missing = PayReportRoutes.Pay.Concat(PayReportRoutes.Mixed).Concat(PayReportRoutes.NoPay).Where(r => !mapped.Contains(r)).ToList();
        Assert.True(missing.Count == 0, "Listed in PayReportRoutes but not mapped (typo or removed report): " + string.Join(", ", missing));
    }

    [Fact]
    public void Order_program_reports_are_not_pay_gated()
    {
        var order = ReportEndpoints().Where(e => e.RoutePattern.RawText!.StartsWith("/reports/order", StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(order);
        Assert.All(order, e => Assert.False(Gated(e), e.RoutePattern.RawText));
    }

    [Fact]
    public void Pay_routes_cover_every_pay_report_the_screens_link_to()
    {
        // The reports the สรุป of #277 names: pay, summary, bank files (PDF and Excel share the route), slips, bonus, adjustment.
        string[] expected =
        [
            "/reports/hr/pay-periods/{id:int}/pay", "/reports/hr/pay-periods/{id:int}/pay-summary", "/reports/hr/pay-periods/{id:int}/bank/{bank}",
            "/reports/hr/pay-periods/{id:int}/slips", "/reports/hr/pay-periods/{id:int}/slips-with-bonus",
            "/reports/hr/pay-periods/{id:int}/bonus-bank/{bank}", "/reports/hr/bonus", "/reports/hr/pay-adjustment",
            "/reports/hr/pay/month-end", "/reports/hr/pay/monthly", "/reports/hr/vacation-pay", "/reports/hr/severance/{employeeId:int}",
            "/reports/hr/two-part-periods/{first:int}/{second:int}/bank",
        ];
        Assert.All(expected, r => Assert.Contains(r, PayReportRoutes.Pay));
    }
}
