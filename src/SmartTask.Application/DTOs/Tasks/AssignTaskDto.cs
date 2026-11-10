namespace SmartTask.Application.DTOs.Tasks;

/// <summary>Request body for <c>PATCH tasks/{id}/assignee</c>.</summary>
public sealed record AssignTaskDto
{
    /// <summary>The new assignee, who must be a project member. <c>null</c> unassigns the task.</summary>
    public Guid? AssigneeId { get; init; }
}
