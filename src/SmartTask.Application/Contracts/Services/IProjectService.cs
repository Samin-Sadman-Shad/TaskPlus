using SmartTask.Application.Common.Models;
using SmartTask.Application.Common.Responses;
using SmartTask.Application.DTOs.Projects;

namespace SmartTask.Application.Contracts.Services;

/// <summary>
/// Use cases behind <c>ProjectsController</c>. A project the current user may not see returns 404,
/// never 403, so its existence is not revealed.
/// </summary>
public interface IProjectService
{
    /// <summary><c>GET projects</c>: 200. Scoped to visible projects (all of them for an admin).</summary>
    Task<BaseResponse<PagedResult<ProjectSummaryDto>>> GetPagedAsync(ProjectQueryParameters parameters, CancellationToken cancellationToken = default);

    /// <summary><c>GET projects/{id}</c>: 200, 404.</summary>
    Task<BaseResponse<ProjectDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary><c>POST projects</c> (admin, project manager): 201, 400, 401, 403, 409 (duplicate name).</summary>
    Task<BaseResponse<ProjectDto>> CreateAsync(CreateProjectDto dto, CancellationToken cancellationToken = default);

    /// <summary><c>PUT projects/{id}</c> (admin, owner): 200, 400, 403, 404, 409 (duplicate name or concurrent edit).</summary>
    Task<BaseResponse<ProjectDto>> UpdateAsync(Guid id, UpdateProjectDto dto, CancellationToken cancellationToken = default);

    /// <summary><c>DELETE projects/{id}</c> (admin, owner): 204, 403, 404, 409. Soft-deletes the project and its tasks together.</summary>
    Task<BaseResponse> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary><c>POST projects/{id}/members</c> (admin, owner): 204, 400 (unknown or inactive user), 403, 404, 409. Idempotent.</summary>
    Task<BaseResponse> AddMemberAsync(Guid projectId, AddProjectMemberDto dto, CancellationToken cancellationToken = default);

    /// <summary><c>DELETE projects/{id}/members/{userId}</c> (admin, owner): 204, 400 (owner), 403, 404, 409.</summary>
    Task<BaseResponse> RemoveMemberAsync(Guid projectId, Guid userId, CancellationToken cancellationToken = default);
}
