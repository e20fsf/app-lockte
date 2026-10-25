using System.Globalization;
using Lockte.Hr;

namespace Lockte.Migration;

/// <summary>
/// ย้ายข้อมูลผลค่าแรง (#51, ข้อตัดสิน #17), after <see cref="HistoryMigration"/> has loaded the open period: ค่าแรง (the
/// last period calculated) and สำเนาของ ค่าแรง (every period, BR-006) from <see cref="LegacyDatabase.PayFrom"/> on (also the
/// periods of ธ.ค. 2567 paid in 2568) into one table, one row per employee per period. ค่าแรง wins over its own copy in the history, because only it keeps SA
/// and L; a history row gets 0 for both (one DQ line per period). The first period of a งวด 2 ช่วง (BR-008) was kept
/// with the whole period's TO_DATE, so it overlaps the second: it is loaded ending the day before the next period. Adds
/// the row counts and, per period, the rows and ΣPAY_W / ΣPAY_OT / ΣHOT read and in the new table. Nothing is written to
/// the change history. A code that is not in the register is expected to have its stand-in already (<see cref="OrphanPayEmployeeMigration"/>).
/// </summary>
public sealed class PayHistoryMigration(HrPayHistoryImport import, HrStubEmployeeImport stubs)
{
    private const string Other = "NF-06";
    private const string BlankDate = "DQ-1";
    private const string Duplicate = "DQ-2";
    private const string Orphan = "DQ-6";
    private const string ResultTable = "hr.PayResults";
    private const string PeriodTable = "hr.PayPeriods";

    /// <summary>The legacy tables, the current one first.</summary>
    public const string CurrentTable = "ค่าแรง";
    public const string HistoryTable = "สำเนาของ ค่าแรง";

    public async Task RunAsync(LegacyDatabase legacy, MigrationReport report)
    {
        var employeeIds = await stubs.GetEmployeeIdsAsync(); // the stand-ins of the orphan codes too, which no list shows
        var (source, excluded, rejected) = (0, 0, 0);
        var read = new List<(DateOnly From, double PayNormal, double PayOvertime, double Hot)>(); // ยอดต้นทาง, every row with FROM_DATE
        var candidates = new Dictionary<(DateOnly From, string Code), Candidate>();
        void Reject(string table, string key, string rule, string problem)
        {
            rejected++;
            report.Add(table, key, rule, problem, MigrationReport.NotLoaded);
        }

        foreach (var table in new[] { CurrentTable, HistoryTable })
        {
            var rows = legacy[table];
            source += legacy.SourceRows(table);
            excluded += legacy.SourceRows(table) - rows.Count; // started and paid before 2568: stays in Access (#17)
            foreach (var row in rows)
            {
                if (Day(row.Date("FROM_DATE")) is not { } from)
                {
                    Reject(table, $"(FROM_DATE ว่าง) {row.Text("ID")}", BlankDate, "ไม่มีวันเริ่มงวด");
                    continue;
                }
                var pay = Pay.Of(row);
                read.Add((from, pay.PayNormal, pay.PayOvertime, pay.Hot));
                var code = EmployeeFormRules.NormalizeCode(row.Text("ID"));
                if (code is null)
                {
                    Reject(table, $"{Iso(from)} (ID ว่าง)", Other, "ไม่มีรหัสพนักงาน");
                    continue;
                }
                var key = $"{Iso(from)} {code}";
                if (!employeeIds.TryGetValue(code, out var employeeId))
                {
                    Reject(table, key, Orphan, "ไม่พบรหัสพนักงานนี้ในทะเบียนพนักงานที่ย้ายแล้ว (แถวกำพร้า)");
                    continue;
                }
                if (Day(row.Date("TO_DATE")) is not { } to || Day(row.Date("PAY_D")) is not { } payDate)
                {
                    Reject(table, key, Other, "ไม่มีวันสิ้นงวด (TO_DATE) หรือวันจ่าย (PAY_D)");
                    continue;
                }
                if (candidates.TryGetValue((from, code), out var first))
                {
                    if (first.Table != table && first.Pay.SameAmounts(pay))
                    {
                        excluded++; // the history's copy of ค่าแรง (BR-006): not counted twice in ยอดต้นทาง either
                        read.RemoveAt(read.Count - 1);
                    }
                    else Reject(table, key, Duplicate, first.Table == table
                        ? "พนักงานคนเดิมซ้ำในงวดเดียวกัน — โหลดแถวแรก"
                        : $"ยอดไม่ตรงกับแถวใน {first.Table} — โหลดแถวจาก {first.Table}");
                    continue;
                }
                candidates[(from, code)] = new Candidate(table, from, to, payDate, employeeId, pay);
            }
        }

        var periods = new List<PayPeriodImport>();
        var byFrom = candidates.Values.GroupBy(c => c.From).OrderBy(g => g.Key).ToList();
        for (var i = 0; i < byFrom.Count; i++)
        {
            var rows = byFrom[i].ToList();
            var (from, to, payDate) = (byFrom[i].Key, rows[0].To, rows[0].PayDate);
            var table = rows[0].Table;
            foreach (var other in rows.Where(r => (r.To, r.PayDate) != (to, payDate)).Select(r => r.Table).Distinct())
                report.Add(other, $"งวด {Iso(from)}", Other, "TO_DATE หรือ PAY_D ไม่เหมือนกันทุกแถวของงวด",
                    $"โหลดโดยใช้ {Iso(to)} จ่าย {Iso(payDate)} ของแถวแรก");
            if (i + 1 < byFrom.Count && to >= byFrom[i + 1].Key)
            {
                var end = byFrom[i + 1].Key.AddDays(-1);
                report.Add(table, $"งวด {Iso(from)}", Other,
                    $"ถึงวันที่ {Iso(to)} ทับงวดถัดไปที่เริ่ม {Iso(byFrom[i + 1].Key)} (ช่วงที่ 1 ของงวด 2 ช่วง BR-008)",
                    $"โหลดโดยแก้ถึงวันที่เป็น {Iso(end)}");
                to = end;
            }
            var fromHistory = rows.Count(r => r.Table == HistoryTable);
            if (fromHistory > 0)
                report.Add(HistoryTable, $"งวด {Iso(from)}", Other,
                    $"อัตรา (SA) และวันลาได้ค่าแรง (L) ไม่มีในสำเนาของ ค่าแรง — {fromHistory} แถว", "โหลดเป็น 0");
            periods.Add(new PayPeriodImport(from, to, payDate, rows.Select(r => r.Pay.ToImport(r.EmployeeId)).ToList()));
        }

        await import.AddAsync(periods);
        var loaded = periods.Sum(p => p.Results.Count);
        report.Tables.Add(new TableCount($"{CurrentTable} + {HistoryTable}", ResultTable, source, excluded, rejected, loaded,
            await import.CountPayResultsAsync()));
        var readPeriods = read.Select(r => r.From).Distinct().Count();
        report.Tables.Add(new TableCount($"{CurrentTable} + {HistoryTable} (งวด)", $"{PeriodTable} ที่มีผลค่าแรง", readPeriods, 0,
            readPeriods - periods.Count, periods.Count, await import.CountPeriodsWithPayAsync()));

        var inDatabase = (await import.GetPeriodTotalsAsync()).ToDictionary(t => t.From);
        foreach (var from in read.Select(r => r.From).Concat(inDatabase.Keys).Distinct().Order())
        {
            var sourceRows = read.Where(r => r.From == from).ToList();
            var expected = candidates.Values.Where(c => c.From == from).Select(c => c.Pay).ToList();
            var saved = inDatabase.GetValueOrDefault(from) ?? new PayPeriodTotal(from, 0, 0, 0, 0);
            void Total(string measure, double sourceValue, double expectedValue, double savedValue)
                => report.MonthlyTotals.Add(new MonthlyTotal(ResultTable, $"งวด {Iso(from)}", measure, sourceValue, expectedValue, savedValue));
            Total("แถว", sourceRows.Count, expected.Count, saved.Rows);
            Total("ค่าแรง (PAY_W)", sourceRows.Sum(r => r.PayNormal), expected.Sum(p => p.PayNormal), (double)saved.PayNormal);
            Total("ค่าล่วงเวลา (PAY_OT)", sourceRows.Sum(r => r.PayOvertime), expected.Sum(p => p.PayOvertime), (double)saved.PayOvertime);
            Total("HOT", sourceRows.Sum(r => r.Hot), expected.Sum(p => p.Hot), (double)saved.Hot);
        }
    }

