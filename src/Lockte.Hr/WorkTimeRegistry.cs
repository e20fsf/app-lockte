using System.Globalization;
using Lockte.Hr.Data;
using Lockte.Org;
using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Hr;

/// <summary>
/// The writes of เวลาทำงาน. สร้างเวลาปกติให้ทุกคน (SCR-11, BR-010), แผนกตกแต่ง2ทำงานยามาดะ (BR-009) and ลบเวลาทั้งวัน (BR-015) are batch jobs: one
/// history event, no row-by-row history (#41). บันทึก/แก้/ลบรายคน (SCR-12/13, BR-013, BR-015) is hand entry with
/// row-by-row history; edits and deletes of one row carry the rowversion the screen loaded, and a newer save by
/// someone else throws <see cref="ConcurrencyConflictException"/>.
/// </summary>
public sealed class WorkTimeRegistry(HrDbContext db, OrgDirectory org, AuditScope audit)
{
    public const string WorkTimeTable = "เวลาทำงาน";
    public const string GenerateAction = "สร้างเวลาปกติ";
    public const string DeleteRangeAction = "ลบเวลาทำงานทั้งวัน";
    public const string MarkYamadaAction = "แผนกตกแต่ง2ทำงานยามาดะ";
    /// <summary>The รหัสลูกค้า the button "แผนกตกแต่ง2ทำงานยามาดะ" writes (คิวรี ตกแต่ง2ทำงานยามาดะ).</summary>
    public const string YamadaMark = "Y";
    /// <summary>T-E_WORK.MARK size (รหัสลูกค้างานรับจ้าง).</summary>
    public const int MarkLength = 2;

    /// <summary>
    /// บันทึก/แก้ทีละแถว (BR-013). As the legacy (⚠ kept) there is no check for repeats nor against the holiday calendar.
    /// Unlike the legacy, a row needs a date, an employee (a blank row is never saved, instead of BR-051's delete on
    /// close), a type of T-ประเภทการทำงาน and both times, which BR-001 needs. MARK is kept upper-case.
    /// </summary>
    public async Task<ServiceResult> SaveAsync(WorkTimeInput input)
    {
        var type = string.IsNullOrWhiteSpace(input.Type) ? null : input.Type.Trim().ToUpperInvariant();
        var mark = string.IsNullOrWhiteSpace(input.Mark) ? null : input.Mark.Trim().ToUpperInvariant();
        if (input.Date is not { } date) return ServiceResult.Fail("กรุณากรอกวันที่");
        if (input.EmployeeId is not { } employeeId) return ServiceResult.Fail("กรุณาเลือกพนักงาน");
        if (type is null) return ServiceResult.Fail("กรุณาเลือกประเภท");
        if (!WorkTypes.Codes.Contains(type)) return ServiceResult.Fail($"ประเภทต้องเป็น {string.Join(", ", WorkTypes.Codes)}");
        if (input.WorkIn is not { } workIn || input.WorkOut is not { } workOut) return ServiceResult.Fail("กรุณากรอกเวลาเข้าและเวลาออก");
        if (input.RestMinutes < 0) return ServiceResult.Fail("นาทีพักต้องไม่ติดลบ");
        if (mark?.Length > MarkLength) return ServiceResult.Fail($"รหัสลูกค้ายาวได้ไม่เกิน {MarkLength} ตัว");
        var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == employeeId);
        if (employee is null) return ServiceResult.Fail("ไม่พบพนักงานที่เลือก");

