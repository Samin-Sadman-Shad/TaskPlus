using SmartTask.Application.Common.Models;
using SmartTask.Domain.Entities;

namespace SmartTask.Application.Contracts.Persistence;

/// <remarks>
/// Tasks belonging to a soft-deleted project are never returned. <see cref="TaskItem"/> has no
/// project navigation, so callers that need the project load it with <see cref="IProjectRepository"/>.
/// </remarks>
public interface ITaskItemRepository : IRepository<TaskItem>
{
    /// <summary>Loads the task with <c>Assignee</c>. Tracked, so it can be updated.</summary>
    Task<TaskItem?> GetDetailsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Paged task list, including <c>Assignee</c>. With <paramref name="visibleToUserId"/> <c>null</c>
    /// (admin) all tasks are returned; otherwise only tasks in projects the user owns or is a member of.
    /// Read-only.
    /// </summary>
    Task<PagedResult<TaskItem>> GetPagedAsync(
        TaskQueryParameters parameters,
        Guid? visibleToUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// All live tasks of a project. Tracked, so they can be removed (soft-deleted) together with the
    /// project in one save.
    /// </summary>
    Task<IReadOnlyList<TaskItem>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken = default);

    /// <summary>Total and completed task counts for a project, computed in SQL.</summary>
    Task<ProjectTaskCounts> GetCountsByProjectAsync(Guid projectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pending (ToDo / InProgress) tasks due between <paramref name="from"/> and <paramref name="to"/>
    /// inclusive, soonest first, including <c>Assignee</c>. Read-only.
    /// </summary>
    Task<IReadOnlyList<TaskItem>> GetUpcomingDueAsync(
        DateOnly from,
        DateOnly to,
        Guid? visibleToUserId,
        int take,
        CancellationToken cancellationToken = default);
}
