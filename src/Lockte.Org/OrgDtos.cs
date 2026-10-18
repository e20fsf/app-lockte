namespace Lockte.Org;

public sealed record SectionDto(int Id, string Name, int SortOrder, byte[] RowVersion);

public sealed record ShiftDto(
    int Id,
    string Code,
    TimeOnly WorkIn,
    TimeOnly WorkOut,
    TimeOnly? BreakStart,
    TimeOnly? BreakEnd,
    TimeOnly? OvertimeIn,
    TimeOnly? OvertimeOut,
    int RestMinutes,
    byte[] RowVersion)
{
    /// <summary>นาทีพักที่เลือกได้ (T-เวลาพัก).</summary>
    public static IReadOnlyList<int> RestMinuteChoices { get; } = [0, 10, 20, 30, 40, 50, 60];

    /// <summary>The shift ends the next day (e.g. B 20:10–05:10).</summary>
    public bool CrossesMidnight => WorkOut < WorkIn;
}

/// <summary>
/// A shift the department works: its หัวหน้ากะ (blank until the employee register links one) and the names printed
/// on documents for this department in this shift.
/// </summary>
public sealed record DepartmentShiftDto(
    int ShiftId,
    string ShiftCode,
    int? SupervisorEmployeeId,
    string? SupervisorName,
    string? ManagerName,
    string? FactoryManagerName,
    byte[] RowVersion);

public sealed record DepartmentDto(
    int Id,
    string Code,
    string Name,
    string EmployeeIdPrefix,
    int SortOrder,
    int SectionId,
    string SectionName,
    int? HeadEmployeeId,
    IReadOnlyList<DepartmentShiftDto> Shifts,
    byte[] RowVersion);

/// <summary>
/// A row of SCR-05 to save, with the shifts the department works. <see cref="Id"/> null = new row;
/// <see cref="RowVersion"/> = as loaded. Name is required: a row is never deleted by clearing it.
/// </summary>
public sealed record DepartmentInput(
    int? Id,
    string Code,
    string Name,
    string EmployeeIdPrefix,
    int SortOrder,
    int? SectionId,
    int? HeadEmployeeId,
    IReadOnlyCollection<DepartmentShiftInput> Shifts,
    byte[]? RowVersion);

/// <summary>A shift ticked on SCR-05 with its printed names. หัวหน้ากะ is linked separately (SetShiftSupervisorAsync).</summary>
public sealed record DepartmentShiftInput(int ShiftId, string? SupervisorName, string? ManagerName, string? FactoryManagerName);

public sealed record SectionInput(int? Id, string Name, int SortOrder, byte[]? RowVersion);

public sealed record ShiftInput(
    int? Id,
    string Code,
    TimeOnly WorkIn,
    TimeOnly WorkOut,
    TimeOnly? BreakStart,
    TimeOnly? BreakEnd,
    TimeOnly? OvertimeIn,
    TimeOnly? OvertimeOut,
    int RestMinutes,
    byte[]? RowVersion);
