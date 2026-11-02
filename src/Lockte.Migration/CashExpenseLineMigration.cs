using System.Globalization;
using System.Text.RegularExpressions;
using Lockte.Cash;
using Lockte.SharedKernel.Persistence;

namespace Lockte.Migration;

/// <summary>What the migration of the expense lines decides on its own and how it reads a legacy number, apart from the database so it can be tested without one.</summary>
public static partial class CashLineRules
{
    /// <summary>A voucher has seven rows on its paper (RPT-01); more lines than that are reported (Q-31).</summary>
    public const int VoucherRows = 7;

    [GeneratedRegex(@"^(0[1-9]|1[0-2])/\d{2,3}$")]
    private static partial Regex VoucherShape();

    /// <summary>The month in a voucher number <c>MM/NN</c> or <c>MM/NNN</c>; null for any other shape (the legacy has <c>9/99</c> and <c>99//99</c>).</summary>
    public static int? VoucherMonth(string voucherNo)
        => VoucherShape().IsMatch(voucherNo) ? int.Parse(voucherNo[..2], CultureInfo.InvariantCulture) : null;

    /// <summary>A Double of Access as a decimal: the value to the 15 digits Access shows (<c>25.01</c>, not 25.010000000000002).</summary>
    public static decimal Decimal(double value) => Convert.ToDecimal(value, CultureInfo.InvariantCulture);

    /// <summary>The value to <paramref name="places"/> decimals, half away from zero, and whether that changed it.</summary>
    public static (decimal Value, bool Changed) Fit(decimal value, int places)
    {
        var rounded = Math.Round(value, places, MidpointRounding.AwayFromZero);
        return (rounded, rounded != value);
    }

    /// <summary>Whether the amount is not quantity × price to the satang (4,980 of the 4,983 are price 0: the amount was typed).</summary>
    public static bool AmountDiffers(decimal qty, decimal price, decimal amount) => Math.Abs(Math.Round(qty * price, 2, MidpointRounding.AwayFromZero) - amount) >= 0.005m;

    public static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string Money(decimal value) => value.ToString("0.####", CultureInfo.InvariantCulture);
}

/// <summary>
/// ย้ายบรรทัดค่าใช้จ่ายของ <c>เงินสดย่อย.accdb</c> (#236): every row of <c>ซื้อของเงินสด</c> becomes a line of <c>cash.ExpenseLines</c>, run after <see cref="CashMasterDataMigration"/> (which has made
/// every type the lines use). The amount is kept as the legacy had it, never worked out again; the type is the one the cleaned TYPE (or the code map) names — a TYPE that finds no type is
/// not loaded and no orphan type is made. A row the legacy would have deleted on leaving the screen (no date, number, item or type, BR-003) is not loaded and is listed. What is only worth
/// knowing — vouchers of more than seven lines, a number used on two days of a year, a month in the number that is not the month of the date, a year typed wrong — is reported and
/// the rows are loaded as they are. The report names items and numbers of real vouchers: kept outside git.
/// </summary>
public sealed class CashExpenseLineMigration(AuditScope audit, CashExpenseLineImport import, ExpenseTypeDirectory typeDirectory, ExpenseLineDirectory lineDirectory)
{
    public const string LinesTable = "ซื้อของเงินสด";
    private const string YearRule = "DQ-Q06";
    private const string NumberRule = "DQ-Q03";
    private const string SizeRule = "DQ-Q31";
    private const string TypeRule = "DQ-Q05";
    private const string AmountRule = "DQ-Q02";
    private const string Other = "NF-06";

    private record struct Source(DateOnly Date, string VoucherNo, string Name, decimal Qty, string? Unit, decimal UnitPrice, decimal Amount, string TypeName, string RawType);

