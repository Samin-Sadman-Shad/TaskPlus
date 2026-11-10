namespace SmartTask.Application.Common.Models.Auth;

/// <summary>
/// A newly generated refresh token. <see cref="RawToken"/> goes to the client once, through
/// <c>IRefreshTokenTransport</c>; only <see cref="TokenHash"/> is ever stored.
/// </summary>
public sealed record GeneratedRefreshToken(string RawToken, string TokenHash, DateTimeOffset ExpiresAt)
{
    /// <summary>Hides the raw token and hash so the record can be logged safely.</summary>
    public override string ToString() => $"{nameof(GeneratedRefreshToken)} {{ RawToken = ***, TokenHash = ***, ExpiresAt = {ExpiresAt:O} }}";
}
