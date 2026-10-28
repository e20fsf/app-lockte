using System.Globalization;
using Lockte.Hr.Data;
using Lockte.Org;
using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Hr;

/// <summary>The formulas of BR-030 โบนัส (คิวรี พิมพ์ลางานทั้งแผนก*) and the rules of BR-035 จ่ายรวมกับค่าแรงงวด.</summary>
public static class Bonus
{
    /// <summary>The SR summed into วันลา: 1–15, paid or not (SR 5 included; the other macro, ปรับค่าแรง, skips it).</summary>
    public static IReadOnlySet<string> CountedTypes { get; } =
        new HashSet<string>(Enumerable.Range(1, 15).Select(sr => sr.ToString(CultureInfo.InvariantCulture)));

    /// <summary>อายุงาน = Round((TO − START − 1) / 365, 3) ปี (RND-B); null without วันเริ่มงาน.</summary>
    public static double? ServiceYears(DateOnly? start, DateOnly to)
        => start is { } s ? Rounding.RndB((to.DayNumber - s.DayNumber - 1) / 365.0, 3) : null;

    /// <summary>
    /// % โบนัส (Round 2, RND-B): วันลา ≥ 25 → 0 · อายุงาน &lt; 0.5 → 0 · &lt; 1 → อายุงาน × 100 − วันลา × 4 · วันลา ≤ 12 → 100 · else
    /// 100 − (วันลา − 12) × 4; negative → 0. No วันเริ่มงาน makes the อายุงาน tests Null (false), as the legacy IIf.
    /// </summary>
    public static double Percent(double? serviceYears, double leaveDays)
    {
        double percent;
        if (leaveDays >= 25) percent = 0;
        else if (serviceYears < 0.5) percent = 0;
        else if (serviceYears < 1) percent = serviceYears.Value * 100 - leaveDays * 4;
        else if (leaveDays <= 12) percent = 100;
        else percent = 100 - (leaveDays - 12) * 4;
        percent = Rounding.RndB(percent, 2);
        return percent < 0 ? 0 : percent;
    }

    /// <summary>โบนัส = RND-A(รายเดือน: SALARY × % / 100 × PAY_M; อื่น ๆ: SA_DAY × % / 100 × PAY_D); never negative.</summary>
    public static decimal Amount(int? employeeType, double salary, double dailyWage, double percent, decimal monthlyMultiplier, decimal dailyMultiplier)
    {
        var amount = employeeType == EmployeeTypes.Monthly
            ? salary * percent / 100 * (double)monthlyMultiplier
            : dailyWage * percent / 100 * (double)dailyMultiplier;
        return Math.Max(0, (decimal)Rounding.RndA(amount));
    }

    /// <summary>
    /// SLIP รวมโบนัส (SLIP22/33 รายเดือน, SLIP44/5 รายวัน HOT1 = N): a รายเดือน employee, or a รายวัน one without HOT who has an
    /// account (the ⚠ of BR-035: a รายวัน without ACC, HOT1 = Y and CLAS 3 are not given the bonus on the slip).
    /// </summary>
    public static bool IsOnSlip(int employeeType, bool hotAllowance, bool hasTtbAccount)
        => employeeType == EmployeeTypes.Monthly || (employeeType == EmployeeTypes.Daily && !hotAllowance && hasTtbAccount);

    /// <summary>
    /// BR-035 on the lists of one งวด: <see cref="BankTransfers.Ttb"/> = ค่าแรงงวด (PAY_W + PAY_OT + HOT) + โบนัส of those with ACC, per
    /// account, only above 0; <see cref="BankTransfers.Kbank"/> = โบนัส + OT งวด (PAY_OT) of those with ACC2, per account, only above 0.
    /// The same bonus is in both lists for an employee with both accounts: paying with both pays it twice (Q-37).
    /// </summary>
    public static IReadOnlyList<BankTransferRow> Payment(IEnumerable<BonusPayInput> inputs)
    {
        var rows = new List<BankTransferRow>();
        foreach (var p in inputs)
        {
            if (BankTransfers.Account(p.AccountTtb) is { } ttb) rows.Add(new(BankTransfers.Ttb, ttb, p.PayNormal + p.PayOvertime + p.Hot + p.Bonus, p.Code));
            if (BankTransfers.Account(p.AccountKbank) is { } kbank) rows.Add(new(BankTransfers.Kbank, kbank, p.PayOvertime + p.Bonus, p.Code));
        }
        return rows.GroupBy(r => (r.Bank, r.Account))
            .Select(g => new BankTransferRow(g.Key.Bank, g.Key.Account, g.Sum(r => r.Amount), g.First().Code))
            .Where(r => r.Amount > 0)
            .OrderBy(r => r.Bank, StringComparer.Ordinal).ThenBy(r => r.Account, StringComparer.Ordinal).ToList();
    }
}

