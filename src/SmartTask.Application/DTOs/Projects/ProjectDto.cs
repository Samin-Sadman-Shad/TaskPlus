using SmartTask.Application.DTOs.Users;

namespace SmartTask.Application.DTOs.Projects;

/// <summary>Project details. Returned by <c>GET projects/{id}</c>, <c>POST projects</c> and <c>PUT projects/{id}</c>.</summary>
public sealed record ProjectDto
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public required DateOnly StartDate { get; init; }

    public DateOnly? EndDate { get; init; }

    public required UserSummaryDto Owner { get; init; }

    /// <summary>
    /// Explicitly added members. The owner is not listed here; it is in <see cref="Owner"/>, although it
    /// always counts as a member (so <see cref="ProjectSummaryDto.MemberCount"/> = <c>Members.Count + 1</c>).
    /// </summary>
    public required IReadOnlyList<ProjectMemberDto> Members { get; init; }

    public required int TaskCount { get; init; }

    public required int CompletedTaskCount { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? UpdatedAt { get; init; }
}
