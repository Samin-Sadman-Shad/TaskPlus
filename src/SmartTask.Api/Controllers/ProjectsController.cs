using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartTask.Api.Authorization;
using SmartTask.Application.Common.Models;
using SmartTask.Application.Common.Responses;
using SmartTask.Application.Contracts.Services;
using SmartTask.Application.DTOs.Projects;

namespace SmartTask.Api.Controllers;

/// <summary>
/// Projects and their members. Write actions require the <c>ProjectManagement</c> policy (admin or
/// project manager). The service then checks that a project manager owns the project.
/// </summary>
[Route("api/v1/projects")]
[ProducesResponseType<BaseResponse>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<BaseResponse>(StatusCodes.Status429TooManyRequests)]
public sealed class ProjectsController(IProjectService projectService) : BaseApiController
{
    /// <summary>Lists the projects visible to the caller: all of them for admins, otherwise those they own or belong to.</summary>
    [HttpGet]
    [ProducesResponseType<BaseResponse<PagedResult<ProjectSummaryDto>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<BaseResponse>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BaseResponse<PagedResult<ProjectSummaryDto>>>> GetPaged([FromQuery] ProjectQueryParameters parameters, CancellationToken cancellationToken)
        => ToActionResult(await projectService.GetPagedAsync(parameters, cancellationToken));

    /// <summary>Gets a project with its owner, members and task counts.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<BaseResponse<ProjectDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<BaseResponse>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BaseResponse<ProjectDto>>> GetById(Guid id, CancellationToken cancellationToken)
        => ToActionResult(await projectService.GetByIdAsync(id, cancellationToken));

    /// <summary>Creates a project owned by the caller.</summary>
    [HttpPost]
    [Authorize(Policy = Policies.ProjectManagement)]
    [ProducesResponseType<BaseResponse<ProjectDto>>(StatusCodes.Status201Created)]
    [ProducesResponseType<BaseResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<BaseResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<BaseResponse>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BaseResponse<ProjectDto>>> Create(CreateProjectDto dto, CancellationToken cancellationToken)
        => ToCreatedResult(await projectService.CreateAsync(dto, cancellationToken), nameof(GetById), p => new { id = p.Id });

    /// <summary>Replaces a project's name, description and dates.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.ProjectManagement)]
    [ProducesResponseType<BaseResponse<ProjectDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<BaseResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<BaseResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<BaseResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<BaseResponse>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BaseResponse<ProjectDto>>> Update(Guid id, UpdateProjectDto dto, CancellationToken cancellationToken)
        => ToActionResult(await projectService.UpdateAsync(id, dto, cancellationToken));

    /// <summary>Deletes a project and its tasks (soft delete).</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.ProjectManagement)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<BaseResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<BaseResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<BaseResponse>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BaseResponse>> Delete(Guid id, CancellationToken cancellationToken)
        => ToActionResult(await projectService.DeleteAsync(id, cancellationToken));

    /// <summary>Adds an active user to the project. Adding an existing member does nothing.</summary>
    [HttpPost("{id:guid}/members")]
    [Authorize(Policy = Policies.ProjectManagement)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<BaseResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<BaseResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<BaseResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<BaseResponse>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BaseResponse>> AddMember(Guid id, AddProjectMemberDto dto, CancellationToken cancellationToken)
        => ToActionResult(await projectService.AddMemberAsync(id, dto, cancellationToken));

    /// <summary>Removes a member from the project. The owner cannot be removed.</summary>
    [HttpDelete("{id:guid}/members/{userId:guid}")]
    [Authorize(Policy = Policies.ProjectManagement)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<BaseResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<BaseResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<BaseResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<BaseResponse>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BaseResponse>> RemoveMember(Guid id, Guid userId, CancellationToken cancellationToken)
        => ToActionResult(await projectService.RemoveMemberAsync(id, userId, cancellationToken));
}
