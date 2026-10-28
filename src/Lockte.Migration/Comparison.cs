using System.Globalization;
using System.Text;
using Lockte.Prod;
using Lockte.Sales;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace Lockte.Migration;

/// <summary>One row of a comparison: the key, its numbers, and the codes the row refers to (used to say why a row of the baseline is not in the database).</summary>
public sealed record CompareItem(string Key, decimal[] Values, string[] Refs);

/// <summary>What one comparison found. Counts and sums only — the rows themselves (real codes) go to <c>compare-details.csv</c>, outside git.</summary>
public sealed record CompareResult(
    string Id, string Title, string AcceptanceCriteria, string[] ValueNames, int BaselineKeys, int NewKeys, int Matched,
    IReadOnlyList<CompareDetail> Different, IReadOnlyList<CompareDetail> BaselineOnlyExplained, IReadOnlyList<CompareDetail> BaselineOnlyUnexplained, IReadOnlyList<CompareDetail> NewOnly,
    decimal[] BaselineTotals, decimal[] NewTotals)
{
    /// <summary>Correctly loaded rows must match 100%: nothing different, nothing unexplained, nothing only in the database.</summary>
    public bool Passed => Different.Count == 0 && BaselineOnlyUnexplained.Count == 0 && NewOnly.Count == 0;
}

public sealed record CompareDetail(string Key, string Reason, decimal[] Baseline, decimal[] New);

/// <summary>
/// ด่านเทียบข้อมูลที่ย้ายแล้วกับ baseline จาก Access (#140): row by row, with the numbers of the baseline exported by <c>tools/baseline/export_baseline.py</c> from the copy after the
/// year-close and the numbers the database gives through the same directories the reports read. A row of the baseline that was not loaded for a reason of the redesign (a product or customer
/// not in the register, no quantity, a repeated key) is set aside <b>with its reason and its totals</b> — shown, never hidden; what is left must match exactly.
/// </summary>
public static class Comparison
{
    private const decimal Tolerance = 0.005m;

