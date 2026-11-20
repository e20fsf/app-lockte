using Lockte.Hr.Data;
using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Hr;

/// <summary>
/// บันทึก/แก้/ลบ ใบเตือน (SCR-32, BR-023). A date and an employee are required (a blank row is never saved); ระดับ is
/// one of T-ชื่อใบเตือน or blank, as the legacy allowed. No rule on the order of the levels (Q-28). Edits and deletes
/// carry the rowversion the screen loaded; a newer save by someone else throws <see cref="ConcurrencyConflictException"/>.
/// </summary>
public sealed class WarningRegistry(HrDbContext db)
{
    public const string WarningTable = "ใบเตือน";
    /// <summary>T-ใบเตือน.REMARK size.</summary>
    public const int RemarkLength = 255;

    public async Task<ServiceResult> SaveAsync(WarningInput input)
    {
        var level = string.IsNullOrWhiteSpace(input.Level) ? null : input.Level.Trim();
        var remark = string.IsNullOrWhiteSpace(input.Remark) ? null : input.Remark.Trim();
        if (input.Date is not { } date) return ServiceResult.Fail("กรุณากรอกวันที่");
        if (input.EmployeeId is not { } employeeId) return ServiceResult.Fail("กรุณาเลือกพนักงาน");
        if (level is not null && !WarningLevels.Names.ContainsKey(level)) return ServiceResult.Fail("ไม่พบระดับใบเตือนนี้");
        if (remark?.Length > RemarkLength) return ServiceResult.Fail($"รายละเอียดยาวได้ไม่เกิน {RemarkLength} ตัว");
        var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == employeeId);
        if (employee is null) return ServiceResult.Fail("ไม่พบพนักงานที่เลือก");

        Warning? row;
        if (input.Id is { } id)
        {
            row = await LoadAsync(id, input.RowVersion);
            if (row is null) return ServiceResult.Fail("ไม่พบรายการนี้ อาจถูกลบไปแล้ว");
        }
        else
        {
            row = db.Warnings.Add(new Warning()).Entity;
        }
        // ID_AfterUpdate filled DEPART from the employee: only when the employee is chosen, so an edit keeps the old แผนก.
        if (row.Id == 0 || row.EmployeeId != employee.Id) row.DepartmentId = employee.DepartmentId;
        row.Date = date;
        row.Employee = employee;
        row.Level = level;
        row.Remark = remark;
        await db.SaveChangesAsync();
        return ServiceResult.Ok(row.Id);
    }

    /// <summary>Deleting a row that is already gone succeeds.</summary>
    public async Task<ServiceResult> DeleteAsync(int id, byte[] loadedRowVersion)
    {
        if (await LoadAsync(id, loadedRowVersion) is not { } row) return ServiceResult.Ok();
        db.Warnings.Remove(row);
        await db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    /// <summary>
    /// ย้ายข้อมูลใบเตือน / ย้ายข้อมูลกลับ (SCR-60, BR-040, UAT 10): the warnings dated from <paramref name="from"/> to <paramref name="to"/>
    /// are moved out of SCR-32 (<paramref name="moved"/> true) or back. Only the flag changes: RPT-30 and the evaluation count them as
    /// before. Each row is kept in the history. <see cref="ServiceResult.Id"/> = rows changed.
    /// </summary>
    public async Task<ServiceResult> MoveAsync(DateOnly from, DateOnly to, bool moved)
    {
        if (from > to) return ServiceResult.Fail(DateRangeRule.Message);
        var rows = await db.Warnings.Include(w => w.Employee)
            .Where(w => w.Date >= from && w.Date <= to && w.Moved != moved)
            .ToListAsync();
        foreach (var row in rows) row.Moved = moved;
        await db.SaveChangesAsync();
        return ServiceResult.Ok(rows.Count);
    }

    /// <summary>With the employee, whose code is the row's รหัสรายการ in the history.</summary>
    private async Task<Warning?> LoadAsync(int id, byte[]? loadedRowVersion)
    {
        var row = await db.Warnings.Include(w => w.Employee).FirstOrDefaultAsync(w => w.Id == id);
        row?.ExpectRowVersion(loadedRowVersion
            ?? throw new ArgumentException("Editing needs the rowversion the screen loaded.", nameof(loadedRowVersion)));
        return row;
    }
}
