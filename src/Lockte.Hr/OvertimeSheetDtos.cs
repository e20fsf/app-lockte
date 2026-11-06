using Lockte.SharedKernel;

namespace Lockte.Hr;

/// <summary>
/// ใบขออนุมัติทำ OT to print (SCR-14, BR-012). As the legacy form OT, วันที่ แผนก เวลาเริ่ม/เลิก เวลาพัก ประเภท are
/// for the whole sheet and each line is one employee with หมายเหตุ (MEMO). Lines without an employee are dropped.
/// </summary>
public sealed record OvertimeSheetInput(DateOnly? Date, int? DepartmentId, TimeOnly? WorkIn, TimeOnly? WorkOut, int RestMinutes, string? Type,
    IReadOnlyList<OvertimeSheetLineInput> Lines);

/// <summary>
/// A line of the subform OT1: รหัส and หมายเหตุ (the screen fills it with the employee's หน้าที่, as the legacy). #411: a line may have its own
/// เวลาเริ่ม/เลิก, เวลาพัก and ประเภท (people of the same day and แผนก working different hours share one sheet); what it leaves null is the
/// sheet's.
/// </summary>
public sealed record OvertimeSheetLineInput(int? EmployeeId, string? Memo, TimeOnly? WorkIn = null, TimeOnly? WorkOut = null, int? RestMinutes = null,
    string? Type = null);

/// <summary>
/// What RPT-16 prints: the sheet's date and แผนก, and its lines by code (the report is sorted by ID), each with its own times (#411).
/// <see cref="Type"/> (the sheet's default) is not printed; it is kept for the screen.
/// </summary>
public sealed record OvertimeSheet(DateOnly Date, string? DepartmentName, string Type, IReadOnlyList<OvertimeSheetLine> Lines);

/// <summary>A line of RPT-16: รหัส ชื่อ-นามสกุล เริ่มงาน เลิกงาน หมายเหตุ.</summary>
public sealed record OvertimeSheetLine(string EmployeeCode, string EmployeeName, TimeOnly WorkIn, TimeOnly WorkOut, string? Memo);

/// <summary>
/// The outcome of printing a sheet: <see cref="ServiceResult.Id"/> = เวลาทำงาน rows saved; <see cref="Sheet"/> is
/// null when the input was refused.
/// </summary>
public sealed record OvertimeSheetOutcome(ServiceResult Result, OvertimeSheet? Sheet);

/// <summary>
/// The dropdowns of SCR-14 for one date: employees still working, by code (combo ID of OT1), and the distinct OT times
/// of the shifts (combos W_IN/W_OUT, T-SHIFT.O_IN/O_OUT).
/// </summary>
public sealed record OvertimeSheetChoices(IReadOnlyList<OvertimeSheetEmployee> Employees, IReadOnlyList<TimeOnly> StartTimes,
    IReadOnlyList<TimeOnly> EndTimes);

/// <summary>
/// An employee to pick: <see cref="Duty"/> (หน้าที่, WORK) is the default หมายเหตุ; <see cref="IsAbsent"/> = recorded
/// ไม่มาทำงาน for the date by BR-061's rule (the legacy flag GRED_2 = "NO").
/// </summary>
public sealed record OvertimeSheetEmployee(int Id, string Code, string Name, string? DepartmentName, string? Duty, bool IsAbsent);
