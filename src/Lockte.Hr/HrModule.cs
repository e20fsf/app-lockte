using Lockte.Hr.Data;
using Lockte.SharedKernel.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Lockte.Hr;

public static class HrModule
{
    public static IServiceCollection AddHrModule(this IServiceCollection services, string connectionString)
        => services.AddModuleDbContext<HrDbContext>(connectionString, HrDbContext.Schema);
}
