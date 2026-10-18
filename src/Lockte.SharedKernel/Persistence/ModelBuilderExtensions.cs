using Microsoft.EntityFrameworkCore;

namespace Lockte.SharedKernel.Persistence;

public static class ModelBuilderExtensions
{
    public const int UserNameLength = 256;

    /// <summary>
    /// Call last in every module's OnModelCreating: maps the <see cref="IAudited"/> columns of each entity
    /// (rowversion as the concurrency token) the same way in every schema.
    /// </summary>
    public static ModelBuilder ApplyLockteConventions(this ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes()
                     .Where(t => typeof(IAudited).IsAssignableFrom(t.ClrType) && !t.IsOwned()))
        {
            var entity = modelBuilder.Entity(entityType.ClrType);
            entity.Property(nameof(IAudited.CreatedBy)).HasMaxLength(UserNameLength).IsRequired();
            entity.Property(nameof(IAudited.UpdatedBy)).HasMaxLength(UserNameLength).IsRequired();
            entity.Property(nameof(IAudited.RowVersion)).IsRowVersion();
        }
        return modelBuilder;
    }
}
