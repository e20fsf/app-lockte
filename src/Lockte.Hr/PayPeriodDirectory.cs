using Lockte.Hr.Data;
using Lockte.Org;
using Lockte.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Hr;

/// <summary>
/// The read side of งวดค่าแรง: the periods, the default dates of the next one (#23 Q-04), the checks before calculating
/// (#22), the calculated time totals (BR-002) and ค่าแรง (BR-003/004), and the rows of RPT-01/02/03. Writes go through <see cref="PayPeriodRegistry"/>.
/// </summary>
public sealed class PayPeriodDirectory(HrDbContext db, OrgDirectory org, TimeProvider clock)
{
    /// <summary>Newest first.</summary>
    public async Task<IReadOnlyList<PayPeriodDto>> ListAsync()
        => await db.PayPeriods.AsNoTracking().OrderByDescending(p => p.FromDate).Select(ToDto).ToListAsync();

    public async Task<PayPeriodDto?> GetAsync(int id)
        => await db.PayPeriods.AsNoTracking().Where(p => p.Id == id).Select(ToDto).FirstOrDefaultAsync();

    /// <summary>
    /// วันที่ที่ระบบเติมให้ตอนตั้งงวดใหม่: the period after the newest one, or — with none yet — the newest usual
    /// period that has ended by today.
    /// </summary>
    public async Task<PayPeriodDates> DefaultDatesAsync()
    {
        var latest = await db.PayPeriods.AsNoTracking().OrderByDescending(p => p.FromDate)
            .Select(p => new { p.FromDate, p.ToDate, p.PayDate }).FirstOrDefaultAsync();
        return latest is null
            ? PayPeriodDates.LastEndedBy(DateOnly.FromDateTime(clock.GetLocalNow().DateTime))
            : PayPeriodDates.After(new PayPeriodDates(latest.FromDate, latest.ToDate, latest.PayDate));
    }

    /// <summary>
    /// ตรวจเวลาทำงานก่อนคำนวณ (#22): working days of the period (not วันหยุดประจำสัปดาห์/ประเพณี) that nobody has
    /// a เวลาปกติ (W) row for yet, and the มาสาย rows (LATE set), by date then code.
    /// </summary>
    public async Task<PayPeriodCheck> CheckAsync(int periodId)
    {
        var period = await db.PayPeriods.AsNoTracking().FirstAsync(p => p.Id == periodId);
        var (from, to) = (period.FromDate, period.ToDate);
        var daysOff = (await db.WeeklyHolidays.Where(h => h.Date >= from && h.Date <= to).Select(h => h.Date).ToListAsync())
            .Concat(await db.PublicHolidays.Where(h => h.Date >= from && h.Date <= to).Select(h => h.Date).ToListAsync())
            .ToHashSet();
        var withTime = (await db.WorkTimes.Where(w => w.Date >= from && w.Date <= to && w.Type == WorkTypes.Normal)
            .Select(w => w.Date).Distinct().ToListAsync()).ToHashSet();
        var missing = Enumerable.Range(0, to.DayNumber - from.DayNumber + 1).Select(from.AddDays)
            .Where(d => !daysOff.Contains(d) && !withTime.Contains(d)).ToList();
        var late = (await db.WorkTimes.AsNoTracking().Where(w => w.Date >= from && w.Date <= to && w.Late != null)
                .Select(w => new { w.Date, w.Employee.Code, w.Employee.Name, w.WorkIn, Late = w.Late!.Value }).ToListAsync())
            .OrderBy(w => w.Date).ThenBy(w => w.Code, StringComparer.Ordinal)
            .Select(w => new LateArrival(w.Date, w.Code, w.Name, w.WorkIn, w.Late)).ToList();
        return new PayPeriodCheck(missing, late);
    }

    /// <summary>The time totals of the last calculation (BR-002), by รหัส ↑; empty before the first one.</summary>
    public async Task<IReadOnlyList<EmployeeTimeTotal>> GetTimeTotalsAsync(int periodId)
        => (await db.PayPeriodTimeTotals.AsNoTracking().Where(t => t.PayPeriodId == periodId)
                .Select(t => new EmployeeTimeTotal(t.EmployeeId, t.Employee.Code, t.Employee.Name, t.Normal, t.PaidLeave, t.UnpaidLeave,
                    t.Overtime, t.Sunday, t.SundayOvertime, t.Holiday, t.HolidayOvertime, t.PublicHolidays, t.WeeklyHolidays,
                    t.WeeklyHolidaysToResign))
                .ToListAsync())
            .OrderBy(t => t.Code, StringComparer.Ordinal).ToList();

