using System.Data.Common;
using System.Globalization;
using Lockte.Hr.Data;
using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Lockte.Hr;

/// <summary>
/// The writes of งวดค่าแรง: ตั้งงวด (SCR-40, hand entry with row history), the steps of เมนู 6 in order (#22), and
/// คำนวณเวลาทำงาน (BR-007 → BR-001 → BR-002) and คำนวณค่าแรง (BR-003..006), each recorded as one history event (#41). Calculating or
/// changing a period takes a lock on that period, so two users never calculate it at the same time (#41).
/// </summary>
public sealed class PayPeriodRegistry(HrDbContext db, AuditScope audit, ICurrentUser user, TimeProvider clock)
{
    public const string PayPeriodTable = "งวดค่าแรง";
    public const string CalculateTimeAction = "คำนวณเวลาทำงาน";
    public const string CalculatePayAction = "คำนวณค่าแรง";
    public const string BusyMessage = "มีผู้ใช้อื่นกำลังคำนวณหรือแก้งวดค่าแรงนี้อยู่ กรุณารอสักครู่แล้วลองใหม่";
    public const string MigratedMessage =
        "งวดนี้ย้ายมาจากระบบเดิมและปิดแล้ว คำนวณเวลาทำงาน คำนวณค่าแรง หรือแก้วันที่ไม่ได้ เพราะข้อมูลเวลาทำงาน การลา และอัตราค่าแรงของงวดเก่าย้ายมาไม่ครบ " +
        "ถ้าคำนวณใหม่ ค่าแรงเดิมจะถูกลบและได้ยอดผิด — พิมพ์รายงานของงวดนี้ได้ตามปกติ";

    /// <summary>The SQL Server application lock of one period (<c>sp_getapplock</c>); public for tests.</summary>
    public static string CalculationLockName(int periodId) => $"hr.PayPeriod.{periodId}";

    /// <summary>
    /// ตั้งงวด: a new period, or new dates for one. A period that changes its dates goes back to
    /// <see cref="PayPeriodStep.Set"/> and loses its time totals (they were for the old dates); a new วันที่จ่าย alone
    /// keeps them. A migrated period (#51) keeps its dates.
    /// </summary>
    public async Task<ServiceResult> SaveAsync(PayPeriodInput input)
    {
        if (input.From is not { } from || input.To is not { } to || input.PayDate is not { } payDate)
            return ServiceResult.Fail("กรุณากรอกจากวันที่ ถึงวันที่ และวันที่จ่าย");
        if (from > to) return ServiceResult.Fail("จากวันที่ต้องไม่หลังถึงวันที่");
        if (input.PartDays is { } partDays && (partDays < 1 || partDays > to.DayNumber - from.DayNumber + 1))
            return ServiceResult.Fail("จำนวนวันที่จ่ายของช่วงต้องอยู่ระหว่าง 1 ถึงจำนวนวันของช่วง");
        if (await db.PayPeriods.AnyAsync(p => p.FromDate == from && p.Id != input.Id))
            return ServiceResult.Fail($"มีงวดค่าแรงที่เริ่มวันที่ {ThaiDate.Format(from)} อยู่แล้ว");

        if (input.Id is not { } id)
        {
            var added = db.PayPeriods.Add(new PayPeriod { FromDate = from, ToDate = to, PayDate = payDate, PartDays = input.PartDays, CompletedStep = PayPeriodStep.Set }).Entity;
            await db.SaveChangesAsync();
            return ServiceResult.Ok(added.Id);
        }

        await using var transaction = await db.Database.BeginTransactionAsync();
        if (!await TryLockAsync(id)) return ServiceResult.Fail(BusyMessage);
        var period = await db.PayPeriods.FirstOrDefaultAsync(p => p.Id == id);
        if (period is null) return ServiceResult.Fail("ไม่พบงวดค่าแรงนี้ อาจถูกลบไปแล้ว");
        period.ExpectRowVersion(input.RowVersion
            ?? throw new ArgumentException("Editing needs the rowversion the screen loaded.", nameof(input)));
        if (period.Migrated && (period.FromDate != from || period.ToDate != to || period.PayDate != payDate || period.PartDays != input.PartDays))
            return ServiceResult.Fail(MigratedMessage);
        if (period.FromDate != from || period.ToDate != to)
        {
            await db.PayPeriodTimeTotals.Where(t => t.PayPeriodId == id).ExecuteDeleteAsync();
            await ForgetPayAsync(period);
            period.CompletedStep = PayPeriodStep.Set;
            period.TimeCalculatedAt = null;
            period.TimeCalculatedBy = null;
        }
        else if (period.PartDays != input.PartDays && period.CompletedStep >= PayPeriodStep.PayCalculated)
        {
            // The days a รายเดือน employee is paid change the ค่าแรง, not the time: calculate the pay again.
            await ForgetPayAsync(period);
            period.CompletedStep = PayPeriodStep.TimeCalculated;
        }
        period.FromDate = from;
        period.ToDate = to;
        period.PayDate = payDate;
        period.PartDays = input.PartDays;
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return ServiceResult.Ok(id);
    }

