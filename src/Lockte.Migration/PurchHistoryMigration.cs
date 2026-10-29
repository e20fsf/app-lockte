using System.Globalization;
using Lockte.Purch;
using Lockte.SharedKernel.Persistence;

namespace Lockte.Migration;

/// <summary>
/// ย้ายประวัติของ <c>วัตถุดิบ.accdb</c> (#268 ผลประเมินผู้ขาย; the documents and the stock book follow in #266, #267): run after the master data (#217) on a database that holds the suppliers.
/// The old table keys a result by the supplier's **name**; a name is matched to the supplier's code by the name itself, a name that no supplier has, or that more than one has, goes to the
/// DQ report to be decided in the code map (<see cref="CodeKinds.SupplierName"/>) and run again. The row for <c>*</c> (all suppliers added up) is no supplier and is not loaded. Loaded through
/// <see cref="PurchHistoryImport"/>, outside the change history (#39).
/// </summary>
public sealed class PurchHistoryMigration(AuditScope audit, PurchHistoryImport import, SupplierDirectory suppliers, MaterialDirectory materials, IPurchEmployeeLookup employees)
{
    private const string Other = "NF-06";
    private const string SupplierName = "DQ-16";
    private const string Duplicate = "DQ-17";

    private LegacyDatabase legacy = null!;
    private CodeMap map = null!;
    private MigrationReport report = null!;

    /// <summary>Once per instance (it is scoped): the run keeps what it loaded in fields.</summary>
    public async Task<MigrationReport> RunAsync(LegacyDatabase legacyDatabase, CodeMap codeMap, MigrationReport? into = null)
    {
        if (this.report is not null) throw new InvalidOperationException("PurchHistoryMigration runs once per scope.");
        (legacy, map, report) = (legacyDatabase, codeMap, into ?? new MigrationReport { Source = legacyDatabase.Source });
        using (audit.SuppressRowChanges())
            await LoadEvaluationsAsync();
            await LoadStockAsync();
        return report;
    }

