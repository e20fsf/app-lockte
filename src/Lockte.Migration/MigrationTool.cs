using System.Runtime.Versioning;
using Lockte.SharedKernel.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace Lockte.Migration;

/// <summary>
/// Command line of the console: <c>--source พนักงาน.accdb --connection "&lt;SQL Server&gt;" --confirm &lt;database&gt;
/// --out &lt;folder&gt; [--map code-map.csv]</c>. The connection may come from env <c>LOCKTE_MIGRATION_SQL</c> instead.
/// <c>--confirm</c> must repeat the database name, because every run deletes the master data there first.
/// </summary>
public sealed record MigrationOptions(string SourceFile, string ConnectionString, string? CodeMapFile, string OutputFolder)
{
    public const string ConnectionVariable = "LOCKTE_MIGRATION_SQL";

    public const string Usage = """
        Lockte.Migration --source <พนักงาน.accdb> --connection "<SQL Server connection string>" --confirm <database>
                         --out <folder> [--map <code-map.csv>]
          ลบข้อมูลหลักในฐานข้อมูล <database> แล้วโหลดใหม่จากสำเนาของไฟล์ต้นทาง (ไฟล์ต้นทางไม่ถูกแก้)
          --connection ใช้ env LOCKTE_MIGRATION_SQL แทนได้ · รายงานเขียนลง <folder>
        """;

    public static (MigrationOptions? Options, string? Error) Parse(IReadOnlyList<string> args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Count; i++)
        {
            if (!args[i].StartsWith("--") || i + 1 >= args.Count) return (null, $"ไม่เข้าใจ '{args[i]}'");
            values[args[i][2..]] = args[++i];
        }
        var connection = values.GetValueOrDefault("connection") ?? Environment.GetEnvironmentVariable(ConnectionVariable);
        if (values.GetValueOrDefault("source") is not { } source) return (null, "ไม่ได้ระบุ --source");
        if (values.GetValueOrDefault("out") is not { } output) return (null, "ไม่ได้ระบุ --out");
        if (string.IsNullOrWhiteSpace(connection)) return (null, $"ไม่ได้ระบุ --connection หรือ env {ConnectionVariable}");
        string database;
        try { database = new SqlConnectionStringBuilder(connection).InitialCatalog; }
        catch (ArgumentException e) { return (null, $"connection string ไม่ถูกต้อง: {e.Message}"); }
        if (string.IsNullOrEmpty(database)) return (null, "connection string ต้องระบุ Database");
        if (!string.Equals(values.GetValueOrDefault("confirm"), database, StringComparison.OrdinalIgnoreCase))
            return (null, $"ต้องยืนยันด้วย --confirm {database} (ข้อมูลหลักในฐานข้อมูลนี้จะถูกลบแล้วโหลดใหม่)");
        return (new MigrationOptions(source, connection, values.GetValueOrDefault("map"), output), null);
    }
}

public static class MigrationTool
{
    /// <summary>Who the loaded rows are created by (CreatedBy / UpdatedBy).</summary>
    public const string UserName = "migration";

    public static IServiceCollection AddMasterDataMigration(this IServiceCollection services)
        => services.AddScoped<MasterDataMigration>();

    /// <summary>
    /// One rehearsal or the final run: reads a copy of <paramref name="sourceFile"/>, deletes and loads the master data,
    /// writes the DQ report, the row counts and the code map used into <paramref name="outputFolder"/>.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static async Task<MigrationReport> RunAsync(IServiceProvider services, string sourceFile, string? codeMapFile, string outputFolder)
    {
        var map = codeMapFile is null ? CodeMap.Empty : CodeMap.Read(codeMapFile);
        var legacy = LegacyDatabase.ReadAccessFile(sourceFile);
        await using var scope = services.CreateAsyncScope();
        var report = await scope.ServiceProvider.GetRequiredService<MasterDataMigration>().RunAsync(legacy, map);
        report.WriteTo(outputFolder);
        return report;
    }
}

/// <summary>The console's <see cref="ICurrentUser"/>.</summary>
public sealed class MigrationUser : ICurrentUser
{
    public string UserName => MigrationTool.UserName;
}
