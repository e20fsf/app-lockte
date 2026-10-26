using Lockte.Hr;
using Lockte.Migration;
using Lockte.Org;
using Lockte.Prod;
using Lockte.Sales;
using Lockte.SharedKernel.Persistence;
using Microsoft.Extensions.DependencyInjection;

// เครื่องมือย้ายข้อมูล (#47, #51): runs on a PC with the Access runtime, never on the server (ADR 0001).
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
    .AddProdModule(options.ConnectionString)
    .AddMasterDataMigration()
    .BuildServiceProvider(validateScopes: true);

// The web app creates and migrates the database (LockteDatabase.MigrateAsync); this tool only fills it.
if (!await OrgModule.IsUpToDateAsync(services) || !await HrModule.IsUpToDateAsync(services) || !await SalesModule.IsUpToDateAsync(services)
    || !await ProdModule.IsUpToDateAsync(services))
{
    Console.Error.WriteLine("ฐานข้อมูลยังไม่เป็นรุ่นล่าสุด — เปิดเว็บแอปกับฐานข้อมูลนี้หนึ่งครั้งก่อน");
    return 1;
}

Console.WriteLine($"อ่านสำเนาของ {options.SourceFile} …");
if (options.Program == MigrationOptions.OrderProgram)
{
    var orderReport = await OrderMigrationTool.RunAsync(services, options.SourceFile, options.CodeMapFile, options.OutputFolder);
    foreach (var table in orderReport.Tables)
        Console.WriteLine($"{table.LegacyTable,-14} → {table.NewTable,-26} ต้นทาง {table.SourceRows,5}  โหลด {table.Loaded,5}  " +
            $"DQ {table.Rejected,4}  ไม่ย้าย {table.Excluded,4}  ในฐานข้อมูล {table.InDatabase,5}  {(table.Balanced ? "ตรง" : "ไม่ตรง")}");
    foreach (var note in orderReport.Notes) Console.WriteLine(note);
    Console.WriteLine($"ข้อยกเว้น DQ {orderReport.Exceptions.Count} รายการ — รายงานอยู่ใน {Path.GetFullPath(options.OutputFolder)}");
    return orderReport.Balanced ? 0 : 1;
}
var report = await MigrationTool.RunAsync(services, options.SourceFile, options.CodeMapFile, options.OutputFolder);
foreach (var table in report.Tables)
    Console.WriteLine($"{table.LegacyTable,-12} → {table.NewTable,-22} ต้นทาง {table.SourceRows,4}  โหลด {table.Loaded,4}  " +
        $"DQ {table.Rejected,3}  ไม่ย้าย {table.Excluded,3}  ในฐานข้อมูล {table.InDatabase,4}  {(table.Balanced ? "ตรง" : "ไม่ตรง")}");
foreach (var month in report.MonthlyTotals.Where(m => !m.Balanced))
    Console.WriteLine($"ยอดรายเดือนไม่ตรง: {month.Table} {month.Month} {month.Measure} ควรเป็น {month.Expected} ในฐานข้อมูล {month.InDatabase}");
Console.WriteLine($"WORK_T (AC-01): ตรวจ {report.WorkTChecked} แถว ไม่ตรง {report.WorkTMismatches.Count} แถว");
Console.WriteLine($"ข้อยกเว้น DQ {report.Exceptions.Count} รายการ — รายงานอยู่ใน {Path.GetFullPath(options.OutputFolder)}");
return report.Balanced && report.WorkTMatches ? 0 : 1;
