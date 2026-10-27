using System.Diagnostics;
using System.Globalization;
using System.Runtime.Versioning;
using System.Text;
using System.Text.RegularExpressions;
using Lockte.Prod;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace Lockte.Migration;

/// <summary>One step of the full run: what it was, how long it took, and its report.</summary>
public sealed record MigrationStep(string Name, TimeSpan Elapsed, MigrationReport Report);

/// <summary>
/// The go-live flag (#134): once the new system is the one in use, the database carries the extended property <c>Lockte.GoLive</c> and the migration tool refuses to run on it — a run deletes
/// and loads again, which on a database people work in would erase their work. Set it with <c>--program mark-go-live</c>; to run again on purpose, drop the property by hand.
/// </summary>
public static class GoLiveGuard
{
    public const string Property = "Lockte.GoLive";

    public static async Task<bool> IsLiveAsync(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("SELECT COUNT(*) FROM sys.extended_properties WHERE class = 0 AND name = @name", connection);
        command.Parameters.AddWithValue("@name", Property);
        return Convert.ToInt32(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture) > 0;
    }

    public static async Task EnsureNotLiveAsync(string connectionString)
    {
        if (await IsLiveAsync(connectionString))
            throw new InvalidOperationException($"ฐานข้อมูลนี้ตั้งธง {Property} แล้ว (เปิดใช้งานจริงแล้ว) — เครื่องมือย้ายข้อมูลลบแล้วโหลดใหม่ จึงไม่รันบนฐานข้อมูลที่ใช้งานอยู่ "
                + $"ถ้าต้องการรันซ้ำจริง ๆ ให้ลบ extended property นี้เองด้วย EXEC sp_dropextendedproperty N'{Property}'");
    }

    public static async Task MarkAsync(string connectionString)
    {
        if (await IsLiveAsync(connectionString)) return;
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("EXEC sp_addextendedproperty @name = @name, @value = N'1'", connection);
        command.Parameters.AddWithValue("@name", Property);
        await command.ExecuteNonQueryAsync();
    }
}

/// <summary>
/// The full run of the migration of both programs with one command (#134): the employee program (พนักงาน.accdb: master data, history, pay), then ผลผลิตและORDER.accdb (master data, ORDER
/// and delivery history, production reports, stock and molds) — customers and sales territories are shared and filled by both — then, if the folder of the recovered report definitions is
/// given, the QA specs. A run deletes and loads everything again, so it can be repeated; one report is written for all of it.
/// </summary>
[SupportedOSPlatform("windows")]
public static class FullMigrationTool
{
    public static async Task<IReadOnlyList<MigrationStep>> RunAsync(IServiceProvider services, MigrationOptions options, Action<string>? progress = null)
    {
        await GoLiveGuard.EnsureNotLiveAsync(options.ConnectionString);
        var employee = options.EmployeeFile ?? throw new ArgumentException("ไม่ได้ระบุ --employee (พนักงาน.accdb)");
        var order = options.OrderFile ?? throw new ArgumentException("ไม่ได้ระบุ --order (ผลผลิตและORDER.accdb)");
        CheckFile(employee, "พนักงาน.accdb");
        CheckFile(order, "ผลผลิตและORDER.accdb");
        if (options.QaDefinitionsFolder is { } folder && !Directory.Exists(folder)) throw new DirectoryNotFoundException($"ไม่พบโฟลเดอร์นิยามรายงาน {folder}");

        var steps = new List<MigrationStep>();
        async Task<MigrationReport> Step(string name, Func<Task<MigrationReport>> run)
        {
            progress?.Invoke($"{name} …");
            var watch = Stopwatch.StartNew();
            try
            {
                var report = await run();
                steps.Add(new MigrationStep(name, watch.Elapsed, report));
                progress?.Invoke($"{name}: เสร็จใน {Format(watch.Elapsed)}");
                return report;
            }
            catch (Exception e)
            {
                throw new InvalidOperationException($"ขั้น '{name}' ล้มเหลวหลัง {Format(watch.Elapsed)}: {e.Message} — ฐานข้อมูลอาจโหลดไม่ครบ ให้แก้สาเหตุแล้วรันซ้ำทั้งชุด (รันซ้ำได้)", e);
            }
        }

        var output = options.OutputFolder;
        await Step("พนักงาน.accdb", () => MigrationTool.RunAsync(services, employee, options.CodeMapFile, Path.Combine(output, "01-employee")));
        await Step("ผลผลิตและORDER.accdb", () => OrderMigrationTool.RunAsync(services, order, options.CodeMapFile, Path.Combine(output, "02-order"), options.RemoveStockDuplicates));
        if (options.QaDefinitionsFolder is { } definitions)
            await Step("สเปคตรวจ QA", async () =>
            {
                await using var scope = services.CreateAsyncScope();
                return await scope.ServiceProvider.GetRequiredService<QaSpecMigration>().RunAsync(definitions, Path.Combine(output, "03-qa-spec"));
            });

        WriteReport(output, steps);
        return steps;
    }

