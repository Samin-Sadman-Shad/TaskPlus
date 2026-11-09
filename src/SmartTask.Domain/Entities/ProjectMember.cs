using SmartTask.Domain.Common;

namespace SmartTask.Domain.Entities;

/// <summary>
/// Join entity between <see cref="Project"/> and <see cref="User"/> (composite key
/// <c>ProjectId + UserId</c>). The factory is internal: members are added only through
/// <see cref="Project.AddMember"/>, so the project keeps control of its membership rules.
/// </summary>
public sealed class ProjectMember
{
    private ProjectMember()
    {
    }

    public Guid ProjectId { get; private set; }

    public Guid UserId { get; private set; }

    public User User { get; private set; } = null!;

    public DateTimeOffset JoinedAt { get; private set; }

    internal static ProjectMember Create(Guid projectId, Guid userId, DateTimeOffset joinedAt) => new()
    {
        ProjectId = Guard.NotEmpty(projectId, nameof(ProjectId)),
        UserId = Guard.NotEmpty(userId, nameof(UserId)),
        JoinedAt = joinedAt,
    };
}
