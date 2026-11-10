using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using SmartTask.Application.Common.Models.Auth;
using SmartTask.Application.Contracts.Identity;

namespace SmartTask.Infrastructure.Identity;

/// <summary>
/// Refresh tokens are 64 cryptographically random bytes, encoded as Base64Url. Only their SHA-256 hash
/// (lower-case hex) is stored, so a database leak does not expose usable tokens. A plain hash without
/// a salt is enough here because the input is random, not a password.
/// </summary>
public sealed class RefreshTokenService(IOptions<JwtOptions> options, TimeProvider timeProvider) : IRefreshTokenService
{
    public const int TokenByteLength = 64;

    public GeneratedRefreshToken Generate()
    {
        var raw = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenByteLength));
        var expiresAt = timeProvider.GetUtcNow().AddDays(options.Value.RefreshTokenDays);

        return new GeneratedRefreshToken(raw, Hash(raw), expiresAt);
    }

    public string Hash(string rawToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(rawToken);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
    }
}
