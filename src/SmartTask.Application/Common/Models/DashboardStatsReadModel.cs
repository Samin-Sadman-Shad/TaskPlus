using SmartTask.Domain.Enums;

namespace SmartTask.Application.Common.Models;

/// <summary>Dashboard figures for one scope: global, a manager's projects, or a member's tasks.</summary>
public sealed record DashboardStatsReadModel
{
    public required int TotalProjects { get; init; }

    public required int TotalTasks { get; init; }

    /// <summary>Has an entry for every <see cref="TaskItemStatus"/>; statuses with no tasks show 0.</summary>
    public required IReadOnlyDictionary<TaskItemStatus, int> TasksByStatus { get; init; }

    /// <summary>Has an entry for every <see cref="TaskPriority"/>; priorities with no tasks show 0.</summary>
    public required IReadOnlyDictionary<TaskPriority, int> TasksByPriority { get; init; }

    public required int CompletedCount { get; init; }

    /// <summary>Tasks that are <see cref="TaskItemStatus.ToDo"/> or <see cref="TaskItemStatus.InProgress"/>.</summary>
    public required int PendingCount { get; init; }

    public required int OverdueCount { get; init; }

    public required IReadOnlyList<UpcomingTaskReadModel> UpcomingDueTasks { get; init; }
}
