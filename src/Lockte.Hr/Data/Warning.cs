using System.Globalization;
using Lockte.SharedKernel.Persistence;

namespace Lockte.Hr.Data;

/// <summary>
/// ใบเตือน (T-ใบเตือน and its history T-ใบเตือนADD in one table for every year, #41): one warning given to one employee.
/// NAME is not copied (the employee's own); DEPART is kept as the employee's แผนก when the warning was saved, as the
/// legacy copied it (BR-067 counts warnings by it). CODE may be blank on migrated rows (DQ-9).
/// </summary>
internal sealed class Warning : IAudited
{
    public int Id { get; set; }
    /// <summary>วันที่ออกใบเตือน (DATE).</summary>
    public DateOnly Date { get; set; }
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    /// <summary>แผนกตอนบันทึก (DEPART) in module องค์กร.</summary>
    public int? DepartmentId { get; set; }
    /// <summary>ระดับใบเตือน (CODE): <see cref="WarningLevels"/>; blank on some migrated rows (DQ-9).</summary>
    public string? Level { get; set; }
    /// <summary>รายละเอียด / เรื่อง (REMARK).</summary>
    public string? Remark { get; set; }

    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    string IHasBusinessKey.BusinessKey => $"{Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)} {Employee?.Code}";
}
