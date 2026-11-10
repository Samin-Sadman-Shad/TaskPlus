using Microsoft.EntityFrameworkCore;
using SmartTask.Application.Contracts.Persistence;
using SmartTask.Domain.Entities;

namespace SmartTask.Persistence.Repositories;

internal sealed class RefreshTokenRepository(SmartTaskDbContext context)
    : Repository<RefreshToken>(context), IRefreshTokenRepository
{
    /// <inheritdoc />
    public Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);
        return Set.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);
    }

    /// <inheritdoc />
    public Task<int> RemoveExpiredAsync(DateTimeOffset olderThan, CancellationToken cancellationToken = default) =>
        Set.Where(t => t.ExpiresAt < olderThan).ExecuteDeleteAsync(cancellationToken);
}
