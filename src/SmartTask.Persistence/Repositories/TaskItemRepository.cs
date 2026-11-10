using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SmartTask.Application.Common.Models;
using SmartTask.Application.Contracts.Persistence;
using SmartTask.Domain.Constants;
using SmartTask.Domain.Entities;
using SmartTask.Persistence.Extensions;
using SortFields = SmartTask.Application.Common.Models.TaskQueryParameters.SortFields;

namespace SmartTask.Persistence.Repositories;

internal sealed class TaskItemRepository(SmartTaskDbContext context, TimeProvider timeProvider)
    : Repository<TaskItem>(context), ITaskItemRepository
{
    private static readonly IReadOnlyDictionary<string, Expression<Func<TaskItem, object?>>> SortMap =
        new Dictionary<string, Expression<Func<TaskItem, object?>>>(StringComparer.OrdinalIgnoreCase)
        {
            [SortFields.Title] = t => t.Title,
            [SortFields.DueDate] = t => t.DueDate,
            [SortFields.Priority] = t => t.Priority,
            [SortFields.Status] = t => t.Status,
            [SortFields.CreatedAt] = t => t.CreatedAt,
        };

    /// <inheritdoc />
    /// <remarks>Tracked. Hides tasks whose project has been soft-deleted.</remarks>
    public override Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Live().FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    /// <inheritdoc />
    public Task<TaskItem?> GetDetailsAsync(Guid id, CancellationToken cancellationToken = default) =>
        Live()
            .Include(t => t.Assignee)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    /// <inheritdoc />
    public Task<PagedResult<TaskItem>> GetPagedAsync(
        TaskQueryParameters parameters,
        Guid? visibleToUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var query = Visible(visibleToUserId).AsNoTracking().Include(t => t.Assignee).AsQueryable();

        if (parameters.ProjectId is { } projectId)
        {
            query = query.Where(t => t.ProjectId == projectId);
        }

        if (parameters.Status is { } status)
        {
            query = query.Where(t => t.Status == status);
        }

        if (parameters.Priority is { } priority)
        {
            query = query.Where(t => t.Priority == priority);
        }

        if (parameters.AssigneeId is { } assigneeId)
        {
            query = query.Where(t => t.AssigneeId == assigneeId);
        }

        if (parameters.DueDateFrom is { } from)
        {
            query = query.Where(t => t.DueDate >= from);
        }

        if (parameters.DueDateTo is { } to)
        {
            query = query.Where(t => t.DueDate <= to);
        }

        if (parameters.IsOverdue is { } overdue)
        {
            query = query.WhereOverdue(Today(), overdue);
        }

        if (!string.IsNullOrWhiteSpace(parameters.Search))
        {
            var pattern = QueryableExtensions.ToContainsPattern(parameters.Search);
            query = query.Where(t =>
                EF.Functions.Like(t.Title, pattern, QueryableExtensions.LikeEscapeCharacter) ||
                (t.Description != null && EF.Functions.Like(t.Description, pattern, QueryableExtensions.LikeEscapeCharacter)));
        }

        return query
            .ApplySorting(parameters.SortBy, parameters.SortDirection, SortMap, SortFields.Default)
            .ToPagedResultAsync(parameters.PageNumber, parameters.PageSize, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TaskItem>> GetUpcomingDueAsync(
        DateOnly from,
        DateOnly to,
        Guid? visibleToUserId,
        int take,
        CancellationToken cancellationToken = default)
    {
        var limit = Math.Clamp(take, 1, PagingConstraints.MaxPageSize);

        return await Visible(visibleToUserId)
            .AsNoTracking()
            .Include(t => t.Assignee)
            .WherePending()
            .Where(t => t.DueDate >= from && t.DueDate <= to)
            .OrderBy(t => t.DueDate)
            .ThenBy(t => t.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    /// <summary>Tasks whose project still exists (not soft-deleted).</summary>
    private IQueryable<TaskItem> Live() => Set.InProjects(Context.Projects);

    private IQueryable<TaskItem> Visible(Guid? userId) => Set.InProjects(Context.Projects.VisibleTo(userId));

    private DateOnly Today() => DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
}
