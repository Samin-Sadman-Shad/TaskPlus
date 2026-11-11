using SmartTask.Application.Common.Models;
using SmartTask.Domain.Entities;
using SmartTask.Domain.Enums;

namespace SmartTask.Application.Contracts.Persistence;

public interface IUserRepository : IRepository<User>
{
    /// <summary>Finds a user by email, case-insensitively (compared on <c>NormalizedEmail</c>). Tracked.</summary>
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>Whether at least one <b>active</b> user has <paramref name="role"/>. Used by the admin bootstrap.</summary>
    Task<bool> ActiveUserWithRoleExistsAsync(UserRole role, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the user (tracked) with only the refresh tokens that are active right now, so
    /// <see cref="User.RevokeAllRefreshTokens"/> and <see cref="User.ChangePasswordHash"/> can revoke them.
    /// </summary>
    Task<User?> GetWithActiveRefreshTokensAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Paged user list for admins. Read-only (not tracked).</summary>
    Task<PagedResult<User>> GetPagedAsync(UserQueryParameters parameters, CancellationToken cancellationToken = default);
}
