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
public sealed class PurchHistoryMigration(AuditScope audit, PurchHistoryImport import, SupplierDirectory suppliers)
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

    /// <summary>The quarter (1–4) of the calendar year a period starts in — used only when the old row has no valid TERM.</summary>
    public static int QuarterOf(DateOnly date) => (date.Month - 1) / 3 + 1;
}
