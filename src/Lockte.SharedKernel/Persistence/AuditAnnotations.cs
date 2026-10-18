using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lockte.SharedKernel.Persistence;

/// <summary>How a table or column appears in the change history. Set in each module's model configuration.</summary>
public static class AuditAnnotations
{
    public const string Name = "Lockte:AuditName";
    public const string Bookkeeping = "Lockte:Bookkeeping";
    public const string Secret = "Lockte:Secret";

    /// <summary>Name shown in the history (e.g. "พนักงาน", "ชื่อที่แสดง") instead of the table/property name.</summary>
    public static EntityTypeBuilder<T> HasAuditName<T>(this EntityTypeBuilder<T> entity, string name) where T : class
        => entity.HasAnnotation(Name, name);

    public static PropertyBuilder<T> HasAuditName<T>(this PropertyBuilder<T> property, string name)
        => property.HasAnnotation(Name, name);

    /// <summary>
    /// A column the system updates on its own (login counters, normalized copies…): changing only these is not
    /// an edit — no UpdatedBy/At, no history.
    /// </summary>
    public static PropertyBuilder<T> IsBookkeeping<T>(this PropertyBuilder<T> property)
        => property.HasAnnotation(Bookkeeping, true);

    /// <summary>History says the column changed but never stores its values (password hash).</summary>
    public static PropertyBuilder<T> IsSecret<T>(this PropertyBuilder<T> property)
        => property.HasAnnotation(Secret, true);

    internal static string AuditName(this IReadOnlyEntityType entityType)
        => entityType.FindAnnotation(Name)?.Value as string
           ?? $"{entityType.GetSchema() ?? entityType.GetDefaultSchema()}.{entityType.GetTableName()}";

    internal static string AuditName(this IReadOnlyProperty property)
        => property.FindAnnotation(Name)?.Value as string ?? property.Name;

    internal static bool IsBookkeeping(this IReadOnlyProperty property) => property.FindAnnotation(Bookkeeping) is not null;

    internal static bool IsSecret(this IReadOnlyProperty property) => property.FindAnnotation(Secret) is not null;
}

/// <summary>
/// รหัสรายการ: the key a user searches the history by (รหัสพนักงาน, ชื่อ user…). Required of every
/// <see cref="IAudited"/> row; known before the save, unlike a database-generated id.
/// </summary>
public interface IHasBusinessKey
{
    string BusinessKey { get; }
}
