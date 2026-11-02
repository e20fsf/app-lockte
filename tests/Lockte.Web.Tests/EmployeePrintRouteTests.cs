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
