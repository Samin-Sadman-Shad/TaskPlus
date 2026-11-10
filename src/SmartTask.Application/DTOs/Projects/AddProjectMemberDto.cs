namespace SmartTask.Application.DTOs.Projects;

/// <summary>Request body for <c>POST projects/{id}/members</c>. The project id comes from the route.</summary>
public sealed record AddProjectMemberDto
{
    public Guid UserId { get; init; }
}
