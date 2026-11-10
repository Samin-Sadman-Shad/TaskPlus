using Microsoft.EntityFrameworkCore;
using SmartTask.Application.Common.Models;
using SmartTask.Application.Contracts.Persistence;
using SmartTask.Domain.Constants;
using SmartTask.Domain.Entities;
using SmartTask.Domain.Enums;
using SmartTask.Persistence.Extensions;

namespace SmartTask.Persistence.Repositories;

/// <summary>
/// Dashboard figures computed with <c>COUNT</c> / <c>GROUP BY</c> in SQL. No task rows are loaded,
/// except the short "upcoming" list.
/// </summary>
internal sealed class DashboardReadRepository(SmartTaskDbContext context) : IDashboardReadRepository
{
    /// <inheritdoc />
    public async Task<DashboardStatsReadModel> GetStatsAsync(
        Guid? scopeUserId,
        UserRole role,
        DateOnly today,
        int upcomingDays,
        CancellationToken cancellationToken = default)
    {
        var (projects, tasks) = Scope(scopeUserId, role);
        tasks = tasks.AsNoTracking();

        // One DbContext cannot run queries in parallel, so these run one after another.
        var totalProjects = await projects.CountAsync(cancellationToken);

        var statusCounts = await tasks
            .GroupBy(t => t.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, cancellationToken);

        var priorityCounts = await tasks
            .GroupBy(t => t.Priority)
            .Select(g => new { Priority = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Priority, x => x.Count, cancellationToken);

        var overdueCount = await tasks.WhereOverdue(today).CountAsync(cancellationToken);

        var upcomingUntil = today.AddDays(
            Math.Clamp(upcomingDays, DashboardConstraints.MinUpcomingDays, DashboardConstraints.MaxUpcomingDays));
        var upcoming = await (
                from t in tasks.WherePending().Where(t => t.DueDate >= today && t.DueDate <= upcomingUntil)
                join p in context.Projects on t.ProjectId equals p.Id
                orderby t.DueDate, t.Id
                select new UpcomingTaskReadModel
                {
                    Id = t.Id,
                    ProjectId = t.ProjectId,
                    ProjectName = p.Name,
                    Title = t.Title,
                    Status = t.Status,
                    Priority = t.Priority,
                    DueDate = t.DueDate!.Value,
                    AssigneeId = t.AssigneeId,
                    AssigneeName = t.Assignee == null ? null : t.Assignee.FirstName + " " + t.Assignee.LastName,
                })
            .Take(DashboardConstraints.UpcomingTaskCount)
            .ToListAsync(cancellationToken);

        var byStatus = WithAllValues(statusCounts);

        return new DashboardStatsReadModel
        {
            TotalProjects = totalProjects,
            TotalTasks = byStatus.Values.Sum(),
            TasksByStatus = byStatus,
            TasksByPriority = WithAllValues(priorityCounts),
            CompletedCount = byStatus[TaskItemStatus.Completed],
            PendingCount = byStatus[TaskItemStatus.ToDo] + byStatus[TaskItemStatus.InProgress],
            OverdueCount = overdueCount,
            UpcomingDueTasks = upcoming,
        };
    }

    private (IQueryable<Project> Projects, IQueryable<TaskItem> Tasks) Scope(Guid? scopeUserId, UserRole role)
    {
        if (role == UserRole.Admin)
        {
            return (context.Projects, context.TaskItems.InProjects(context.Projects));
        }

        if (scopeUserId is not { } userId)
        {
            throw new ArgumentException($"A user id is required for the {role} dashboard scope.", nameof(scopeUserId));
        }

        return role switch
        {
            UserRole.ProjectManager => ManagerScope(userId),
            UserRole.TeamMember => (
                context.Projects.VisibleTo(userId),
                context.TaskItems.Where(t => t.AssigneeId == userId).InProjects(context.Projects)),
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown role."),
        };
    }

    private (IQueryable<Project> Projects, IQueryable<TaskItem> Tasks) ManagerScope(Guid userId)
    {
        var owned = context.Projects.Where(p => p.OwnerId == userId);
        return (owned, context.TaskItems.InProjects(owned));
    }

    /// <summary>Adds a 0 entry for every enum value missing from <paramref name="counts"/>, so clients get a fixed shape.</summary>
    private static Dictionary<TEnum, int> WithAllValues<TEnum>(Dictionary<TEnum, int> counts)
        where TEnum : struct, Enum =>
        Enum.GetValues<TEnum>().ToDictionary(v => v, v => counts.GetValueOrDefault(v));
}
