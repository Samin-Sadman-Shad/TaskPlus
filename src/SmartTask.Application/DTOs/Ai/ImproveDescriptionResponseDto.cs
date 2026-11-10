namespace SmartTask.Application.DTOs.Ai;

/// <summary>Response of <c>POST ai/task-descriptions/improve</c>. Nothing is saved; the client decides whether to use the text.</summary>
public sealed record ImproveDescriptionResponseDto
{
    public required string OriginalDescription { get; init; }

    public required string ImprovedDescription { get; init; }

    public required IReadOnlyList<string> Suggestions { get; init; }
}
