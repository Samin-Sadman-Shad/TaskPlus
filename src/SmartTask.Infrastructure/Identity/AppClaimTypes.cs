using Microsoft.IdentityModel.JsonWebTokens;

namespace SmartTask.Infrastructure.Identity;

/// <summary>
/// The JWT claim names written by <see cref="JwtTokenService"/> and read by the API. Inbound claim
/// mapping is turned off, so these short names reach <c>ClaimsPrincipal</c> unchanged.
/// </summary>
public static class AppClaimTypes
{
    public const string Subject = JwtRegisteredClaimNames.Sub;
    public const string Email = JwtRegisteredClaimNames.Email;
    public const string Name = JwtRegisteredClaimNames.Name;
    public const string Role = "role";
    public const string TokenId = JwtRegisteredClaimNames.Jti;
}
