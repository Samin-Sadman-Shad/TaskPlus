using SmartTask.Domain.Enums;

namespace SmartTask.Application.DTOs.Tasks;

/// <summary>
/// Task list item. Returned (paged) by <c>GET tasks</c> and <c>GET projects/{projectId}/tasks</c>, and
/// used for <see cref="Dashboard.DashboardStatsDto.UpcomingDueTasks"/>.
/// </summary>
public sealed record TaskSummaryDto
{
    public required Guid Id { get; init; }

    public required Guid ProjectId { get; init; }

    public required string Title { get; init; }

    public required TaskItemStatus Status { get; init; }

    public required TaskPriority Priority { get; init; }

    public DateOnly? DueDate { get; init; }

    public required bool IsOverdue { get; init; }

    public string? AssigneeName { get; init; }
}