    public async Task RunAsync(LegacyDatabase legacy, CodeMap map, MigrationReport report)
    {
        var typesByName = (await typeDirectory.ListAllAsync()).ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
        var rows = legacy[LinesTable];
        var loaded = new List<(Source Line, int TypeId)>();
        var rejected = 0;
        var number = 0;
        foreach (var row in rows)
        {
            number++;
            var key = $"แถวที่ {number.ToString(CultureInfo.InvariantCulture)}";
            var rawType = row["TYPE"] as string;
            var type = CashMigrationRules.Name(rawType);
            var voucher = row.Text("SUF");
            var name = row.Text("NAME");
            if (row.Date("DATE") is not { } date || voucher is null || name is null || type is null)
            {
                rejected++;
                report.Add(LinesTable, key, Other, "ไม่มี" + string.Join(" ", new[] { row.Date("DATE") is null ? "วันที่" : null, voucher is null ? "เลขที่" : null, name is null ? "รายการ" : null, type is null ? "ประเภท" : null }.OfType<string>())
                    + " — ไม่โหลด (ระบบเดิมลบแถวนี้ตอนออกจากหน้าจอ BR-003)", MigrationReport.NotLoaded);
                continue;
            }
            var target = map.Find(CodeKinds.CashExpenseType, type)?.Code is { } mapped ? CashMigrationRules.Name(mapped) ?? type : type;
            if (!typesByName.TryGetValue(target, out var typeDto))
            {
                rejected++;
                report.Add(LinesTable, $"{CashLineRules.Iso(DateOnly.FromDateTime(date))} {voucher}", TypeRule, $"ประเภท '{target}' ไม่อยู่ในทะเบียน — ไม่โหลด (ต้องผ่านขั้นข้อมูลหลักก่อน ไม่สร้างประเภทกำพร้า)", MigrationReport.NotLoaded);
                continue;
            }

            var day = DateOnly.FromDateTime(date);
            var (qty, qtyChanged) = CashLineRules.Fit(CashLineRules.Decimal(row.Number("QTY") ?? 0), 3);
            var (price, priceChanged) = CashLineRules.Fit(CashLineRules.Decimal(row.Number("U_PRICE") ?? 0), 4);
            var (amount, amountChanged) = CashLineRules.Fit(CashLineRules.Decimal(row.Number("AMOUNT") ?? 0), 2);
            var at = $"{CashLineRules.Iso(day)} {voucher}";
            if (qtyChanged) report.Add(LinesTable, at, AmountRule, $"จำนวนเกิน 3 ตำแหน่ง → ปัดเป็น {CashLineRules.Money(qty)} (เดิม {CashLineRules.Money(CashLineRules.Decimal(row.Number("QTY") ?? 0))})", "แก้อัตโนมัติ");
            if (priceChanged) report.Add(LinesTable, at, AmountRule, $"ราคาเกิน 4 ตำแหน่ง → ปัดเป็น {CashLineRules.Money(price)} (เดิม {CashLineRules.Money(CashLineRules.Decimal(row.Number("U_PRICE") ?? 0))})", "แก้อัตโนมัติ");
            if (amountChanged) report.Add(LinesTable, at, AmountRule, $"จำนวนเงินเกิน 2 ตำแหน่ง → ปัดเป็น {CashLineRules.Money(amount)} (เดิม {CashLineRules.Money(CashLineRules.Decimal(row.Number("AMOUNT") ?? 0))})", "แก้อัตโนมัติ");
            if (day.Year == 2011) report.Add(LinesTable, at, YearRule, "วันที่ปี 2011 (🔍 พิมพ์ปีผิด) — โหลดตามเดิม รอคำตัดสิน Q-06", "โหลดตามเดิม");
            loaded.Add((new Source(day, voucher, name, qty, row.Text("UNIT"), price, amount, typeDto.Name, type), typeDto.Id));
        }

        var inserted = loaded.Select(l => new ImportedExpenseLine(l.Line.Date, l.Line.VoucherNo, l.Line.Name, l.Line.Qty, l.Line.Unit, l.Line.UnitPrice, l.Line.Amount, l.TypeId)).ToList();
        using (audit.SuppressRowChanges()) await import.LoadAsync(inserted);

        ReportVouchers(report, loaded.Select(l => l.Line).ToList());
        await CountBackAsync(legacy, report, loaded.Select(l => l.Line).ToList(), rows.Count, rejected);
    }

    /// <summary>Things worth knowing about the numbers of the vouchers; nothing here changes a row.</summary>
    private static void ReportVouchers(MigrationReport report, List<Source> lines)
    {
        foreach (var g in lines.GroupBy(l => (l.Date, l.VoucherNo)).Where(g => g.Count() > CashLineRules.VoucherRows).OrderBy(g => g.Key.Date).ThenBy(g => g.Key.VoucherNo, StringComparer.Ordinal))
            report.Add(LinesTable, $"{CashLineRules.Iso(g.Key.Date)} {g.Key.VoucherNo}", SizeRule,
                $"ใบสำคัญจ่ายมี {g.Count().ToString(CultureInfo.InvariantCulture)} บรรทัด เกิน {CashLineRules.VoucherRows} แถวของกระดาษ" + (g.Select(l => l.TypeName).Distinct().Count() > 1 ? " และมีหลายประเภทในใบเดียว" : ""),
                "รายงานอย่างเดียว (พิมพ์เป็นหลายแผ่น)");
        foreach (var g in lines.GroupBy(l => (l.Date.Year, l.VoucherNo)).Where(g => g.Select(l => l.Date).Distinct().Count() > 1).OrderBy(g => g.Key.Year).ThenBy(g => g.Key.VoucherNo, StringComparer.Ordinal))
            report.Add(LinesTable, $"{g.Key.Year.ToString(CultureInfo.InvariantCulture)} {g.Key.VoucherNo}", NumberRule,
                $"เลขที่เดียวกันใช้ {g.Select(l => l.Date).Distinct().Count().ToString(CultureInfo.InvariantCulture)} วันที่ในปีเดียวกัน (ใบสำคัญจ่าย = คู่ วันที่+เลขที่)", "รายงานอย่างเดียว");
        foreach (var l in lines)
        {
            var month = CashLineRules.VoucherMonth(l.VoucherNo);
            if (month is null) report.Add(LinesTable, $"{CashLineRules.Iso(l.Date)} {l.VoucherNo}", NumberRule, "เลขที่ไม่ใช่รูป MM/NN หรือ MM/NNN — โหลดตามข้อความเดิม", "รายงานอย่างเดียว");
            else if (month != l.Date.Month) report.Add(LinesTable, $"{CashLineRules.Iso(l.Date)} {l.VoucherNo}", NumberRule, $"เดือนในเลขที่ ({month.Value.ToString(CultureInfo.InvariantCulture)}) ไม่ตรงเดือนของวันที่ ({l.Date.Month.ToString(CultureInfo.InvariantCulture)})", "รายงานอย่างเดียว");
        }
    }

