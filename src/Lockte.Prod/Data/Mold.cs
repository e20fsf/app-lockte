using Lockte.SharedKernel.Persistence;

namespace Lockte.Prod.Data;

/// <summary>
/// แม่พิมพ์ (T-รหัสพิมพ์, SCR-17). Internal to the module (ADR 0002): other modules see <see cref="MoldDto"/> from
/// <see cref="MoldDirectory"/>.
/// </summary>
internal sealed class Mold : IAudited
{
    public int Id { get; set; }
    /// <summary>รหัสแม่พิมพ์ เช่น <c>BF-029-1/1</c> (business key, unique, upper case).</summary>
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>ชิ้น/พิมพ์ (QTYS) — pieces per shot, used for the shot count (BR-008).</summary>
    public int? PiecesPerShot { get; set; }
    /// <summary>เกณฑ์บำรุงรักษา: จำนวนครั้งการฉีด (SHOT).</summary>
    public int? MaintenanceShots { get; set; }
    /// <summary>เกณฑ์บำรุงรักษา: จำนวนชิ้น (PCS).</summary>
    public int? MaintenancePieces { get; set; }
    /// <summary>เกณฑ์บำรุงรักษา: จำนวนปี (YEAR).</summary>
    public int? MaintenanceYears { get; set; }
    /// <summary>แผนก of module องค์กร that uses the mold (checked on save; no database key across modules, like HR).</summary>
    public int? DepartmentId { get; set; }
    /// <summary>ผู้ทำแม่พิมพ์ (MARKER).</summary>
    public string? Maker { get; set; }
    /// <summary>วันที่เริ่มใช้ (DATE).</summary>
    public DateOnly? StartDate { get; set; }
    /// <summary>วันที่เลิกใช้ (OUT_DATE); blank = still in use.</summary>
    public DateOnly? EndDate { get; set; }
    /// <summary>ราคาแม่พิมพ์ (COST), บาท.</summary>
    public decimal Cost { get; set; }
    /// <summary>เจ้าของแม่พิมพ์ (OWNER): a customer's name, or the company.</summary>
    public string? Owner { get; set; }
    /// <summary>น้ำหนักอลูมิเนียมทั้งหมด (AL WEIGHT), กิโลกรัม.</summary>
    public decimal? AluminumWeight { get; set; }

    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    string IHasBusinessKey.BusinessKey => Code;
}
