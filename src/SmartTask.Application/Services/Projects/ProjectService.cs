using Microsoft.Extensions.Logging;
using SmartTask.Application.Common.Models;
using SmartTask.Application.Common.Responses;
using SmartTask.Application.Contracts.Identity;
using SmartTask.Application.Contracts.Persistence;
using SmartTask.Application.Contracts.Services;
using SmartTask.Application.DTOs.Projects;
using SmartTask.Application.Services.Authorization;
using SmartTask.Domain.Entities;

namespace SmartTask.Application.Services.Projects;

public sealed class ProjectService(
    IProjectRepository projects,
    ITaskItemRepository tasks,
    IUserRepository users,
    IUnitOfWork unitOfWork,
    IProjectAccessGuard access,
    ICurrentUserService currentUser,
    TimeProvider time,
    ILogger<ProjectService> logger) : IProjectService
{
    public async Task<BaseResponse<PagedResult<ProjectSummaryDto>>> GetPagedAsync(ProjectQueryParameters parameters, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var page = await projects.GetPagedAsync(parameters, access.VisibilityScopeUserId, cancellationToken);
        return BaseResponse<PagedResult<ProjectSummaryDto>>.Ok(page.Map(p => p.ToSummaryDto()));
    }

    public async Task<BaseResponse<ProjectDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var project = await projects.GetDetailsAsync(id, cancellationToken);
        if (project is null || !access.CanViewProject(project))
        {
            return BaseResponse<ProjectDto>.NotFound(ResponseMessages.ProjectNotFound);
        }

        return BaseResponse<ProjectDto>.Ok(await ToDtoAsync(project, cancellationToken));
    }

    public Task<BaseResponse<ProjectDto>> CreateAsync(CreateProjectDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return BusinessErrorHandling.RunAsync(
            async () =>
            {
                if (currentUser.UserId is not { } ownerId)
                {
                    return BaseResponse<ProjectDto>.Unauthorized(ResponseMessages.AuthenticationRequired);
                }

                if (!access.HasManagementRole)
                {
                    return Denied<ProjectDto>(ResponseMessages.ManagerOnly, null);
                }

                if (await projects.NameExistsForOwnerAsync(ownerId, dto.Name, null, cancellationToken))
                {
                    return BaseResponse<ProjectDto>.Conflict(ResponseMessages.ProjectNameTaken);
                }

                // The owner always counts as a member (Project.HasMember), so no member row is added.
                var project = Project.Create(dto.Name, dto.Description, dto.StartDate, dto.EndDate, ownerId);
                await projects.AddAsync(project, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                logger.LogInformation("Project {ProjectId} created by {UserId}", project.Id, ownerId);

                var created = await projects.GetDetailsAsync(project.Id, cancellationToken) ?? project;
                return BaseResponse<ProjectDto>.Created(created.ToDto(0, 0));
            },
            (status, message) => BaseResponse<ProjectDto>.Fail(status, message),
            logger);
    }

    public Task<BaseResponse<ProjectDto>> UpdateAsync(Guid id, UpdateProjectDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return BusinessErrorHandling.RunAsync(
            async () =>
            {
                var project = await projects.GetDetailsAsync(id, cancellationToken);
                if (ManageCheck<ProjectDto>(project, id) is { } failure)
                {
                    return failure;
                }

                if (await projects.NameExistsForOwnerAsync(project!.OwnerId, dto.Name, project.Id, cancellationToken))
                {
                    return BaseResponse<ProjectDto>.Conflict(ResponseMessages.ProjectNameTaken);
                }

                project.Update(dto.Name, dto.Description, dto.StartDate, dto.EndDate);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                logger.LogInformation("Project {ProjectId} updated by {UserId}", id, currentUser.UserId);
                return BaseResponse<ProjectDto>.Ok(await ToDtoAsync(project, cancellationToken));
            },
            (status, message) => BaseResponse<ProjectDto>.Fail(status, message),
            logger);
    }

    public Task<BaseResponse> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        BusinessErrorHandling.RunAsync(
            async () =>
            {
                var project = await projects.GetDetailsAsync(id, cancellationToken);
                if (ManageCheck(project, id) is { } failure)
                {
                    return failure;
                }

                // Soft-delete the tasks in the same save, so a deleted project never leaves live tasks behind.
                var projectTasks = await tasks.GetByProjectAsync(id, cancellationToken);
                foreach (var task in projectTasks)
                {
                    tasks.Remove(task);
                }

                projects.Remove(project!);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                logger.LogInformation("Project {ProjectId} and {TaskCount} tasks deleted by {UserId}", id, projectTasks.Count, currentUser.UserId);
                return BaseResponse.NoContent();
            },
            (status, message) => BaseResponse.Fail(status, message),
            logger);

    public Task<BaseResponse> AddMemberAsync(Guid projectId, AddProjectMemberDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return BusinessErrorHandling.RunAsync(
            async () =>
            {
                var project = await projects.GetDetailsAsync(projectId, cancellationToken);
                if (ManageCheck(project, projectId) is { } failure)
                {
                    return failure;
                }

                var user = await users.GetByIdAsync(dto.UserId, cancellationToken);
                if (user is not { IsActive: true })
                {
                    return BaseResponse.BadRequest(ResponseMessages.UserNotFoundOrInactive);
                }

                project!.AddMember(user.Id, time.GetUtcNow());
                await unitOfWork.SaveChangesAsync(cancellationToken);

                logger.LogInformation("User {MemberId} added to project {ProjectId} by {UserId}", user.Id, projectId, currentUser.UserId);
                return BaseResponse.NoContent();
            },
            (status, message) => BaseResponse.Fail(status, message),
            logger);
    }

    public Task<BaseResponse> RemoveMemberAsync(Guid projectId, Guid userId, CancellationToken cancellationToken = default) =>
        BusinessErrorHandling.RunAsync(
            async () =>
            {
                var project = await projects.GetDetailsAsync(projectId, cancellationToken);
                if (ManageCheck(project, projectId) is { } failure)
                {
                    return failure;
                }

                if (!project!.HasMember(userId))
                {
                    return BaseResponse.NotFound(ResponseMessages.MemberNotFound);
                }

                // Throws DomainException (400) for the owner.
                project.RemoveMember(userId);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                logger.LogInformation("User {MemberId} removed from project {ProjectId} by {UserId}", userId, projectId, currentUser.UserId);
                return BaseResponse.NoContent();
            },
            (status, message) => BaseResponse.Fail(status, message),
            logger);

    private async Task<ProjectDto> ToDtoAsync(Project project, CancellationToken cancellationToken)
    {
        var counts = await tasks.GetCountsByProjectAsync(project.Id, cancellationToken);
        return project.ToDto(counts.Total, counts.Completed);
    }

    /// <summary>404 when the project is missing or not visible; 403 when it is visible but the user can't manage it.</summary>
    private BaseResponse<T>? ManageCheck<T>(Project? project, Guid projectId)
    {
        if (project is null || !access.CanViewProject(project))
        {
            return BaseResponse<T>.NotFound(ResponseMessages.ProjectNotFound);
        }

        return access.CanManageProject(project) ? null : Denied<T>(ResponseMessages.ProjectModifyForbidden, projectId);
    }

    /// <inheritdoc cref="ManageCheck{T}"/>
    private BaseResponse? ManageCheck(Project? project, Guid projectId)
    {
        if (project is null || !access.CanViewProject(project))
        {
            return BaseResponse.NotFound(ResponseMessages.ProjectNotFound);
        }

        if (access.CanManageProject(project))
        {
            return null;
        }

        LogDenied(ResponseMessages.ProjectModifyForbidden, projectId);
        return BaseResponse.Forbidden(ResponseMessages.ProjectModifyForbidden);
    }

    private BaseResponse<T> Denied<T>(string message, Guid? projectId)
    {
        LogDenied(message, projectId);
        return BaseResponse<T>.Forbidden(message);
    }

    private void LogDenied(string message, Guid? projectId) =>
        logger.LogWarning("User {UserId} denied on project {ProjectId}: {Reason}", currentUser.UserId, projectId, message);
}
