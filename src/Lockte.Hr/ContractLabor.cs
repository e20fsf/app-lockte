using Lockte.Hr.Data;
using Lockte.Org;
using Lockte.Sales;
using Lockte.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Hr;

/// <summary>
/// ค่าแรงงานรับจ้าง (SCR-44, BR-009, มาโคร คำนวณเวลาทำงาน1 → RPT-58): the pay of the เวลาทำงาน whose MARK is one
/// ลูกค้างานรับจ้างผลิต, by the same formulas (BR-002..004), summed by แผนก. The customers come from ทะเบียนลูกค้า of
/// module ขายและส่งของ (<see cref="CustomerDirectory"/>, ADR 0002).
/// <para>
/// As the legacy macro, step by step (#41, ⚠ kept): W/O1/S/O2/H/O3 are only the rows of the customer; it appends no
/// การลา (คำนวณเวลาทำงาน11/12 are not in it, so L = L(NO) = 0) and no SUN_DAY (no คำนวณค่าแรง01); HOLI and SUN are
/// counted as in the usual run (คำนวณเวลาทำงาน18/19 read T-EMPLO, not the customer's rows). Only the employees with a
/// row of the customer in the dates are in the summary: the legacy also paid who never worked for the customer (BR-005
/// picked them by their HOLI/SUN), a defect not kept (decided 2026-10-20). แผนก is the employee's current one
/// (สรุปค่าแรง joins T-EMPLO).
/// </para>
/// <para>
/// Nothing is stored: the legacy wrote over the period's เวลาทำงาน and ค่าแรง tables (so the usual ค่าแรง had to be
/// calculated again after it); here the result is calculated for the print only and the งวดค่าแรง is not touched.
/// </para>
/// </summary>
public sealed class ContractLaborDirectory(HrDbContext db, OrgDirectory org, CustomerDirectory customers)
{
    /// <summary>The choices of ลูกค้า (combo CUS of ฟอร์ม ตั้งวันที่ตัดWEEK1, T-ชื่อลูกค้า), by รหัส.</summary>
    public Task<IReadOnlyList<CustomerDto>> ListCustomersAsync() => customers.GetAllAsync();

    /// <summary>
    /// RPT-58 for the dates (FROM_DA, TO_DA) and the customer's รหัส; null when no customer has that รหัส.
    /// </summary>
    public async Task<ContractLaborReport?> GetReportAsync(DateOnly from, DateOnly to, string customerCode)
    {
        if (await customers.FindByCodeAsync(customerCode) is not { } customer) return null;
        var code = customer.Code;
        var weekly = await db.WeeklyHolidays.AsNoTracking().Where(h => h.Date >= from && h.Date <= to).Select(h => h.Date).ToListAsync();
        var holidays = await db.PublicHolidays.AsNoTracking().Where(h => h.Date >= from && h.Date <= to).OrderBy(h => h.Date)
            .Select(h => h.Date).ToListAsync();
        var employees = await db.Employees.AsNoTracking().Where(e => !e.IsMigrationStub)
            .Select(e => new
            {
                Person = new PeriodTime.Person(e.Id, e.Code, e.Name, e.StartDate, e.ResignDate, e.EmployeeType),
                Pay = new PayEmployee(e.EmployeeType, e.Salary, e.DailyWage, e.Evaluation1Score, e.HotAllowance, e.ResignDate),
                e.DepartmentId,
            })
            .ToListAsync();
        var work = await db.WorkTimes.AsNoTracking().Where(w => w.Date >= from && w.Date <= to && w.Mark == code)
            .Select(w => new PeriodTime.Work(w.EmployeeId, w.Type, w.WorkIn, w.WorkOut, w.RestMinutes)).ToListAsync();

        // วันที่จ่าย (PAY_D) is read by none of BR-002..004; the legacy only copied it into ค่าแรง.
        var dates = new PayPeriodDates(from, to, to);
        var workers = work.Select(w => w.EmployeeId).ToHashSet();
        var totals = PeriodTime.Total(dates, employees.Where(e => workers.Contains(e.Person.Id)).Select(e => e.Person), work, [], weekly,
                holidays)
            .Select(t => t with { WeeklyHolidaysToResign = 0 });
        var facts = new PayPeriodFacts(dates, weekly.Count, holidays.Count > 0 ? holidays[0] : null);
        var byId = employees.ToDictionary(e => e.Person.Id);
        var departments = (await org.GetDepartmentsAsync()).ToDictionary(d => d.Id, d => d.Name);
        var lines = totals
            .Select(t => (byId[t.EmployeeId].DepartmentId, Pay: PayCalculation.Calculate(facts, t, byId[t.EmployeeId].Pay)))
            .Where(r => r.Pay is not null)
            .Select(r => new PayLine(r.Pay!, r.DepartmentId, r.DepartmentId is { } d ? departments.GetValueOrDefault(d) : null))
            .OrderBy(l => l.Pay.Code, StringComparer.Ordinal)
            .ToList();
        var thai = StringComparer.Create(ThaiDate.Culture, ignoreCase: false);
        var summary = lines
            .GroupBy(l => l.DepartmentName)
            .Select(g => new DepartmentPayTotal(g.Key, g.Count(), g.Sum(l => l.Pay.PayNormal), g.Sum(l => l.Pay.PayOvertime), g.Sum(l => l.Pay.Hot)))
            .OrderBy(l => l.DepartmentName is not null).ThenBy(l => l.DepartmentName, thai)
            .ToList();
        return new ContractLaborReport(from, to, customer.Code, customer.Name, weekly.Count, holidays.Count, summary, lines);
    }
}

/// <summary>
/// RPT-58 สรุปค่าแรงงานรับจ้างตามลูกค้า (รายงาน สรุปค่าแรง1 ← คิวรี สรุปค่าแรง4): the dates, the customer, the วันหยุด
/// counts of the header and one line per แผนก (Thai order, blank first) of the employees calculated
/// (<see cref="Employees"/>, by รหัส ↑).
/// </summary>
public sealed record ContractLaborReport(DateOnly From, DateOnly To, string CustomerCode, string CustomerName, int WeeklyHolidays,
    int PublicHolidays, IReadOnlyList<DepartmentPayTotal> Lines, IReadOnlyList<PayLine> Employees)
{
    /// <summary>รวม: TO − FROM + 1.</summary>
    public int TotalDays => To.DayNumber - From.DayNumber + 1;

    /// <summary>วันทำงาน: TO − FROM − SUNDAY − HOLIDAY + 1.</summary>
    public int WorkDays => TotalDays - WeeklyHolidays - PublicHolidays;

    /// <summary>คิดค่าแรง: TO − FROM − SUNDAY + 1.</summary>
    public int PaidDays => TotalDays - WeeklyHolidays;

    public DepartmentPayTotal Sum => new(null, Lines.Sum(l => l.Employees), Lines.Sum(l => l.PayNormal), Lines.Sum(l => l.PayOvertime),
        Lines.Sum(l => l.Hot));
}
