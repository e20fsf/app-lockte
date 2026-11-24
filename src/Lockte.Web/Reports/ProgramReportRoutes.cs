using Lockte.Web.Accounts;
using Microsoft.AspNetCore.Authorization;

namespace Lockte.Web.Reports;

/// <summary>
/// ADR 0005: every report is under <c>/reports/{program key}/…</c> and needs the policy of that program, so a user who
/// may not open a program gets 403 from its reports too. A test fails for a report route outside the five programs.
/// </summary>
public static class ProgramReportRoutes
{
    /// <summary>Adds <see cref="AuthPolicies.Program"/> of its program to every route of a group (and of the groups inside it).</summary>
    public static TBuilder RequireProgramOfRoute<TBuilder>(this TBuilder group) where TBuilder : IEndpointConventionBuilder
    {
        group.Add(endpoint =>
        {
            if (endpoint is RouteEndpointBuilder route && ProgramAccess.OfReportRoute(route.RoutePattern.RawText) is { } program)
                route.Metadata.Add(new AuthorizeAttribute(AuthPolicies.Program(program)));
        });
        return group;
    }
}
