using Lockte.Hr.Data;
using Lockte.Org;
using Lockte.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Hr;

/// <summary>
/// ใบขออนุมัติทำ OT (SCR-14, BR-012). The legacy staging table T-OT is not kept (#41: one-row/staging tables become
/// service parameters): the screen holds the sheet and <see cref="PrintAsync"/> takes it whole. Not kept either: BR-012
/// #1–2, which cleared every working employee's GRED_2 (เกรดประเมินครั้งที่ 2) to flag who is ไม่มาทำงาน — the flag is
/// read by nothing in the form; <see cref="GetChoicesAsync"/> marks those employees instead.
/// </summary>
public sealed class OvertimeSheetRegistry(HrDbContext db, OrgDirectory org)
{
    /// <summary>T-OT.MEMO size.</summary>
    public const int MemoLength = 255;

    /// <summary>
    /// เวลาเข้า / เวลาออก offered besides the OT times of the shifts (#356, UAT-20): the one place these are listed. The box still takes
    /// any other time typed.
    /// </summary>
    public static IReadOnlyList<TimeOnly> ExtraStartTimes { get; } = [new(15, 20), new(15, 0), new(16, 0), new(17, 0)];

    public static IReadOnlyList<TimeOnly> ExtraEndTimes { get; } = [new(18, 20), new(15, 0), new(16, 0), new(17, 0), new(18, 0), new(19, 0), new(20, 0)];

    /// <summary>The times of the shifts and the extras, each once, earliest first.</summary>
    public static IReadOnlyList<TimeOnly> WithExtras(IEnumerable<TimeOnly> shiftTimes, IEnumerable<TimeOnly> extras)
        => [.. shiftTimes.Concat(extras).Distinct().Order()];

    /// <summary>
    /// #411: the แผนก the master data calls "ทั่วไป" (everyone, no one แผนก) is offered and printed as "แผนกรวม" on this sheet; the name of the
    /// แผนก itself is not changed.
    /// </summary>
    public static string DepartmentLabel(string name) => name == "ทั่วไป" ? "แผนกรวม" : name;

    public async Task<OvertimeSheetChoices> GetChoicesAsync(DateOnly date)
    {
        var dayBefore = date.AddDays(-1);
        var absent = (await db.Absences.AsNoTracking()
                .Where(a => (a.Date == date && a.WorkIn < AbsenceDirectory.NightShiftAfter)
                            || (a.Date == dayBefore && a.WorkIn > AbsenceDirectory.NightShiftAfter))
                .Select(a => a.EmployeeId)
                .ToListAsync())
            .ToHashSet();
        var departments = (await org.GetDepartmentsAsync()).ToDictionary(d => d.Id, d => d.Name);
        var employees = (await db.Employees.AsNoTracking().Where(e => e.ResignDate == null)
                .Select(e => new { e.Id, e.Code, e.Name, e.DepartmentId, e.Duty })
                .ToListAsync())
            .OrderBy(e => e.Code, StringComparer.Ordinal)
            .Select(e => new OvertimeSheetEmployee(e.Id, e.Code, e.Name,
                e.DepartmentId is { } d ? departments.GetValueOrDefault(d) : null, e.Duty, absent.Contains(e.Id)))
            .ToList();
        var shifts = await org.GetShiftsAsync();
        return new OvertimeSheetChoices(employees,
            WithExtras(shifts.Select(s => s.OvertimeIn).OfType<TimeOnly>(), ExtraStartTimes),
            WithExtras(shifts.Select(s => s.OvertimeOut).OfType<TimeOnly>(), ExtraEndTimes));
    }

