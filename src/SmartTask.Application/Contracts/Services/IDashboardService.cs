using SmartTask.Application.Common.Responses;
using SmartTask.Application.DTOs.Dashboard;

namespace SmartTask.Application.Contracts.Services;

/// <summary>Use case behind <c>DashboardController</c>.</summary>
public interface IDashboardService
{
    /// <summary>
    /// <c>GET dashboard?upcomingDays=</c>: 200. Scope: global for an admin, owned projects for a project
    /// manager, assigned tasks for a team member. <paramref name="upcomingDays"/> is clamped to
    /// <c>DashboardConstraints.MinUpcomingDays</c> to <c>MaxUpcomingDays</c>.
    /// </summary>
    Task<BaseResponse<DashboardStatsDto>> GetStatsAsync(int upcomingDays, CancellationToken cancellationToken = default);
}
