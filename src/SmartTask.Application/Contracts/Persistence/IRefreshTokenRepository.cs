using SmartTask.Domain.Entities;

namespace SmartTask.Application.Contracts.Persistence;

public interface IRefreshTokenRepository : IRepository<RefreshToken>
{
    /// <summary>
    /// Finds a token by its hash. Tracked, so it can be revoked. <see cref="RefreshToken"/> has no
    /// user navigation: load the owner with <see cref="IUserRepository"/> using <see cref="RefreshToken.UserId"/>.
    /// </summary>
    Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes tokens that expired before <paramref name="olderThan"/>, directly in the database
    /// (no <see cref="IUnitOfWork.SaveChangesAsync"/> needed). Returns the number of rows deleted.
    /// </summary>
    Task<int> RemoveExpiredAsync(DateTimeOffset olderThan, CancellationToken cancellationToken = default);
}
