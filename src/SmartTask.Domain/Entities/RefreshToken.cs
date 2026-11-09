using SmartTask.Domain.Common;
using SmartTask.Domain.Constants;
using SmartTask.Domain.Exceptions;

namespace SmartTask.Domain.Entities;

/// <summary>
/// A refresh token issued to a user. Only the SHA-256 hash of the raw token is stored; the raw
/// value is handed to the client once, at issue time.
/// </summary>
public sealed class RefreshToken : BaseEntity
{
    private RefreshToken()
    {
    }

    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; } = null!;

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedByIp { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public string? RevokedReason { get; private set; }

    /// <summary>Hash of the token that replaced this one during rotation; used for reuse detection.</summary>
    public string? ReplacedByTokenHash { get; private set; }

    public bool IsRevoked => RevokedAt is not null;

    public static RefreshToken Create(
        Guid userId,
        string tokenHash,
        DateTimeOffset expiresAt,
        DateTimeOffset createdAt,
        string? ip)
    {
        if (expiresAt <= createdAt)
        {
            throw new DomainException("Refresh token expiry must be after its creation time.");
        }

        return new RefreshToken
        {
            UserId = Guard.NotEmpty(userId, nameof(UserId)),
            TokenHash = RequiredHash(tokenHash, nameof(TokenHash)),
            ExpiresAt = expiresAt,
            CreatedAt = createdAt,
            CreatedByIp = Guard.OptionalText(ip, nameof(CreatedByIp), RefreshTokenConstraints.CreatedByIpMaxLength),
        };
    }

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;

    /// <summary>A token is active when it has not been revoked and has not expired.</summary>
    public bool IsActive(DateTimeOffset now) => !IsRevoked && !IsExpired(now);

    /// <summary>
    /// Revokes the token. Pass <paramref name="replacedByHash"/> when revoking because of rotation so
    /// that a later reuse of this token can be detected.
    /// </summary>
    /// <exception cref="DomainException">The token is already revoked.</exception>
    public void Revoke(DateTimeOffset at, string reason, string? replacedByHash = null)
    {
        if (IsRevoked)
        {
            throw new DomainException("Refresh token is already revoked.");
        }

        RevokedReason = Guard.RequiredText(reason, nameof(RevokedReason), RefreshTokenConstraints.RevokedReasonMaxLength);
        ReplacedByTokenHash = replacedByHash is null ? null : RequiredHash(replacedByHash, nameof(ReplacedByTokenHash));
        RevokedAt = at;
    }

    private static string RequiredHash(string? hash, string name)
    {
        if (string.IsNullOrWhiteSpace(hash))
        {
            throw new DomainException($"{name} is required.");
        }

        return hash;
    }
}
