using Lockte.Hr.Data;
using Lockte.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Hr;

/// <summary>
/// BR-032 ปรับค่าแรงประจำปี (SCR-52, มาโคร ปรับค่าแรง1 → คิวรี พิมพ์ลางานทั้งแผนก*, L*, M*, SUM*), with the legacy's ⚠ kept:
/// <list type="bullet">
/// <item>who: not resigned, in the แผนก (all when none), รหัส not LT*; รายเดือน (ประเภท 1) and รายวัน (ประเภท 2) apart.</item>
/// <item>รวมวันลา = Round(Σ SUM_D, 2) of the leaves with FROM_D ≥ from and TO_D ≤ to of SR 1–15 <b>except SR 5</b>
/// ลาอุบัติเหตุในงาน: the macro never runs คิวรี พิมพ์ลางานทั้งแผนก8 (SR 5), so that column stays at its default 0.</item>
/// <item>ใบเตือนวาจา = the CODE 1 ใบเตือน of the range whatever their แผนก; ใบเตือนหนังสือ = CODE 2 with DEPART = the แผนก
/// chosen, so none when it is ทุกแผนก (Q-31). คิวรี M1/M4 group by (Date, ID, CODE, DEPART): repeats on one day count once.</item>
/// </list>
/// The legacy also listed a resigned-not, LT* or other-แผนก employee who only had a leave, a late day or a CODE 1 ใบเตือน in
/// the range (they came into พิมพ์ลางานทั้งแผนก10 by those queries); here the list is only the employees above (decided
/// 2026-10-21).
/// </summary>
public sealed class PayAdjustmentDirectory(HrDbContext db)
{
    public async Task<PayAdjustmentReport> GetReportAsync(PayAdjustmentInput input)
    {
        var people = await db.Employees.AsNoTracking()
            .Where(e => e.ResignDate == null && !e.Code.StartsWith("LT"))
            .Where(e => input.DepartmentId == null || e.DepartmentId == input.DepartmentId)
            .Where(e => e.EmployeeType == EmployeeTypes.Monthly || e.EmployeeType == EmployeeTypes.Daily)
            .Select(e => new { e.Id, e.Code, e.Name, e.StartDate, e.EmployeeType, e.Salary, e.DailyWage })
            .ToListAsync();
        var ids = people.Select(p => p.Id).ToList();
        var leave = (await db.Leaves.AsNoTracking()
                .Where(l => ids.Contains(l.EmployeeId) && l.FromDate >= input.From && l.ToDate <= input.To)
                .Select(l => new { l.EmployeeId, l.Type, l.Days })
                .ToListAsync())
            .Where(l => PayAdjustment.CountedTypes.Contains(l.Type))
            .GroupBy(l => l.EmployeeId).ToDictionary(g => g.Key, g => g.Sum(l => l.Days));
        var warnings = await db.Warnings.AsNoTracking()
            .Where(w => w.Employee.ResignDate == null && w.Date >= input.From && w.Date <= input.To
                        && (w.Level == WarningLevels.Verbal || w.Level == WarningLevels.Written))
            .Select(w => new { w.EmployeeId, w.Date, w.Level, w.DepartmentId })
            .Distinct()
            .ToListAsync();
        var verbal = warnings.Where(w => w.Level == WarningLevels.Verbal)
            .GroupBy(w => w.EmployeeId).ToDictionary(g => g.Key, g => g.Count());
        var written = warnings.Where(w => w.Level == WarningLevels.Written && input.DepartmentId != null && w.DepartmentId == input.DepartmentId)
            .GroupBy(w => w.EmployeeId).ToDictionary(g => g.Key, g => g.Count());

        List<PayAdjustmentLine> Lines(int type) => people.Where(p => p.EmployeeType == type).OrderBy(p => p.Code, StringComparer.Ordinal).Select(p =>
        {
            var days = Rounding.RndB(leave.GetValueOrDefault(p.Id), 2);
            double? service = p.StartDate is { } start ? (input.To.DayNumber - start.DayNumber) / 365.0 : null;
            var rate = PayAdjustment.RateOf(service, input.Steps);
            if (type == EmployeeTypes.Monthly)
            {
                var increase = PayAdjustment.MonthlyIncrease(p.Salary, days, rate, input.LeaveCap);
                return new PayAdjustmentLine(p.Code, p.Name, p.StartDate, service, Rounding.RndB(p.Salary, 0), days, 0, 0, rate, increase,
                    Math.Floor(increase + p.Salary));
            }
            var (w1, w2) = (verbal.GetValueOrDefault(p.Id), written.GetValueOrDefault(p.Id));
            var add = PayAdjustment.DailyIncrease(p.DailyWage, days, rate, w1, w2, input.LeaveCap);
            return new PayAdjustmentLine(p.Code, p.Name, p.StartDate, service, Rounding.RndB(p.DailyWage, 0), days, w1, w2, rate, add,
                Rounding.RndB(add + p.DailyWage, 0));
        }).ToList();
        return new PayAdjustmentReport(input, Lines(EmployeeTypes.Monthly), Lines(EmployeeTypes.Daily));
    }
}

/// <summary>The formulas of BR-032 (คิวรี พิมพ์ลางานทั้งแผนกSUM3 / SUM31 รายเดือน, SUM4 / SUM41 รายวัน).</summary>
public static class PayAdjustment
{
    /// <summary>The SR summed into รวมวันลา: 1–15 but 5 (the macro skips คิวรี พิมพ์ลางานทั้งแผนก8); SR 16 has no column.</summary>
    public static IReadOnlySet<string> CountedTypes { get; } =
        new HashSet<string>(Enumerable.Range(1, 15).Where(sr => sr != 5).Select(sr => sr.ToString(System.Globalization.CultureInfo.InvariantCulture)));

    /// <summary>
    /// STEP of อายุงาน (TO − START) / 365, unrounded: &lt; 1 → STEP1, &lt; 2 → STEP2, &lt; 4 → STEP3, &lt; 7 → STEP4, else STEP5.
    /// No วันเริ่มงาน makes every IIf test Null (false), so STEP5 — as the legacy.
    /// </summary>
    public static double RateOf(double? serviceYears, IReadOnlyList<double> steps)
        => serviceYears switch
        {
            < 1 => steps[0],
            < 2 => steps[1],
            < 4 => steps[2],
            < 7 => steps[3],
            _ => steps[4],
        };

    /// <summary>รายเดือน ปรับเพิ่ม = Round(วันลา ≥ TTL ? 0 : SALARY × STEP / 100, 0) (RND-B); no ใบเตือน (Q-48).</summary>
    public static double MonthlyIncrease(double salary, double leaveDays, double rate, double leaveCap)
        => Rounding.RndB(leaveDays >= leaveCap ? 0 : salary * rate / 100, 0);

    /// <summary>รายวัน ปรับเพิ่ม = RND-A(max(0, วันลา ≥ TTL ? 0 : SA_DAY × (STEP − วาจา − 2 × หนังสือ) / 100)).</summary>
    public static double DailyIncrease(double dailyWage, double leaveDays, double rate, int verbalWarnings, int writtenWarnings, double leaveCap)
    {
        var raw = leaveDays >= leaveCap ? 0 : dailyWage * (rate - verbalWarnings - 2 * writtenWarnings) / 100;
        return Rounding.RndA(raw < 0 ? 0 : raw);
    }
}
