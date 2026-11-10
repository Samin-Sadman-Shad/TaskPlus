using SmartTask.Application.DTOs.Tasks;
using SmartTask.Domain.Enums;

namespace SmartTask.Application.DTOs.Dashboard;

/// <summary>
/// Response of <c>GET dashboard</c>. The scope depends on the caller's role: global for admins, own
/// projects for project managers, own tasks for team members.
/// </summary>
public sealed record DashboardStatsDto
{
    public required int TotalProjects { get; init; }

    public required int TotalTasks { get; init; }

    /// <summary>Has every <see cref="TaskItemStatus"/> as a key (0 when empty), so the client chart keeps a fixed shape.</summary>
    public required IReadOnlyDictionary<TaskItemStatus, int> TasksByStatus { get; init; }

    /// <summary>Has every <see cref="TaskPriority"/> as a key (0 when empty), so the client chart keeps a fixed shape.</summary>
    public required IReadOnlyDictionary<TaskPriority, int> TasksByPriority { get; init; }

    public required int CompletedCount { get; init; }

    /// <summary>Tasks in <see cref="TaskItemStatus.ToDo"/> or <see cref="TaskItemStatus.InProgress"/>.</summary>
    public required int PendingCount { get; init; }

    public required int OverdueCount { get; init; }

    public required IReadOnlyList<TaskSummaryDto> UpcomingDueTasks { get; init; }
}
