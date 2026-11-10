using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SmartTask.Application.Common.Models.Auth;
using SmartTask.Application.Contracts.Identity;
using SmartTask.Domain.Entities;

namespace SmartTask.Infrastructure.Identity;

/// <summary>Issues HMAC-SHA256 access tokens with <see cref="JsonWebTokenHandler"/>.</summary>
public sealed class JwtTokenService(IOptions<JwtOptions> options, TimeProvider timeProvider) : IJwtTokenService
{
    private readonly JsonWebTokenHandler _handler = new();

    public AccessTokenResult CreateAccessToken(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var jwt = options.Value;
        var now = timeProvider.GetUtcNow();
        var expiresAt = now.AddMinutes(jwt.AccessTokenMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            Claims = new Dictionary<string, object>
            {
                [AppClaimTypes.Subject] = user.Id.ToString(),
                [AppClaimTypes.Email] = user.Email,
                [AppClaimTypes.Name] = user.FullName,
                [AppClaimTypes.Role] = user.Role.ToString(),
                [AppClaimTypes.TokenId] = Guid.NewGuid().ToString(),
            },
            SigningCredentials = new SigningCredentials(jwt.CreateSigningKey(), SecurityAlgorithms.HmacSha256),
        };

        return new AccessTokenResult(_handler.CreateToken(descriptor), expiresAt);
    }
}
