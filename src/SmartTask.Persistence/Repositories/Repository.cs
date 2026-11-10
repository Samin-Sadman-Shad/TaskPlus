using Microsoft.EntityFrameworkCore;
using SmartTask.Application.Contracts.Persistence;
using SmartTask.Domain.Common;

namespace SmartTask.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="IRepository{TEntity}"/>, used as the base for the specific repositories.</summary>
internal class Repository<TEntity>(SmartTaskDbContext context) : IRepository<TEntity>
    where TEntity : BaseEntity
{
    protected readonly SmartTaskDbContext Context = context;

    protected readonly DbSet<TEntity> Set = context.Set<TEntity>();

    /// <inheritdoc />
    /// <remarks>Tracked.</remarks>
    public virtual Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Set.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default) =>
        Set.AnyAsync(e => e.Id == id, cancellationToken);

    public async Task AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        await Set.AddAsync(entity, cancellationToken);
    }

    public void Update(TEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        Set.Update(entity);
    }

    public void Remove(TEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        Set.Remove(entity);
    }
}
