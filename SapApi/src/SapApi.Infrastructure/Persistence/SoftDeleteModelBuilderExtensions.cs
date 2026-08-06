using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SapApi.Domain.Entities;

namespace SapApi.Infrastructure.Persistence;

internal static class SoftDeleteModelBuilderExtensions
{
    /// <summary>PostgreSQL partial-index predicate for active (non-deleted) rows.</summary>
    internal const string ActiveRowFilter = "\"IsDeleted\" = false";

    internal static IndexBuilder<TEntity> IsUniqueAmongActiveRows<TEntity>(this IndexBuilder<TEntity> indexBuilder)
        where TEntity : class, ISoftDeletable =>
        indexBuilder.IsUnique().HasFilter(ActiveRowFilter);

    internal static void ApplySoftDeleteQueryFilters(this ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
                continue;

            ApplyFilter(modelBuilder, entityType);
        }
    }

    private static void ApplyFilter(ModelBuilder modelBuilder, IMutableEntityType entityType)
    {
        var parameter = Expression.Parameter(entityType.ClrType, "e");
        var isDeletedProperty = Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted));
        var filter = Expression.Lambda(
            Expression.Equal(isDeletedProperty, Expression.Constant(false)),
            parameter);

        modelBuilder.Entity(entityType.ClrType).HasQueryFilter(filter);
    }

    internal static void ConfigureSoftDeleteProperty<TEntity>(this EntityTypeBuilder<TEntity> builder)
        where TEntity : class, ISoftDeletable
    {
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);
    }
}
