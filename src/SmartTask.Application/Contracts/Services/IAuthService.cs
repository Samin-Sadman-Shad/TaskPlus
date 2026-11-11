using SmartTask.Application.Common.Responses;
using SmartTask.Application.DTOs.Auth;
using SmartTask.Application.DTOs.Users;

namespace SmartTask.Application.Contracts.Services;

/// <summary>
/// Use cases behind <c>AuthController</c>. The refresh token never appears in a request or response
/// body. It is read and written through <c>IRefreshTokenTransport</c> (an HttpOnly cookie).
/// </summary>
public interface IAuthService
{
    /// <summary><c>POST auth/register</c>: 201, 400, 409 (email taken). The new user is always a <c>TeamMember</c>.</summary>
    Task<BaseResponse<UserDto>> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken = default);

    /// <summary><c>POST auth/login</c>: 200 with a new refresh cookie, or 401 "Invalid email or password." (unknown email, wrong password or inactive user).</summary>
    Task<BaseResponse<AuthResponseDto>> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>POST auth/refresh</c>: 200 with a rotated refresh cookie, or 401 for a missing, unknown,
    /// expired or reused token. Reusing a revoked token revokes all of that user's sessions.
    /// </summary>
    Task<BaseResponse<AuthResponseDto>> RefreshAsync(CancellationToken cancellationToken = default);

    /// <summary><c>POST auth/logout</c>: always 204. Revokes the cookie's token if it is active and clears the cookie. Idempotent.</summary>
    Task<BaseResponse> LogoutAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>POST users/me/password</c>: 200 with new tokens and a new refresh cookie, 400 (wrong current
    /// password, or the new one breaks the policy), 401. Every other session is signed out, and an
    /// admin-set password stops being temporary (<c>MustChangePassword</c> becomes false).
    /// </summary>
    Task<BaseResponse<AuthResponseDto>> ChangePasswordAsync(ChangePasswordDto request, CancellationToken cancellationToken = default);
}
