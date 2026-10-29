using Lockte.Hr;
using Lockte.Migration;
using Lockte.Org;
using Lockte.Prod;
using Lockte.Purch;
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
    .AddPurchModule(options.ConnectionString)
    .AddMasterDataMigration()
    .BuildServiceProvider(validateScopes: true);

// The web app creates and migrates the database (LockteDatabase.MigrateAsync); this tool only fills it.
if (!await OrgModule.IsUpToDateAsync(services) || !await HrModule.IsUpToDateAsync(services) || !await SalesModule.IsUpToDateAsync(services)
    || !await ProdModule.IsUpToDateAsync(services) || !await PurchModule.IsUpToDateAsync(services))
{
    Console.Error.WriteLine("ฐานข้อมูลยังไม่เป็นรุ่นล่าสุด — เปิดเว็บแอปกับฐานข้อมูลนี้หนึ่งครั้งก่อน");
    return 1;
}

if (options.Program == MigrationOptions.QaSpecProgram)
{
    Console.WriteLine($"อ่านนิยามรายงานจาก {options.SourceFile} …");
    await using var qaScope = services.CreateAsyncScope();
    var qaReport = await qaScope.ServiceProvider.GetRequiredService<QaSpecMigration>().RunAsync(options.SourceFile, options.OutputFolder);
    foreach (var table in qaReport.Tables)
        Console.WriteLine($"{table.LegacyTable,-34} ต้นทาง {table.SourceRows,4}  โหลด {table.Loaded,4}  DQ {table.Rejected,3}  ไม่ย้าย {table.Excluded,4}  ในฐานข้อมูล {table.InDatabase,4}  {(table.Balanced ? "ตรง" : "ไม่ตรง")}");
    foreach (var note in qaReport.Notes) Console.WriteLine(note);
    Console.WriteLine($"รายงานอยู่ใน {Path.GetFullPath(options.OutputFolder)}");
    return qaReport.Balanced ? 0 : 1;
}
if (options.Program == MigrationOptions.CompareProgram)
{
    Console.WriteLine($"เทียบฐานข้อมูลกับ baseline ใน {options.BaselineFolder} (วันตัด {options.CutDate:yyyy-MM-dd}) …");
    var results = await Comparison.RunAsync(services, options.ConnectionString, options.BaselineFolder!, options.CutDate!.Value);
    Comparison.WriteReport(options.OutputFolder, results, options.CutDate.Value);
    foreach (var r in results)
        Console.WriteLine($"{r.Id,-8} {r.AcceptanceCriteria,-6} {(r.Passed ? "ผ่าน" : "ไม่ผ่าน"),-8} ต้นทาง {r.BaselineKeys,8:N0}  ตรง {r.Matched,8:N0}  ไม่ตรง {r.Different.Count,5:N0}  หักออก {r.BaselineOnlyExplained.Count,6:N0}  หายไม่มีเหตุผล {r.BaselineOnlyUnexplained.Count,5:N0}  เฉพาะฐานข้อมูล {r.NewOnly.Count,5:N0}");
    Console.WriteLine($"รายงานอยู่ใน {Path.GetFullPath(options.OutputFolder)}");
    return results.All(r => r.Passed) ? 0 : 1;
}
if (options.Program == MigrationOptions.ComparePeriodProgram)
{
    if (options.Calculate) await GoLiveGuard.EnsureNotLiveAsync(options.ConnectionString);
    Console.WriteLine($"เทียบงวดที่เริ่ม {options.PeriodFrom:yyyy-MM-dd} กับ baseline ใน {options.BaselineFolder} …");
    var comparison = await PeriodComparison.RunAsync(services, options.BaselineFolder!, options.PeriodFrom!.Value, options.Calculate);
    PeriodComparison.WriteReport(options.OutputFolder, comparison);
    foreach (var c in comparison.Criteria)
        Console.WriteLine($"{c.Id,-6} {c.Title,-28} {(!c.HasBaseline ? "ไม่ได้เทียบ" : c.Passed ? "ตรง" : "ไม่ตรง"),-12} เทียบ {c.People.Count,5:N0}  ช่องไม่ตรง {c.Mismatches.Count,5:N0}");
    Console.WriteLine($"รายงานอยู่ใน {Path.GetFullPath(options.OutputFolder)}");
    return comparison.Passed ? 0 : 1;
}
if (options.Program == MigrationOptions.MarkGoLiveProgram)
{
    await GoLiveGuard.MarkAsync(options.ConnectionString);
    Console.WriteLine($"ตั้งธง {GoLiveGuard.Property} บนฐานข้อมูลแล้ว — เครื่องมือย้ายข้อมูลจะไม่รันบนฐานข้อมูลนี้อีก");
    return 0;
}
if (options.Program == MigrationOptions.AllProgram)
{
    var steps = await FullMigrationTool.RunAsync(services, options, Console.WriteLine);
    foreach (var step in steps)
    {
        Console.WriteLine($"== {step.Name}: {step.Elapsed.TotalSeconds:0} วินาที, ตาราง {step.Report.Tables.Count} ({(step.Report.Balanced ? "ตรงทั้งหมด" : "มีตารางไม่ตรง")}), ข้อยกเว้น DQ {step.Report.Exceptions.Count}");
        foreach (var table in step.Report.Tables.Where(t => !t.Balanced))
            Console.WriteLine($"   ไม่ตรง: {table.LegacyTable} ต้นทาง {table.SourceRows} ไม่ย้าย {table.Excluded} DQ {table.Rejected} โหลด {table.Loaded} ในฐานข้อมูล {table.InDatabase}");
    }
    Console.WriteLine($"รายงานฉบับเดียวอยู่ใน {Path.GetFullPath(Path.Combine(options.OutputFolder, "summary.md"))}");
    return steps.All(s => s.Report.Balanced) ? 0 : 1;
}
Console.WriteLine($"อ่านสำเนาของ {options.SourceFile} …");
if (options.Program == MigrationOptions.PurchHistoryProgram)
{
    var historyReport = await PurchHistoryTool.RunAsync(services, options.SourceFile, options.CodeMapFile, options.OutputFolder);
    foreach (var table in historyReport.Tables)
        Console.WriteLine($"{table.LegacyTable,-22} → {table.NewTable,-28} ต้นทาง {table.SourceRows,5}  โหลด {table.Loaded,5}  DQ {table.Rejected,4}  ไม่ย้าย {table.Excluded,4}  ในฐานข้อมูล {table.InDatabase,5}  {(table.Balanced ? "ตรง" : "ไม่ตรง")}");
    foreach (var note in historyReport.Notes) Console.WriteLine(note);
    Console.WriteLine($"ข้อยกเว้น DQ {historyReport.Exceptions.Count} รายการ — รายงานอยู่ใน {Path.GetFullPath(options.OutputFolder)}");
    return historyReport.Balanced ? 0 : 1;
}
if (options.Program == MigrationOptions.PurchMasterProgram)
{
    var purchReport = await PurchMigrationTool.RunAsync(services, options.SourceFile, options.CodeMapFile, options.OutputFolder);
    foreach (var table in purchReport.Tables)
        Console.WriteLine($"{table.LegacyTable,-22} → {table.NewTable,-28} ต้นทาง {table.SourceRows,5}  โหลด {table.Loaded,5}  DQ {table.Rejected,4}  ไม่ย้าย {table.Excluded,4}  ในฐานข้อมูล {table.InDatabase,5}  {(table.Balanced ? "ตรง" : "ไม่ตรง")}");
    foreach (var note in purchReport.Notes) Console.WriteLine(note);
    Console.WriteLine($"ข้อยกเว้น DQ {purchReport.Exceptions.Count} รายการ — รายงานอยู่ใน {Path.GetFullPath(options.OutputFolder)}");
    return purchReport.Balanced ? 0 : 1;
}
if (options.Program == MigrationOptions.OrderProgram)
{
    var orderReport = await OrderMigrationTool.RunAsync(services, options.SourceFile, options.CodeMapFile, options.OutputFolder, options.RemoveStockDuplicates);
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
