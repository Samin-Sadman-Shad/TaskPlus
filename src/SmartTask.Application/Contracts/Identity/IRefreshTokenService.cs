using SmartTask.Application.Common.Models.Auth;

namespace SmartTask.Application.Contracts.Identity;

public interface IRefreshTokenService
{
    /// <summary>Creates a new random refresh token, its hash (the only part that is stored) and its expiry.</summary>
    GeneratedRefreshToken Generate();

    /// <summary>Hashes a raw token the same way <see cref="Generate"/> does, to look up a presented token.</summary>
    string Hash(string rawToken);
}
