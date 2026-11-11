using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartTask.Api.Authorization;
using SmartTask.Application.Common.Responses;
using SmartTask.Application.Contracts.Services;
using SmartTask.Application.DTOs.Dashboard;
using SmartTask.Domain.Constants;

namespace SmartTask.Api.Controllers;

/// <summary>Dashboard statistics, scoped to the caller's role.</summary>
[Route("api/v1/dashboard")]
[Authorize(Roles = AppRoles.Any)]
[ProducesResponseType<BaseResponse>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<BaseResponse>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<BaseResponse>(StatusCodes.Status429TooManyRequests)]
public sealed class DashboardController(IDashboardService dashboardService) : BaseApiController
{
    /// <summary>
    /// Gets task counts by status and priority, overdue and pending counts, and the tasks due in the
    /// next <paramref name="upcomingDays"/> days (clamped to 1–30). Admins see everything, project
    /// managers their own projects, team members their own tasks.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<BaseResponse<DashboardStatsDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<BaseResponse<DashboardStatsDto>>> Get([FromQuery] int upcomingDays = DashboardConstraints.DefaultUpcomingDays, CancellationToken cancellationToken = default)
        => ToActionResult(await dashboardService.GetStatsAsync(upcomingDays, cancellationToken));
}
