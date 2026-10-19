using Lockte.Hr;
using Lockte.Hr.Data;
using Lockte.Migration;
using Lockte.Org;
using Lockte.Org.Data;
using Lockte.Sales;
using Lockte.Sales.Data;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

// เครื่องมือย้ายข้อมูล (#47): runs on a PC with the Access runtime, never on the server (ADR 0001).
Console.OutputEncoding = System.Text.Encoding.UTF8;
if (!OperatingSystem.IsWindows())
{
    Console.Error.WriteLine("ต้องรันบน Windows ที่มี Microsoft Access Runtime (ACE OLE DB 16.0)");
    return 2;
}
var (options, error) = MigrationOptions.Parse(args);
if (options is null)
{
    Console.Error.WriteLine(error);
    Console.Error.WriteLine(MigrationOptions.Usage);
    return 2;
}

var services = new ServiceCollection()
    .AddSingleton(TimeProvider.System)
    .AddSingleton<ICurrentUser, MigrationUser>()
    .AddOrgModule(options.ConnectionString)
    .AddHrModule(options.ConnectionString)
    .AddSalesModule(options.ConnectionString)
    .AddMasterDataMigration()
    .BuildServiceProvider(validateScopes: true);

await using (var scope = services.CreateAsyncScope())
{
    // The web app creates and migrates the database (LockteDatabase.MigrateAsync); this tool only fills it.
    DbContext[] contexts =
    [
        scope.ServiceProvider.GetRequiredService<OrgDbContext>(),
        scope.ServiceProvider.GetRequiredService<HrDbContext>(),
        scope.ServiceProvider.GetRequiredService<SalesDbContext>(),
    ];
    foreach (var context in contexts)
        if ((await context.Database.GetPendingMigrationsAsync()).Any())
        {
            Console.Error.WriteLine($"ฐานข้อมูลยังไม่เป็นรุ่นล่าสุด ({context.GetType().Name}) — เปิดเว็บแอปกับฐานข้อมูลนี้หนึ่งครั้งก่อน");
            return 1;
        }
}

Console.WriteLine($"อ่านสำเนาของ {options.SourceFile} …");
var report = await MigrationTool.RunAsync(services, options.SourceFile, options.CodeMapFile, options.OutputFolder);
foreach (var table in report.Tables)
    Console.WriteLine($"{table.LegacyTable,-12} → {table.NewTable,-22} ต้นทาง {table.SourceRows,4}  โหลด {table.Loaded,4}  " +
        $"DQ {table.Rejected,3}  ไม่ย้าย {table.Excluded,3}  ในฐานข้อมูล {table.InDatabase,4}  {(table.Balanced ? "ตรง" : "ไม่ตรง")}");
Console.WriteLine($"ข้อยกเว้น DQ {report.Exceptions.Count} รายการ — รายงานอยู่ใน {Path.GetFullPath(options.OutputFolder)}");
return report.Balanced ? 0 : 1;
