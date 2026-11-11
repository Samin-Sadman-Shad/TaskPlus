using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartTask.Api.Authorization;
using SmartTask.Application.Common.Models;
using SmartTask.Application.Common.Responses;
using SmartTask.Application.Contracts.Services;
using SmartTask.Application.DTOs.Tasks;

namespace SmartTask.Api.Controllers;

/// <summary>
/// Tasks, both nested under their project (<c>projects/{projectId}/tasks</c>) and on their own
/// (<c>tasks/{id}</c>). Changing status is open to any signed-in user, because the assignee may do it.
/// The service decides who may change which task.
/// </summary>
[Route("api/v1")]
[Authorize(Roles = AppRoles.Any)]
[ProducesResponseType<BaseResponse>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<BaseResponse>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<BaseResponse>(StatusCodes.Status429TooManyRequests)]
public sealed class TasksController(ITaskService taskService) : BaseApiController
{
    /// <summary>Lists one project's tasks, paged, filtered and sorted.</summary>
    [HttpGet("projects/{projectId:guid}/tasks")]
    [ProducesResponseType<BaseResponse<PagedResult<TaskSummaryDto>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<BaseResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<BaseResponse>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BaseResponse<PagedResult<TaskSummaryDto>>>> GetPagedByProject(Guid projectId, [FromQuery] TaskQueryParameters parameters, CancellationToken cancellationToken)
        => ToActionResult(await taskService.GetPagedByProjectAsync(projectId, parameters, cancellationToken));

    /// <summary>Creates a task in the project. New tasks start as ToDo.</summary>
    [HttpPost("projects/{projectId:guid}/tasks")]
    [Authorize(Roles = AppRoles.Management)]
    [ProducesResponseType<BaseResponse<TaskDto>>(StatusCodes.Status201Created)]
    [ProducesResponseType<BaseResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<BaseResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<BaseResponse>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BaseResponse<TaskDto>>> Create(Guid projectId, CreateTaskDto dto, CancellationToken cancellationToken)
        => ToCreatedResult(await taskService.CreateAsync(projectId, dto, cancellationToken), nameof(GetById), t => new { id = t.Id });

    /// <summary>Lists tasks across all projects visible to the caller, paged, filtered and sorted.</summary>
    [HttpGet("tasks")]
    [ProducesResponseType<BaseResponse<PagedResult<TaskSummaryDto>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<BaseResponse>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BaseResponse<PagedResult<TaskSummaryDto>>>> GetPaged([FromQuery] TaskQueryParameters parameters, CancellationToken cancellationToken)
        => ToActionResult(await taskService.GetPagedAsync(parameters, cancellationToken));

    /// <summary>Gets a task with its project name and assignee.</summary>
    [HttpGet("tasks/{id:guid}")]
    [ProducesResponseType<BaseResponse<TaskDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<BaseResponse>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BaseResponse<TaskDto>>> GetById(Guid id, CancellationToken cancellationToken)
        => ToActionResult(await taskService.GetByIdAsync(id, cancellationToken));

    /// <summary>Replaces a task's title, description, priority and due date.</summary>
    [HttpPut("tasks/{id:guid}")]
    [Authorize(Roles = AppRoles.Management)]
    [ProducesResponseType<BaseResponse<TaskDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<BaseResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<BaseResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<BaseResponse>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BaseResponse<TaskDto>>> Update(Guid id, UpdateTaskDto dto, CancellationToken cancellationToken)
        => ToActionResult(await taskService.UpdateAsync(id, dto, cancellationToken));

    /// <summary>Deletes a task (soft delete).</summary>
    [HttpDelete("tasks/{id:guid}")]
    [Authorize(Roles = AppRoles.Management)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<BaseResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<BaseResponse>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BaseResponse>> Delete(Guid id, CancellationToken cancellationToken)
        => ToActionResult(await taskService.DeleteAsync(id, cancellationToken));

    /// <summary>Moves a task to a new status, if the transition rules allow it. Admins, the project owner and the assignee may do this.</summary>
    [HttpPatch("tasks/{id:guid}/status")]
    [ProducesResponseType<BaseResponse<TaskDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<BaseResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<BaseResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<BaseResponse>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BaseResponse<TaskDto>>> UpdateStatus(Guid id, UpdateTaskStatusDto dto, CancellationToken cancellationToken)
        => ToActionResult(await taskService.UpdateStatusAsync(id, dto, cancellationToken));

    /// <summary>Assigns the task to a project member, or unassigns it when the assignee id is null.</summary>
    [HttpPatch("tasks/{id:guid}/assignee")]
    [Authorize(Roles = AppRoles.Management)]
    [ProducesResponseType<BaseResponse<TaskDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<BaseResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<BaseResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<BaseResponse>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BaseResponse<TaskDto>>> Assign(Guid id, AssignTaskDto dto, CancellationToken cancellationToken)
        => ToActionResult(await taskService.AssignAsync(id, dto, cancellationToken));
}