    private async Task LoadEvaluationsAsync()
    {
        const string table = "ประเมินผู้ขาย";
        var rows = legacy[table];
        var (excluded, rejected) = (0, 0);
        var all = await suppliers.GetAllAsync();
        var byCode = all.ToDictionary(s => s.Code, StringComparer.OrdinalIgnoreCase);
        var byName = all.ToLookup(s => s.Name.Trim(), StringComparer.Ordinal);

        await import.DeleteEvaluationsAsync();
        var loaded = new List<ImportedEvaluation>();
        var keys = new HashSet<(int, DateOnly, DateOnly)>();
        var expected = new long[6];
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var position = $"แถวที่ {i + 1}";
            if (row.Text("SUP") is not { } name) { rejected++; report.Add(table, position, Other, "ไม่มีชื่อผู้ขาย", MigrationReport.NotLoaded); continue; }
            if (name == "*") { excluded++; continue; } // all the suppliers added up (BR-050): no supplier, by design
            if (row.Date("FROM_DATE") is not { } from || row.Date("TO_DATE") is not { } to || from.Year < 1990 || to < from)
            {
                rejected++;
                report.Add(table, position, Other, "ช่วงวันที่ไม่ถูกต้องหรือไม่มี", MigrationReport.NotLoaded);
                continue;
            }

            Lockte.Purch.SupplierDto? supplier;
            var decision = map.Find(CodeKinds.SupplierName, name);
            if (decision?.Code is { } code)
            {
                if (!byCode.TryGetValue(code, out supplier)) { rejected++; report.Add(table, position, SupplierName, $"ตารางจับคู่ให้รหัส '{code}' แต่ไม่มีผู้ขายรหัสนี้", MigrationReport.NotLoaded); continue; }
            }
            else
            {
                var found = byName[name.Trim()].ToList();
                if (found.Count != 1)
                {
                    rejected++;
                    report.Add(table, position, SupplierName, found.Count == 0
                        ? $"ไม่พบผู้ขายที่ชื่อตรงกับ SUP — ใส่ {CodeKinds.SupplierName},<ชื่อเดิม>,<รหัสผู้ขาย> ในตารางจับคู่ (ชื่อ {name.Length} ตัวอักษร)"
                        : $"ชื่อ SUP ตรงกับผู้ขาย {found.Count} ราย ({string.Join(", ", found.Select(f => f.Code))}) — ใส่ {CodeKinds.SupplierName},<ชื่อเดิม>,<รหัสผู้ขาย> ในตารางจับคู่", MigrationReport.NotLoaded);
                    continue;
                }
                supplier = found[0];
            }
            if (!keys.Add((supplier.Id, DateOnly.FromDateTime(from), DateOnly.FromDateTime(to))))
            {
                rejected++;
                report.Add(table, position, Duplicate, $"ผู้ขาย {supplier.Code} ช่วงวันที่เดียวกันซ้ำ — โหลดแถวแรก", MigrationReport.NotLoaded);
                continue;
            }

            var counts = new[] { "EXPR1", "EXPR2", "EXPR3", "EXPR4", "EXPR5", "EXPR6" }.Select(c => (int)Math.Round(row.Number(c) ?? 0)).ToArray();
            var quarter = int.TryParse(row.Text("TERM"), NumberStyles.None, CultureInfo.InvariantCulture, out var q) && q is >= 1 and <= 4 ? q : QuarterOf(DateOnly.FromDateTime(from));
            if (row.Text("TERM") is { } term && !(int.TryParse(term, out var t) && t is >= 1 and <= 4)) report.Add(table, position, Other, $"ไตรมาส '{term}' ไม่ใช่ 1–4 — ใช้ไตรมาสของวันเริ่มช่วง", MigrationReport.LoadedBlank);
            loaded.Add(new ImportedEvaluation(supplier.Id, DateOnly.FromDateTime(from), DateOnly.FromDateTime(to), quarter, counts[0], counts[1], counts[2], counts[3], counts[4], counts[5],
                Math.Clamp((int)Math.Round(row.Number("POINT1") ?? 0), 0, 5), Math.Clamp((int)Math.Round(row.Number("POINT2") ?? 0), 0, 5)));
            for (var n = 0; n < 6; n++) expected[n] += counts[n];
        }
        await import.AddEvaluationsAsync(loaded);