    /// <summary>RPT-01 เวลาทำงานรายคน; null until the period's time is calculated.</summary>
    public async Task<TimeReport?> GetTimeReportAsync(int periodId)
    {
        var period = await GetAsync(periodId);
        if (period is null || period.CompletedStep < PayPeriodStep.TimeCalculated) return null;
        var weekly = await db.WeeklyHolidays.CountAsync(h => h.Date >= period.From && h.Date <= period.To);
        var holidays = await db.PublicHolidays.Where(h => h.Date >= period.From && h.Date <= period.To)
            .Select(h => h.Date).OrderBy(d => d).ToListAsync();
        return new TimeReport(period, weekly, holidays.Count, holidays.Count > 0 ? holidays[0] : null, await GetTimeTotalsAsync(periodId));
    }

    /// <summary>ค่าแรงของงวดจากการคำนวณครั้งล่าสุด (BR-003/004), by รหัส ↑; empty before the first one.</summary>
    public async Task<IReadOnlyList<EmployeePay>> GetPayAsync(int periodId)
        => (await PayLinesAsync(periodId)).Select(l => l.Pay).ToList();

    /// <summary>The pay and accounts of every employee paid in the period, by รหัส ↑ — what BR-033's transfer lists are built from.</summary>
    public async Task<IReadOnlyList<BankPayInput>> GetBankInputsAsync(int periodId)
        => await db.PayResults.AsNoTracking().Where(r => r.PayPeriodId == periodId).OrderBy(r => r.Employee.Code)
            .Select(r => new BankPayInput(r.Employee.Code, r.Employee.BankAccountTtb, r.Employee.BankAccountKbank, r.PayNormal, r.PayOvertime, r.Hot,
                r.Employee.Name, r.Employee.EnglishTitle, r.Employee.EnglishName))
            .ToListAsync();

    /// <summary>
    /// The two ช่วง of a งวดคร่อมปีใหม่ (BR-008, #58) when they are: both set with จำนวนวัน, the second starting the day after the
    /// first ends, both paid in this system (ค่าแรง calculated here); null otherwise.
    /// </summary>
    private async Task<(PayPeriodDto Part1, PayPeriodDto Part2, PayPeriodDto Whole)?> TwoPartsAsync(int part1Id, int part2Id)
    {
        if (await GetAsync(part1Id) is not { } a || await GetAsync(part2Id) is not { } b) return null;
        if (a.PartDays is null || b.PartDays is null || b.From != a.To.AddDays(1)) return null;
        if (a.CompletedStep < PayPeriodStep.PayCalculated || b.CompletedStep < PayPeriodStep.PayCalculated) return null;
        if (a.PayCalculatedAt is null || b.PayCalculatedAt is null) return null;
        return (a, b, b with { From = a.From });
    }

    /// <summary>
    /// สรุปค่าแรงทั้ง 2 ช่วง (RPT-11, มาโคร สรุปค่าแรงช่วง1+2): per department, an employee counted once with the pay of both ช่วง; the header
    /// spans the two. Null unless <see cref="TwoPartsAsync"/>.
    /// </summary>
    public async Task<PaySummaryReport?> GetTwoPartSummaryAsync(int part1Id, int part2Id)
    {
        if (await TwoPartsAsync(part1Id, part2Id) is not var (_, _, whole)) return null;
        var first = await PayLinesAsync(part1Id);
        var second = await PayLinesAsync(part2Id);
        var weekly = await db.WeeklyHolidays.CountAsync(h => h.Date >= whole.From && h.Date <= whole.To);
        var holidays = await db.PublicHolidays.CountAsync(h => h.Date >= whole.From && h.Date <= whole.To);
        return new PaySummaryReport(whole, weekly, holidays, PayCombination.Departments(first, second));
    }

