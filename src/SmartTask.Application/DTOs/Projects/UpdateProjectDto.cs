namespace SmartTask.Application.DTOs.Projects;

/// <summary>Request body for <c>PUT projects/{id}</c>. The id comes from the route. This is a full replacement of the editable fields.</summary>
public sealed record UpdateProjectDto
{
    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public DateOnly StartDate { get; init; }

    public DateOnly? EndDate { get; init; }
}
