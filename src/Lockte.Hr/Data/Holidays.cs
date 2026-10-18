using System.Globalization;
using Lockte.SharedKernel.Persistence;

namespace Lockte.Hr.Data;

/// <summary>วันหยุดประจำสัปดาห์ (T-SUNDAY): one row per day off, entered or added four weeks ahead (BR-014).</summary>
internal sealed class WeeklyHoliday : IAudited
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    /// <summary>ป้าย (ITEM), "วันอาทิตย์" by default; BR-014 continues each label separately.</summary>
    public string Label { get; set; } = "";

    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    string IHasBusinessKey.BusinessKey => Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}

/// <summary>วันหยุดประเพณี (T-HOLIDAY): one row per public holiday, ~15 a year.</summary>
internal sealed class PublicHoliday : IAudited
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    /// <summary>ชื่อวันหยุด (ITEM).</summary>
    public string? Name { get; set; }

    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    string IHasBusinessKey.BusinessKey => Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
