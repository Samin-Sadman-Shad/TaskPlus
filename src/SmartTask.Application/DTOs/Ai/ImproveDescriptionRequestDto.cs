namespace SmartTask.Application.DTOs.Ai;

/// <summary>Request body for <c>POST ai/task-descriptions/improve</c>.</summary>
public sealed record ImproveDescriptionRequestDto
{
    /// <summary>Optional task title, given to the model as context.</summary>
    public string? Title { get; init; }

    public string Description { get; init; } = string.Empty;
}
