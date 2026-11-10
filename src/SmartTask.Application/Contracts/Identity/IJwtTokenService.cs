using SmartTask.Application.Common.Models.Auth;
using SmartTask.Domain.Entities;

namespace SmartTask.Application.Contracts.Identity;

public interface IJwtTokenService
{
    /// <summary>
    /// Creates a signed, short-lived access token with the claims <c>sub</c>, <c>email</c>, <c>name</c>,
    /// <c>role</c>, <c>jti</c> and <c>iat</c>.
    /// </summary>
    AccessTokenResult CreateAccessToken(User user);
}