    /// <summary>
    /// พิมพ์เวลาทำงาน (มาโคร คำนวณเวลาทำงาน): BR-007 ลบรายการซ้ำ in the period, then BR-001/BR-002 totals per
    /// employee, which replace the period's previous ones (Q-22). <see cref="ServiceResult.Id"/> = employees calculated.
    /// </summary>
    public async Task<ServiceResult> CalculateTimeAsync(int periodId)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        if (!await TryLockAsync(periodId)) return ServiceResult.Fail(BusyMessage);
        var period = await db.PayPeriods.FirstOrDefaultAsync(p => p.Id == periodId);
        if (period is null) return ServiceResult.Fail("ไม่พบงวดค่าแรงนี้ อาจถูกลบไปแล้ว");
        if (CheckStep(period, PayPeriodStep.TimeCalculated) is { } blocked) return blocked;
        var (from, to) = (period.FromDate, period.ToDate);

        using (audit.SuppressRowChanges())
        {
            var repeats = await RemoveRepeatsAsync(from, to);
            var totals = PeriodTime.Total(new PayPeriodDates(from, to, period.PayDate),
                await db.Employees.AsNoTracking().Where(e => !e.IsMigrationStub)
                    .Select(e => new PeriodTime.Person(e.Id, e.Code, e.Name, e.StartDate, e.ResignDate, e.EmployeeType)).ToListAsync(),
                await db.WorkTimes.AsNoTracking().Where(w => w.Date >= from && w.Date <= to)
                    .Select(w => new PeriodTime.Work(w.EmployeeId, w.Type, w.WorkIn, w.WorkOut, w.RestMinutes)).ToListAsync(),
                await db.Leaves.AsNoTracking().Where(l => l.FromDate >= from && l.ToDate <= to)
                    .Select(l => new PeriodTime.LeaveDays(l.EmployeeId, l.FromDate, l.ToDate, l.Days, l.Paid)).ToListAsync(),
                await db.WeeklyHolidays.AsNoTracking().Where(h => h.Date >= from && h.Date <= to).Select(h => h.Date).ToListAsync(),
                await db.PublicHolidays.AsNoTracking().Where(h => h.Date >= from && h.Date <= to).Select(h => h.Date).ToListAsync());

            await db.PayPeriodTimeTotals.Where(t => t.PayPeriodId == periodId).ExecuteDeleteAsync();
            await ForgetPayAsync(period); // calculated from the old totals
            db.PayPeriodTimeTotals.AddRange(totals.Select(t => new PayPeriodTimeTotal
            {
                PayPeriod = period, EmployeeId = t.EmployeeId, Normal = t.Normal, PaidLeave = t.PaidLeave, UnpaidLeave = t.UnpaidLeave,
                Overtime = t.Overtime, Sunday = t.Sunday, SundayOvertime = t.SundayOvertime, Holiday = t.Holiday,
                HolidayOvertime = t.HolidayOvertime, PublicHolidays = t.PublicHolidays, WeeklyHolidays = t.WeeklyHolidays,
                WeeklyHolidaysToResign = t.WeeklyHolidaysToResign,
            }));
            period.CompletedStep = PayPeriodStep.TimeCalculated;
            period.TimeCalculatedAt = clock.GetUtcNow();
            period.TimeCalculatedBy = user.UserName;
            db.AddHistoryEvent(CalculateTimeAction,
                new { ตั้งแต่ = Iso(from), ถึง = Iso(to), วันที่จ่าย = Iso(period.PayDate), ลบรายการซ้ำ = repeats },
                totals.Count, PayPeriodTable);
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return ServiceResult.Ok(totals.Count);
        }
    }

    /// <summary>
    /// พิมพ์ค่าแรง (SCR-41, มาโคร คำนวณค่าแรง): BR-003/004 on the period's BR-002 totals, for every employee BR-005
    /// calculated whose type a legacy formula has; the results replace the period's previous ones (BR-006, Q-22: only this
    /// period's — the legacy also deleted the history of later periods). <see cref="ServiceResult.Id"/> = employees paid.
    /// </summary>
    public async Task<ServiceResult> CalculatePayAsync(int periodId)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        if (!await TryLockAsync(periodId)) return ServiceResult.Fail(BusyMessage);
        var period = await db.PayPeriods.FirstOrDefaultAsync(p => p.Id == periodId);
        if (period is null) return ServiceResult.Fail("ไม่พบงวดค่าแรงนี้ อาจถูกลบไปแล้ว");
        if (CheckStep(period, PayPeriodStep.PayCalculated) is { } blocked) return blocked;
        var (from, to) = (period.FromDate, period.ToDate);

        using (audit.SuppressRowChanges())
        {
            var rows = await db.PayPeriodTimeTotals.AsNoTracking().Where(t => t.PayPeriodId == periodId)
                .Select(t => new
                {
                    Time = new EmployeeTimeTotal(t.EmployeeId, t.Employee.Code, t.Employee.Name, t.Normal, t.PaidLeave, t.UnpaidLeave,
                        t.Overtime, t.Sunday, t.SundayOvertime, t.Holiday, t.HolidayOvertime, t.PublicHolidays, t.WeeklyHolidays,
                        t.WeeklyHolidaysToResign),
                    Employee = new PayEmployee(t.Employee.EmployeeType, t.Employee.Salary, t.Employee.DailyWage, t.Employee.Evaluation1Score,
                        t.Employee.HotAllowance, t.Employee.ResignDate),
                    t.Employee.DepartmentId,
                })
                .ToListAsync();
            var facts = new PayPeriodFacts(new PayPeriodDates(from, to, period.PayDate),
                await db.WeeklyHolidays.CountAsync(h => h.Date >= from && h.Date <= to),
                await db.PublicHolidays.Where(h => h.Date >= from && h.Date <= to).OrderBy(h => h.Date).Select(h => (DateOnly?)h.Date)
                    .FirstOrDefaultAsync(), period.PartDays);
            var results = rows
                .Select(r => (r.Employee, r.DepartmentId, Pay: PayCalculation.Calculate(facts, r.Time, r.Employee)))
                .Where(r => r.Pay is not null)
                .Select(r => new PayResult
                {
                    PayPeriod = period, EmployeeId = r.Pay!.EmployeeId, DepartmentId = r.DepartmentId, EmployeeType = r.Employee.EmployeeType!.Value,
                    Rate = r.Pay.Rate, Normal = r.Pay.Normal, PaidLeave = r.Pay.PaidLeave, Overtime = r.Pay.Overtime,
                    HolidayHours = r.Pay.HolidayHours, PayNormal = r.Pay.PayNormal, PayOvertime = r.Pay.PayOvertime, Hot = r.Pay.Hot,
                    PublicHolidays = r.Pay.PublicHolidays, WeeklyHolidays = r.Pay.WeeklyHolidays,
                    OvertimePay = r.Pay.Parts.Overtime, SundayPay = r.Pay.Parts.Sunday, SundayOvertimePay = r.Pay.Parts.SundayOvertime,
                    HolidayPay = r.Pay.Parts.Holiday, HolidayOvertimePay = r.Pay.Parts.HolidayOvertime,
                    WelfareEmployee = Welfare.Contribution(r.Pay.Total), WelfareCompany = Welfare.Contribution(r.Pay.Total),
                })
                .ToList();

            await db.PayResults.Where(r => r.PayPeriodId == periodId).ExecuteDeleteAsync();
            db.PayResults.AddRange(results);
            period.CompletedStep = PayPeriodStep.PayCalculated;
            period.PayCalculatedAt = clock.GetUtcNow();
            period.PayCalculatedBy = user.UserName;
            db.AddHistoryEvent(CalculatePayAction,
                new
                {
                    ตั้งแต่ = Iso(from), ถึง = Iso(to), วันที่จ่าย = Iso(period.PayDate),
                    ค่าแรง = results.Sum(r => r.PayNormal), ค่าล่วงเวลา = results.Sum(r => r.PayOvertime), HOT = results.Sum(r => r.Hot),
                },
                results.Count, PayPeriodTable);
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return ServiceResult.Ok(results.Count);
        }
    }

    /// <summary>
    /// Marks a printing step of เมนู 6 done (พิมพ์สรุปค่าแรง, ส่งแบงค์, SLIP — their tickets call it). A step whose step
    /// before is not done is refused. A calculation step done again sends the period back to it.
    /// </summary>
    public async Task<ServiceResult> CompleteStepAsync(int periodId, PayPeriodStep step)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        if (!await TryLockAsync(periodId)) return ServiceResult.Fail(BusyMessage);
        var period = await db.PayPeriods.FirstOrDefaultAsync(p => p.Id == periodId);
        if (period is null) return ServiceResult.Fail("ไม่พบงวดค่าแรงนี้ อาจถูกลบไปแล้ว");
        if (CheckStep(period, step) is { } blocked) return blocked;
        period.CompletedStep = PayPeriodSteps.Calculates(step) || step > period.CompletedStep ? step : period.CompletedStep;
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return ServiceResult.Ok(periodId);
    }

    /// <summary>
    /// #22 ล็อกไม่ให้ข้ามขั้น: the step before <paramref name="step"/> must be done. A migrated period (#51) is never
    /// calculated: the legacy time, leave and rates behind its ค่าแรง did not all move, so a new calculation would replace
    /// the right amounts with wrong ones. Its printing steps stay open.
    /// </summary>
    internal static ServiceResult? CheckStep(PayPeriod period, PayPeriodStep step)
        => period.Migrated && PayPeriodSteps.Calculates(step) ? ServiceResult.Fail(MigratedMessage)
            : PayPeriodSteps.Allowed(period.CompletedStep, step) ? null
            : ServiceResult.Fail($"ต้องทำขั้น \"{PayPeriodSteps.Names[step - 1]}\" ก่อน");

    /// <summary>
    /// The period's application lock for the open transaction, without waiting: false when someone else holds it.
    /// Released when the transaction ends.
    /// </summary>
    internal async Task<bool> TryLockAsync(int periodId)
    {
        var connection = db.Database.GetDbConnection();
        await using DbCommand command = connection.CreateCommand();
        command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction()
            ?? throw new InvalidOperationException("The pay period lock needs an open transaction.");
        command.CommandText = """
            DECLARE @result int;
            EXEC @result = sp_getapplock @Resource = @name, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 0;
            SELECT @result;
            """;
        var name = command.CreateParameter();
        name.ParameterName = "@name";
        name.Value = CalculationLockName(periodId);
        command.Parameters.Add(name);
        return Convert.ToInt32(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture) >= 0;
    }

    /// <summary>
    /// BR-007 (คิวรี ลบข้อมูลซ้ำ1–10): one row per key, the first entered kept (the legacy kept any one). The legacy
    /// cleaned its "current" tables; with one table for every year (#41) the period's dates stand in for them:
    /// เวลาทำงาน by DATE & W_IN & TYPE & ID, การลา starting in the period by FROM_D & TO_D & FROM_T & ID.
    /// Returns the rows deleted.
    /// </summary>
    private async Task<int> RemoveRepeatsAsync(DateOnly from, DateOnly to)
    {
        var work = await db.WorkTimes.AsNoTracking().Where(w => w.Date >= from && w.Date <= to)
            .Select(w => new { w.Id, w.Date, w.WorkIn, w.Type, w.EmployeeId }).ToListAsync();
        var workRepeats = work.GroupBy(w => (w.Date, w.WorkIn, w.Type, w.EmployeeId))
            .SelectMany(g => g.OrderBy(w => w.Id).Skip(1)).Select(w => w.Id).ToList();
        var leaves = await db.Leaves.AsNoTracking().Where(l => l.FromDate >= from && l.FromDate <= to)
            .Select(l => new { l.Id, l.FromDate, l.ToDate, l.FromTime, l.EmployeeId }).ToListAsync();
        var leaveRepeats = leaves.GroupBy(l => (l.FromDate, l.ToDate, l.FromTime, l.EmployeeId))
            .SelectMany(g => g.OrderBy(l => l.Id).Skip(1)).Select(l => l.Id).ToList();

        var deleted = 0;
        if (workRepeats.Count > 0) deleted += await db.WorkTimes.Where(w => workRepeats.Contains(w.Id)).ExecuteDeleteAsync();
        if (leaveRepeats.Count > 0) deleted += await db.Leaves.Where(l => leaveRepeats.Contains(l.Id)).ExecuteDeleteAsync();
        return deleted;
    }

    /// <summary>The period's ค่าแรง no longer holds (its time or dates changed): deleted, to be calculated again.</summary>
    private async Task ForgetPayAsync(PayPeriod period)
    {
        await db.PayResults.Where(r => r.PayPeriodId == period.Id).ExecuteDeleteAsync();
        period.PayCalculatedAt = null;
        period.PayCalculatedBy = null;
    }

    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