    private static DateOnly? Day(DateTime? legacyDate) => legacyDate is { } d ? DateOnly.FromDateTime(d) : null;

    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private sealed record Candidate(string Table, DateOnly From, DateOnly To, DateOnly PayDate, int EmployeeId, Pay Pay);

    /// <summary>
    /// The amounts of one legacy row. ค่าแรง keeps W and OT as Double, the history as Long (rounded half to even when copied);
    /// both are loaded as they are. SA and L are only in ค่าแรง (0 for a history row).
    /// </summary>
    private sealed record Pay(double Rate, double Normal, double PaidLeave, double Overtime, double HolidayHours, double PayNormal,
        double PayOvertime, double Hot, double PublicHolidays, double WeeklyHolidays)
    {
        public static Pay Of(LegacyRow row) => new(N(row, "SA"), N(row, "W"), N(row, "L"), N(row, "OT"), N(row, "TT_OT"), N(row, "PAY_W"),
            N(row, "PAY_OT"), Math.Round(N(row, "HOT"), 2), N(row, "HOLIDAY"), N(row, "SUNDAY"));

        private static double N(LegacyRow row, string column) => row.Number(column) ?? 0;

        /// <summary>The fields both tables keep, as the history stores them.</summary>
        public bool SameAmounts(Pay copy) => (PayNormal, PayOvertime, Hot, HolidayHours) == (copy.PayNormal, copy.PayOvertime, copy.Hot, copy.HolidayHours)
            && Math.Abs(Normal - copy.Normal) <= 0.5 && Math.Abs(Overtime - copy.Overtime) <= 0.5;

        public PayResultImport ToImport(int employeeId) => new(employeeId, Rate, Normal, PaidLeave, Overtime, Whole(HolidayHours),
            (decimal)PayNormal, (decimal)PayOvertime, (decimal)Hot, Whole(PublicHolidays), Whole(WeeklyHolidays));

        private static int Whole(double value) => (int)Math.Round(value, MidpointRounding.ToEven);
    }
}
