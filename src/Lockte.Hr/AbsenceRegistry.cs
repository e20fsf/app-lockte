using Lockte.Hr.Data;
using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Hr;

/// <summary>
/// บันทึก/แก้/ลบ ไม่มาทำงาน (SCR-10, BR-011). A date and an employee are required — a blank row is never saved
/// (instead of the legacy delete of rows with ID1 blank on close) — and an employee is recorded once per day.
/// Edits and deletes carry the rowversion the screen loaded; a newer save by someone else throws
/// <see cref="ConcurrencyConflictException"/>. Record absences before สร้างเวลาปกติ (BR-010) for the day.
/// </summary>
public sealed class AbsenceRegistry(HrDbContext db)
{
    /// <summary>T-NO_WORK.W_IN: 08:00 on every legacy row (the day shift of BR-061).</summary>
    public static readonly TimeOnly DefaultWorkIn = new(8, 0);
    /// <summary>T-NO_WORK.REMARK size.</summary>
    public const int RemarkLength = 255;

    public async Task<ServiceResult> SaveAsync(AbsenceInput input)
    {
        var remark = string.IsNullOrWhiteSpace(input.Remark) ? null : input.Remark.Trim();
        if (input.Date is not { } date) return ServiceResult.Fail("กรุณากรอกวันที่");
        if (input.EmployeeId is not { } employeeId) return ServiceResult.Fail("กรุณาเลือกพนักงาน");
        if (remark?.Length > RemarkLength) return ServiceResult.Fail($"หมายเหตุยาวได้ไม่เกิน {RemarkLength} ตัว");
        var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == employeeId);
        if (employee is null) return ServiceResult.Fail("ไม่พบพนักงานที่เลือก");
        if (await db.Absences.AnyAsync(a => a.Date == date && a.EmployeeId == employeeId && a.Id != input.Id))
            return ServiceResult.Fail($"บันทึก {employee.Code} ไม่มาทำงานวันที่ {ThaiDate.Format(date)} ไว้แล้ว");

        Absence? row;
        if (input.Id is { } id)
        {
            row = await LoadAsync(id, input.RowVersion);
            if (row is null) return ServiceResult.Fail("ไม่พบรายการนี้ อาจถูกลบไปแล้ว");
        }
        else
        {
            row = db.Absences.Add(new Absence()).Entity;
        }
        row.Date = date;
        row.Employee = employee;
        row.WorkIn = input.WorkIn ?? DefaultWorkIn;
        row.Remark = remark;
        await db.SaveChangesAsync();
        return ServiceResult.Ok(row.Id);
    }

    /// <summary>Deleting a row that is already gone succeeds.</summary>
    public async Task<ServiceResult> DeleteAsync(int id, byte[] loadedRowVersion)
    {
        if (await LoadAsync(id, loadedRowVersion) is not { } row) return ServiceResult.Ok();
        db.Absences.Remove(row);
        await db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    /// <summary>With the employee, whose code is the row's รหัสรายการ in the history.</summary>
    private async Task<Absence?> LoadAsync(int id, byte[]? loadedRowVersion)
    {
        var row = await db.Absences.Include(a => a.Employee).FirstOrDefaultAsync(a => a.Id == id);
        row?.ExpectRowVersion(loadedRowVersion
            ?? throw new ArgumentException("Editing needs the rowversion the screen loaded.", nameof(loadedRowVersion)));
        return row;
    }
}
