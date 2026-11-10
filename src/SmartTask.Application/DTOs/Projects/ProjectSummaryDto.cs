namespace SmartTask.Application.DTOs.Projects;

/// <summary>Project list item. Returned (paged) by <c>GET projects</c>.</summary>
public sealed record ProjectSummaryDto
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public required DateOnly StartDate { get; init; }

    public DateOnly? EndDate { get; init; }

    public required string OwnerName { get; init; }

    public required int TaskCount { get; init; }

    public required int CompletedTaskCount { get; init; }

    /// <summary>Explicit members plus the owner.</summary>
    public required int MemberCount { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
