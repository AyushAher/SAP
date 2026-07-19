using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace SapApi.Infrastructure.Persistence;

/// <summary>
/// The app's DbContext pool is configured with QueryTrackingBehavior.NoTracking globally (see
/// DependencyInjection.cs) for scalability with large SAP datasets. That means every entity returned
/// by a query is untracked, so services that mutate a fetched entity must explicitly mark it Modified
/// before SaveChangesAsync or the change is silently dropped.
///
/// A naive fix (DbSet.Update(entity) or Entry(entity).State = Modified) works the first time, but
/// throws "cannot be tracked because another instance with the same key value is already being
/// tracked" as soon as a SECOND, independently-fetched instance of the same row needs to be marked
/// Modified later in the same request/DbContext scope — e.g. ApprovalExecutionService.ExecuteAsync and
/// FinalizeApprovalAsync both look up and update the same StageWisePayment row via separate queries
/// within one HTTP request.
///
/// AttachModified handles both cases: if this entity's key is already tracked, it copies this
/// instance's current values onto the tracked entry instead of attaching a duplicate.
/// </summary>
public static class EntityTrackingExtensions
{
    public static void AttachModified<TEntity>(this DbContext context, TEntity entity) where TEntity : class
    {
        EntityEntry<TEntity>? existing = FindTrackedEntry(context, entity);

        if (existing is null)
        {
            context.Entry(entity).State = EntityState.Modified;
            return;
        }

        if (!ReferenceEquals(existing.Entity, entity))
            existing.CurrentValues.SetValues(entity);

        existing.State = EntityState.Modified;
    }

    private static EntityEntry<TEntity>? FindTrackedEntry<TEntity>(DbContext context, TEntity entity) where TEntity : class
    {
        var key = context.Model.FindEntityType(typeof(TEntity))?.FindPrimaryKey()
            ?? throw new InvalidOperationException($"{typeof(TEntity).Name} has no primary key configured.");

        var keyValues = key.Properties
            .Select(p => p.PropertyInfo?.GetValue(entity))
            .ToArray();

        return context.ChangeTracker.Entries<TEntity>()
            .FirstOrDefault(e =>
            {
                for (var i = 0; i < key.Properties.Count; i++)
                {
                    var currentValue = key.Properties[i].PropertyInfo?.GetValue(e.Entity);
                    if (!Equals(currentValue, keyValues[i]))
                        return false;
                }
                return true;
            });
    }
}
