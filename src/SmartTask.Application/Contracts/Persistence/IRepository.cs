using SmartTask.Domain.Common;

namespace SmartTask.Application.Contracts.Persistence;

/// <summary>
/// Basic persistence operations for an aggregate. Changes are written only when
/// <see cref="IUnitOfWork.SaveChangesAsync"/> is called.
/// </summary>
public interface IRepository<TEntity>
    where TEntity : BaseEntity
{
    /// <summary>Returns the entity, tracked so changes to it are saved; <c>null</c> if it does not exist or is soft-deleted.</summary>
    Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a detached entity as modified. Entities loaded through this repository are already
    /// tracked and do not need this call.
    /// </summary>
    void Update(TEntity entity);

    /// <summary>Deletes the entity. <see cref="ISoftDeletable"/> entities are soft-deleted on save.</summary>
    void Remove(TEntity entity);
}
