using Lockte.Hr.Data;
using Lockte.Org;
using Lockte.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Hr;

/// <summary>
/// The reports over many งวดค่าแรง (BR-062, BR-063): RPT-08 ค่าแรงสิ้นเดือน and its two summaries, RPT-09 สรุปค่าแรงแต่ละเดือน
/// and RPT-10 รวมค่าแรง. The legacy read its history สำเนาของ ค่าแรง; here every year is in ค่าแรงของงวด (#41), chosen by
/// the dates of the งวด. Only PAY_W and PAY_OT are read: a period moved from the legacy history has no Rate or PaidLeave
/// (#51). The legacy summary tables (สรุปบัญชีคำนวณค่าจ้าง, สรุปค่าแรงแต่ละเดือน2024) are not kept — each report is summed
/// from the dates asked.
/// </summary>
public sealed class PayHistoryReportDirectory(HrDbContext db, OrgDirectory org)
{
    /// <summary>
    /// RPT-08 (คิวรี พิมพ์ค่าแรงสิ้นเดือน4 → 5 → 6): every งวด with from ≤ ถึงวันที่ ≤ to, of the employees whose แผนก today
    /// is <paramref name="departmentId"/> (null = all) and who have no เกรดประเมินครั้งที่ 1 (GRED_1, ❓ Q-44), one line per
    /// employee by รหัส ↑.
    /// </summary>
    public async Task<MonthEndPayReport> GetMonthEndReportAsync(DateOnly from, DateOnly to, int? departmentId)
    {
        var lines = await MonthEndLinesAsync(from, to, departmentId);
        var name = departmentId is { } d ? (await org.GetDepartmentAsync(d))?.Name : null;
        return new MonthEndPayReport(from, to, name, lines);
    }

    /// <summary>
    /// สรุปบัญชีคำนวณค่าจ้าง / สรุปค่าแรงประจำเดือน (คิวรี พิมพ์ค่าแรงสิ้นเดือน9): the lines of <see cref="GetMonthEndReportAsync"/>
    /// by the employee's แผนก today. The legacy kept the last run of "พิมพ์" in a table; this sums the same dates again.
    /// </summary>
    public async Task<MonthEndPaySummary> GetMonthEndSummaryAsync(DateOnly from, DateOnly to, int? departmentId)
    {
        var thai = StringComparer.Create(ThaiDate.Culture, ignoreCase: false);
        var lines = (await MonthEndLinesAsync(from, to, departmentId))
            .GroupBy(l => l.DepartmentName)
            .Select(g => new MonthEndDepartmentTotal(g.Key, g.Count(), g.Sum(l => l.Wage), g.Sum(l => l.Total)))
            .OrderBy(l => l.DepartmentName is not null).ThenBy(l => l.DepartmentName, thai)
            .ToList();
        return new MonthEndPaySummary(from, to, lines);
    }

    /// <summary>
    /// RPT-09 (คิวรี สรุปค่าแรงแต่ละเดิอน1 → 2..25 → 26): every งวด with from ≤ วันที่จ่าย ≤ to, Σ(PAY_W + PAY_OT) by
    /// <see cref="PayMonth"/> and <see cref="CodeDepartments.Of"/>, by แผนก (Thai order as the legacy GROUP BY).
    /// </summary>
    public async Task<MonthlyPayReport> GetMonthlyReportAsync(DateOnly from, DateOnly to)
    {
        var rows = await db.PayResults.AsNoTracking()
            .Where(r => r.PayPeriod.PayDate >= from && r.PayPeriod.PayDate <= to)
            .Select(r => new { r.Employee.Code, r.PayPeriod.PayDate, Amount = r.PayNormal + r.PayOvertime })
            .ToListAsync();
        var thai = StringComparer.Create(ThaiDate.Culture, ignoreCase: false);
        var lines = rows.GroupBy(r => CodeDepartments.Of(r.Code))
            .OrderBy(g => g.Key, thai)
            .Select(g => new MonthlyPayLine(g.Key, Enumerable.Range(1, 12)
                .Select(m => g.Where(r => PayMonth(r.PayDate) == m).Select(r => r.Amount).ToList())
                .Select(month => month.Count == 0 ? (decimal?)null : month.Sum()).ToList()))
            .ToList();
        return new MonthlyPayReport(from, to, lines);
    }

