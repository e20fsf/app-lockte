using Lockte.SharedKernel.Persistence;

namespace Lockte.Sales.Data;

/// <summary>
/// ลูกค้า (T-P_CUS, SCR-02) — one register for every kind of work (CONTEXT.md): the งานรับจ้างผลิต customers the
/// employee program reads (#41, BR-009) and the customers of ORDER. Internal to the module (ADR 0002): other modules see
/// <see cref="CustomerDto"/> from <see cref="CustomerDirectory"/>.
/// </summary>
internal sealed class Customer : IAudited
{
    public int Id { get; set; }
    /// <summary>รหัสลูกค้า (business key, unique, upper case); the ORDER program's codes look like <c>SS-GG-NNN</c>.</summary>
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>เครดิต เป็นจำนวนวัน (เงินสด = 0, CONTEXT.md); blank = not known.</summary>
    public int? CreditDays { get; set; }
    /// <summary>ที่อยู่ (เลขที่ หมู่ ตำบล).</summary>
    public string? Address { get; set; }
    /// <summary>อำเภอ (ไม่ต้องใส่ "อ.").</summary>
    public string? District { get; set; }
    public string? Province { get; set; }
    public string? PostalCode { get; set; }
    /// <summary>ชื่อขนส่ง (ADD1).</summary>
    public string? TransportName { get; set; }
    /// <summary>ที่อยู่ขนส่ง (ADDDE).</summary>
    public string? TransportAddress { get; set; }
    /// <summary>โทรฯ ขนส่ง (ADDDE1).</summary>
    public string? TransportPhone { get; set; }
    public string? Phone1 { get; set; }
    public string? Phone2 { get; set; }
    /// <summary>เขตการขายที่ดูแล (SALE_ID).</summary>
    public int? SalesTerritoryId { get; set; }
    public SalesTerritory? SalesTerritory { get; set; }
    /// <summary>หมายเหตุ (REMARK): ชื่อ + ที่อยู่รวมเป็นข้อความเดียวสำหรับพิมพ์.</summary>
    public string? Remark { get; set; }

    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    string IHasBusinessKey.BusinessKey => Code;
}

/// <summary>
/// เขตการขาย (T-เซล, SCR-07): the code means a territory, not a person (Q-22) — the sales representative of a territory
/// is an employee of module พนักงานและค่าแรง (#16), blank for e.g. <c>00</c> ออฟฟิศ.
/// </summary>
internal sealed class SalesTerritory : IAudited
{
    public int Id { get; set; }
    /// <summary>รหัสเขต <c>00</c>–<c>11</c> (business key, unique).</summary>
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>พนักงานขาย: an employee of module พนักงานและค่าแรง, checked on save.</summary>
    public int? EmployeeId { get; set; }

    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    string IHasBusinessKey.BusinessKey => Code;
}
