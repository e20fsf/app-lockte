using Lockte.SharedKernel.Persistence;

namespace Lockte.Org.Data;

// Internal to the module (ADR 0002): other modules see DTOs from OrgDirectory, never these.

/// <summary>ฝ่าย — กลุ่มของแผนก.</summary>
internal sealed class Section : IAudited
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int SortOrder { get; set; }

    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    string IHasBusinessKey.BusinessKey => Name;
}

/// <summary>
/// แผนก — one row per department for the whole company (#16), replacing T-แผนก's one row per department × shift.
/// The shifts it works, with what T-แผนก kept per shift, are its <see cref="Shifts"/>.
/// </summary>
internal sealed class Department : IAudited
{
    public int Id { get; set; }
    /// <summary>รหัสแผนก (business key, unique).</summary>
    public string Code { get; set; } = "";
    /// <summary>ชื่อสะกดมาตรฐาน (unique).</summary>
    public string Name { get; set; } = "";
    /// <summary>ตัวอักษรนำหน้ารหัสพนักงาน (T-แผนก.CODE without the "*"), e.g. "D".</summary>
    public string EmployeeIdPrefix { get; set; } = "";
    /// <summary>ลำดับการแสดงผล (T-แผนก.SUF).</summary>
    public int SortOrder { get; set; }
    public int SectionId { get; set; }
    public Section Section { get; set; } = null!;
    /// <summary>หัวหน้าแผนก: an employee of module พนักงานและค่าแรง; blank until the employee register exists.</summary>
    public int? HeadEmployeeId { get; set; }
    /// <summary>อัตรากำลัง (T-แผนก2.QTY): planned headcount, shown against the actual count (BR-054).</summary>
    public int? StaffingTarget { get; set; }

    public List<DepartmentShift> Shifts { get; set; } = [];

    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    string IHasBusinessKey.BusinessKey => Code;
}

/// <summary>กะ — company-wide shift register A–G with standard times (T-SHIFT rows that have a code).</summary>
internal sealed class Shift : IAudited
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public TimeOnly WorkIn { get; set; }
    /// <summary>Earlier than <see cref="WorkIn"/> = the next day (legacy stored 1899-12-31).</summary>
    public TimeOnly WorkOut { get; set; }
    public TimeOnly? BreakStart { get; set; }
    public TimeOnly? BreakEnd { get; set; }
    public TimeOnly? OvertimeIn { get; set; }
    public TimeOnly? OvertimeOut { get; set; }
    /// <summary>นาทีพัก (REST_T).</summary>
    public int RestMinutes { get; set; }

    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    string IHasBusinessKey.BusinessKey => Code;
}

/// <summary>
/// แผนกในกะ — a shift the department works (T-แผนก row), with its หัวหน้ากะ and the names T-แผนก printed per
/// department × shift.
/// </summary>
internal sealed class DepartmentShift : IAudited
{
    public int Id { get; set; }
    public int DepartmentId { get; set; }
    public Department Department { get; set; } = null!;
    public int ShiftId { get; set; }
    public Shift Shift { get; set; } = null!;
    /// <summary>หัวหน้ากะ: an employee of module พนักงานและค่าแรง; blank until the employee register links one.</summary>
    public int? SupervisorEmployeeId { get; set; }
    /// <summary>ชื่อหัวหน้า (T-แผนก.SUPER) — printed on documents.</summary>
    public string? SupervisorName { get; set; }
    /// <summary>ผู้จัดการ (T-แผนก.MANAGER) — printed on documents.</summary>
    public string? ManagerName { get; set; }
    /// <summary>ผู้จัดการโรงงาน (T-แผนก.FACTORY MANAGER) — printed on certificates (BR-070).</summary>
    public string? FactoryManagerName { get; set; }

    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    // Loaded with both navigations by OrgRegistry before every save.
    string IHasBusinessKey.BusinessKey => $"{Department?.Code} กะ {Shift?.Code}";
}