    /// <summary>
    /// RPT-10 (คิวรี รวมค่าแรง1 → 2): every งวด with ตั้งแต่วันที่ ≥ from and ถึงวันที่ ≤ to, ΣPAY_W and ΣPAY_OT by the
    /// employee's แผนก today (not the one the งวด was paid under, BR-062).
    /// </summary>
    public async Task<PayTotalsReport> GetPayTotalsReportAsync(DateOnly from, DateOnly to)
    {
        var rows = await db.PayResults.AsNoTracking()
            .Where(r => r.PayPeriod.FromDate >= from && r.PayPeriod.ToDate <= to)
            .GroupBy(r => r.Employee.DepartmentId)
            .Select(g => new { DepartmentId = g.Key, PayNormal = g.Sum(r => r.PayNormal), PayOvertime = g.Sum(r => r.PayOvertime) })
            .ToListAsync();
        var departments = await DepartmentNamesAsync();
        var thai = StringComparer.Create(ThaiDate.Culture, ignoreCase: false);
        var lines = rows
            .Select(r => new DepartmentPaySum(r.DepartmentId is { } d ? departments.GetValueOrDefault(d) : null, r.PayNormal, r.PayOvertime))
            .OrderBy(l => l.DepartmentName is not null).ThenBy(l => l.DepartmentName, thai)
            .ToList();
        return new PayTotalsReport(from, to, lines);
    }

    /// <summary>
    /// เดือนที่ยอดเข้า (BR-062): the month of วันที่จ่าย, but a payment on the 1st counts in the month before (1 ม.ค. → ธ.ค.).
    /// </summary>
    public static int PayMonth(DateOnly payDate) => payDate.Day == 1 ? (payDate.Month == 1 ? 12 : payDate.Month - 1) : payDate.Month;

    private async Task<List<MonthEndPayLine>> MonthEndLinesAsync(DateOnly from, DateOnly to, int? departmentId)
    {
        var rows = await db.PayResults.AsNoTracking()
            .Where(r => r.PayPeriod.ToDate >= from && r.PayPeriod.ToDate <= to && r.Employee.Evaluation1Grade == null
                && (departmentId == null || r.Employee.DepartmentId == departmentId))
            .GroupBy(r => r.EmployeeId)
            .Select(g => new { EmployeeId = g.Key, PayNormal = g.Sum(r => r.PayNormal), PayOvertime = g.Sum(r => r.PayOvertime) })
            .ToListAsync();
        var ids = rows.Select(r => r.EmployeeId).ToList();
        var employees = await db.Employees.AsNoTracking().Where(e => ids.Contains(e.Id))
            .Select(e => new { e.Id, e.Code, e.Name, e.NationalId, e.DepartmentId, e.EmployeeType, e.Salary, e.DailyWage })
            .ToDictionaryAsync(e => e.Id);
        var departments = await DepartmentNamesAsync();
        return rows.Select(r =>
            {
                var e = employees[r.EmployeeId];
                // The legacy IIf on CLAS: Expr1 tests <> "1" and the others = "1", so an empty ประเภท gets SALARY as อัตรา
                // but ΣPAY_W as ค่าจ้าง and a จำนวนวัน — kept as it was.
                var monthly = e.EmployeeType == EmployeeTypes.Monthly;
                var rate = e.EmployeeType is { } t && t != EmployeeTypes.Monthly ? e.DailyWage : e.Salary;
                double? days = monthly || e.DailyWage == 0
                    ? null
                    // Access Round: half to even, here on the decimal quotient (as the legacy query on the migrated data, #56).
                    : (double)Math.Round(r.PayNormal / (decimal)e.DailyWage, 1, MidpointRounding.ToEven);
                return new MonthEndPayLine(e.Code, e.Name, e.NationalId, e.DepartmentId is { } d ? departments.GetValueOrDefault(d) : null,
                    rate, days, monthly ? (decimal)e.Salary : r.PayNormal, r.PayNormal, r.PayOvertime);
            })
            .OrderBy(l => l.Code, StringComparer.Ordinal).ToList();
    }

    private async Task<Dictionary<int, string>> DepartmentNamesAsync()
        => (await org.GetDepartmentsAsync()).ToDictionary(d => d.Id, d => d.Name);
}

/// <summary>
/// แผนกจากตัวอักษรแรกของรหัสพนักงาน (BR-062, คิวรี สรุปค่าแรงแต่ละเดือน2): RPT-09 groups by these names, not by the
/// employee's แผนก; any other letter is แผนกแม่พิมพ์. Access compared text ignoring case.
/// </summary>
public static class CodeDepartments
{
    public const string Other = "แผนกแม่พิมพ์";

    public static IReadOnlyDictionary<char, string> Names { get; } = new Dictionary<char, string>
    {
        ['A'] = "แผนกประกอบ",
        ['B'] = "แผนกอัดผ้าเบรก",
        ['D'] = "แผนกไดคาสท์",
        ['F'] = "แผนกจัดซื้อ",
        ['G'] = "แผนกฝนผ้าเบรก",
        ['K'] = "แผนกผ้าคลัทช์และดิสเบรก",
        ['M'] = "แผนกซ่อมบำรุง",
        ['O'] = "แผนกบุคคลและธุรการ",
        ['P'] = "แผนกบรรจุ",
        ['Q'] = "แผนกประกันคุณภาพ",
        ['S'] = "แผนกสโตร์",
        ['T'] = "แผนกตกแต่ง",
    };

    public static string Of(string code)
        => code.Length > 0 && Names.TryGetValue(char.ToUpperInvariant(code[0]), out var name) ? name : Other;
}
