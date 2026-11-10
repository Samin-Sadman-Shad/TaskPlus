namespace SmartTask.Application.DTOs.Projects;

/// <summary>Request body for <c>POST projects</c>. The owner is the authenticated user and is never taken from the body.</summary>
public sealed record CreateProjectDto
{
    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public DateOnly StartDate { get; init; }

    public DateOnly? EndDate { get; init; }
}