    private static void CheckFile(string path, string label)
    {
        if (!File.Exists(path)) throw new FileNotFoundException($"ไม่พบไฟล์ {label}: {path}", path);
        var extension = Path.GetExtension(path);
        if (!extension.Equals(".accdb", StringComparison.OrdinalIgnoreCase) && !extension.Equals(".mdb", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"{path} ไม่ใช่ไฟล์ Access (.accdb) — คาดว่าเป็น {label}");
        var header = new byte[20];
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (stream.Length < 50_000 || stream.Read(header, 0, header.Length) < header.Length) throw new InvalidDataException($"{path} เล็กเกินไปที่จะเป็น {label} — ใช่สำเนาที่ถูกต้องหรือไม่");
        }
        catch (IOException e)
        {
            throw new IOException($"เปิด {path} อ่านไม่ได้: {e.Message} — ปิดโปรแกรม Access ที่เปิดไฟล์นี้อยู่แล้วทำสำเนาใหม่", e);
        }
        var text = Encoding.ASCII.GetString(header, 4, 15);
        if (!text.StartsWith("Standard ACE DB", StringComparison.Ordinal) && !text.StartsWith("Standard Jet DB", StringComparison.Ordinal))
            throw new InvalidDataException($"{path} ไม่ใช่ไฟล์ฐานข้อมูล Access — ใช่สำเนาของ {label} หรือไม่");
    }

    private static string Format(TimeSpan span) => span.TotalMinutes >= 1 ? $"{(int)span.TotalMinutes} นาที {span.Seconds} วินาที" : $"{span.TotalSeconds:0.#} วินาที";

    // ---------- the one report ----------

    private static readonly Regex Number = new(@"\d[\d,.]*", RegexOptions.Compiled);
    private static readonly Regex Quoted = new(@"'[^']*'", RegexOptions.Compiled);

    /// <summary>The problem without what is special to a row (quoted values, numbers), so a thousand rows with one problem are one line.</summary>
    public static string Kind(string problem) => Number.Replace(Quoted.Replace(problem, "'…'"), "N");

