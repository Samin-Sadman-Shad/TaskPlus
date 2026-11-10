namespace SmartTask.Application.Common.Models;

/// <summary>A project as shown in list views. The counts are computed in the database.</summary>
public sealed record ProjectListItem
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public required DateOnly StartDate { get; init; }

    public DateOnly? EndDate { get; init; }

    public required Guid OwnerId { get; init; }

    public required string OwnerName { get; init; }

    /// <summary>Explicit members plus the owner, who always counts as a member.</summary>
    public required int MemberCount { get; init; }

    public required int TaskCount { get; init; }

    public required int CompletedTaskCount { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? UpdatedAt { get; init; }
}