        WorkTime? row;
        if (input.Id is { } id)
        {
            row = await LoadAsync(id, input.RowVersion);
            if (row is null) return ServiceResult.Fail("ไม่พบรายการนี้ อาจถูกลบไปแล้ว");
        }
        else
        {
            row = db.WorkTimes.Add(new WorkTime()).Entity;
        }
        row.Date = date;
        row.Employee = employee;
        row.Type = type;
        row.WorkIn = workIn;
        row.WorkOut = workOut;
        row.RestMinutes = input.RestMinutes;
        row.Late = input.Late;
        row.Mark = mark;
        await db.SaveChangesAsync();
        return ServiceResult.Ok(row.Id);
    }

    /// <summary>ลบทีละแถว (subform E_WORK1 of SCR-13). Deleting a row that is already gone succeeds.</summary>
    public async Task<ServiceResult> DeleteAsync(int id, byte[] loadedRowVersion)
    {
        if (await LoadAsync(id, loadedRowVersion) is not { } row) return ServiceResult.Ok();
        db.WorkTimes.Remove(row);
        await db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    /// <summary>
    /// ลบข้อมูลเฉพาะคน (BR-015, ฟอร์ม วันทำงาน2): every row of the employee on <paramref name="date"/> with
    /// <paramref name="type"/> W (ลบเวลาปกติทันที) or O1 (ลบ OT ทันที), repeats included. Hand work on one person, so
    /// the history keeps each deleted row. <see cref="ServiceResult.Id"/> = rows deleted. The legacy MARK = "Y" flag
    /// step is not kept: it also deleted rows of any date whose MARK was the customer code Y (BR-015 ⚠).
    /// </summary>
    public async Task<ServiceResult> DeleteOfEmployeeAsync(DateOnly date, int employeeId, string type)
    {
        if (CheckDeletableType(type) is { } error) return error;
        var rows = await db.WorkTimes.Include(w => w.Employee)
            .Where(w => w.Date == date && w.EmployeeId == employeeId && w.Type == type)
            .ToListAsync();
        db.WorkTimes.RemoveRange(rows);
        await db.SaveChangesAsync();
        return ServiceResult.Ok(rows.Count);
    }

    /// <summary>
    /// ลบข้อมูลทั้งวัน (BR-015, ฟอร์ม วันทำงาน3): every employee's rows from <paramref name="from"/> to
    /// <paramref name="to"/> (both included) with <paramref name="type"/> W or O1. A batch job: one history event.
    /// <see cref="ServiceResult.Id"/> = rows deleted.
    /// </summary>
    public async Task<ServiceResult> DeleteRangeAsync(DateOnly from, DateOnly to, string type)
    {
        if (CheckDeletableType(type) is { } error) return error;
        if (from > to) return ServiceResult.Fail("วันที่เริ่มต้องไม่หลังวันที่สิ้นสุด");
        using (audit.SuppressRowChanges())
        {
            var rows = await db.WorkTimes.Where(w => w.Date >= from && w.Date <= to && w.Type == type).ToListAsync();
            db.WorkTimes.RemoveRange(rows);
            db.AddHistoryEvent(DeleteRangeAction, new { ตั้งแต่ = Iso(from), ถึง = Iso(to), ประเภท = type }, rows.Count, WorkTimeTable);
            await db.SaveChangesAsync();
            return ServiceResult.Ok(rows.Count);
        }
    }

    /// <summary>
    /// มาโคร แมโคร2 for one day, as the legacy (#41, ⚠ kept): every employee with RESIGN blank and a shift gets a row
    /// TYPE W with the shift's W_IN, W_OUT and REST_T; rows of the day with TYPE blank become W; then every row of the
    /// day of an employee recorded ไม่มาทำงาน that day is deleted (also rows made before the absence was recorded).
    /// The legacy deletes such rows on every date of the open period, not only this day: kept to this day until the pay
    /// period exists (Q-51). No E_WORK_A backup copy (one table, #41). ⚠ It does not check whether the day was already made (a second press adds the rows again, BR-007 removes the
    /// repeats), nor the start date, nor วันอาทิตย์/วันหยุด (Q-24).
    /// </summary>
    public async Task<ServiceResult> GenerateNormalTimeAsync(DateOnly date)
    {
        var shifts = (await org.GetShiftsAsync()).ToDictionary(s => s.Id);
        var absent = (await db.Absences.Where(a => a.Date == date).Select(a => a.EmployeeId).ToListAsync()).ToHashSet();
        var working = await db.Employees.Where(e => e.ResignDate == null && e.ShiftId != null)
            .Select(e => new { e.Id, ShiftId = e.ShiftId!.Value })
            .ToListAsync();
        var added = working
            .Where(e => shifts.ContainsKey(e.ShiftId) && !absent.Contains(e.Id))
            .Select(e =>
            {
                var shift = shifts[e.ShiftId];
                return new WorkTime
                {
                    Date = date, EmployeeId = e.Id, Type = WorkTypes.Normal,
                    WorkIn = shift.WorkIn, WorkOut = shift.WorkOut, RestMinutes = shift.RestMinutes,
                };
            })
            .ToList();

        using (audit.SuppressRowChanges())
        {
            var untyped = await db.WorkTimes.Where(w => w.Date == date && w.Type == null).ToListAsync();
            foreach (var row in untyped) row.Type = WorkTypes.Normal;
            var removed = await db.WorkTimes.Where(w => w.Date == date && absent.Contains(w.EmployeeId)).ToListAsync();
            db.WorkTimes.RemoveRange(removed);
            db.WorkTimes.AddRange(added);
            db.AddHistoryEvent(GenerateAction,
                new { วันที่ = Iso(date), ไม่มาทำงาน = absent.Count, ลบเวลาคนไม่มา = removed.Count },
                added.Count, WorkTimeTable);
            await db.SaveChangesAsync();
        }
        return ServiceResult.Ok(added.Count);
    }

    /// <summary>
    /// ปุ่ม "แผนกตกแต่ง2ทำงานยามาดะ" (SCR-11, BR-009, คิวรี ตกแต่ง2ทำงานยามาดะ): MARK = Y on every row with MARK blank
    /// of an employee whose รหัส starts with U, so ค่าแรงงานรับจ้าง (SCR-44) counts them for customer Y. The legacy
    /// changed the whole E_WORK — the rows since the last ปิดงวดค่าแรง; with one table for every year (#41) this is the
    /// usual งวดค่าแรง that has <paramref name="date"/>. A batch job: one history event. <see cref="ServiceResult.Id"/> =
    /// rows marked.
    /// </summary>
    public async Task<ServiceResult> MarkYamadaAsync(DateOnly date)
    {
        var period = PayPeriodDates.Containing(date);
        using (audit.SuppressRowChanges())
        {
            var rows = await db.WorkTimes
                .Where(w => w.Date >= period.From && w.Date <= period.To && w.Mark == null && w.Employee.Code.StartsWith("U"))
                .ToListAsync();
            foreach (var row in rows) row.Mark = YamadaMark;
            db.AddHistoryEvent(MarkYamadaAction, new { ตั้งแต่ = Iso(period.From), ถึง = Iso(period.To), ลูกค้า = YamadaMark },
                rows.Count, WorkTimeTable);
            await db.SaveChangesAsync();
            return ServiceResult.Ok(rows.Count);
        }
    }

    /// <summary>BR-015 ⚠ kept: the buttons only delete W and O1; S, O2, H, O3 are deleted row by row.</summary>
    private static ServiceResult? CheckDeletableType(string type)
        => type is WorkTypes.Normal or WorkTypes.Overtime ? null : ServiceResult.Fail("ปุ่มลบรับเฉพาะประเภท W หรือ O1 (ประเภทอื่นลบทีละแถว)");

    /// <summary>With the employee, whose code is part of the row's รหัสรายการ in the history.</summary>
    private async Task<WorkTime?> LoadAsync(int id, byte[]? loadedRowVersion)
    {
        var row = await db.WorkTimes.Include(w => w.Employee).FirstOrDefaultAsync(w => w.Id == id);
        row?.ExpectRowVersion(loadedRowVersion
            ?? throw new ArgumentException("Editing needs the rowversion the screen loaded.", nameof(loadedRowVersion)));
        return row;
    }

    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
