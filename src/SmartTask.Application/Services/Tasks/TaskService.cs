using Microsoft.Extensions.Logging;
using SmartTask.Application.Common.Models;
using SmartTask.Application.Common.Responses;
using SmartTask.Application.Contracts.Identity;
using SmartTask.Application.Contracts.Persistence;
using SmartTask.Application.Contracts.Services;
using SmartTask.Application.DTOs.Tasks;
using SmartTask.Application.Services.Authorization;
using SmartTask.Domain.Entities;

namespace SmartTask.Application.Services.Tasks;

public sealed class TaskService(
    ITaskItemRepository tasks,
    IProjectRepository projects,
    IUserRepository users,
    IUnitOfWork unitOfWork,
    IProjectAccessGuard access,
    ICurrentUserService currentUser,
    TimeProvider time,
    ILogger<TaskService> logger) : ITaskService
{
    public async Task<BaseResponse<PagedResult<TaskSummaryDto>>> GetPagedAsync(TaskQueryParameters parameters, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var page = await tasks.GetPagedAsync(parameters, access.VisibilityScopeUserId, cancellationToken);
        var today = Today();
        return BaseResponse<PagedResult<TaskSummaryDto>>.Ok(page.Map(t => t.ToSummaryDto(today)));
    }

    public async Task<BaseResponse<PagedResult<TaskSummaryDto>>> GetPagedByProjectAsync(
        Guid projectId,
        TaskQueryParameters parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        if (!await access.CanViewProjectAsync(projectId, cancellationToken))
        {
            return BaseResponse<PagedResult<TaskSummaryDto>>.NotFound(ResponseMessages.ProjectNotFound);
        }

        // The route's project id always wins over a ProjectId in the query string.
        return await GetPagedAsync(parameters with { ProjectId = projectId }, cancellationToken);
    }

    public async Task<BaseResponse<TaskDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var (task, project) = await LoadVisibleAsync(id, cancellationToken);
        return task is null
            ? BaseResponse<TaskDto>.NotFound(ResponseMessages.TaskNotFound)
            : BaseResponse<TaskDto>.Ok(task.ToDto(project!.Name, Today()));
    }

    public Task<BaseResponse<TaskDto>> CreateAsync(Guid projectId, CreateTaskDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return Guarded(async () =>
        {
            var project = await projects.GetDetailsAsync(projectId, cancellationToken);
            if (project is null || !access.CanViewProject(project))
            {
                return BaseResponse<TaskDto>.NotFound(ResponseMessages.ProjectNotFound);
            }

            if (!access.CanManageProject(project))
            {
                return Denied(ResponseMessages.TaskModifyForbidden, null);
            }

            var (assignable, assignee) = await ResolveAssigneeAsync(project, dto.AssigneeId, cancellationToken);
            if (!assignable)
            {
                return BaseResponse<TaskDto>.BadRequest(ResponseMessages.AssigneeNotMember);
            }

            var task = TaskItem.Create(projectId, dto.Title, dto.Description, dto.Priority, dto.DueDate, dto.AssigneeId);
            await tasks.AddAsync(task, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Task {TaskId} created in project {ProjectId} by {UserId}", task.Id, projectId, currentUser.UserId);
            return BaseResponse<TaskDto>.Created(task.ToDto(project.Name, Today(), assignee));
        });
    }

    public Task<BaseResponse<TaskDto>> UpdateAsync(Guid id, UpdateTaskDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return Guarded(async () =>
        {
            var (task, project) = await LoadVisibleAsync(id, cancellationToken);
            if (ManageCheck(task, project) is { } failure)
            {
                return failure;
            }

            task!.UpdateDetails(dto.Title, dto.Description, dto.Priority, dto.DueDate);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Task {TaskId} updated by {UserId}", id, currentUser.UserId);
            return BaseResponse<TaskDto>.Ok(task.ToDto(project!.Name, Today()));
        });
    }

    public Task<BaseResponse> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        BusinessErrorHandling.RunAsync(
            async () =>
            {
                var (task, project) = await LoadVisibleAsync(id, cancellationToken);
                if (ManageCheck(task, project) is { } failure)
                {
                    return BaseResponse.Fail(failure.Status, failure.Message!);
                }

                tasks.Remove(task!);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                logger.LogInformation("Task {TaskId} deleted by {UserId}", id, currentUser.UserId);
                return BaseResponse.NoContent();
            },
            (status, message) => BaseResponse.Fail(status, message),
            logger);

    public Task<BaseResponse<TaskDto>> UpdateStatusAsync(Guid id, UpdateTaskStatusDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return Guarded(async () =>
        {
            var (task, project) = await LoadVisibleAsync(id, cancellationToken);
            if (task is null)
            {
                return BaseResponse<TaskDto>.NotFound(ResponseMessages.TaskNotFound);
            }

            if (!access.CanChangeTaskStatus(project!, task))
            {
                return Denied(ResponseMessages.TaskStatusForbidden, id);
            }

            // The domain enforces the transition table; an illegal move throws (→ 400).
            var previous = task.Status;
            task.ChangeStatus(dto.Status, time.GetUtcNow());
            await unitOfWork.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Task {TaskId} status {From} → {To} by {UserId}", id, previous, dto.Status, currentUser.UserId);
            return BaseResponse<TaskDto>.Ok(task.ToDto(project!.Name, Today()));
        });
    }

    public Task<BaseResponse<TaskDto>> AssignAsync(Guid id, AssignTaskDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return Guarded(async () =>
        {
            var (task, project) = await LoadVisibleAsync(id, cancellationToken);
            if (ManageCheck(task, project) is { } failure)
            {
                return failure;
            }

            var (assignable, assignee) = await ResolveAssigneeAsync(project!, dto.AssigneeId, cancellationToken);
            if (!assignable)
            {
                return BaseResponse<TaskDto>.BadRequest(ResponseMessages.AssigneeNotMember);
            }

            task!.AssignTo(dto.AssigneeId);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Task {TaskId} assigned to {AssigneeId} by {UserId}", id, dto.AssigneeId, currentUser.UserId);
            return BaseResponse<TaskDto>.Ok(task.ToDto(project!.Name, Today(), assignee));
        });
    }

    /// <summary>
    /// Loads the task with its assignee, and its project with members. Returns <c>(null, null)</c>
    /// when either is missing or the project is not visible to the current user, so the caller answers 404.
    /// </summary>
    private async Task<(TaskItem? Task, Project? Project)> LoadVisibleAsync(Guid taskId, CancellationToken cancellationToken)
    {
        var task = await tasks.GetDetailsAsync(taskId, cancellationToken);
        if (task is null)
        {
            return (null, null);
        }

        var project = await projects.GetDetailsAsync(task.ProjectId, cancellationToken);
        return project is not null && access.CanViewProject(project) ? (task, project) : (null, null);
    }

    /// <summary>404 for a missing or invisible task; 403 when the user can't manage its project.</summary>
    private BaseResponse<TaskDto>? ManageCheck(TaskItem? task, Project? project)
    {
        if (task is null || project is null)
        {
            return BaseResponse<TaskDto>.NotFound(ResponseMessages.TaskNotFound);
        }

        return access.CanManageProject(project) ? null : Denied(ResponseMessages.TaskModifyForbidden, task.Id);
    }

    /// <summary>
    /// <c>null</c> (unassigned) is always fine. Otherwise the user must be a member of the project
    /// (the owner counts) and active. Returns the loaded user, so the response can show the new
    /// assignee without relying on EF to update the task's navigation.
    /// </summary>
    private async Task<(bool Assignable, User? Assignee)> ResolveAssigneeAsync(Project project, Guid? assigneeId, CancellationToken cancellationToken)
    {
        if (assigneeId is not { } userId)
        {
            return (true, null);
        }

        if (!project.HasMember(userId))
        {
            return (false, null);
        }

        var user = await users.GetByIdAsync(userId, cancellationToken);
        return user is { IsActive: true } ? (true, user) : (false, null);
    }

    private Task<BaseResponse<TaskDto>> Guarded(Func<Task<BaseResponse<TaskDto>>> operation) =>
        BusinessErrorHandling.RunAsync(operation, (status, message) => BaseResponse<TaskDto>.Fail(status, message), logger);

    private BaseResponse<TaskDto> Denied(string message, Guid? taskId)
    {
        logger.LogWarning("User {UserId} denied on task {TaskId}: {Reason}", currentUser.UserId, taskId, message);
        return BaseResponse<TaskDto>.Forbidden(message);
    }

    private DateOnly Today() => DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
}
