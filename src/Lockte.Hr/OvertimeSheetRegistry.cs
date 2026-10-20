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
            shifts.Select(s => s.OvertimeIn).OfType<TimeOnly>().Distinct().Order().ToList(),
            shifts.Select(s => s.OvertimeOut).OfType<TimeOnly>().Distinct().Order().ToList());
    }

    /// <summary>
    /// มาโคร ขออนุมัติทำ OT (BR-012 #4): drop the lines without an employee, add one เวลาทำงาน row per line (DATE, ID,
    /// W_IN, W_OUT, TYPE, REST_T) and return what RPT-16 prints. Rows are hand entry, so the history keeps each one.
    /// As the legacy (⚠ kept, BR-013) there is no check for repeats: the same employee twice, or printing the same sheet
    /// again, adds the rows again. A sheet without anyone saves nothing and still prints (D-6: "ไม่มีข้อมูล").
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

        string? departmentName = null;
        if (input.DepartmentId is { } departmentId)
        {
            if (await org.GetDepartmentAsync(departmentId) is not { } department) return Refused("ไม่พบแผนกที่เลือก");
            departmentName = department.Name;
        }

        var lines = input.Lines.Where(l => l.EmployeeId is not null)
            .Select(l => (EmployeeId: l.EmployeeId!.Value, Memo: string.IsNullOrWhiteSpace(l.Memo) ? null : l.Memo.Trim()))
            .ToList();
        if (lines.Any(l => l.Memo?.Length > MemoLength)) return Refused($"หมายเหตุยาวได้ไม่เกิน {MemoLength} ตัวอักษร");
        var ids = lines.Select(l => l.EmployeeId).Distinct().ToList();
        var employees = await db.Employees.Where(e => ids.Contains(e.Id)).ToDictionaryAsync(e => e.Id);
        if (employees.Count != ids.Count) return Refused("ไม่พบพนักงานที่เลือก");

        foreach (var line in lines)
        {
            db.WorkTimes.Add(new WorkTime
            {
                Date = date, Employee = employees[line.EmployeeId], Type = type,
                WorkIn = workIn, WorkOut = workOut, RestMinutes = input.RestMinutes,
            });
        }
        await db.SaveChangesAsync();

        var printed = lines
            .Select(l => new OvertimeSheetLine(employees[l.EmployeeId].Code, employees[l.EmployeeId].Name, l.Memo))
            .OrderBy(l => l.EmployeeCode, StringComparer.Ordinal) // stable: the same code keeps the keyed order
            .ToList();
        return new OvertimeSheetOutcome(ServiceResult.Ok(lines.Count), new OvertimeSheet(date, departmentName, workIn, workOut, type, printed));
    }

    private static OvertimeSheetOutcome Refused(string error) => new(ServiceResult.Fail(error), null);
}