/// <summary>
/// SCR-50 คำนวณโบนัส (BR-030): the bonus of every employee of the แผนก, written as the one latest run (the legacy replaced EMPLOYEEโบนัส each
/// time) and recorded as a history event.
/// </summary>
public sealed class BonusRegistry(HrDbContext db, AuditScope audit)
{
    public const string CalculateAction = "คำนวณโบนัส";
    public const string BonusTable = "การคำนวณโบนัส";

    /// <summary><see cref="ServiceResult.Id"/> = employees counted.</summary>
    public async Task<ServiceResult> CalculateAsync(BonusInput input)
    {
        if (input.From > input.To) return ServiceResult.Fail("ตั้งแต่วันที่ต้องไม่หลังถึงวันที่");
        if (input.MonthlyMultiplier < 0 || input.DailyMultiplier < 0) return ServiceResult.Fail("ตัวคูณต้องไม่ติดลบ");

        var people = await db.Employees.AsNoTracking()
            .Where(e => e.ResignDate == null && !e.Code.StartsWith("LT") && !e.IsMigrationStub)
            .Where(e => input.DepartmentId == null || e.DepartmentId == input.DepartmentId)
            .Select(e => new { e.Id, e.EmployeeType, e.StartDate, e.Salary, e.DailyWage })
            .ToListAsync();
        var ids = people.Select(p => p.Id).ToList();
        var leave = (await db.Leaves.AsNoTracking()
                .Where(l => ids.Contains(l.EmployeeId) && l.FromDate >= input.From && l.ToDate <= input.To)
                .Select(l => new { l.EmployeeId, l.Type, l.Days })
                .ToListAsync())
            .Where(l => Bonus.CountedTypes.Contains(l.Type))
            .GroupBy(l => l.EmployeeId).ToDictionary(g => g.Key, g => g.Sum(l => l.Days));

        var results = people.Select(p =>
        {
            var days = Rounding.RndB(leave.GetValueOrDefault(p.Id), 2);
            var service = Bonus.ServiceYears(p.StartDate, input.To);
            var percent = Bonus.Percent(service, days);
            return new BonusResult
            {
                EmployeeId = p.Id, LeaveDays = days, ServiceYears = service, Percent = percent,
                Amount = Bonus.Amount(p.EmployeeType, p.Salary, p.DailyWage, percent, input.MonthlyMultiplier, input.DailyMultiplier),
            };
        }).ToList();

        await using var transaction = await db.Database.BeginTransactionAsync();
        using (audit.SuppressRowChanges())
        {
            await db.BonusRuns.ExecuteDeleteAsync(); // the results go with it (cascade)
            db.BonusRuns.Add(new BonusRun
            {
                FromDate = input.From, ToDate = input.To, DepartmentId = input.DepartmentId,
                MonthlyMultiplier = input.MonthlyMultiplier, DailyMultiplier = input.DailyMultiplier, Results = results,
            });
            db.AddHistoryEvent(CalculateAction,
                new
                {
                    ตั้งแต่ = input.From.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), ถึง = input.To.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    แผนก = input.DepartmentId, ตัวคูณรายเดือน = input.MonthlyMultiplier, ตัวคูณรายวัน = input.DailyMultiplier, โบนัสรวม = results.Sum(r => r.Amount),
                },
                results.Count, BonusTable);
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        return ServiceResult.Ok(results.Count);
    }
}

