using SmartTask.Application.Common.Models;
using SmartTask.Domain.Enums;

namespace SmartTask.Application.Contracts.Persistence;

/// <summary>Read-only dashboard queries. Every figure is computed in the database.</summary>
public interface IDashboardReadRepository
{
    /// <summary>
    /// Computes the dashboard for a role's scope:
    /// <list type="bullet">
    /// <item><see cref="UserRole.Admin"/>: every project and task (<paramref name="scopeUserId"/> is ignored).</item>
    /// <item><see cref="UserRole.ProjectManager"/>: projects owned by <paramref name="scopeUserId"/> and their tasks.</item>
    /// <item><see cref="UserRole.TeamMember"/>: projects the user belongs to and the tasks assigned to them.</item>
    /// </list>
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="scopeUserId"/> is <c>null</c> for a non-admin role.</exception>
    Task<DashboardStatsReadModel> GetStatsAsync(
        Guid? scopeUserId,
        UserRole role,
        DateOnly today,
        CancellationToken cancellationToken = default);
}