    /// <summary>AC-01: the lines read back from the database against the legacy — rows by year, the sum by month and by type, and the raw names with what they became.</summary>
    private async Task CountBackAsync(LegacyDatabase legacy, MigrationReport report, List<Source> loaded, int sourceRows, int rejected)
    {
        var inDatabase = await lineDirectory.ListAsync(DateOnly.MinValue, DateOnly.MaxValue);

        // rows: one line for the whole table and one for each year, so a year that goes wrong shows
        report.Tables.Add(new TableCount(LinesTable, "cash.ExpenseLines", sourceRows, 0, rejected, loaded.Count, inDatabase.Count));
        foreach (var year in loaded.Select(l => l.Date.Year).Union(inDatabase.Select(l => l.Date.Year)).Distinct().Order())
        {
            var (inFile, here) = (loaded.Count(l => l.Date.Year == year), inDatabase.Count(l => l.Date.Year == year));
            report.Tables.Add(new TableCount($"{LinesTable} ปี {year.ToString(CultureInfo.InvariantCulture)}", $"cash.ExpenseLines ปี {year.ToString(CultureInfo.InvariantCulture)}", inFile, 0, 0, inFile, here));
        }

        // the sum of the amounts: the source = the legacy rows read; expected = those loaded (the rows left out are in the DQ report); then the database
        var legacySource = legacy[LinesTable].Where(r => r.Date("DATE") is not null).ToList();
        foreach (var month in loaded.Select(l => (l.Date.Year, l.Date.Month)).Union(inDatabase.Select(l => (l.Date.Year, l.Date.Month))).Distinct().Order())
        {
            var label = $"{month.Year.ToString("0000", CultureInfo.InvariantCulture)}-{month.Month.ToString("00", CultureInfo.InvariantCulture)}";
            var all = legacySource.Where(r => r.Date("DATE") is { } d && (d.Year, d.Month) == month).Sum(r => r.Number("AMOUNT") ?? 0);
            report.MonthlyTotals.Add(new MonthlyTotal(LinesTable, label, "จำนวนเงิน", all, (double)loaded.Where(l => (l.Date.Year, l.Date.Month) == month).Sum(l => l.Amount),
                (double)inDatabase.Where(l => (l.Date.Year, l.Date.Month) == month).Sum(l => l.Amount)));
        }
        foreach (var type in loaded.Select(l => l.TypeName).Union(inDatabase.Select(l => l.ExpenseTypeName)).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal))
            report.MonthlyTotals.Add(new MonthlyTotal(LinesTable, "ประเภท " + type, "จำนวนเงิน",
                (double)loaded.Where(l => string.Equals(l.TypeName, type, StringComparison.OrdinalIgnoreCase)).Sum(l => l.Amount),
                (double)loaded.Where(l => string.Equals(l.TypeName, type, StringComparison.OrdinalIgnoreCase)).Sum(l => l.Amount),
                (double)inDatabase.Where(l => string.Equals(l.ExpenseTypeName, type, StringComparison.OrdinalIgnoreCase)).Sum(l => l.Amount)));

        // the raw names as typed in TYPE and the type each became (the amounts of the types above are those after this mapping)
        foreach (var g in loaded.GroupBy(l => l.RawType, StringComparer.OrdinalIgnoreCase).OrderBy(g => g.Key, StringComparer.Ordinal))
            report.Notes.Add($"ยอดตามชื่อดิบ '{g.Key}': {g.Count().ToString(CultureInfo.InvariantCulture)} แถว รวม {CashLineRules.Money(g.Sum(l => l.Amount))} → ประเภท '{g.First().TypeName}'");

        var differs = loaded.Count(l => CashLineRules.AmountDiffers(l.Qty, l.UnitPrice, l.Amount));
        var priceZero = loaded.Count(l => l.UnitPrice == 0 && CashLineRules.AmountDiffers(l.Qty, l.UnitPrice, l.Amount));
        report.Notes.Add($"จำนวนเงิน ≠ จำนวน × ราคา {differs.ToString(CultureInfo.InvariantCulture)} แถว (ราคา 0 อยู่ {priceZero.ToString(CultureInfo.InvariantCulture)} แถว) — เก็บตามต้นทาง ไม่คำนวณใหม่");
    }
}
