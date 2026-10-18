using Lockte.Org.Data;
using Lockte.SharedKernel.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Lockte.Org;

public static class OrgModule
{
    public static IServiceCollection AddOrgModule(this IServiceCollection services, string connectionString)
        => services.AddModuleDbContext<OrgDbContext>(connectionString, OrgDbContext.Schema);
}
