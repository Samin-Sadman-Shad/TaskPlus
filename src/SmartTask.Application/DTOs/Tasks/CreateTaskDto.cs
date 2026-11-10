using SmartTask.Domain.Enums;

namespace SmartTask.Application.DTOs.Tasks;

/// <summary>
/// Request body for <c>POST projects/{projectId}/tasks</c>. The project comes from the route. There is
/// no status field, because new tasks always start as <see cref="TaskItemStatus.ToDo"/>.
/// </summary>
public sealed record CreateTaskDto : ITaskDetails
{
    public string Title { get; init; } = string.Empty;

    public string? Description { get; init; }

    public TaskPriority Priority { get; init; } = TaskPriority.Medium;

    public DateOnly? DueDate { get; init; }

    /// <summary>Optional. Must be a member of the project (checked by the service).</summary>
    public Guid? AssigneeId { get; init; }
}
