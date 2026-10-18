using System.Globalization;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lockte.Hr.Data;

// Internal to the module (ADR 0002): other modules ask HolidayCalendar, never these.

/// <summary>วันหยุดประจำสัปดาห์ (T-SUNDAY) — one row per date, usually a Sunday but may be moved (Q-26).</summary>
internal sealed class WeeklyHoliday : IAudited
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    /// <summary>ป้าย (ITEM). "เพิ่ม" (BR-014) extends each label's dates as its own series.</summary>
    public string Label { get; set; } = "";

    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    string IHasBusinessKey.BusinessKey => Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}

/// <summary>วันหยุดประเพณี (T-HOLIDAY) — one row per date, entered by HR about 15 a year.</summary>
internal sealed class PublicHoliday : IAudited
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    /// <summary>ชื่อวันหยุด (ITEM); may be blank as in the legacy table.</summary>
    public string? Name { get; set; }

    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    string IHasBusinessKey.BusinessKey => Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}

/// <summary>Column sizes as in T-SUNDAY / T-HOLIDAY, so the data migration never truncates.</summary>
internal sealed class WeeklyHolidayConfiguration : IEntityTypeConfiguration<WeeklyHoliday>
{
    public void Configure(EntityTypeBuilder<WeeklyHoliday> w)
    {
        w.HasAuditName("วันหยุดประจำสัปดาห์");
        w.Property(x => x.Date).HasAuditName("วันที่");
        w.Property(x => x.Label).HasMaxLength(HolidayRegistry.WeeklyLabelLength).HasAuditName("ป้าย");
        w.HasIndex(x => x.Date).IsUnique();
    }
}

internal sealed class PublicHolidayConfiguration : IEntityTypeConfiguration<PublicHoliday>
{
    public void Configure(EntityTypeBuilder<PublicHoliday> h)
    {
        h.HasAuditName("วันหยุดประเพณี");
        h.Property(x => x.Date).HasAuditName("วันที่");
        h.Property(x => x.Name).HasMaxLength(HolidayRegistry.PublicHolidayNameLength).HasAuditName("ชื่อวันหยุด");
        h.HasIndex(x => x.Date).IsUnique();
    }
}
