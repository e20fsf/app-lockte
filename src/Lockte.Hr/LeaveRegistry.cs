using Lockte.Hr.Data;
using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Hr;

/// <summary>
/// The writes of การลา (SCR-30/31). Hand entry on one person: every change, also ลบตามช่วงวัน, is kept row by row in
/// the history. Edits and deletes of one row carry the rowversion the screen loaded; a newer save by someone else
/// throws <see cref="ConcurrencyConflictException"/>.
/// </summary>
public sealed class LeaveRegistry(HrDbContext db, TimeProvider clock)
{
    public const string LeaveTable = "การลา";
    /// <summary>T-LEAVE.REMARK size.</summary>
    public const int RemarkLength = 28;
    public static readonly TimeOnly DefaultFromTime = new(8, 0);
    public static readonly TimeOnly DefaultToTime = new(17, 0);

    /// <summary>
    /// บันทึก/แก้ทีละแถว. BR-020 runs at the legacy check point (#41): when ได้ค่าแรง is chosen — a new row with Y/N or a
    /// saved row whose Y/N changed — which is also when the legacy saved the row (Macro1 closes the form). A failed check
    /// saves nothing (the legacy cleared SUM_D and Macro1 deleted the row). ⚠ kept: an edit that leaves Y/N as it was
    /// is not checked again, even with more days. "ใช้ไปแล้ว" = the employee's other ได้ค่าแรง rows whose FROM_D is in
    /// the calendar year of this row's FROM_D (Q-27, instead of the legacy LEAVE table emptied once a year).
    /// BR-021: a ได้ค่าแรง row whose PAY is still 0 gets PAY = SUM_D × SALA_DAY (ไม่ปัด); ⚠ kept: a PAY once set stays.
    /// Unlike the legacy, a row needs dates in order, SUM_D above 0 and an SR of T-ประเภทการลางาน.
    /// </summary>
    public async Task<LeaveSaveResult> SaveAsync(LeaveInput input)
    {
        if (await ValidateAsync(input) is { } error) return new(error, null);
        var (employeeId, from, days, type) = (input.EmployeeId!.Value, input.FromDate!.Value, input.Days!.Value, input.Type!.Trim());
        var employee = await db.Employees.FirstAsync(e => e.Id == employeeId);

        Leave? row = null;
        if (input.Id is { } id)
        {
            row = await LoadAsync(id, input.RowVersion);
            if (row is null) return new(ServiceResult.Fail("ไม่พบรายการนี้ อาจถูกลบไปแล้ว"), null);
        }

        LeaveQuotaCheck? quota = null;
        if (input.Paid is { } paid && (row is null || row.Paid != paid))
        {
            var used = paid ? await UsedPaidDaysAsync(employeeId, from.Year, row?.Id) : new Dictionary<string, double>();
            quota = LeaveQuota.Check(type, paid, days, used, employee.Sex, LeaveQuota.HasVacation(employee.StartDate, Today));
            if (!quota.CanSave) return new(ServiceResult.Fail(quota.Message!), quota);
        }

        if (row is null)
        {
            row = db.Leaves.Add(new Leave { DailyWage = input.DailyWage ?? employee.DailyWage }).Entity;
        }
        else if (input.DailyWage is { } wage)
        {
            row.DailyWage = wage;
        }
        row.Employee = employee;
        row.RecordedOn = input.RecordedOn ?? (row.Id == 0 ? Today : row.RecordedOn);
        row.FromDate = from;
        row.ToDate = input.ToDate!.Value;
        row.FromTime = input.FromTime ?? DefaultFromTime;
        row.ToTime = input.ToTime ?? DefaultToTime;
        row.Days = days;
        row.Type = type;
        row.Paid = input.Paid;
        row.Remark = string.IsNullOrWhiteSpace(input.Remark) ? null : input.Remark.Trim();
        if (row.Paid == true && row.Pay == 0) row.Pay = row.Days * row.DailyWage;
        await db.SaveChangesAsync();
        return new(ServiceResult.Ok(row.Id), quota);
    }

