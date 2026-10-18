using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Hr.Data;

/// <summary>
/// module พนักงานและค่าแรง (ADR 0002): พนักงาน เวลาทำงาน การลา ใบเตือน งวดค่าแรง — writes only to schema <c>hr</c>.
/// Screens and tests go through this module's public services, never through this DbContext.
/// </summary>
public sealed class HrDbContext(DbContextOptions<HrDbContext> options) : ModuleDbContext(options, Schema)
{
    public const string Schema = "hr";
}
