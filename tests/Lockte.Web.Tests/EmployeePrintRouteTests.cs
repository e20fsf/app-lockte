using Lockte.Web.Reports;

namespace Lockte.Web.Tests;

/// <summary>#350–#352: the documents printed from an employee's data page carry pay, so their routes stay behind ViewPay (ADR 0004).</summary>
public sealed class EmployeePrintRouteTests
{
    [Theory]
    [InlineData("/reports/hr/employees/{id:int}/kor2")]
    [InlineData("/reports/hr/employees/{id:int}/contract")]
    [InlineData("/reports/hr/employees/{id:int}/hiring-form")]
    public void The_employee_documents_are_pay_routes(string route) => Assert.Contains(route, PayReportRoutes.Pay);
}

/// <summary>#351: the two contract buttons choose the form, no kind follows the employee's ประเภท as before.</summary>
public sealed class ContractKindTests
{
    [Theory]
    [InlineData("monthly", false, true)]
    [InlineData("MONTHLY", false, true)]
    [InlineData("daily", true, false)]
    [InlineData(null, true, true)]
    [InlineData(null, false, false)]
    [InlineData("other", true, true)]
    public void The_kind_picks_the_contract(string? kind, bool employeeIsMonthly, bool expected)
        => Assert.Equal(expected, Lockte.Web.Reports.Hr.OnboardingReports.ContractIsMonthly(kind, employeeIsMonthly));

    [Fact]
    public void The_href_carries_the_kind()
    {
        Assert.Equal("reports/hr/employees/7/contract?kind=monthly", Lockte.Web.Reports.Hr.OnboardingReports.ContractHref(7, "monthly"));
        Assert.Equal("reports/hr/employees/7/contract", Lockte.Web.Reports.Hr.OnboardingReports.ContractHref(7));
    }
}

/// <summary>#352: the button of ฟอร์มการรับพนักงาน carries the typed dates (invariant yyyy-MM-dd, not the Thai calendar) and none when blank.</summary>
public sealed class HiringFormHrefTests
{
    [Fact]
    public void The_href_carries_the_dates_in_the_iso_format()
    {
        var href = Lockte.Web.Reports.Hr.OnboardingReports.HiringFormHref(7, new DateOnly(2026, 8, 3), null);

        Assert.Equal("reports/hr/employees/7/hiring-form?applied=2026-08-03&interviewed=", href);
    }
}
