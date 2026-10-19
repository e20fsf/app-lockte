using System.Globalization;
using Lockte.SharedKernel.Persistence;

namespace Lockte.Hr.Data;

/// <summary>
/// ไม่มาทำงาน (T-NO_WORK): one employee not at work one day. BR-010 makes no เวลาปกติ for them and RPT-17 lists them
/// (BR-061). Only DATE1, ID1, REMARK and W_IN are kept: the other columns are constant and read by nothing (data-model).
/// </summary>
internal sealed class Absence : IAudited
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    /// <summary>เวลาเข้า (W_IN): above 17:00 = a night shift that started this date, reported the next day (BR-061).</summary>
    public TimeOnly WorkIn { get; set; }
    /// <summary>หมายเหตุ (REMARK), printed as เหตุผล on RPT-17.</summary>
    public string? Remark { get; set; }

    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    string IHasBusinessKey.BusinessKey => $"{Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)} {Employee?.Code}";
}

/// <summary>
/// เวลาทำงาน (T-E_WORK and its history T-ADD_EW in one table for every year, #41): one kind of work of one employee
/// on one day. Rows may repeat (the legacy removes repeats when the period is calculated, BR-007).
/// </summary>
internal sealed class WorkTime : IAudited
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    /// <summary>ประเภทการทำงาน (TYPE): <see cref="WorkTypes"/>; blank on a few migrated rows (BR-010 fills it for its day).</summary>
    public string? Type { get; set; }
    public TimeOnly? WorkIn { get; set; }
    /// <summary>เวลาออก (W_OUT): earlier than <see cref="WorkIn"/> = the next day (the legacy 1899-12-31).</summary>
    public TimeOnly? WorkOut { get; set; }
    /// <summary>เวลาเข้างานจริงเมื่อมาสาย (LATE); blank = on time (BR-066).</summary>
    public TimeOnly? Late { get; set; }
    /// <summary>นาทีพัก (REST_T).</summary>
    public int RestMinutes { get; set; }
    /// <summary>รหัสลูกค้างานรับจ้าง (MARK, BR-009).</summary>
    public string? Mark { get; set; }

    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    string IHasBusinessKey.BusinessKey => $"{Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)} {Employee?.Code} {Type}";
}
