using SmartTask.Domain.Enums;

namespace SmartTask.Application.Common.Models;

/// <summary>Filters for the task list. Default sort: <c>createdAt desc</c>.</summary>
public sealed record TaskQueryParameters : PageQuery
{
    /// <summary>Sort keys the repository accepts (case-insensitive). Validators reuse this set.</summary>
    public static readonly IReadOnlySet<string> AllowedSortFields = SortFieldSet(
        SortFields.Title, SortFields.DueDate, SortFields.Priority, SortFields.Status, SortFields.CreatedAt);

    public Guid? ProjectId { get; init; }

    public TaskItemStatus? Status { get; init; }

    public TaskPriority? Priority { get; init; }

    public Guid? AssigneeId { get; init; }

    public DateOnly? DueDateFrom { get; init; }

    public DateOnly? DueDateTo { get; init; }

    /// <summary><c>true</c>: only overdue tasks. <c>false</c>: only tasks that are not overdue. <c>null</c>: both.</summary>
    public bool? IsOverdue { get; init; }

    public static class SortFields
    {
        public const string Title = "title";
        public const string DueDate = "dueDate";
        public const string Priority = "priority";
        public const string Status = "status";
        public const string CreatedAt = "createdAt";
        public const string Default = CreatedAt;
    }
}