    /// <summary>
    /// ค่าแรงส่งแบงค์ทั้ง 2 ช่วง (SCR-47, BR-008 ข้อ 4): the transfer lists of BR-033 on the pay of both ช่วง added up per employee. Null
    /// unless <see cref="TwoPartsAsync"/>.
    /// </summary>
    public async Task<TwoPartBank?> GetTwoPartBankAsync(int part1Id, int part2Id)
    {
        if (await TwoPartsAsync(part1Id, part2Id) is not var (_, _, whole)) return null;
        var inputs = (await GetBankInputsAsync(part1Id)).Concat(await GetBankInputsAsync(part2Id))
            .GroupBy(i => i.Code)
            .Select(g => g.First() with { PayNormal = g.Sum(i => i.PayNormal), PayOvertime = g.Sum(i => i.PayOvertime), Hot = g.Sum(i => i.Hot) })
            .OrderBy(i => i.Code, StringComparer.Ordinal).ToList();
        var rows = BankTransfers.Build(inputs);
        return new TwoPartBank(whole, rows, BankTransfers.Summary(rows));
    }

    /// <summary>RPT-02 for one department (null = ทุกแผนก); null until the period's ค่าแรง is calculated.</summary>
    public async Task<PayReport?> GetPayReportAsync(int periodId, int? departmentId)
    {
        var period = await GetAsync(periodId);
        if (period is null || period.CompletedStep < PayPeriodStep.PayCalculated) return null;
        var lines = (await PayLinesAsync(periodId))
            .Where(l => (departmentId is null || l.DepartmentId == departmentId) && l.Pay.PayNormal + l.Pay.PayOvertime > 0).ToList();
        var name = departmentId is { } d ? (await org.GetDepartmentAsync(d))?.Name : null;
        return new PayReport(period, name, lines);
    }

    /// <summary>RPT-03; null until the period's ค่าแรง is calculated.</summary>
    public async Task<PaySummaryReport?> GetPaySummaryAsync(int periodId)
    {
        var period = await GetAsync(periodId);
        if (period is null || period.CompletedStep < PayPeriodStep.PayCalculated) return null;
        var thai = StringComparer.Create(ThaiDate.Culture, ignoreCase: false);
        var lines = (await PayLinesAsync(periodId))
            .GroupBy(l => l.DepartmentName)
            .Select(g => new DepartmentPayTotal(g.Key, g.Count(), g.Sum(l => l.Pay.PayNormal), g.Sum(l => l.Pay.PayOvertime), g.Sum(l => l.Pay.Hot)))
            .OrderBy(l => l.DepartmentName is not null).ThenBy(l => l.DepartmentName, thai)
            .ToList();
        return new PaySummaryReport(period, await db.WeeklyHolidays.CountAsync(h => h.Date >= period.From && h.Date <= period.To),
            await db.PublicHolidays.CountAsync(h => h.Date >= period.From && h.Date <= period.To), lines);
    }