        var (inDatabase, sums) = await import.CountEvaluationsAsync();
        report.Tables.Add(new TableCount(table, "purch.SupplierEvaluations", rows.Count, excluded, rejected, loaded.Count, inDatabase));
        string[] measures = ["ผ่าน", "ไม่ผ่าน", "มีใบรับรอง", "ไม่มีใบรับรอง", "ส่งทัน", "ส่งไม่ทัน"];
        for (var n = 0; n < 6; n++)
            report.MonthlyTotals.Add(new MonthlyTotal(table, "ทุกไตรมาส", measures[n], expected[n], expected[n], sums[n]));
        report.Notes.Add($"ผลประเมินผู้ขาย: ผลรวมทุกผู้ขาย (SUP = *) {excluded} แถว ไม่ย้าย");
    }


    // ---------- สมุดสต็อก (Stock) และยอดยกมาของทุกการตัด (ต้นงวดP_ST) (#267) ----------

    private const string UnknownMaterial = "DQ-07";
    private const string NegativeBalance = "DQ-08";
    private const string BadDate = "DQ-09";
    private const string BadEmployee = "DQ-10";

    private async Task LoadStockAsync()
    {
        const string table = "Stock";
        var rows = legacy[table];
        var materialByCode = (await materials.GetAllAsync()).ToDictionary(m => m.Code, StringComparer.OrdinalIgnoreCase);
        var supplierByCode = (await suppliers.GetAllAsync()).ToDictionary(s => s.Code, StringComparer.OrdinalIgnoreCase);
        var employeeCache = new Dictionary<string, int?>(StringComparer.OrdinalIgnoreCase);
        var today = DateOnly.FromDateTime(DateTime.Today);

        await import.DeleteStockAsync();
        var loaded = new List<ImportedStockEntry>();
        var expected = new Dictionary<int, decimal>();
        var unmatched = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        int rejected = 0;
        decimal sumIn = 0, sumOut = 0;
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var position = $"แถวที่ {i + 1}";
            var quantity = (decimal)(row.Number("RE_QTY") ?? 0);
            if (row.Text("MTR_CODE") is not { } code) { rejected++; report.Add(table, position, Other, "ไม่มีรหัสวัตถุดิบ", MigrationReport.NotLoaded); continue; }
            if (!materialByCode.TryGetValue(code, out var material))
            {
                // DQ-07: matched by the code in capitals; what cannot be matched is reported with its quantity, which stays in the totals of the report
                rejected++;
                unmatched[code.ToUpperInvariant()] = unmatched.GetValueOrDefault(code.ToUpperInvariant()) + quantity;
                report.Add(table, code, UnknownMaterial, $"รหัสวัตถุดิบไม่อยู่ในทะเบียน (จำนวน {quantity.ToString("0.####", CultureInfo.InvariantCulture)})", MigrationReport.NotLoaded);
                continue;
            }
            if (row.Date("RE_DATE") is not { } legacyDate) { rejected++; report.Add(table, code, BadDate, $"{position}: ไม่มีวันที่ — ไม่โหลด (จำนวน {quantity.ToString("0.####", CultureInfo.InvariantCulture)})", MigrationReport.NotLoaded); unmatched[code.ToUpperInvariant() + " (ไม่มีวันที่)"] = quantity; continue; }
            var date = DateOnly.FromDateTime(legacyDate);
            if (date.Year < 2000 || date > today)
                report.Add(table, code, BadDate, $"{position}: วันที่ {date:yyyy-MM-dd} ผิดปกติ — โหลดตามเดิม ไม่แก้เอง", "โหลดตามเดิม");

            var mark = row.Text("MARK")?.ToLowerInvariant();
            var document = row.Text("INVOICE_NO");
            var kind = document == "stock" ? StockEntryKind.OpeningBalance : mark == "st" ? StockEntryKind.Receipt : mark == "tr" ? StockEntryKind.Issue : StockEntryKind.Adjustment;
            int? supplierId = null, employeeId = null;
            string? note = null;
            var who = row.Text("EMP_ID");
            if (kind == StockEntryKind.Receipt && who is not null)
            {
                if (supplierByCode.TryGetValue(who, out var supplier)) supplierId = supplier.Id;
                else report.Add(table, code, Other, $"{position}: ผู้ขาย '{who}' ของแถวรับไม่อยู่ในทะเบียนผู้ขาย", MigrationReport.LoadedBlank);
            }
            else if (kind == StockEntryKind.Issue && who is not null)
            {
                if (!employeeCache.TryGetValue(who, out employeeId)) employeeCache[who] = employeeId = await FindEmployeeAsync(who);
                if (employeeId is null)
                {
                    note = $"รหัสพนักงานเดิม: {who}";
                    report.Add(table, code, BadEmployee, $"{position}: รหัสพนักงานผู้เบิก '{who}' ไม่พบในทะเบียนพนักงาน — เก็บเป็นหมายเหตุ ใส่ {CodeKinds.StockEmployee},{who},<รหัสพนักงาน> ในตารางจับคู่", MigrationReport.LoadedBlank);
                }
            }
            loaded.Add(new ImportedStockEntry(date, document, row.Text("INV_SUF"), material.Id, quantity, kind, supplierId, employeeId, kind == StockEntryKind.Receipt && row.Number("U_PRICE") is { } price and > 0 ? (decimal)price : null, note));
            expected[material.Id] = expected.GetValueOrDefault(material.Id) + quantity;
            if (quantity >= 0) sumIn += quantity; else sumOut += quantity;
        }
        await import.AddStockEntriesAsync(loaded);

        // AC-18: the balance of every material = the sum of its rows in the old book
        var actual = await import.GetStockBalancesAsync();
        var codeOf = materialByCode.Values.ToDictionary(m => m.Id, m => m.Code);
        foreach (var (id, sum) in expected.Where(e => actual.GetValueOrDefault(e.Key) != e.Value))
            report.MonthlyTotals.Add(new MonthlyTotal(table, codeOf[id], "ยอดคงเหลือ", (double)sum, (double)sum, (double)actual.GetValueOrDefault(id)));
        foreach (var (id, sum) in actual.Where(a => a.Value < 0).OrderBy(a => codeOf[a.Key], StringComparer.Ordinal))
            report.Add(table, codeOf[id], NegativeBalance, $"ยอดคงเหลือติดลบ {sum.ToString("0.####", CultureInfo.InvariantCulture)} — โหลดตามเดิม ห้ามปรับเป็น 0 เอง รอตรวจนับ (Q-13)", "โหลดตามเดิม");
        var (entries, _) = await import.CountStockAsync();
        report.Tables.Add(new TableCount(table, "purch.StockEntries", rows.Count, 0, rejected, loaded.Count, entries));
        report.MonthlyTotals.Add(new MonthlyTotal(table, "ทั้งหมด", "Σ จำนวนที่โหลด (รับ/ยกมา +)", (double)sumIn, (double)sumIn, (double)(await StockSumAsync(positive: true))));
        report.MonthlyTotals.Add(new MonthlyTotal(table, "ทั้งหมด", "Σ จำนวนที่โหลด (เบิก −)", (double)sumOut, (double)sumOut, (double)(await StockSumAsync(positive: false))));
        foreach (var (code, sum) in unmatched.OrderBy(u => u.Key, StringComparer.Ordinal))
            report.Notes.Add($"สต็อก: แถวที่ไม่ได้โหลด {code} รวม {sum.ToString("0.####", CultureInfo.InvariantCulture)}");

        await LoadCutBalancesAsync(materialByCode);
    }

    private async Task<decimal> StockSumAsync(bool positive)
        => (await import.GetStockRowQuantitiesAsync()).Where(q => positive ? q >= 0 : q < 0).Sum();

    private async Task<int?> FindEmployeeAsync(string text)
    {
        var decision = map.Find(CodeKinds.StockEmployee, text);
        if (decision?.Code == CodeKinds.Nobody) return null;
        return (await employees.FindByCodeAsync(decision?.Code ?? text))?.Id;
    }

    private async Task LoadCutBalancesAsync(Dictionary<string, MaterialDto> materialByCode)
    {
        const string table = "ต้นงวดP_ST";
        var rows = legacy[table];
        var sourceRows = legacy.SourceRows(table);
        var totals = new Dictionary<(DateOnly, int), decimal>();
        var rejected = 0;
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.Date("DATE") is not { } legacyDate || row.Text("PD_C") is not { } code) { rejected++; report.Add(table, $"แถวที่ {i + 1}", Other, "ไม่มีวันที่หรือรหัสวัตถุดิบ", MigrationReport.NotLoaded); continue; }
            if (!materialByCode.TryGetValue(code, out var material)) { rejected++; report.Add(table, code, UnknownMaterial, "รหัสวัตถุดิบไม่อยู่ในทะเบียน", MigrationReport.NotLoaded); continue; }
            var key = (DateOnly.FromDateTime(legacyDate), material.Id);
            totals[key] = totals.GetValueOrDefault(key) + (decimal)(row.Number("QTY") ?? 0);
        }
        await import.AddPeriodBalancesAsync(totals.Select(t => new ImportedPeriodBalance(t.Key.Item1, t.Key.Item2, t.Value)).ToList());
        var (_, balances) = await import.CountStockAsync();
        // rows of one cut and one material that repeat are added up into the one balance (counted as moved with it)
        var merged = rows.Count - rejected - totals.Count;
        report.Tables.Add(new TableCount(table, "purch.StockPeriodBalances", sourceRows, sourceRows - rows.Count + merged, rejected, totals.Count, balances));
        if (sourceRows > rows.Count) report.Notes.Add($"ต้นงวดP_ST: ย้ายเฉพาะยอดยกมาตั้งแต่ {LegacyDatabase.PurchCutFrom:yyyy-MM-dd} ({sourceRows - rows.Count} แถวเก่ากว่านั้นไม่ย้าย)");
    }

    /// <summary>The quarter (1–4) of the calendar year a period starts in — used only when the old row has no valid TERM.</summary>
    public static int QuarterOf(DateOnly date) => (date.Month - 1) / 3 + 1;
}