    /// <summary>
    /// มาโคร ขออนุมัติทำ OT (BR-012 #4): drop the lines without an employee, add one เวลาทำงาน row per line (DATE, ID,
    /// W_IN, W_OUT, TYPE, REST_T) and return what RPT-16 prints. Rows are hand entry, so the history keeps each one.
    /// แก้ครั้งที่ 12: each row keeps the sheet's แผนก and the line's หมายเหตุ (<see cref="OvertimeSheetEntry"/>), and the sheet returned is every
    /// line printed for the date and แผนก (<see cref="GetSheetLinesAsync"/>) — the legacy's T-OT kept the earlier ช่วงเวลา after หลายกะ. Only the
    /// lines given are saved: the screen sends only the new ones, so a line printed before is not saved twice (the legacy copied it again).
    /// The same employee twice in the lines given still adds two rows (⚠ kept, BR-013). Without new lines nothing is saved and the sheet so far
    /// prints again (D-6: "ไม่มีข้อมูล" when there is none).
    /// ⚠ OT is recorded when the sheet is printed, before the manager signs it (Q-25).
    /// </summary>
    public async Task<OvertimeSheetOutcome> PrintAsync(OvertimeSheetInput input)
    {
        var type = string.IsNullOrWhiteSpace(input.Type) ? null : input.Type.Trim().ToUpperInvariant();
        if (input.Date is not { } date) return Refused("กรุณากรอกวันที่");
        if (type is null) return Refused("กรุณาเลือกประเภท");
        if (!WorkTypes.Codes.Contains(type)) return Refused($"ประเภทต้องเป็น {string.Join(", ", WorkTypes.Codes)}");
        if (input.WorkIn is not { } workIn || input.WorkOut is not { } workOut) return Refused("กรุณากรอกเวลาเริ่มและเวลาเลิก");
        if (input.RestMinutes < 0) return Refused("นาทีพักต้องไม่ติดลบ");
        if (input.Lines.Any(l => l.RestMinutes < 0)) return Refused("นาทีพักต้องไม่ติดลบ");

        string? departmentName = null;
        if (input.DepartmentId is { } departmentId)
        {
            if (await org.GetDepartmentAsync(departmentId) is not { } department) return Refused("ไม่พบแผนกที่เลือก");
            departmentName = DepartmentLabel(department.Name);
        }

        // #411: a line has its own times, rest and type, or the sheet's
        var lines = new List<(int EmployeeId, string? Memo, TimeOnly WorkIn, TimeOnly WorkOut, int RestMinutes, string Type)>();
        foreach (var l in input.Lines.Where(l => l.EmployeeId is not null))
        {
            var lineType = string.IsNullOrWhiteSpace(l.Type) ? type : l.Type.Trim().ToUpperInvariant();
            if (!WorkTypes.Codes.Contains(lineType)) return Refused($"ประเภทต้องเป็น {string.Join(", ", WorkTypes.Codes)}");
            lines.Add((l.EmployeeId!.Value, string.IsNullOrWhiteSpace(l.Memo) ? null : l.Memo.Trim(), l.WorkIn ?? workIn, l.WorkOut ?? workOut,
                l.RestMinutes ?? input.RestMinutes, lineType));
        }
        if (lines.Any(l => l.Memo?.Length > MemoLength)) return Refused($"หมายเหตุยาวได้ไม่เกิน {MemoLength} ตัวอักษร");
        var ids = lines.Select(l => l.EmployeeId).Distinct().ToList();
        var employees = await db.Employees.Where(e => ids.Contains(e.Id)).ToDictionaryAsync(e => e.Id);
        if (employees.Count != ids.Count) return Refused("ไม่พบพนักงานที่เลือก");

        foreach (var line in lines)
        {
            var row = new WorkTime
            {
                Date = date, Employee = employees[line.EmployeeId], Type = line.Type,
                WorkIn = line.WorkIn, WorkOut = line.WorkOut, RestMinutes = line.RestMinutes,
            };
            db.WorkTimes.Add(row);
            db.OvertimeSheetEntries.Add(new OvertimeSheetEntry { WorkTime = row, DepartmentId = input.DepartmentId, Memo = line.Memo });
        }
        await db.SaveChangesAsync();

        // แก้ครั้งที่ 12: the sheet is everyone printed for this date and แผนก so far, not only these lines.
        var sheet = new OvertimeSheet(date, departmentName, type, await GetSheetLinesAsync(date, input.DepartmentId));
        return new OvertimeSheetOutcome(ServiceResult.Ok(lines.Count), sheet);
    }

    /// <summary>
    /// แก้ครั้งที่ 12: what was printed for <paramref name="date"/> and <paramref name="departmentId"/> (null = no แผนก) — every ช่วงเวลา, by
    /// code, then in the order printed; the screen shows them above the lines being keyed.
    /// </summary>
    public async Task<IReadOnlyList<OvertimeSheetLine>> GetSheetLinesAsync(DateOnly date, int? departmentId)
    {
        var rows = await db.OvertimeSheetEntries.AsNoTracking()
            .Where(s => s.WorkTime.Date == date && s.DepartmentId == departmentId)
            .Select(s => new
            {
                s.WorkTimeId, s.WorkTime.Employee.Code, s.WorkTime.Employee.Name, s.WorkTime.WorkIn, s.WorkTime.WorkOut, s.Memo, s.WorkTime.Type,
            })
            .ToListAsync();
        return rows
            .OrderBy(r => r.Code, StringComparer.Ordinal).ThenBy(r => r.WorkTimeId)
            .Select(r => new OvertimeSheetLine(r.Code, r.Name, r.WorkIn ?? default, r.WorkOut ?? default, r.Memo, r.Type))
            .ToList();
    }

    private static OvertimeSheetOutcome Refused(string error) => new(ServiceResult.Fail(error), null);
}