    /// <summary>
    /// RPT-04 สลิปเงินเดือน (BR-034): one slip per employee paid, by รหัส ↑; null until ค่าแรง is calculated here — a
    /// period moved from the legacy history has no pay parts or เวลาทำงานรวม to print. <paramref name="withBonus"/> = RPT-42 สลิปรวมโบนัส
    /// (BR-035): only the employees of <see cref="Bonus.IsOnSlip"/> with a bonus in the latest คำนวณโบนัส, NET = ค่าแรง + โบนัส; null
    /// when there is no bonus run.
    /// </summary>
    public async Task<PaySlipBatch?> GetSlipsAsync(int periodId, bool withBonus = false)
    {
        var period = await GetAsync(periodId);
        if (period is null || period.CompletedStep < PayPeriodStep.PayCalculated || period.PayCalculatedAt is null) return null;
        var times = (await GetTimeTotalsAsync(periodId)).ToDictionary(t => t.EmployeeId);
        var rows = await db.PayResults.AsNoTracking().Where(r => r.PayPeriodId == periodId)
            .Select(r => new
            {
                Pay = new EmployeePay(r.EmployeeId, r.Employee.Code, r.Employee.Name, r.Rate, r.Normal, r.PaidLeave, r.Overtime, r.HolidayHours,
                    r.PayNormal, r.PayOvertime, r.Hot, r.PublicHolidays, r.WeeklyHolidays,
                    new OvertimeParts(r.OvertimePay, r.SundayPay, r.SundayOvertimePay, r.HolidayPay, r.HolidayOvertimePay)),
                r.EmployeeType,
                HasTtbAccount = r.Employee.BankAccountTtb != null,
                r.Employee.HotAllowance,
                r.WelfareEmployee,
                r.WelfareCompany,
            })
            .ToListAsync();
        Dictionary<int, decimal>? bonus = null;
        if (withBonus)
        {
            var run = await db.BonusRuns.AsNoTracking().OrderByDescending(b => b.Id).Select(b => b.Id).FirstOrDefaultAsync();
            if (run == 0) return null;
            bonus = await db.BonusResults.AsNoTracking().Where(b => b.BonusRunId == run).ToDictionaryAsync(b => b.EmployeeId, b => b.Amount);
        }
        // รวมเงินสะสมทั้งหมด: every period up to this one, never reset (assumption - #180, to confirm with HR).
        var accumulated = await db.PayResults.AsNoTracking().Where(r => r.PayPeriod.FromDate <= period.From)
            .GroupBy(r => r.EmployeeId).Select(g => new { EmployeeId = g.Key, Sum = g.Sum(r => r.WelfareEmployee) })
            .ToDictionaryAsync(g => g.EmployeeId, g => g.Sum);
        var dates = new PayPeriodDates(period.From, period.To, period.PayDate);
        var slips = rows.Where(r => times.ContainsKey(r.Pay.EmployeeId))
            .Where(r => bonus is null || (Bonus.IsOnSlip(r.EmployeeType, r.HotAllowance, r.HasTtbAccount) && bonus.GetValueOrDefault(r.Pay.EmployeeId) > 0))
            .Select(r => PaySlips.Build(dates, times[r.Pay.EmployeeId], r.Pay, r.EmployeeType, r.HasTtbAccount,
                r.WelfareEmployee, r.WelfareCompany, accumulated.GetValueOrDefault(r.Pay.EmployeeId))
                with { Bonus = bonus?.GetValueOrDefault(r.Pay.EmployeeId) ?? 0 })
            .OrderBy(s => s.Code, StringComparer.Ordinal).ToList();
        return new PaySlipBatch(period, slips, await ListSlipSignersAsync());
    }

    /// <summary>ผู้ลงนามบนสลิป, one per <see cref="SlipSignerRole"/> (a role never saved comes back blank).</summary>
    public async Task<IReadOnlyList<SlipSignerDto>> ListSlipSignersAsync()
    {
        var saved = await db.SlipSigners.AsNoTracking()
            .Select(s => new SlipSignerDto(s.Role, s.Name, s.Signature, s.SignatureContentType, s.RowVersion)).ToListAsync();
        return Enum.GetValues<SlipSignerRole>()
            .Select(role => saved.FirstOrDefault(s => s.Role == role) ?? new SlipSignerDto(role, null, null, null, null)).ToList();
    }

    private async Task<List<PayLine>> PayLinesAsync(int periodId)
    {
        var rows = await db.PayResults.AsNoTracking().Where(r => r.PayPeriodId == periodId)
            .Select(r => new
            {
                Pay = new EmployeePay(r.EmployeeId, r.Employee.Code, r.Employee.Name, r.Rate, r.Normal, r.PaidLeave, r.Overtime, r.HolidayHours,
                    r.PayNormal, r.PayOvertime, r.Hot, r.PublicHolidays, r.WeeklyHolidays,
                    new OvertimeParts(r.OvertimePay, r.SundayPay, r.SundayOvertimePay, r.HolidayPay, r.HolidayOvertimePay)),
                r.DepartmentId,
            })
            .ToListAsync();
        var departments = (await org.GetDepartmentsAsync()).ToDictionary(d => d.Id, d => d.Name);
        return rows.Select(r => new PayLine(r.Pay, r.DepartmentId, r.DepartmentId is { } d ? departments.GetValueOrDefault(d) : null))
            .OrderBy(l => l.Pay.Code, StringComparer.Ordinal).ToList();
    }

    private static readonly System.Linq.Expressions.Expression<Func<PayPeriod, PayPeriodDto>> ToDto = p =>
        new PayPeriodDto(p.Id, p.FromDate, p.ToDate, p.PayDate, p.CompletedStep, p.TimeCalculatedAt, p.TimeCalculatedBy, p.RowVersion,
            p.PayCalculatedAt, p.PayCalculatedBy, p.Migrated, p.PartDays);
}
