using SmartTask.Domain.Common;
using SmartTask.Domain.Constants;
using SmartTask.Domain.Exceptions;

namespace SmartTask.Domain.Entities;

/// <summary>
/// A project owned by one user, with members and tasks. The owner always counts as a member
/// and can never be removed.
/// </summary>
public sealed class Project : AuditableEntity, ISoftDeletable
{
    private readonly List<ProjectMember> _members = [];
    private readonly List<TaskItem> _tasks = [];

    private Project()
    {
    }

    public string Name { get; private set; } = null!;

    public string? Description { get; private set; }

    public DateOnly StartDate { get; private set; }

    public DateOnly? EndDate { get; private set; }

    public Guid OwnerId { get; private set; }

    public User Owner { get; private set; } = null!;

    public IReadOnlyCollection<ProjectMember> Members => _members.AsReadOnly();

    public IReadOnlyCollection<TaskItem> Tasks => _tasks.AsReadOnly();

    public byte[] RowVersion { get; private set; } = [];

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public Guid? DeletedBy { get; private set; }

    /// <summary>Creates a project.</summary>
    /// <exception cref="DomainException"><paramref name="endDate"/> is before <paramref name="startDate"/>, or input is invalid.</exception>
    public static Project Create(string name, string? description, DateOnly startDate, DateOnly? endDate, Guid ownerId)
    {
        var project = new Project
        {
            OwnerId = Guard.NotEmpty(ownerId, nameof(OwnerId)),
        };

        project.SetDetails(name, description, startDate, endDate);

        return project;
    }

    /// <exception cref="DomainException"><paramref name="endDate"/> is before <paramref name="startDate"/>, or input is invalid.</exception>
    public void Update(string name, string? description, DateOnly startDate, DateOnly? endDate) =>
        SetDetails(name, description, startDate, endDate);

    /// <summary>
    /// Adds a member. Idempotent: adding an existing member, or the owner (who is implicitly a
    /// member), does nothing.
    /// </summary>
    public void AddMember(Guid userId, DateTimeOffset joinedAt)
    {
        Guard.NotEmpty(userId, nameof(userId));

        if (HasMember(userId))
        {
            return;
        }

        _members.Add(ProjectMember.Create(Id, userId, joinedAt));
    }

    /// <summary>Removes a member. Removing a user who is not a member does nothing.</summary>
    /// <exception cref="DomainException"><paramref name="userId"/> is the owner.</exception>
    public void RemoveMember(Guid userId)
    {
        if (IsOwnedBy(userId))
        {
            throw new DomainException("The project owner cannot be removed from the project.");
        }

        _members.RemoveAll(m => m.UserId == userId);
    }

    public bool IsOwnedBy(Guid userId) => OwnerId == userId;

    /// <summary>True for the owner and for every explicitly added member.</summary>
    public bool HasMember(Guid userId) => IsOwnedBy(userId) || _members.Exists(m => m.UserId == userId);

    private void SetDetails(string name, string? description, DateOnly startDate, DateOnly? endDate)
    {
        if (endDate < startDate)
        {
            throw new DomainException("End date cannot be earlier than start date.");
        }

        Name = Guard.RequiredText(name, nameof(Name), ProjectConstraints.NameMaxLength, ProjectConstraints.NameMinLength);
        Description = Guard.OptionalText(description, nameof(Description), ProjectConstraints.DescriptionMaxLength);
        StartDate = startDate;
        EndDate = endDate;
    }
}
