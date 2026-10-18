using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Org.Data;

/// <summary>
/// module องค์กร (ADR 0002): ฝ่าย แผนก กะ หัวหน้า — writes only to schema <c>org</c>.
/// Other modules read through this module's public services, never through this DbContext.
/// </summary>
public sealed class OrgDbContext(DbContextOptions<OrgDbContext> options) : ModuleDbContext(options, Schema)
{
    public const string Schema = "org";
}