    /// <summary>ลบทีละแถว. Deleting a row that is already gone succeeds.</summary>
    public async Task<ServiceResult> DeleteAsync(int id, byte[] loadedRowVersion)
    {
        if (await LoadAsync(id, loadedRowVersion) is not { } row) return ServiceResult.Ok();
        db.Leaves.Remove(row);
        await db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    /// <summary>
    /// ลบข้อมูลการลางาน (SCR-31, BR-022): the employee's rows whose FROM_D is from <paramref name="from"/> to
    /// <paramref name="to"/> (both included), wherever TO_D is. <see cref="ServiceResult.Id"/> = rows deleted.
    /// </summary>
    public async Task<ServiceResult> DeleteRangeAsync(int employeeId, DateOnly from, DateOnly to)
    {
        if (from > to) return ServiceResult.Fail("วันที่เริ่มต้องไม่หลังวันที่สิ้นสุด");
        var rows = await db.Leaves.Include(l => l.Employee)
            .Where(l => l.EmployeeId == employeeId && l.FromDate >= from && l.FromDate <= to)
            .ToListAsync();
        db.Leaves.RemoveRange(rows);
        await db.SaveChangesAsync();
        return ServiceResult.Ok(rows.Count);
    }

    private async Task<ServiceResult?> ValidateAsync(LeaveInput input)
    {
        if (input.EmployeeId is not { } employeeId) return ServiceResult.Fail("กรุณาเลือกพนักงาน");
        if (input.FromDate is not { } from || input.ToDate is not { } to) return ServiceResult.Fail("กรุณากรอกวันที่ลา");
        if (from > to) return ServiceResult.Fail("วันที่เริ่มต้องไม่หลังวันที่สิ้นสุด");
        if (input.Days is not { } days) return ServiceResult.Fail("กรุณากรอกจำนวนวัน");
        if (days <= 0) return ServiceResult.Fail("จำนวนวันต้องมากกว่า 0");
        if (string.IsNullOrWhiteSpace(input.Type)) return ServiceResult.Fail("กรุณาเลือกประเภทการลา");
        if (!LeaveTypes.Names.ContainsKey(input.Type.Trim())) return ServiceResult.Fail("ไม่พบประเภทการลานี้");
        if (input.Remark?.Trim().Length > RemarkLength) return ServiceResult.Fail($"เหตุผลยาวได้ไม่เกิน {RemarkLength} ตัวอักษร");
        if (!await db.Employees.AnyAsync(e => e.Id == employeeId)) return ServiceResult.Fail("ไม่พบพนักงานที่เลือก");
        return null;
    }

    /// <summary>ใช้ไปแล้ว of BR-020 by SR: the employee's ได้ค่าแรง days of the calendar year, without the row being saved.</summary>
    private async Task<Dictionary<string, double>> UsedPaidDaysAsync(int employeeId, int year, int? exceptId)
        => await LeaveDirectory.PaidOfYear(db.Leaves, employeeId, year)
            .Where(l => l.Id != exceptId)
            .GroupBy(l => l.Type)
            .Select(g => new { g.Key, Days = g.Sum(l => l.Days) })
            .ToDictionaryAsync(g => g.Key, g => g.Days);

    /// <summary>With the employee, whose code is part of the row's รหัสรายการ in the history.</summary>
    private async Task<Leave?> LoadAsync(int id, byte[]? loadedRowVersion)
    {
        var row = await db.Leaves.Include(l => l.Employee).FirstOrDefaultAsync(l => l.Id == id);
        row?.ExpectRowVersion(loadedRowVersion
            ?? throw new ArgumentException("Editing needs the rowversion the screen loaded.", nameof(loadedRowVersion)));
        return row;
    }

    private DateOnly Today => DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
}
