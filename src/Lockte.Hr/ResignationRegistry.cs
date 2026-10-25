using Lockte.Hr.Data;
using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Hr;

/// <summary>
/// BR-044 บันทึกการลาออก (SCR-33, มาโคร บันทึกพนักงานที่ลาออก): วันที่ลาออก → วันลาออก, เหตุผล → หมายเหตุ (MEM), as the
/// legacy UPDATE. A blank date clears วันลาออก (the employee is working again), as the legacy wrote whatever was typed.
/// </summary>
public sealed class ResignationRegistry(HrDbContext db)
{
    public const int ReasonLength = 255;

    /// <param name="rowVersion">The employee's rowversion as the screen loaded it; a newer save throws <see cref="ConcurrencyConflictException"/>.</param>
    public async Task<ServiceResult> ResignAsync(int employeeId, DateOnly? resignDate, string? reason, byte[] rowVersion)
    {
        var text = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (text?.Length > ReasonLength) return ServiceResult.Fail($"เหตุผลยาวได้ไม่เกิน {ReasonLength} ตัวอักษร");
        var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == employeeId && !e.IsMigrationStub);
        if (employee is null) return ServiceResult.Fail("ไม่พบพนักงานคนนี้");
        employee.ExpectRowVersion(rowVersion);
        employee.ResignDate = resignDate;
        employee.Memo = text;
        await db.SaveChangesAsync();
        return ServiceResult.Ok(employee.Id);
    }
}
