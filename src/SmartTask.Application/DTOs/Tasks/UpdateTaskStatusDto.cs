using SmartTask.Domain.Enums;

namespace SmartTask.Application.DTOs.Tasks;

/// <summary>
/// Request body for <c>PATCH tasks/{id}/status</c>. Allowed for admins, the project owner and the
/// task's assignee. The domain enforces the transition table.
/// </summary>
public sealed record UpdateTaskStatusDto
{
    public TaskItemStatus Status { get; init; }
}