    public static async Task<IReadOnlyList<CompareResult>> RunAsync(IServiceProvider services, string connectionString, string baselineFolder, DateOnly cut)
    {
        var codes = await RegisterCodesAsync(connectionString);
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var results = new List<CompareResult>();

        var orders = provider.GetRequiredService<SalesOrderReportDirectory>();
        var deliveries = provider.GetRequiredService<SalesDeliveryReportDirectory>();
        var stock = provider.GetRequiredService<StockDirectory>();
        var closing = provider.GetRequiredService<StockClosingDirectory>();
        var molds = provider.GetRequiredService<MoldUsageDirectory>();
        var production = provider.GetRequiredService<ProductionReportDirectory>();

        var backlog = Read(baselineFolder, "backlog.csv").Select(r => new CompareItem($"{r["REF_NO"]}|{r["SUF_NO"]}", [Num(r["QTY"])], [r["CUS_ID"], r["PD_C"]])).ToList();
        results.Add(Compare("RPT-02", "ORDER ค้างส่งต่อบรรทัด", "AC-19", ["จำนวนค้าง"], backlog,
            (await orders.GetBackorderAsync()).Select(l => new CompareItem($"{l.OrderNo}|{l.LineNo}", [l.Quantity], [l.CustomerCode, l.ProductCode ?? ""])).ToList(),
            uniqueKey: true, item => OrderLineReason(item, codes, quantityIndex: 0)));

        var receipts = Read(baselineFolder, "receipts.csv").Select(r => new CompareItem($"{r["REF_NO"]}|{r["SUF_NO"]}", [Num(r["QTY"]), Num(r["AMOUNT"])], [r["CUS_ID"], r["PD_C"]])).ToList();
        results.Add(Compare("RPT-06", "การรับ ORDER ต่อบรรทัด (ประวัติ) → สรุปต่อเขตการขาย", "AC-17", ["จำนวนสั่ง", "จำนวนเงิน"], receipts,
            (await orders.GetReceivedAsync()).Select(l => new CompareItem($"{l.OrderNo}|{l.LineNo}", [l.Quantity, l.Amount ?? 0m], [l.CustomerCode, l.ProductCode ?? ""])).ToList(),
            uniqueKey: true, item => OrderLineReason(item, codes, quantityIndex: 0)));

        var baselineDeliveries = Read(baselineFolder, "deliveries.csv").ToList();
        var newDeliveries = await deliveries.GetDeliveriesAsync(DateOnly.MinValue, DateOnly.MaxValue);
        results.Add(Compare("RPT-07", "การส่งสินค้า (จำนวนรายการ / Σ จำนวน / Σ มูลค่า / Σ กล่อง)", "AC-16", ["จำนวน", "มูลค่า", "กล่อง"],
            baselineDeliveries.Select(r => new CompareItem($"{r["INV_NO"]}|{r["INV_SUF"]}|{r["REF_NO"]}|{r["PD_C"]}|{r["DL_DATE"]}", [Num(r["DL_QTY"]), Num(r["DL_NET"]), Num(r["DISC_PS"])], [r["CUS_ID"], r["PD_C"]])).ToList(),
            newDeliveries.Select(d => new CompareItem($"{d.InvoiceNo}|{d.InvoiceLine}|{d.OrderNo}|{d.ProductCode}|{d.DeliveryDate:yyyy-MM-dd}", [d.Quantity, d.Amount ?? 0m, d.Boxes ?? 0m], [d.CustomerCode, d.ProductCode ?? ""])).ToList(),
            uniqueKey: false, item => CustomerProductReason(item, codes)));
        results.Add(Compare("RPT-07x", "สรุปการส่งออกต่อลูกค้าต่อเดือน (มูลค่า)", "AC-23", ["มูลค่า"],
            baselineDeliveries.GroupBy(r => $"{r["CUS_ID"]}|{r["DL_DATE"][..7]}").Select(g => new CompareItem(g.Key, [g.Sum(r => Num(r["DL_NET"]))], [g.First()["CUS_ID"], g.First()["PD_C"]])).ToList(),
            newDeliveries.GroupBy(d => $"{d.CustomerCode}|{d.DeliveryDate:yyyy-MM}").Select(g => new CompareItem(g.Key, [g.Sum(d => d.Amount ?? 0m)], [g.First().CustomerCode, ""])).ToList(),
            uniqueKey: false, item => codes.Customers.Contains(item.Refs[0]) ? null : "ลูกค้าไม่มีในทะเบียนลูกค้า (ไม่โหลด)"));

        results.Add(Compare("RPT-13", $"ยอดยกมาวันตัด {cut:yyyy-MM-dd} (จำนวน / มูลค่า = จำนวน × FML)", "AC-11", ["จำนวน", "มูลค่า"],
            Read(baselineFolder, "stock-opening.csv").Select(r => new CompareItem(r["PD_C"], [Num(r["QTY"]), Math.Round(Num(r["QTY"]) * Num(r["FML"]), 2)], [r["PD_C"]])).ToList(),
            (await closing.GetCutReportAsync(cut)).Select(l => new CompareItem(l.ProductCode, [l.Quantity, Math.Round(l.Quantity * (l.UnitCost ?? 0m), 2)], [l.ProductCode])).ToList(),
            uniqueKey: false, item => codes.Products.Contains(item.Refs[0]) ? null : "รหัสสินค้าไม่มีในทะเบียนสินค้า (ไม่โหลด)"));

        results.Add(Compare("STOCK", "ยอดสต็อกคงเหลือปัจจุบันต่อสินค้า (Σ QTY ของ P_ST)", "AC-09", ["จำนวนคงเหลือ"],
            Read(baselineFolder, "stock-balance.csv").Select(r => new CompareItem(r["PD_C"], [Num(r["QTY"])], [r["PD_C"]])).Where(i => i.Values[0] != 0).ToList(),
            (await stock.GetBalancesAsync(nonZeroOnly: true)).Select(b => new CompareItem(b.ProductCode, [b.Quantity], [b.ProductCode])).ToList(),
            uniqueKey: false, item => codes.Products.Contains(item.Refs[0]) ? null : "รหัสสินค้าไม่มีในทะเบียนสินค้า (ไม่โหลด)"));

        results.Add(Compare("RPT-15", $"ยอดสะสมแม่พิมพ์วันตัด {cut:yyyy-MM-dd}", "AC-13", ["ยอดสะสม"],
            Read(baselineFolder, "mold-cumulative.csv").Select(r => new CompareItem(r["MOULD_ID"], [Num(r["PD_QTY"])], [r["MOULD_ID"]])).ToList(),
            (await molds.GetCumulativeAsync(cut)).Select(m => new CompareItem(m.MoldCode, [m.Cumulative], [m.MoldCode])).ToList(),
            uniqueKey: false, item => codes.Molds.Contains(item.Refs[0]) ? null : "รหัสแม่พิมพ์ไม่มีในทะเบียนแม่พิมพ์ (ไม่โหลด)"));

        var yield = Read(baselineFolder, "yield.csv").ToList();
        var first = yield.Count == 0 ? new DateOnly(2025, 1, 1) : DateOnly.Parse(yield.Min(r => r["MONTH"]) + "-01", CultureInfo.InvariantCulture);
        results.Add(Compare("RPT-16", "ผลผลิตต่อเดือน × สินค้า (จำนวนดี / เสีย)", "AC-18", ["ดี", "เสีย"],
            yield.Where(r => r["MONTH"].Length == 7).GroupBy(r => $"{r["MONTH"]}|{r["PD_C"]}").Select(g => new CompareItem(g.Key, [g.Sum(r => Num(r["QTY"])), g.Sum(r => Num(r["NG"]))], [g.First()["PD_C"]])).ToList(),
            (await production.GetYieldLinesAsync(first, DateOnly.MaxValue)).GroupBy(l => $"{l.Date:yyyy-MM}|{l.ProductCode}").Select(g => new CompareItem(g.Key, [g.Sum(l => l.Good), g.Sum(l => l.Reject)], [g.First().ProductCode])).ToList(),
            uniqueKey: false, item => codes.Products.Contains(item.Refs[0]) ? null : "รหัสสินค้าไม่มีในทะเบียนสินค้า (ไม่โหลด)"));
        return results;
    }

