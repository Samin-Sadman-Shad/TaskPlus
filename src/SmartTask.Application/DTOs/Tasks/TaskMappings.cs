using SmartTask.Application.Common.Models;
using SmartTask.Application.DTOs.Users;
using SmartTask.Domain.Entities;
using SmartTask.Domain.Enums;

namespace SmartTask.Application.DTOs.Tasks;

/// <summary>
/// One-way mapping from <see cref="TaskItem"/> and <see cref="UpcomingTaskReadModel"/> to the task read DTOs.
/// <c>IsOverdue</c> depends on the date, so every method takes <c>today</c> instead of reading the clock.
/// </summary>
public static class TaskMappings
{
    /// <summary>
    /// Maps a task loaded with <c>ITaskItemRepository.GetDetailsAsync</c> (which includes <c>Assignee</c>).
    /// <see cref="TaskItem"/> has no project navigation, so the caller passes the project name. It has
    /// already loaded the project for the permission check.
    /// </summary>
    /// <exception cref="InvalidOperationException">The task has an assignee that was not loaded.</exception>
    public static TaskDto ToDto(this TaskItem task, string projectName, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(task);
        return task.ToDto(projectName, today, LoadedAssignee(task));
    }

    /// <summary>
    /// Maps a task with an explicitly supplied assignee. Use this right after <c>TaskItem.AssignTo</c>
    /// or <c>TaskItem.Create</c>, when the service has just loaded the assignee and the task's
    /// navigation may not reflect the change yet.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="assignee"/> does not match <see cref="TaskItem.AssigneeId"/>.</exception>
    public static TaskDto ToDto(this TaskItem task, string projectName, DateOnly today, User? assignee)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectName);

        if (assignee?.Id != task.AssigneeId)
        {
            throw new ArgumentException($"Assignee {assignee?.Id} does not match task {task.Id}'s assignee {task.AssigneeId}.", nameof(assignee));
        }

        return new TaskDto
        {
            Id = task.Id,
            ProjectId = task.ProjectId,
            ProjectName = projectName,
            Title = task.Title,
            Description = task.Description,
            Status = task.Status,
            Priority = task.Priority,
            DueDate = task.DueDate,
            IsOverdue = task.IsOverdue(today),
            Assignee = assignee?.ToSummaryDto(),
            CompletedAt = task.CompletedAt,
            CreatedAt = task.CreatedAt,
            UpdatedAt = task.UpdatedAt,
        };
    }

    /// <summary>Maps a task from <c>ITaskItemRepository.GetPagedAsync</c> (which includes <c>Assignee</c>).</summary>
    /// <example>Paged list: <c>pagedTasks.Map(t =&gt; t.ToSummaryDto(today))</c>.</example>
    /// <exception cref="InvalidOperationException">The task has an assignee that was not loaded.</exception>
    public static TaskSummaryDto ToSummaryDto(this TaskItem task, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(task);

        return new TaskSummaryDto
        {
            Id = task.Id,
            ProjectId = task.ProjectId,
            Title = task.Title,
            Status = task.Status,
            Priority = task.Priority,
            DueDate = task.DueDate,
            IsOverdue = task.IsOverdue(today),
            AssigneeName = LoadedAssignee(task)?.FullName,
        };
    }

    /// <summary>Maps a dashboard "upcoming" row. It uses the same overdue rule as <see cref="TaskItem.IsOverdue"/>.</summary>
    public static TaskSummaryDto ToSummaryDto(this UpcomingTaskReadModel task, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(task);

        return new TaskSummaryDto
        {
            Id = task.Id,
            ProjectId = task.ProjectId,
            Title = task.Title,
            Status = task.Status,
            Priority = task.Priority,
            DueDate = task.DueDate,
            IsOverdue = task.DueDate < today && task.Status is TaskItemStatus.ToDo or TaskItemStatus.InProgress,
            AssigneeName = task.AssigneeName,
        };
    }

    /// <summary>
    /// Returns the assignee, or <c>null</c> for an unassigned task. Throws if the task has an assignee
    /// whose navigation was not included, so a missing name is never shown as "unassigned".
    /// </summary>
    private static User? LoadedAssignee(TaskItem task)
    {
        if (task.AssigneeId is null)
        {
            return null;
        }

        return task.Assignee ?? throw new InvalidOperationException(
            $"Assignee of task {task.Id} was not loaded. Use a repository method that includes Assignee.");
    }
}