/// <summary>RPT-35 and the payment lists of BR-035, read from the latest <see cref="BonusRegistry.CalculateAsync"/>.</summary>
public sealed class BonusDirectory(HrDbContext db, OrgDirectory org)
{
    /// <summary>The parameters of the last run, to pre-fill SCR-50 ("ค่าครั้งก่อน"); null before the first one.</summary>
    public async Task<BonusInput?> GetLastInputAsync()
        => await db.BonusRuns.AsNoTracking().OrderByDescending(r => r.Id)
            .Select(r => new BonusInput(r.FromDate, r.ToDate, r.DepartmentId, r.MonthlyMultiplier, r.DailyMultiplier)).FirstOrDefaultAsync();

    public async Task<BonusReport?> GetReportAsync()
    {
        var run = await db.BonusRuns.AsNoTracking().OrderByDescending(r => r.Id)
            .Select(r => new { r.Id, r.FromDate, r.ToDate, r.DepartmentId, r.MonthlyMultiplier, r.DailyMultiplier, r.UpdatedAt, r.UpdatedBy })
            .FirstOrDefaultAsync();
        if (run is null) return null;
        var rows = await db.BonusResults.AsNoTracking().Where(r => r.BonusRunId == run.Id)
            .Select(r => new { r.Employee.Code, r.Employee.Name, r.Employee.DepartmentId, r.Employee.StartDate, r.ServiceYears, r.LeaveDays, r.Percent, r.Amount })
            .ToListAsync();
        var departments = (await org.GetDepartmentsAsync()).ToDictionary(d => d.Id, d => d.Name);
        string? Department(int? id) => id is { } d ? departments.GetValueOrDefault(d) : null;
        return new BonusReport(new BonusInput(run.FromDate, run.ToDate, run.DepartmentId, run.MonthlyMultiplier, run.DailyMultiplier),
            Department(run.DepartmentId), run.UpdatedAt, run.UpdatedBy,
            rows.OrderBy(r => r.Code, StringComparer.Ordinal)
                .Select(r => new BonusLine(r.Code, r.Name, Department(r.DepartmentId), r.StartDate, r.ServiceYears, r.LeaveDays, r.Percent, r.Amount)).ToList());
    }

    /// <summary>
    /// BR-035 RPT-40/41: the lists of a งวด (its ค่าแรง calculated here) with the latest bonus added; null without a bonus run or before
    /// ค่าแรง is calculated. An employee with a bonus but no pay in the งวด is in the lists by the bonus alone.
    /// </summary>
    public async Task<BonusPayment?> GetPaymentAsync(PayPeriodDto period)
    {
        var run = await db.BonusRuns.AsNoTracking().OrderByDescending(r => r.Id).Select(r => r.Id).FirstOrDefaultAsync();
        if (run == 0 || period.CompletedStep < PayPeriodStep.PayCalculated || period.PayCalculatedAt is null) return null;
        var pay = await db.PayResults.AsNoTracking().Where(r => r.PayPeriodId == period.Id)
            .Select(r => new { r.EmployeeId, r.PayNormal, r.PayOvertime, r.Hot }).ToDictionaryAsync(r => r.EmployeeId);
        var bonus = await db.BonusResults.AsNoTracking().Where(r => r.BonusRunId == run).Select(r => new { r.EmployeeId, r.Amount }).ToDictionaryAsync(r => r.EmployeeId);
        var ids = pay.Keys.Union(bonus.Keys).ToList();
        var inputs = (await db.Employees.AsNoTracking().Where(e => ids.Contains(e.Id))
                .Select(e => new { e.Id, e.Code, e.BankAccountTtb, e.BankAccountKbank }).ToListAsync())
            .OrderBy(e => e.Code, StringComparer.Ordinal)
            .Select(e => new BonusPayInput(e.Code, e.BankAccountTtb, e.BankAccountKbank, pay.GetValueOrDefault(e.Id)?.PayNormal ?? 0,
                pay.GetValueOrDefault(e.Id)?.PayOvertime ?? 0, pay.GetValueOrDefault(e.Id)?.Hot ?? 0, bonus.GetValueOrDefault(e.Id)?.Amount ?? 0));
        var rows = Bonus.Payment(inputs);
        return new BonusPayment(period, (await GetLastInputAsync())!, rows, BankTransfers.Summary(rows));
    }
}
