using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SmartTask.Api.Authorization;
using SmartTask.Api.RateLimiting;
using SmartTask.Application.Common.Models;
using SmartTask.Application.Common.Responses;
using SmartTask.Application.Contracts.Services;
using SmartTask.Application.DTOs.Auth;
using SmartTask.Application.DTOs.Users;

namespace SmartTask.Api.Controllers;

/// <summary>The current user's profile, plus user administration for admins.</summary>
[Route("api/v1/users")]
[Authorize(Roles = AppRoles.Any)]
[ProducesResponseType<BaseResponse>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<BaseResponse>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<BaseResponse>(StatusCodes.Status429TooManyRequests)]
public sealed class UsersController(IUserService userService, IAuthService authService) : BaseApiController
{
    /// <summary>Gets the signed-in user's profile.</summary>
    [HttpGet("me")]
    [AllowedWhilePasswordChangeRequired]
    [ProducesResponseType<BaseResponse<UserDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<BaseResponse>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BaseResponse<UserDto>>> GetCurrent(CancellationToken cancellationToken)
        => ToActionResult(await userService.GetCurrentAsync(cancellationToken));

    /// <summary>
    /// Changes the caller's password. The current password is required. Every other session is signed
    /// out; this one gets a new access token and refresh cookie. Required first for a user whose password
    /// an admin set (<c>mustChangePassword</c>); every other endpoint answers 403 <c>PASSWORD_CHANGE_REQUIRED</c> until then.
    /// </summary>
    [HttpPost("me/password")]
    [AllowedWhilePasswordChangeRequired]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType<BaseResponse<AuthResponseDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<BaseResponse>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BaseResponse<AuthResponseDto>>> ChangePassword(ChangePasswordDto dto, CancellationToken cancellationToken)
        => ToActionResult(await authService.ChangePasswordAsync(dto, cancellationToken));

    /// <summary>Updates the signed-in user's first and last name.</summary>
    [HttpPut("me")]
    [ProducesResponseType<BaseResponse<UserDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<BaseResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<BaseResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<BaseResponse>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BaseResponse<UserDto>>> UpdateCurrent(UpdateUserProfileDto dto, CancellationToken cancellationToken)
        => ToActionResult(await userService.UpdateCurrentProfileAsync(dto, cancellationToken));

    /// <summary>Lists users, paged, filtered and sorted (admin only).</summary>
    [HttpGet]
    [Authorize(Roles = AppRoles.Admin)]
    [ProducesResponseType<BaseResponse<PagedResult<UserDto>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<BaseResponse>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BaseResponse<PagedResult<UserDto>>>> GetPaged([FromQuery] UserQueryParameters parameters, CancellationToken cancellationToken)
        => ToActionResult(await userService.GetPagedAsync(parameters, cancellationToken));

    /// <summary>
    /// Creates an account with any role and an initial password. Self-registration only creates team
    /// members; this is how an admin adds project managers or other admins directly.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = AppRoles.Admin)]
    [ProducesResponseType<BaseResponse<UserDto>>(StatusCodes.Status201Created)]
    [ProducesResponseType<BaseResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<BaseResponse>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BaseResponse<UserDto>>> Create(CreateUserDto dto, CancellationToken cancellationToken)
        => ToCreatedResult(await userService.CreateAsync(dto, cancellationToken), nameof(GetById), u => new { id = u.Id });

    /// <summary>Finds active users by name or email, for member and assignee pickers (up to 20 results).</summary>
    [HttpGet("lookup")]
    [Authorize(Roles = AppRoles.Management)]
    [ProducesResponseType<BaseResponse<IReadOnlyList<UserSummaryDto>>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<BaseResponse<IReadOnlyList<UserSummaryDto>>>> Lookup([FromQuery] string? search, CancellationToken cancellationToken)
        => ToActionResult(await userService.LookupAsync(search, cancellationToken));

    /// <summary>Gets a user by id (admin only).</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = AppRoles.Admin)]
    [ProducesResponseType<BaseResponse<UserDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<BaseResponse>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BaseResponse<UserDto>>> GetById(Guid id, CancellationToken cancellationToken)
        => ToActionResult(await userService.GetByIdAsync(id, cancellationToken));

    /// <summary>Changes a user's role (admin only). Admins cannot demote themselves.</summary>
    [HttpPatch("{id:guid}/role")]
    [Authorize(Roles = AppRoles.Admin)]
    [ProducesResponseType<BaseResponse<UserDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<BaseResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<BaseResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<BaseResponse>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BaseResponse<UserDto>>> ChangeRole(Guid id, ChangeUserRoleDto dto, CancellationToken cancellationToken)
        => ToActionResult(await userService.ChangeRoleAsync(id, dto, cancellationToken));

    /// <summary>Re-activates a deactivated user (admin only).</summary>
    [HttpPost("{id:guid}/activate")]
    [Authorize(Roles = AppRoles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<BaseResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<BaseResponse>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BaseResponse>> Activate(Guid id, CancellationToken cancellationToken)
        => ToActionResult(await userService.ActivateAsync(id, cancellationToken));

    /// <summary>Deactivates a user and revokes their refresh tokens (admin only). Admins cannot deactivate themselves.</summary>
    [HttpPost("{id:guid}/deactivate")]
    [Authorize(Roles = AppRoles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<BaseResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<BaseResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<BaseResponse>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BaseResponse>> Deactivate(Guid id, CancellationToken cancellationToken)
        => ToActionResult(await userService.DeactivateAsync(id, cancellationToken));
}
