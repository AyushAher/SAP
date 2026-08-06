namespace SapApi.Domain.Entities;

/// <summary>
/// Marks an entity as soft-deletable. <see cref="AppDbContext"/> applies a global query filter
/// (<c>IsDeleted == false</c>) and converts hard deletes to setting <see cref="IsDeleted"/> = true.
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
}
