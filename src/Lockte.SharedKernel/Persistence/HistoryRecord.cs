using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Lockte.SharedKernel.Persistence;

public enum HistoryKind
{
    /// <summary>One column of one row changed by a person (เพิ่ม / แก้ / ลบ).</summary>
    Change = 1,
    /// <summary>A batch job or calculation, recorded once instead of row by row.</summary>
    Event = 2,
    Login = 3,
}

/// <summary>
/// ประวัติการแก้ไข (#39, #43): table <c>audit.History</c>, kept forever, never deleted.
/// Every module DbContext maps it so its own saves write history in the same transaction;
/// only the audit DbContext creates the table (<see cref="HistoryModel.Map"/>).
/// </summary>
public sealed class HistoryRecord
{
    public long Id { get; set; }
    public DateTimeOffset At { get; set; }
    public string UserName { get; set; } = "";
    public HistoryKind Kind { get; set; }
    /// <summary>Which table (its audit name), e.g. "ผู้ใช้"; for events, the table the job worked on if any.</summary>
    public string? Table { get; set; }
    /// <summary>รหัสรายการ: the row's business key (รหัสพนักงาน, ชื่อ user…).</summary>
    public string? RowKey { get; set; }
    /// <summary>เพิ่ม / แก้ / ลบ, the job name, or the login outcome.</summary>
    public string Action { get; set; } = "";
    public string? Column { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    /// <summary>Event parameters (JSON) or login detail.</summary>
    public string? Detail { get; set; }
    public int? RowCount { get; set; }
    /// <summary>Groups the column changes of one save.</summary>
    public Guid? ChangeSet { get; set; }
}

/// <summary><see cref="HistoryRecord.Action"/> of <see cref="HistoryKind.Change"/> rows.</summary>
public static class HistoryActions
{
    public const string Added = "เพิ่ม";
    public const string Modified = "แก้";
    public const string Deleted = "ลบ";
}

public static class HistoryModel
{
    public const string Schema = "audit";
    public const string Table = "History";

    public static void Map(ModelBuilder modelBuilder, bool ownsTable)
        => modelBuilder.Entity<HistoryRecord>(history =>
        {
            history.ToTable(Table, Schema, t =>
            {
                if (!ownsTable) t.ExcludeFromMigrations();
            });
            history.Property(h => h.UserName).HasMaxLength(ModelBuilderExtensions.UserNameLength);
            history.Property(h => h.Table).HasMaxLength(128);
            history.Property(h => h.RowKey).HasMaxLength(200);
            history.Property(h => h.Action).HasMaxLength(200);
            history.Property(h => h.Column).HasMaxLength(128);
            history.HasIndex(h => h.At);
            history.HasIndex(h => new { h.Table, h.RowKey });
            history.HasIndex(h => h.UserName);
        });
}

public static class HistoryEventExtensions
{
    private static readonly JsonSerializerOptions Json = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>
    /// Records a batch job or calculation as one history row (ใคร ทำอะไร พารามิเตอร์ เวลา จำนวนแถว); saved with the
    /// next SaveChanges. Run the job's row writes inside <see cref="AuditScope.SuppressRowChanges"/> so they are not
    /// also recorded row by row.
    /// </summary>
    public static void AddHistoryEvent(
        this DbContext context, string action, object? parameters = null, int? rowCount = null, string? table = null)
        => context.Add(new HistoryRecord
        {
            Kind = HistoryKind.Event,
            Action = action,
            Detail = parameters is null ? null : JsonSerializer.Serialize(parameters, Json),
            RowCount = rowCount,
            Table = table,
        });
}
