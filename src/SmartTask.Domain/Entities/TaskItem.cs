using SmartTask.Domain.Common;
using SmartTask.Domain.Constants;
using SmartTask.Domain.Enums;
using SmartTask.Domain.Exceptions;

namespace SmartTask.Domain.Entities;

/// <summary>
/// A task inside a project. Named <c>TaskItem</c> to avoid clashing with
/// <see cref="System.Threading.Tasks.Task"/>. Status changes only through <see cref="ChangeStatus"/>.
/// </summary>
/// <remarks>
/// Two rules need data from outside this entity, so the application layer enforces them:
/// the assignee must be a member of the project, and the due date must not be in the past on create.
/// </remarks>
public sealed class TaskItem : AuditableEntity, ISoftDeletable
{
    /// <summary>Allowed status transitions. Anything not listed throws <see cref="InvalidStatusTransitionException"/>.</summary>
    private static readonly IReadOnlyDictionary<TaskItemStatus, TaskItemStatus[]> AllowedTransitions =
        new Dictionary<TaskItemStatus, TaskItemStatus[]>
        {
            [TaskItemStatus.ToDo] = [TaskItemStatus.InProgress, TaskItemStatus.Completed, TaskItemStatus.Cancelled],
            [TaskItemStatus.InProgress] = [TaskItemStatus.ToDo, TaskItemStatus.Completed, TaskItemStatus.Cancelled],
            [TaskItemStatus.Completed] = [TaskItemStatus.InProgress],
            [TaskItemStatus.Cancelled] = [TaskItemStatus.ToDo],
        };

    private TaskItem()
    {
    }

    public Guid ProjectId { get; private set; }

    public string Title { get; private set; } = null!;

    public string? Description { get; private set; }

    public TaskItemStatus Status { get; private set; }

    public TaskPriority Priority { get; private set; }

    public DateOnly? DueDate { get; private set; }

    public Guid? AssigneeId { get; private set; }

    public User? Assignee { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public Guid? DeletedBy { get; private set; }

    /// <summary>Creates a task with status <see cref="TaskItemStatus.ToDo"/>.</summary>
    public static TaskItem Create(
        Guid projectId,
        string title,
        string? description,
        TaskPriority priority,
        DateOnly? dueDate,
        Guid? assigneeId)
    {
        var task = new TaskItem
        {
            ProjectId = Guard.NotEmpty(projectId, nameof(ProjectId)),
            Status = TaskItemStatus.ToDo,
        };

        task.UpdateDetails(title, description, priority, dueDate);
        task.AssignTo(assigneeId);

        return task;
    }

    public void UpdateDetails(string title, string? description, TaskPriority priority, DateOnly? dueDate)
    {
        Title = Guard.RequiredText(title, nameof(Title), TaskItemConstraints.TitleMaxLength, TaskItemConstraints.TitleMinLength);
        Description = Guard.OptionalText(description, nameof(Description), TaskItemConstraints.DescriptionMaxLength);
        Priority = Guard.DefinedEnum(priority, nameof(Priority));
        DueDate = dueDate;
    }

    /// <summary>Assigns the task to a user; <c>null</c> unassigns it.</summary>
    public void AssignTo(Guid? userId)
    {
        if (userId is { } id)
        {
            Guard.NotEmpty(id, nameof(AssigneeId));
        }

        AssigneeId = userId;
    }

    /// <summary>
    /// Moves the task to <paramref name="newStatus"/> if the transition table allows it.
    /// Entering <see cref="TaskItemStatus.Completed"/> sets <see cref="CompletedAt"/>; leaving it clears it.
    /// </summary>
    /// <exception cref="InvalidStatusTransitionException">The transition is not allowed.</exception>
    public void ChangeStatus(TaskItemStatus newStatus, DateTimeOffset now)
    {
        if (!CanTransitionTo(newStatus))
        {
            throw new InvalidStatusTransitionException(Status, newStatus);
        }

        Status = newStatus;
        CompletedAt = newStatus == TaskItemStatus.Completed ? now : null;
    }

    public bool CanTransitionTo(TaskItemStatus newStatus) =>
        AllowedTransitions.TryGetValue(Status, out var targets) && targets.Contains(newStatus);

    /// <summary>Overdue means the due date is before <paramref name="today"/> and the task is still ToDo or InProgress.</summary>
    public bool IsOverdue(DateOnly today) =>
        DueDate < today && Status is TaskItemStatus.ToDo or TaskItemStatus.InProgress;
}
