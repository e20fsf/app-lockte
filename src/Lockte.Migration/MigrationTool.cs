using System.Runtime.Versioning;
using Lockte.Hr;
using Lockte.Org;
using Lockte.Prod;
using Lockte.Purch;
using Lockte.Sales;
using Lockte.SharedKernel.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace Lockte.Migration;

/// <summary>
/// Command line of the console: <c>--source พนักงาน.accdb --connection "&lt;SQL Server&gt;" --confirm &lt;database&gt;
/// --out &lt;folder&gt; [--map code-map.csv]</c>. The connection may come from env <c>LOCKTE_MIGRATION_SQL</c> instead.
/// <c>--confirm</c> must repeat the database name, because every run deletes the master data there first.
/// <c>--program order</c> (#107) reads <c>ผลผลิตและORDER.accdb</c> instead of <c>พนักงาน.accdb</c> (the default,
/// <c>employee</c>); run it after the employee program's migration on the same database.
/// </summary>
public sealed record MigrationOptions(string SourceFile, string ConnectionString, string? CodeMapFile, string OutputFolder, string Program = MigrationOptions.EmployeeProgram, bool RemoveStockDuplicates = false,
    string? EmployeeFile = null, string? OrderFile = null, string? QaDefinitionsFolder = null, string? BaselineFolder = null, DateOnly? CutDate = null, DateOnly? PeriodFrom = null, bool Calculate = false)
{
    public const string EmployeeProgram = "employee";
    public const string OrderProgram = "order";
    /// <summary>The master data of <c>วัตถุดิบ.accdb</c> (#217): ผู้ขาย, วัตถุดิบ, the lists, the prefix of each แผนก.</summary>
    public const string PurchMasterProgram = "purch-master";
    public const string QaSpecProgram = "qa-spec";
    /// <summary>Both programs (and the QA specs) in one run, with one report.</summary>
    public const string AllProgram = "all";
    /// <summary>Sets the go-live flag on the database: the tool will not run on it again.</summary>
    public const string MarkGoLiveProgram = "mark-go-live";
    /// <summary>Compares the migrated database with the baseline exported from Access (#140); reads only.</summary>
    public const string CompareProgram = "compare";
    /// <summary>Compares one closed period with Access: เวลารวม, ค่าแรง, ยอดส่งธนาคาร, สลิป (#57).</summary>
    public const string ComparePeriodProgram = "compare-period";

    public const string ConnectionVariable = "LOCKTE_MIGRATION_SQL";

    public const string Usage = """
        Lockte.Migration --program all --employee <พนักงาน.accdb> --order <ผลผลิตและORDER.accdb> [--qa <reports_decompiled>] --connection "<SQL Server connection string>"
                         --confirm <database> --out <folder> [--map <code-map.csv>] [--remove-stock-duplicates yes]
          รันทั้งชุดในคำสั่งเดียว: โปรแกรมพนักงาน → ผลผลิตและORDER → (ถ้าระบุ --qa) สเปคตรวจ QA · ลบแล้วโหลดใหม่ รันซ้ำได้ · รายงานฉบับเดียวใน <folder>/summary.md
          ฐานข้อมูลที่ตั้งธงเปิดใช้งานจริงแล้ว (--program mark-go-live --confirm <database>) จะไม่ยอมให้รัน
        Lockte.Migration --program compare-period --baseline <folder: inputs.csv pay-history.csv slip.csv bank.csv> --from <YYYY-MM-DD วันเริ่มงวด> [--calculate yes] --connection "<SQL Server connection string>" --out <folder>
          เทียบงวดที่ปิดแล้วใน Access กับฐานข้อมูล: เวลารวม (AC-02) ค่าแรง (AC-03) ยอดส่งธนาคาร (AC-06) สลิป (AC-07) ในคำสั่งเดียว → <folder>/period-compare-summary.md · period-compare-people.csv · period-compare-details.csv (ข้อมูลจริง เก็บนอก git)
          ไม่ใส่ --calculate = อ่านอย่างเดียว (งวดต้องคำนวณแล้ว) · --calculate yes = สร้างงวดและคำนวณเวลา/ค่าแรงก่อน (เขียนลงฐานข้อมูล ใช้กับฐานข้อมูลที่ทิ้งได้ ไม่ใช้กับฐานข้อมูลที่เปิดใช้งานจริง)
        Lockte.Migration --program compare --baseline <folder ของ export_baseline.py> --cut <YYYY-MM-DD> --connection "<SQL Server connection string>" --out <folder>
          เทียบข้อมูลที่ย้ายแล้วกับ baseline จาก Access ทีละแถว (อ่านอย่างเดียว ไม่แก้ฐานข้อมูล) → <folder>/compare-summary.md · compare-details.csv (ข้อมูลจริง เก็บนอก git)
        Lockte.Migration --source <พนักงาน.accdb> --connection "<SQL Server connection string>" --confirm <database>
                         --out <folder> [--map <code-map.csv>] [--program employee|order|qa-spec] [--remove-stock-duplicates yes]
          ลบข้อมูลหลักและประวัติในฐานข้อมูล <database> แล้วโหลดใหม่จากสำเนาของไฟล์ต้นทาง (ไฟล์ต้นทางไม่ถูกแก้)
          --program qa-spec = สกัดสเปคตรวจ QA จากโฟลเดอร์นิยามรายงานที่กู้ไว้ (--source เป็นโฟลเดอร์ reports_decompiled; ผลอยู่ในฐานข้อมูลและ <folder> เท่านั้น ห้าม commit)
          --program order = ย้ายข้อมูลหลักของ ผลผลิตและORDER.accdb (รันหลังย้ายข้อมูลโปรแกรมพนักงานลงฐานข้อมูลเดียวกัน)
          --program purch-master = ย้ายข้อมูลหลักของ วัตถุดิบ.accdb: ผู้ขาย วัตถุดิบ ตัวเลือก และ prefix เลขที่ใบขอซื้อของแผนก (รันหลังโปรแกรมพนักงาน/ORDER; ฐานข้อมูลต้องยังไม่มีเอกสารจัดซื้อ; ใช้กับฐานข้อมูลทดสอบแยกชื่อเท่านั้น)
          --remove-stock-duplicates yes = ตัดผลผลิตที่นับซ้ำใน P_ST ตาม Q-47 (ค่าเริ่มต้น: ไม่ตัด รายงานจำนวนอย่างเดียว)
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
        var program = values.GetValueOrDefault("program") ?? EmployeeProgram;
        if (program is not (EmployeeProgram or OrderProgram or PurchMasterProgram or QaSpecProgram or AllProgram or MarkGoLiveProgram or CompareProgram or ComparePeriodProgram))
            return (null, $"--program ต้องเป็น {EmployeeProgram}, {OrderProgram}, {PurchMasterProgram}, {QaSpecProgram}, {AllProgram}, {MarkGoLiveProgram}, {CompareProgram} หรือ {ComparePeriodProgram}");
        var whole = program is AllProgram or MarkGoLiveProgram or CompareProgram or ComparePeriodProgram;
        if (!whole && values.GetValueOrDefault("source") is null) return (null, "ไม่ได้ระบุ --source");
        var source = values.GetValueOrDefault("source") ?? "";
        if (program == AllProgram && (values.GetValueOrDefault("employee") is null || values.GetValueOrDefault("order") is null)) return (null, "--program all ต้องระบุ --employee และ --order");
        if (program != MarkGoLiveProgram && values.GetValueOrDefault("out") is null) return (null, "ไม่ได้ระบุ --out");
        var output = values.GetValueOrDefault("out") ?? "";
        if (string.IsNullOrWhiteSpace(connection)) return (null, $"ไม่ได้ระบุ --connection หรือ env {ConnectionVariable}");
        string database;
        try { database = new SqlConnectionStringBuilder(connection).InitialCatalog; }
        catch (ArgumentException e) { return (null, $"connection string ไม่ถูกต้อง: {e.Message}"); }
        if (string.IsNullOrEmpty(database)) return (null, "connection string ต้องระบุ Database");
        DateOnly? cut = null;
        if (program == CompareProgram)
        {
            if (values.GetValueOrDefault("baseline") is null) return (null, "--program compare ต้องระบุ --baseline (โฟลเดอร์จาก tools/baseline/export_baseline.py)");
            if (!DateOnly.TryParseExact(values.GetValueOrDefault("cut"), "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var parsed)) return (null, "--program compare ต้องระบุ --cut YYYY-MM-DD (วันตัดสต็อก/แม่พิมพ์ที่ใช้ตอนส่งออก baseline)");
            cut = parsed;
        }
        DateOnly? periodFrom = null;
        if (program == ComparePeriodProgram)
        {
            if (values.GetValueOrDefault("baseline") is null) return (null, "--program compare-period ต้องระบุ --baseline (โฟลเดอร์ที่มี inputs.csv, pay-history.csv, slip.csv, bank.csv)");
            if (!DateOnly.TryParseExact(values.GetValueOrDefault("from"), "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var from)) return (null, "--program compare-period ต้องระบุ --from YYYY-MM-DD (วันเริ่มงวด)");
            periodFrom = from;
        }
        if (program is not (CompareProgram or ComparePeriodProgram) && !string.Equals(values.GetValueOrDefault("confirm"), database, StringComparison.OrdinalIgnoreCase))
            return (null, $"ต้องยืนยันด้วย --confirm {database} (ข้อมูลหลักในฐานข้อมูลนี้จะถูกลบแล้วโหลดใหม่)");
        return (new MigrationOptions(source, connection, values.GetValueOrDefault("map"), output, program, string.Equals(values.GetValueOrDefault("remove-stock-duplicates"), "yes", StringComparison.OrdinalIgnoreCase),
            values.GetValueOrDefault("employee"), values.GetValueOrDefault("order"), values.GetValueOrDefault("qa"), values.GetValueOrDefault("baseline"), cut, periodFrom, string.Equals(values.GetValueOrDefault("calculate"), "yes", StringComparison.OrdinalIgnoreCase)), null);
    }
}

public static class MigrationTool
{
    /// <summary>Who the loaded rows are created by (CreatedBy / UpdatedBy).</summary>
    public const string UserName = "migration";

    public static IServiceCollection AddMasterDataMigration(this IServiceCollection services)
        => services.AddScoped<MasterDataMigration>().AddScoped<HistoryMigration>()
            // The delete-everything and bulk-load services of the modules: registered for this tool only, never in the web app.
            .AddScoped<HrDataReset>().AddScoped<OrgDataReset>().AddScoped<SalesDataReset>().AddScoped<HrHistoryImport>()
            .AddScoped<WarningMigration>().AddScoped<HrWarningImport>()
            .AddScoped<ProdDataReset>().AddScoped<OrgMachineReset>().AddScoped<OrderMasterDataMigration>()
            .AddScoped<SalesOrderImport>().AddScoped<OrderHistoryMigration>().AddScoped<ProductionReportImport>().AddScoped<ProductionReportMigration>().AddScoped<StockImport>().AddScoped<StockMigration>().AddScoped<QaSpecImport>().AddScoped<QaSpecMigration>()
            .AddScoped<PurchMasterImport>().AddScoped<PurchMasterDataMigration>()
            .AddScoped<OrphanPayEmployeeMigration>().AddScoped<HrStubEmployeeImport>().AddScoped<PayHistoryMigration>().AddScoped<HrPayHistoryImport>();

    /// <summary>
    /// One rehearsal or the final run: reads a copy of <paramref name="sourceFile"/>, deletes and loads the master data
    /// (#47), the history, the open pay period with its ไม่มาทำงาน and the ผลค่าแรง of every period (#51), writes the DQ report, the row counts, the monthly totals of 2026, the WORK_T check and
    /// the code map used into <paramref name="outputFolder"/>.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static async Task<MigrationReport> RunAsync(IServiceProvider services, string sourceFile, string? codeMapFile, string outputFolder)
    {
        var map = codeMapFile is null ? CodeMap.Empty : CodeMap.Read(codeMapFile);
        var legacy = LegacyDatabase.ReadAccessFile(sourceFile);
        await using var scope = services.CreateAsyncScope();
        var report = await scope.ServiceProvider.GetRequiredService<MasterDataMigration>().RunAsync(legacy, map);
        await scope.ServiceProvider.GetRequiredService<HistoryMigration>().RunAsync(legacy, report);
        await scope.ServiceProvider.GetRequiredService<WarningMigration>().RunAsync(legacy, report); // ใบเตือน (#59)
        await scope.ServiceProvider.GetRequiredService<OrphanPayEmployeeMigration>().RunAsync(legacy, report); // พนักงานตัวแทนของรหัสกำพร้าที่มีผลค่าแรง
        await scope.ServiceProvider.GetRequiredService<PayHistoryMigration>().RunAsync(legacy, report); // ผลค่าแรง, after the open period
        report.WriteTo(outputFolder);
        return report;
    }
}

public static class OrderMigrationTool
{
    /// <summary>
    /// The master-data run of ผลผลิตและORDER (#107): reads a copy of <paramref name="sourceFile"/>, deletes and loads สินค้า,
    /// แม่พิมพ์ and เครื่องจักร and fills ลูกค้า and เขตการขาย, writes the DQ report, the row counts, the code map used and the
    /// employee matches into <paramref name="outputFolder"/>.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static async Task<MigrationReport> RunAsync(IServiceProvider services, string sourceFile, string? codeMapFile, string outputFolder, bool removeStockDuplicates = false)
    {
        var map = codeMapFile is null ? CodeMap.Empty : CodeMap.Read(codeMapFile);
        var legacy = LegacyDatabase.ReadOrderFile(sourceFile);
        await using var scope = services.CreateAsyncScope();
        var report = await scope.ServiceProvider.GetRequiredService<OrderMasterDataMigration>().RunAsync(legacy, map);
        await scope.ServiceProvider.GetRequiredService<OrderHistoryMigration>().RunAsync(legacy, map, report); // ORDER, ประวัติ ORDER, ประวัติการส่ง (#112)
        await scope.ServiceProvider.GetRequiredService<ProductionReportMigration>().RunAsync(legacy, map, report); // ใบรายงานผลผลิต 7 แผนก (#122)
        await scope.ServiceProvider.GetRequiredService<StockMigration>().RunAsync(legacy, map, report, removeStockDuplicates); // สมุดสต็อกและการใช้งานแม่พิมพ์ (#126)
        report.WriteTo(outputFolder);
        return report;
    }
}

public static class PurchMigrationTool
{
    /// <summary>
    /// The master-data run of วัตถุดิบ (#217): reads a copy of <paramref name="sourceFile"/>, deletes and loads the lists, the prefixes, ผู้ขาย and วัตถุดิบ, writes the DQ report, the row counts and
    /// the code map used into <paramref name="outputFolder"/> (real data: kept outside git).
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static async Task<MigrationReport> RunAsync(IServiceProvider services, string sourceFile, string? codeMapFile, string outputFolder)
    {
        var map = codeMapFile is null ? CodeMap.Empty : CodeMap.Read(codeMapFile);
        var legacy = LegacyDatabase.ReadPurchFile(sourceFile);
        await using var scope = services.CreateAsyncScope();
        var report = await scope.ServiceProvider.GetRequiredService<PurchMasterDataMigration>().RunAsync(legacy, map);
        report.WriteTo(outputFolder);
        return report;
    }
}

/// <summary>The console's <see cref="ICurrentUser"/>.</summary>
public sealed class MigrationUser : ICurrentUser
{
    public string UserName => MigrationTool.UserName;
}
