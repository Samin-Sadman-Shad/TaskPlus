using SmartTask.Application.Common.Models;
using SmartTask.Application.Common.Responses;
using SmartTask.Application.DTOs.Tasks;

namespace SmartTask.Application.Contracts.Services;

/// <summary>
/// Use cases behind <c>TasksController</c>. A task is visible when its project is. Otherwise the
/// result is 404, never 403.
/// </summary>
public interface ITaskService
{
    /// <summary><c>GET tasks</c>: 200. Scoped to tasks in visible projects.</summary>
    Task<BaseResponse<PagedResult<TaskSummaryDto>>> GetPagedAsync(TaskQueryParameters parameters, CancellationToken cancellationToken = default);

    /// <summary><c>GET projects/{projectId}/tasks</c>: 200, 404.</summary>
    Task<BaseResponse<PagedResult<TaskSummaryDto>>> GetPagedByProjectAsync(Guid projectId, TaskQueryParameters parameters, CancellationToken cancellationToken = default);

    /// <summary><c>GET tasks/{id}</c>: 200, 404.</summary>
    Task<BaseResponse<TaskDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary><c>POST projects/{projectId}/tasks</c> (admin, owner): 201, 400 (assignee not a member), 403, 404, 409.</summary>
    Task<BaseResponse<TaskDto>> CreateAsync(Guid projectId, CreateTaskDto dto, CancellationToken cancellationToken = default);

    /// <summary><c>PUT tasks/{id}</c> (admin, owner): 200, 400, 403, 404, 409.</summary>
    Task<BaseResponse<TaskDto>> UpdateAsync(Guid id, UpdateTaskDto dto, CancellationToken cancellationToken = default);

    /// <summary><c>DELETE tasks/{id}</c> (admin, owner): 204, 403, 404, 409.</summary>
    Task<BaseResponse> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary><c>PATCH tasks/{id}/status</c> (admin, owner, assignee): 200, 400 (illegal transition), 403, 404, 409.</summary>
    Task<BaseResponse<TaskDto>> UpdateStatusAsync(Guid id, UpdateTaskStatusDto dto, CancellationToken cancellationToken = default);

    /// <summary><c>PATCH tasks/{id}/assignee</c> (admin, owner): 200, 400 (assignee not a member), 403, 404, 409.</summary>
    Task<BaseResponse<TaskDto>> AssignAsync(Guid id, AssignTaskDto dto, CancellationToken cancellationToken = default);
}
