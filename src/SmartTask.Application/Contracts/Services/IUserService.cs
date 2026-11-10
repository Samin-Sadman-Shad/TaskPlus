using SmartTask.Application.Common.Models;
using SmartTask.Application.Common.Responses;
using SmartTask.Application.DTOs.Users;

namespace SmartTask.Application.Contracts.Services;

/// <summary>Use cases behind <c>UsersController</c>. All methods act as the current user.</summary>
public interface IUserService
{
    /// <summary><c>GET users/me</c>: 200, 401, 404.</summary>
    Task<BaseResponse<UserDto>> GetCurrentAsync(CancellationToken cancellationToken = default);

    /// <summary><c>PUT users/me</c>: 200, 400, 401, 404, 409.</summary>
    Task<BaseResponse<UserDto>> UpdateCurrentProfileAsync(UpdateUserProfileDto dto, CancellationToken cancellationToken = default);

    /// <summary><c>GET users</c> (admin): 200, 403.</summary>
    Task<BaseResponse<PagedResult<UserDto>>> GetPagedAsync(UserQueryParameters parameters, CancellationToken cancellationToken = default);

    /// <summary><c>GET users/lookup?search=</c> (admin, project manager): 200, 403. Active users only, at most <c>PagingConstraints.LookupMaxResults</c>.</summary>
    Task<BaseResponse<IReadOnlyList<UserSummaryDto>>> LookupAsync(string? search, CancellationToken cancellationToken = default);

    /// <summary><c>GET users/{id}</c> (admin): 200, 403, 404.</summary>
    Task<BaseResponse<UserDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary><c>PATCH users/{id}/role</c> (admin): 200, 400 (self-demotion), 403, 404, 409.</summary>
    Task<BaseResponse<UserDto>> ChangeRoleAsync(Guid id, ChangeUserRoleDto dto, CancellationToken cancellationToken = default);

    /// <summary><c>POST users/{id}/activate</c> (admin): 204, 403, 404, 409.</summary>
    Task<BaseResponse> ActivateAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary><c>POST users/{id}/deactivate</c> (admin): 204, 400 (self), 403, 404, 409. Also revokes the user's refresh tokens.</summary>
    Task<BaseResponse> DeactivateAsync(Guid id, CancellationToken cancellationToken = default);
}
