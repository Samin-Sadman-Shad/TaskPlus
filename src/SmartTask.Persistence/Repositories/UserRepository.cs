using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SmartTask.Application.Common.Models;
using SmartTask.Application.Contracts.Persistence;
using SmartTask.Domain.Entities;
using SmartTask.Domain.Enums;
using SmartTask.Persistence.Extensions;
using SortFields = SmartTask.Application.Common.Models.UserQueryParameters.SortFields;

namespace SmartTask.Persistence.Repositories;

internal sealed class UserRepository(SmartTaskDbContext context, TimeProvider timeProvider)
    : Repository<User>(context), IUserRepository
{
    private static readonly IReadOnlyDictionary<string, Expression<Func<User, object?>>> SortMap =
        new Dictionary<string, Expression<Func<User, object?>>>(StringComparer.OrdinalIgnoreCase)
        {
            [SortFields.Name] = u => u.FirstName + " " + u.LastName,
            [SortFields.Email] = u => u.NormalizedEmail,
            [SortFields.CreatedAt] = u => u.CreatedAt,
        };

    /// <inheritdoc />
    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(email);
        return Set.FirstOrDefaultAsync(u => u.NormalizedEmail == normalized, cancellationToken);
    }

    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(email);
        return Set.AnyAsync(u => u.NormalizedEmail == normalized, cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> ActiveUserWithRoleExistsAsync(UserRole role, CancellationToken cancellationToken = default) =>
        Set.AnyAsync(u => u.Role == role && u.IsActive, cancellationToken);

    /// <inheritdoc />
    public Task<User?> GetWithActiveRefreshTokensAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();

        return Set
            .Include(u => u.RefreshTokens.Where(t => t.RevokedAt == null && t.ExpiresAt > now))
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
    }

    /// <inheritdoc />
    public Task<PagedResult<User>> GetPagedAsync(UserQueryParameters parameters, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var query = Set.AsNoTracking();

        if (parameters.Role is { } role)
        {
            query = query.Where(u => u.Role == role);
        }

        if (parameters.IsActive is { } isActive)
        {
            query = query.Where(u => u.IsActive == isActive);
        }

        if (!string.IsNullOrWhiteSpace(parameters.Search))
        {
            var pattern = QueryableExtensions.ToContainsPattern(parameters.Search);
            query = query.Where(u =>
                EF.Functions.Like(u.FirstName, pattern, QueryableExtensions.LikeEscapeCharacter) ||
                EF.Functions.Like(u.LastName, pattern, QueryableExtensions.LikeEscapeCharacter) ||
                EF.Functions.Like(u.Email, pattern, QueryableExtensions.LikeEscapeCharacter));
        }

        return query
            .ApplySorting(parameters.SortBy, parameters.SortDirection, SortMap, SortFields.Default)
            .ToPagedResultAsync(parameters.PageNumber, parameters.PageSize, cancellationToken);
    }

    /// <summary>Same normalisation as <see cref="User.Create"/>.</summary>
    private static string Normalize(string email)
    {
        ArgumentNullException.ThrowIfNull(email);
        return email.Trim().ToUpperInvariant();
    }
}
