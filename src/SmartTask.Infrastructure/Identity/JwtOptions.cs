using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace SmartTask.Infrastructure.Identity;

/// <summary>
/// Bound from the <c>Jwt</c> configuration section and validated on startup by
/// <see cref="JwtOptionsValidator"/>. <see cref="SigningKey"/> must come from user-secrets or an
/// environment variable (<c>Jwt__SigningKey</c>), never from a checked-in file.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>HMAC-SHA256 needs a key of at least 256 bits.</summary>
    public const int MinSigningKeyBytes = 32;

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    public string SigningKey { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 15;

    public int RefreshTokenDays { get; set; } = 7;

    /// <summary>The key used both to sign tokens and to validate them, so the two can never disagree.</summary>
    public SymmetricSecurityKey CreateSigningKey() => new(Encoding.UTF8.GetBytes(SigningKey));
}
