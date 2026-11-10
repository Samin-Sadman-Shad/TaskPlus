using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SmartTask.Api.RateLimiting;
using SmartTask.Application.Common.Responses;
using SmartTask.Application.Contracts.Services;
using SmartTask.Application.DTOs.Auth;
using SmartTask.Application.DTOs.Users;

namespace SmartTask.Api.Controllers;

/// <summary>
/// Registration and session endpoints. They are anonymous because a caller has no valid access token
/// yet, or it has expired. The refresh token travels only in the <c>stms_refresh</c> HttpOnly cookie.
/// </summary>
[Route("api/v1/auth")]
[AllowAnonymous]
public sealed class AuthController(IAuthService authService) : BaseApiController
{
    /// <summary>Creates a new account. New accounts are always team members.</summary>
    [HttpPost("register")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType<BaseResponse<UserDto>>(StatusCodes.Status201Created)]
    [ProducesResponseType<BaseResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<BaseResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<BaseResponse>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<BaseResponse<UserDto>>> Register(RegisterRequestDto request, CancellationToken cancellationToken)
        => ToActionResult(await authService.RegisterAsync(request, cancellationToken));

    /// <summary>Signs in. Returns an access token and sets the refresh-token cookie.</summary>
    [HttpPost("login")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType<BaseResponse<AuthResponseDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<BaseResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<BaseResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<BaseResponse>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<BaseResponse<AuthResponseDto>>> Login(LoginRequestDto request, CancellationToken cancellationToken)
        => ToActionResult(await authService.LoginAsync(request, cancellationToken));

    /// <summary>Exchanges the refresh-token cookie for a new access token, and rotates the cookie.</summary>
    [HttpPost("refresh")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType<BaseResponse<AuthResponseDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<BaseResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<BaseResponse>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<BaseResponse<AuthResponseDto>>> Refresh(CancellationToken cancellationToken)
        => ToActionResult(await authService.RefreshAsync(cancellationToken));

    /// <summary>Signs out: revokes the refresh token and clears its cookie. Always succeeds.</summary>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<BaseResponse>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<BaseResponse>> Logout(CancellationToken cancellationToken)
        => ToActionResult(await authService.LogoutAsync(cancellationToken));
}
