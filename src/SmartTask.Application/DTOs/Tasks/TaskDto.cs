using SmartTask.Application.DTOs.Users;
using SmartTask.Domain.Enums;

namespace SmartTask.Application.DTOs.Tasks;

/// <summary>
/// Task details. Returned by <c>GET tasks/{id}</c>, <c>POST projects/{projectId}/tasks</c>,
/// <c>PUT tasks/{id}</c>, <c>PATCH tasks/{id}/status</c> and <c>PATCH tasks/{id}/assignee</c>.
/// </summary>
public sealed record TaskDto
{
    public required Guid Id { get; init; }

    public required Guid ProjectId { get; init; }

    public required string ProjectName { get; init; }

    public required string Title { get; init; }

    public string? Description { get; init; }

    public required TaskItemStatus Status { get; init; }

    public required TaskPriority Priority { get; init; }

    public DateOnly? DueDate { get; init; }

    public required bool IsOverdue { get; init; }

    public UserSummaryDto? Assignee { get; init; }

    public DateTimeOffset? CompletedAt { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? UpdatedAt { get; init; }
}
