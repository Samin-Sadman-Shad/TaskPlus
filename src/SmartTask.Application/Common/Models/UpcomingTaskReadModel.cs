using SmartTask.Domain.Enums;

namespace SmartTask.Application.Common.Models;

/// <summary>A pending task that is due soon, as shown on the dashboard.</summary>
public sealed record UpcomingTaskReadModel
{
    public required Guid Id { get; init; }

    public required Guid ProjectId { get; init; }

    public required string ProjectName { get; init; }

    public required string Title { get; init; }

    public required TaskItemStatus Status { get; init; }

    public required TaskPriority Priority { get; init; }

    public required DateOnly DueDate { get; init; }

    public Guid? AssigneeId { get; init; }

    public string? AssigneeName { get; init; }
}
