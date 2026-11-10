using SmartTask.Domain.Enums;

namespace SmartTask.Application.DTOs.Tasks;

/// <summary>
/// Request body for <c>PUT tasks/{id}</c>. Status and assignee are not here. They have their own
/// endpoints (<see cref="UpdateTaskStatusDto"/>, <see cref="AssignTaskDto"/>) because they carry different permissions.
/// </summary>
public sealed record UpdateTaskDto
{
    public string Title { get; init; } = string.Empty;

    public string? Description { get; init; }

    public TaskPriority Priority { get; init; } = TaskPriority.Medium;

    public DateOnly? DueDate { get; init; }
}