    private static string? OrderLineReason(CompareItem item, RegisterCodes codes, int quantityIndex)
    {
        if (item.Values[quantityIndex] == 0) return "ไม่มีจำนวน (ไม่โหลดตามกติกา)";
        return CustomerProductReason(item, codes);
    }

    private static string? CustomerProductReason(CompareItem item, RegisterCodes codes)
    {
        if (!codes.Customers.Contains(item.Refs[0])) return "ลูกค้าไม่มีในทะเบียนลูกค้า (ไม่โหลด)";
        if (!codes.Products.Contains(item.Refs[1])) return "รหัสสินค้าไม่มีในทะเบียนสินค้า (ไม่โหลด)";
        return null;
    }

    /// <summary>Compare two lists of rows: the same key is added up on both sides (a repeated key of a unique-key table keeps the first row of the baseline, the rest are set aside).</summary>
    public static CompareResult Compare(string id, string title, string criteria, string[] valueNames, IReadOnlyList<CompareItem> baseline, IReadOnlyList<CompareItem> database, bool uniqueKey, Func<CompareItem, string?> explain)
    {
        var explained = new List<CompareDetail>();
        var unexplained = new List<CompareDetail>();
        var different = new List<CompareDetail>();
        var onlyDatabase = new List<CompareDetail>();

        var baselineByKey = new Dictionary<string, CompareItem>();
        foreach (var item in baseline)
        {
            if (!baselineByKey.TryGetValue(item.Key, out var existing)) baselineByKey[item.Key] = item;
            else if (uniqueKey) explained.Add(new CompareDetail(item.Key, "คีย์ซ้ำในต้นทาง — ใช้แถวแรก", item.Values, []));
            else baselineByKey[item.Key] = existing with { Values = Add(existing.Values, item.Values) };
        }
        var databaseByKey = new Dictionary<string, CompareItem>();
        foreach (var item in database)
            databaseByKey[item.Key] = databaseByKey.TryGetValue(item.Key, out var existing) ? existing with { Values = Add(existing.Values, item.Values) } : item;

        var matched = 0;
        foreach (var (key, item) in baselineByKey)
        {
            if (databaseByKey.TryGetValue(key, out var other))
            {
                if (item.Values.Zip(other.Values).All(p => Math.Abs(p.First - p.Second) <= Tolerance)) matched++;
                else different.Add(new CompareDetail(key, "ค่าไม่ตรงกัน", item.Values, other.Values));
            }
            else if (explain(item) is { } reason) explained.Add(new CompareDetail(key, reason, item.Values, []));
            else unexplained.Add(new CompareDetail(key, "มีในต้นทาง ไม่มีในฐานข้อมูล (ไม่มีเหตุผลตามกติกา)", item.Values, []));
        }
        foreach (var (key, item) in databaseByKey)
            if (!baselineByKey.ContainsKey(key)) onlyDatabase.Add(new CompareDetail(key, "มีในฐานข้อมูล ไม่มีในต้นทาง", [], item.Values));

        return new CompareResult(id, title, criteria, valueNames, baselineByKey.Count, databaseByKey.Count, matched, different, explained, unexplained, onlyDatabase,
            Totals(baseline.Select(i => i.Values), valueNames.Length), Totals(database.Select(i => i.Values), valueNames.Length));
    }

