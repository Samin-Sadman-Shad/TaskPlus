using SmartTask.Application.Common.Responses;
using SmartTask.Application.Contracts.Identity;
using SmartTask.Application.Contracts.Persistence;
using SmartTask.Application.Contracts.Services;
using SmartTask.Application.DTOs.Dashboard;
using SmartTask.Application.Services.Authorization;
using SmartTask.Domain.Constants;
using SmartTask.Domain.Enums;

namespace SmartTask.Application.Services.Dashboard;

public sealed class DashboardService(
    IDashboardReadRepository dashboard,
    IProjectAccessGuard access,
    ICurrentUserService currentUser,
    TimeProvider time) : IDashboardService
{
    public async Task<BaseResponse<DashboardStatsDto>> GetStatsAsync(int upcomingDays, CancellationToken cancellationToken = default)
    {
        if (currentUser.Role is not { } role)
        {
            return BaseResponse<DashboardStatsDto>.Unauthorized(ResponseMessages.AuthenticationRequired);
        }

        var today = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
        var days = Math.Clamp(upcomingDays, DashboardConstraints.MinUpcomingDays, DashboardConstraints.MaxUpcomingDays);

        // The guard returns null for admins (global), otherwise the current user id. The repository
        // narrows that to owned projects (manager) or assigned tasks (team member).
        var stats = await dashboard.GetStatsAsync(access.VisibilityScopeUserId, role, today, days, cancellationToken);

        // ToDto fills every status and priority key, with 0 where there are no tasks.
        return BaseResponse<DashboardStatsDto>.Ok(stats.ToDto(today));
    }
}
