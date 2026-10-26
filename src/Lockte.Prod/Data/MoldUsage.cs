using Lockte.SharedKernel.Persistence;

namespace Lockte.Prod.Data;

/// <summary>
/// การใช้งานแม่พิมพ์ของงวดปัจจุบัน (T-การใช้งานของพิมพ์0, #125): the shots of a mold on a day counted from the production lines (BR-008), or its
/// <see cref="IsOpening"/> balance carried over at the last cut (<c>MARK = ST</c>). Derived data, made again at will: a cut writes no row-by-row history.
/// Internal to the module (ADR 0002).
/// </summary>
internal sealed class MoldUsage : IAudited
{
    public int Id { get; set; }
    public int MoldId { get; set; }
    public Mold Mold { get; set; } = null!;
    public DateOnly Day { get; set; }
    /// <summary>จำนวนครั้งที่ใช้ (PD_QTY).</summary>
    public int Shots { get; set; }
    /// <summary>ยอดยกมา (MARK = ST).</summary>
    public bool IsOpening { get; set; }

    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    string IHasBusinessKey.BusinessKey => $"{Mold?.Code} {Day:yyyy-MM-dd}";
}

/// <summary>
/// ยอดสะสมการใช้งานแม่พิมพ์ ณ วันตัด (T-ตันงวดการใช้งานของพิมพ์0, #125): kept for every cut date as a history, so a cut can be made again from the
/// one before (ตัดซ้ำ). Only molds still in use (no วันที่เลิกใช้) get one.
/// </summary>
internal sealed class MoldCutoff : IAudited
{
    public int Id { get; set; }
    public int MoldId { get; set; }
    public Mold Mold { get; set; } = null!;
    public DateOnly CutDate { get; set; }
    public int Cumulative { get; set; }

    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    string IHasBusinessKey.BusinessKey => $"{Mold?.Code} {CutDate:yyyy-MM-dd}";
}
