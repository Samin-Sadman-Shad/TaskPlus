using SmartTask.Domain.Entities;
using SmartTask.Domain.Enums;

namespace SmartTask.Persistence.Extensions;

/// <summary>
/// Visibility and status filters shared by the project, task and dashboard repositories, so the
/// rule "a user sees projects they own or belong to" is written once.
/// </summary>
internal static class AccessQueryExtensions
{
    /// <summary>Projects owned by, or with a member row for, <paramref name="userId"/>. <c>null</c> means no restriction.</summary>
    public static IQueryable<Project> VisibleTo(this IQueryable<Project> projects, Guid? userId) =>
        userId is { } id
            ? projects.Where(p => p.OwnerId == id || p.Members.Any(m => m.UserId == id))
            : projects;

    /// <summary>
    /// Tasks whose project is in <paramref name="projects"/>. Because the project set has the
    /// soft-delete filter, this also hides tasks of deleted projects.
    /// </summary>
    public static IQueryable<TaskItem> InProjects(this IQueryable<TaskItem> tasks, IQueryable<Project> projects) =>
        tasks.Where(t => projects.Any(p => p.Id == t.ProjectId));

    /// <summary>Tasks that are <see cref="TaskItemStatus.ToDo"/> or <see cref="TaskItemStatus.InProgress"/>.</summary>
    public static IQueryable<TaskItem> WherePending(this IQueryable<TaskItem> tasks) =>
        tasks.Where(t => t.Status == TaskItemStatus.ToDo || t.Status == TaskItemStatus.InProgress);

    /// <summary>Same rule as <see cref="TaskItem.IsOverdue"/>, written so EF can translate it to SQL.</summary>
    public static IQueryable<TaskItem> WhereOverdue(this IQueryable<TaskItem> tasks, DateOnly today, bool overdue = true) =>
        overdue
            ? tasks.Where(t => t.DueDate < today && (t.Status == TaskItemStatus.ToDo || t.Status == TaskItemStatus.InProgress))
            : tasks.Where(t => !(t.DueDate < today && (t.Status == TaskItemStatus.ToDo || t.Status == TaskItemStatus.InProgress)));
}
