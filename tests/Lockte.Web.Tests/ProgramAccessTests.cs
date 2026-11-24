using System.Security.Claims;
using Lockte.Web.Accounts;
using Lockte.Web.Menus;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Lockte.Web.Tests;

/// <summary>
/// สิทธิ์เข้าโปรแกรม (ADR 0005) without a database: the stored list, the claim, the policy per program, which program a
/// page or report belongs to, and that every report carries the policy of its program.
/// </summary>
public sealed class ProgramAccessTests
{
    private static ClaimsPrincipal User(IEnumerable<string> programs, params string[] flags)
        => new(new ClaimsIdentity(programs.Select(p => new Claim(AccountClaims.Program, p))
            .Concat(flags.Select(f => new Claim(f, "1"))).Append(new Claim(ClaimTypes.Name, "u")), "test"));

    private static async Task<bool> PolicyAllowsAsync(ClaimsPrincipal user, string policy)
    {
        var services = new ServiceCollection().AddLogging().AddAuthorization(AuthPolicies.Configure).BuildServiceProvider();
        return (await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(user, null, policy)).Succeeded;
    }

    [Fact]
    public void Stored_list_keeps_known_programs_in_chooser_order()
    {
        Assert.Equal(["hr", "order", "purch", "calib", "cash"], ProgramAccess.AllKeys);
        Assert.Equal("hr,order,purch,calib,cash", ProgramAccess.All); // also written by the UserPrograms migration
        Assert.Equal("order,calib", ProgramAccess.Format(["calib", "order", "calib", "nonsense"]));
        Assert.Equal("", ProgramAccess.Format(null));
        Assert.Equal(["order", "cash"], ProgramAccess.Parse(" order , cash,,bogus"));
        Assert.Empty(ProgramAccess.Parse(null));
    }

    [Theory]
    [InlineData("/reports/hr/warnings", "hr")]
    [InlineData("/reports/order/stock/x", "order")]
    [InlineData("/reports/purch/lists", "purch")]
    [InlineData("/reports/calib/plan", "calib")]
    [InlineData("/reports/cash/voucher", "cash")]
    [InlineData("/reports/other/x", null)]
    [InlineData("/hr/employees", null)]
    public void Report_route_belongs_to_the_program_in_its_path(string route, string? program)
        => Assert.Equal(program, ProgramAccess.OfReportRoute(route));

    [Theory]
    [InlineData("menu/3", "hr")]
    [InlineData("org/departments", "hr")]
    [InlineData("hr/employees/12", "hr")]
    [InlineData("order", "order")]
    [InlineData("purch/menu/2", "purch")]
    [InlineData("calib", "calib")]
    [InlineData("cash/menu/1", "cash")]
    public void Page_belongs_to_its_program(string path, string program)
        => Assert.Equal(program, ProgramMenus.Of(path)?.Key);

    [Theory]
    [InlineData("programs")]
    [InlineData("document-numbers")]
    [InlineData("admin/users")]
    public void Pages_of_no_program_stay_open(string path) => Assert.Null(ProgramMenus.Of(path));

    [Fact]
    public async Task Program_policy_allows_only_staff_given_that_program_and_never_the_admin()
    {
        var qa = User(["order", "calib"]);
        Assert.True(await PolicyAllowsAsync(qa, AuthPolicies.Program("order")));
        Assert.True(await PolicyAllowsAsync(qa, AuthPolicies.Program("calib")));
        Assert.False(await PolicyAllowsAsync(qa, AuthPolicies.Program("hr")));
        Assert.False(await PolicyAllowsAsync(User(["hr"], AccountClaims.Admin), AuthPolicies.Program("hr")));
        Assert.False(await PolicyAllowsAsync(User(["hr"], AccountClaims.MustChangePassword), AuthPolicies.Program("hr")));
        Assert.False(AccountClaims.CanUseProgram(User(["hr"], AccountClaims.Admin), "hr"));
    }

    [Fact]
    public void Every_report_route_belongs_to_a_program_and_carries_its_policy()
    {
        var reports = PayAccessTests.ReportEndpoints()
            .Where(e => e.RoutePattern.RawText!.StartsWith("/reports/", StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(reports);

        var outside = reports.Select(e => e.RoutePattern.RawText!).Where(r => ProgramAccess.OfReportRoute(r) is null).ToList();
        Assert.True(outside.Count == 0, "Report route(s) not under /reports/{program}: " + string.Join(", ", outside));

        Assert.All(reports, e =>
        {
            var policy = AuthPolicies.Program(ProgramAccess.OfReportRoute(e.RoutePattern.RawText)!);
            Assert.True(e.Metadata.GetOrderedMetadata<IAuthorizeData>().Any(a => a.Policy == policy), e.RoutePattern.RawText);
        });
    }

    [Fact]
    public void Sheet_has_user01_to_user18_with_the_ticked_programs()
    {
        var sheet = InitialUsers.Sheet;
        Assert.Equal(Enumerable.Range(1, 18).Select(n => $"user{n:00}"), sheet.Select(u => u.UserName));
        Assert.Equal("hr,order,purch,calib,cash", ProgramAccess.Format(sheet[0].Programs)); // au
        Assert.Equal("order", ProgramAccess.Format(sheet[4].Programs)); // p'su
        Assert.Equal("hr,order,purch", ProgramAccess.Format(sheet[9].Programs)); // แต๋ว
        Assert.Equal("hr,order,purch,cash", ProgramAccess.Format(sheet[10].Programs)); // นก
        Assert.Equal("order,purch", ProgramAccess.Format(sheet[11].Programs)); // บอม
        Assert.All(sheet.Skip(12).Take(4), u => Assert.Equal("order,calib", ProgramAccess.Format(u.Programs))); // QA1–4
        Assert.All(new[] { 7, 8, 17, 18 }, n => Assert.Empty(sheet[n - 1].Programs)); // สำรอง
    }
}