    private static decimal[] Add(decimal[] a, decimal[] b) => a.Zip(b, (x, y) => x + y).ToArray();

    private static decimal[] Totals(IEnumerable<decimal[]> rows, int width)
    {
        var total = new decimal[width];
        foreach (var row in rows)
            for (var i = 0; i < width; i++) total[i] += row[i];
        return total;
    }

    private static decimal Num(string text) => text.Length == 0 ? 0m : decimal.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);

    // ---------- baseline files ----------

    /// <summary>Rows of a CSV written by export_baseline.py (UTF-8, header row, quoted fields).</summary>
    public static IEnumerable<Dictionary<string, string>> Read(string folder, string file)
    {
        var path = Path.Combine(folder, file);
        if (!File.Exists(path)) throw new FileNotFoundException($"ไม่พบไฟล์ baseline {file} ใน {folder} — รัน tools/baseline/export_baseline.py ก่อน");
        using var reader = new StreamReader(path, Encoding.UTF8);
        var header = ParseLine(reader.ReadLine() ?? throw new InvalidDataException($"{file} ว่าง"));
        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0) continue;
            var cells = ParseLine(line);
            yield return header.Select((name, i) => (name, value: i < cells.Count ? cells[i] : "")).ToDictionary(p => p.name, p => p.value);
        }
    }

    private static List<string> ParseLine(string line)
    {
        var cells = new List<string>();
        var cell = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (quoted) { if (c == '"' && i + 1 < line.Length && line[i + 1] == '"') { cell.Append('"'); i++; } else if (c == '"') quoted = false; else cell.Append(c); }
            else if (c == '"') quoted = true;
            else if (c == ',') { cells.Add(cell.ToString()); cell.Clear(); }
            else cell.Append(c);
        }
        cells.Add(cell.ToString());
        return cells;
    }

    // ---------- the register, to say why a row was not loaded ----------

    private sealed record RegisterCodes(HashSet<string> Products, HashSet<string> Customers, HashSet<string> Molds);

    private static async Task<RegisterCodes> RegisterCodesAsync(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        async Task<HashSet<string>> CodesAsync(string sql)
        {
            await using var command = new SqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync();
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (await reader.ReadAsync()) set.Add(reader.GetString(0).Trim());
            return set;
        }
        return new RegisterCodes(await CodesAsync("SELECT Code FROM prod.Products"), await CodesAsync("SELECT Code FROM sales.Customers"), await CodesAsync("SELECT Code FROM prod.Molds"));
    }

    // ---------- report ----------

    public static void WriteReport(string folder, IReadOnlyList<CompareResult> results, DateOnly cut)
    {
        Directory.CreateDirectory(folder);
        var summary = new StringBuilder();
        summary.AppendLine("# รายงานเทียบข้อมูลที่ย้ายกับ baseline จาก Access (#140)").AppendLine();
        summary.AppendLine($"ตัวเลขรวมเท่านั้น (รายการรายแถวที่มีรหัสจริงอยู่ใน `compare-details.csv` เก็บนอก git) · วันตัด {cut:yyyy-MM-dd}").AppendLine();
        summary.AppendLine("เกณฑ์: ส่วนที่โหลดถูกต้องต้องตรง 100% — ไม่มีค่าไม่ตรง ไม่มีแถวต้นทางที่หายโดยไม่มีเหตุผลตามกติกา ไม่มีแถวที่มีเฉพาะในฐานข้อมูล · แถวที่ไม่โหลดตามหลัก \"ออกแบบใหม่\" แสดงยอดแยกไว้ ไม่ซ่อน").AppendLine();
        summary.AppendLine("| รายงาน | AC | ผล | แถวต้นทาง | แถวฐานข้อมูล | ตรง | ค่าไม่ตรง | หักออก (มีเหตุผล) | หายไม่มีเหตุผล | เฉพาะฐานข้อมูล |");
        summary.AppendLine("|---|---|---|---|---|---|---|---|---|---|");
        foreach (var r in results)
            summary.AppendLine($"| {r.Id} {r.Title} | {r.AcceptanceCriteria} | {(r.Passed ? "ผ่าน" : "ไม่ผ่าน")} | {r.BaselineKeys:N0} | {r.NewKeys:N0} | {r.Matched:N0} | {r.Different.Count:N0} | {r.BaselineOnlyExplained.Count:N0} | {r.BaselineOnlyUnexplained.Count:N0} | {r.NewOnly.Count:N0} |");
        summary.AppendLine();
        foreach (var r in results)
        {
            summary.AppendLine($"## {r.Id} — {r.Title} ({r.AcceptanceCriteria}): {(r.Passed ? "ผ่าน" : "ไม่ผ่าน")}").AppendLine();
            summary.AppendLine("| ค่า | รวมต้นทาง | รวมฐานข้อมูล | ผลต่าง | หักออก (มีเหตุผล) |");
            summary.AppendLine("|---|---|---|---|---|");
            for (var i = 0; i < r.ValueNames.Length; i++)
                summary.AppendLine($"| {r.ValueNames[i]} | {r.BaselineTotals[i]:N2} | {r.NewTotals[i]:N2} | {r.BaselineTotals[i] - r.NewTotals[i]:N2} | {r.BaselineOnlyExplained.Sum(d => d.Baseline.Length > i ? d.Baseline[i] : 0):N2} |");
            summary.AppendLine();
            foreach (var group in r.BaselineOnlyExplained.GroupBy(d => d.Reason).OrderByDescending(g => g.Count()))
                summary.AppendLine($"- หักออก: {group.Key} — {group.Count():N0} แถว (Σ {string.Join(" / ", Enumerable.Range(0, r.ValueNames.Length).Select(i => group.Sum(d => d.Baseline.Length > i ? d.Baseline[i] : 0).ToString("N2", CultureInfo.InvariantCulture)))})");
            if (r.Different.Count > 0) summary.AppendLine($"- ค่าไม่ตรงกัน {r.Different.Count:N0} แถว — ดู compare-details.csv");
            if (r.BaselineOnlyUnexplained.Count > 0) summary.AppendLine($"- หายไปโดยไม่มีเหตุผลตามกติกา {r.BaselineOnlyUnexplained.Count:N0} แถว — ดู compare-details.csv");
            if (r.NewOnly.Count > 0) summary.AppendLine($"- มีเฉพาะในฐานข้อมูล {r.NewOnly.Count:N0} แถว — ดู compare-details.csv");
            summary.AppendLine();
        }
        File.WriteAllText(Path.Combine(folder, "compare-summary.md"), summary.ToString(), new UTF8Encoding(true));
        Csv.Write(Path.Combine(folder, "compare-details.csv"), ["รายงาน", "ประเภท", "คีย์", "เหตุผล", "ค่าต้นทาง", "ค่าฐานข้อมูล"],
            results.SelectMany(r => Rows(r, "ค่าไม่ตรง", r.Different).Concat(Rows(r, "หักออก", r.BaselineOnlyExplained)).Concat(Rows(r, "หายไม่มีเหตุผล", r.BaselineOnlyUnexplained)).Concat(Rows(r, "เฉพาะฐานข้อมูล", r.NewOnly))));
    }

    private static IEnumerable<object?[]> Rows(CompareResult r, string kind, IEnumerable<CompareDetail> details)
        => details.Select(d => new object?[] { r.Id, kind, d.Key, d.Reason, string.Join(" / ", d.Baseline.Select(v => v.ToString(CultureInfo.InvariantCulture))), string.Join(" / ", d.New.Select(v => v.ToString(CultureInfo.InvariantCulture))) });
}