    public static void WriteReport(string folder, IReadOnlyList<MigrationStep> steps)
    {
        Directory.CreateDirectory(folder);
        Csv.Write(Path.Combine(folder, "run-info.csv"), ["ขั้น", "ไฟล์ต้นทาง", "ขนาด (ไบต์)", "แก้ไขล่าสุด (UTC)", "อ่านสำเนาเมื่อ (UTC)", "ใช้เวลา", "ตารางตรงทุกตาราง", "ข้อยกเว้น DQ"],
            steps.Select(s => new object?[]
            {
                s.Name, s.Report.Source?.FileName, s.Report.Source?.Bytes, s.Report.Source?.LastWriteUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                s.Report.Source?.ReadAtUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture), Format(s.Elapsed), s.Report.Balanced ? "ตรง" : "ไม่ตรง", s.Report.Exceptions.Count,
            }));
        Csv.Write(Path.Combine(folder, "row-counts-all.csv"), ["ขั้น", "ตารางเดิม", "ตารางใหม่", "แถวต้นทาง", "ไม่ย้ายตามกติกา", "ข้อยกเว้น DQ", "โหลด", "ในฐานข้อมูล", "ตรง"],
            steps.SelectMany(s => s.Report.Tables.Select(t => new object?[] { s.Name, t.LegacyTable, t.NewTable, t.SourceRows, t.Excluded, t.Rejected, t.Loaded, t.InDatabase, t.Balanced ? "ตรง" : "ไม่ตรง" })));
        Csv.Write(Path.Combine(folder, "monthly-totals-all.csv"), ["ขั้น", "ตาราง", "เดือน", "ยอด", "ต้นทาง", "ต้นทางหลังข้อยกเว้น DQ", "ในฐานข้อมูล", "ตรง"],
            steps.SelectMany(s => s.Report.MonthlyTotals.Select(m => new object?[]
                { s.Name, m.Table, m.Month, m.Measure, m.Source.ToString("0.######", CultureInfo.InvariantCulture), m.Expected.ToString("0.######", CultureInfo.InvariantCulture),
                  m.InDatabase.ToString("0.######", CultureInfo.InvariantCulture), m.Balanced ? "ตรง" : "ไม่ตรง" })));
        Csv.Write(Path.Combine(folder, "dq-exceptions-all.csv"), ["ขั้น", "ตาราง", "รายการ", "ปัญหา DQ", "รายละเอียด", "การจัดการ"],
            steps.SelectMany(s => s.Report.Exceptions.Select(e => new object?[] { s.Name, e.Table, e.Key, e.Rule, e.Problem, e.Action })));
        var groups = steps.SelectMany(s => s.Report.Exceptions.Select(e => (Step: s.Name, e.Table, e.Rule, Problem: Kind(e.Problem), e.Action)))
            .GroupBy(x => x).OrderBy(g => g.Key.Step, StringComparer.Ordinal).ThenByDescending(g => g.Count()).ToList();
        Csv.Write(Path.Combine(folder, "dq-summary.csv"), ["ขั้น", "ตาราง", "ปัญหา DQ", "ลักษณะปัญหา", "การจัดการ", "จำนวน"],
            groups.Select(g => new object?[] { g.Key.Step, g.Key.Table, g.Key.Rule, g.Key.Problem, g.Key.Action, g.Count() }));
        File.WriteAllText(Path.Combine(folder, "summary.md"), Summary(steps, groups), new UTF8Encoding(true));
    }

    private static string Summary(IReadOnlyList<MigrationStep> steps, List<IGrouping<(string Step, string Table, string Rule, string Problem, string Action), (string Step, string Table, string Rule, string Problem, string Action)>> groups)
    {
        var text = new StringBuilder("# รายงานการย้ายข้อมูลทั้งชุด\n\n");
        text.Append("ข้อมูลจริงไม่ถูกเขียนในรายงานนี้นอกจากตัวเลขรวม; รายการรายแถวอยู่ใน `dq-exceptions-all.csv` (เก็บนอก git)\n\n## ขั้นตอนและเวลา\n\n| ขั้น | ใช้เวลา | ตรงทุกตาราง | ข้อยกเว้น DQ |\n|---|---|---|---|\n");
        foreach (var s in steps) text.Append($"| {s.Name} | {Format(s.Elapsed)} | {(s.Report.Balanced ? "ตรง" : "**ไม่ตรง**")} | {s.Report.Exceptions.Count:N0} |\n");
        text.Append($"\nรวม {Format(TimeSpan.FromTicks(steps.Sum(s => s.Elapsed.Ticks)))}\n");
        foreach (var s in steps.Where(s => s.Report.Source is not null))
            text.Append($"\n- {s.Name}: ไฟล์ `{s.Report.Source!.FileName}` {s.Report.Source.Bytes / 1_000_000:N0} MB แก้ไขล่าสุด {s.Report.Source.LastWriteUtc:yyyy-MM-dd HH:mm} UTC อ่านสำเนาเมื่อ {s.Report.Source.ReadAtUtc:yyyy-MM-dd HH:mm} UTC");
        text.Append("\n\n## จำนวนแถว (AC-20)\n\nแถวต้นทาง = ไม่ย้ายตามกติกา + ข้อยกเว้น DQ + โหลด และโหลด = ในฐานข้อมูล ทุกตาราง:\n\n");
        var unbalanced = steps.SelectMany(s => s.Report.Tables.Where(t => !t.Balanced).Select(t => $"{s.Name}: {t.LegacyTable} (ต้นทาง {t.SourceRows:N0}, ไม่ย้าย {t.Excluded:N0}, DQ {t.Rejected:N0}, โหลด {t.Loaded:N0}, ในฐานข้อมูล {t.InDatabase:N0})")).ToList();
        text.Append(unbalanced.Count == 0 ? $"ตรงทั้ง {steps.Sum(s => s.Report.Tables.Count)} ตาราง\n" : string.Join("\n", unbalanced.Select(u => "- **ไม่ตรง** " + u)) + "\n");
        var months = steps.SelectMany(s => s.Report.MonthlyTotals.Where(m => !m.Balanced).Select(m => $"{s.Name}: {m.Table} {m.Month} {m.Measure}")).ToList();
        text.Append(months.Count == 0 ? $"\nยอดรายเดือนตรง {steps.Sum(s => s.Report.MonthlyTotals.Count):N0} รายการ\n" : "\n" + string.Join("\n", months.Select(u => "- **ยอดไม่ตรง** " + u)) + "\n");
        text.Append("\n## ข้อยกเว้น DQ จัดกลุ่ม (ชนิด จำนวน การจัดการ)\n\n| ขั้น | ตาราง | ปัญหา | ลักษณะ | การจัดการ | จำนวน |\n|---|---|---|---|---|---|\n");
        foreach (var g in groups.Take(80)) text.Append($"| {g.Key.Step} | {g.Key.Table} | {g.Key.Rule} | {g.Key.Problem.Replace("|", "\\|")} | {g.Key.Action} | {g.Count():N0} |\n");
        if (groups.Count > 80) text.Append($"\n… และอีก {groups.Count - 80:N0} กลุ่ม (ดู `dq-summary.csv`)\n");
        text.Append("\n## หมายเหตุของแต่ละขั้น\n");
        foreach (var s in steps)
        {
            text.Append($"\n### {s.Name}\n\n");
            foreach (var note in s.Report.Notes) text.Append($"- {note}\n");
        }
        return text.ToString();
    }
}
