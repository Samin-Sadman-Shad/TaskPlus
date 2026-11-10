using SmartTask.Application.Common.Exceptions;

namespace SmartTask.Application.Contracts.Persistence;

/// <summary>Commits the changes made through the repositories. One call per use case means one transaction.</summary>
public interface IUnitOfWork
{
    /// <summary>Saves all pending changes.</summary>
    /// <exception cref="ConflictException">A concurrency token mismatch or a unique-index violation.</exception>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs <paramref name="action"/> in an explicit transaction under the database retry strategy.
    /// On a transient failure the whole action is retried, so it must be safe to run again and must
    /// call <see cref="SaveChangesAsync"/> itself.
    /// </summary>
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default);
}
