using System.Globalization;
using Lockte.Hr.Data;
using Lockte.Org;
using Lockte.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Hr;

/// <summary>
/// ข้อมูลรายงานของเอกสารพิมพ์รายบุคคล (#65): one query per RPT returning what the page prints, computed as the legacy
/// queries did — "วันนี้" is the clock's local Now() with its time of day, as Access used it (BR-070 rounds Now − START).
/// </summary>
public sealed class HrDocumentDirectory(HrDbContext db, OrgDirectory org, TimeProvider clock)
{
    private DateTime Now => clock.GetLocalNow().DateTime;

    /// <summary>RPT-50 ใบลางาน (BR-065); null when there is no such employee.</summary>
    public async Task<LeaveFormData?> GetLeaveFormAsync(int employeeId)
    {
        if (await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == employeeId) is not { } e) return null;
        var now = Now;
        var today = DateOnly.FromDateTime(now);
        var leaves = await db.Leaves.AsNoTracking().Where(l => l.EmployeeId == employeeId && l.FromDate.Year == today.Year)
            .Select(l => new { l.Type, l.Days }).ToListAsync();
        var byType = leaves.GroupBy(l => l.Type).ToDictionary(g => g.Key, g => g.Sum(l => l.Days));
        var (verbal, written) = await CountWarningsAsync(employeeId);
        var place = await PlaceAsync(e);
        return new LeaveFormData(e.Code, e.Name, place.Department?.Name, place.ShiftCode, e.StartDate,
            OnProbation: e.StartDate is { } start && today.DayNumber - start.DayNumber < 90,
            VacationText(e.StartDate, today), PersonalLeaveText(e.StartDate, today), today.Year,
            leaves.Count == 0 ? null : leaves.Sum(l => l.Days), byType, verbal, written);
    }

    /// <summary>
    /// ใบเตือนด้วยวาจา (CODE 1) / เป็นหนังสือ (CODE 2) of the employee for RPT-50. ใบเตือน is built by #59; until it
    /// lands both are 0, as the legacy printed for someone without any.
    /// </summary>
    private static Task<(int Verbal, int Written)> CountWarningsAsync(int employeeId) => Task.FromResult((0, 0));

    /// <summary>
    /// คิวรี พิมพ์ใบลางาน14 Expr4: "ไม่มีสิทธิลาพักร้อน" in the first 364 days; then 6 วัน from the second calendar year
    /// on, else by the month of START (12 → 6.5 … 1 → 12).
    /// </summary>
    public static string VacationText(DateOnly? startDate, DateOnly today)
    {
        if (startDate is not { } start) return "";
        if (today.DayNumber - start.DayNumber + 1 < 365) return "ไม่มีสิทธิลาพักร้อน";
        if (today.Year - start.Year > 1) return "มีสิทธิลาพักร้อน 6 วัน";
        var days = 6 + 0.5 * (13 - start.Month);
        return $"มีสิทธิลาพักร้อน {days.ToString("0.#", CultureInfo.InvariantCulture)} วัน";
    }

    /// <summary>คิวรี พิมพ์ใบลางาน14 Expr5: อายุงาน ≥ 365 วัน → 6 วัน, otherwise 3 วัน (⚠ BR-065 vs BR-020).</summary>
    public static string PersonalLeaveText(DateOnly? startDate, DateOnly today)
        => startDate is { } start && today.DayNumber - start.DayNumber + 1 >= 365 ? "ลากิจได้ค่าแรง 6 วัน" : "ลากิจได้ค่าแรง 3 วัน";

    /// <summary>RPT-53 หนังสือรับรองการทำงาน / เงินเดือน (BR-070).</summary>
    public async Task<CertificateData?> GetCertificateAsync(int employeeId)
        => await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == employeeId) is { } e ? await CertificateAsync(e, Now) : null;

    /// <summary>RPT-54 ฟอร์มเกษียณ / ฟอร์มปลดออก (BR-070) with the scale and reason typed on the screen.</summary>
    public async Task<SeveranceData?> GetSeveranceAsync(int employeeId, SeveranceScale scale, string? reason)
    {
        if (await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == employeeId) is not { } e) return null;
        var now = Now;
        var days = scale.DaysFor(ServiceDays(e.StartDate, now) ?? 0);
        // Val(Format(วันชดเชย × SA_DAY, "#")) — RND-A to whole baht.
        var compensation = Math.Round((decimal)(days * e.DailyWage), 0, MidpointRounding.AwayFromZero);
        return new SeveranceData(await CertificateAsync(e, now), e.DailyWage, days, compensation,
            string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(), scale);
    }

    /// <summary>RPT-55 ใบลาออก with the date and reason as typed on SCR-33 (the legacy printed the form's values).</summary>
    public async Task<ResignationLetterData?> GetResignationLetterAsync(int employeeId, DateOnly? resignDate, string? reason)
    {
        if (await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == employeeId) is not { } e) return null;
        var place = await PlaceAsync(e);
        return new ResignationLetterData(e.Code, e.Name, place.Department?.Name, place.ShiftCode, e.StartDate, resignDate,
            string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            place.Names?.SupervisorName, place.Names?.ManagerName, place.Names?.FactoryManagerName);
    }

    /// <summary>RPT-55 รายงานพนักงานที่ลาออก: everyone with a วันลาออก, by วันลาออก then รหัส (no date filter, as before).</summary>
    public async Task<IReadOnlyList<ResignedEmployee>> ListResignedAsync()
    {
        var rows = await db.Employees.AsNoTracking().Where(e => e.ResignDate != null)
            .Select(e => new { e.Code, e.Name, e.StartDate, ResignDate = e.ResignDate!.Value, e.Memo }).ToListAsync();
        return rows.OrderBy(r => r.ResignDate).ThenBy(r => r.Code, StringComparer.Ordinal)
            .Select(r => new ResignedEmployee(r.Code, r.Name, r.StartDate, r.ResignDate,
                r.StartDate is { } start ? (r.ResignDate.DayNumber - start.DayNumber) / 365.0 : null, r.Memo))
            .ToList();
    }

    private async Task<CertificateData> CertificateAsync(Employee e, DateTime now)
    {
        var place = await PlaceAsync(e);
        var factoryManager = place.Names?.FactoryManagerName
            ?? place.Department?.Shifts.Select(s => s.FactoryManagerName).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n));
        return new CertificateData(e.Name, LongText(e.BirthDate),
            e.BirthDate is { } born ? (int)Math.Floor((now - born.ToDateTime(TimeOnly.MinValue)).TotalDays / 365) : null,
            e.Position, place.Department?.Name, LongText(e.StartDate), ServiceText(e.StartDate, now),
            e.EmployeeType == EmployeeTypes.Monthly ? e.Salary : e.DailyWage * 26,
            factoryManager, LongText(DateOnly.FromDateTime(now)));
    }

    /// <summary>Expr1 = Round(Now − START) (RND-B): days of service, the time of day included.</summary>
    public static double? ServiceDays(DateOnly? startDate, DateTime now)
        => startDate is { } start ? Rounding.RndB((now - start.ToDateTime(TimeOnly.MinValue)).TotalDays, 0) : null;

    /// <summary>
    /// Expr10 รวมเป็นระยะเวลา: &lt; 30 วัน → "n วัน"; ปีเต็ม ≤ 0 → "m  เดือน"; เศษเดือน ≤ 0 → "y  ปี"; else "y  ปี  m  เดือน",
    /// with ปี/เดือน from DateDiff("m") (calendar months, the day ignored).
    /// </summary>
    public static string ServiceText(DateOnly? startDate, DateTime now)
    {
        if (startDate is not { } start) return "";
        var days = ServiceDays(start, now)!.Value;
        var months = (now.Year - start.Year) * 12 + now.Month - start.Month;
        var years = months / 12;
        var rest = months % 12;
        if (days < 30) return $"{days.ToString(CultureInfo.InvariantCulture)} วัน";
        if (years <= 0) return $"{rest}  เดือน";
        return rest <= 0 ? $"{years}  ปี" : $"{years}  ปี  {rest}  เดือน";
    }

    /// <summary>"วัน  เดือน  ชื่อเดือน  พ.ศ.  ปี+543" as the legacy queries joined it, e.g. "22  เดือน  กรกฎาคม  พ.ศ.  2562".</summary>
    public static string LongText(DateOnly? date)
        => date is { } d ? $"{d.Day}  เดือน  {d.ToString("MMMM", ThaiDate.Culture)}  พ.ศ.  {d.Year + 543}" : "";

    /// <summary>The employee's แผนก, กะ and the names printed for that แผนก in that กะ (JOIN แผนก ON DEPART AND SHIFT).</summary>
    private async Task<(DepartmentDto? Department, string? ShiftCode, DepartmentShiftDto? Names)> PlaceAsync(Employee e)
    {
        var department = e.DepartmentId is { } d ? await org.GetDepartmentAsync(d) : null;
        var shift = e.ShiftId is { } s ? (await org.GetShiftsAsync()).FirstOrDefault(x => x.Id == s)?.Code : null;
        return (department, shift, department?.Shifts.FirstOrDefault(x => x.ShiftId == e.ShiftId));
    }
}
