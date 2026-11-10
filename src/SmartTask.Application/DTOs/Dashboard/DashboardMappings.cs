using SmartTask.Application.Common.Models;
using SmartTask.Application.DTOs.Tasks;

namespace SmartTask.Application.DTOs.Dashboard;

/// <summary>One-way mapping from <see cref="DashboardStatsReadModel"/> to <see cref="DashboardStatsDto"/>.</summary>
public static class DashboardMappings
{
    public static DashboardStatsDto ToDto(this DashboardStatsReadModel stats, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(stats);

        return new DashboardStatsDto
        {
            TotalProjects = stats.TotalProjects,
            TotalTasks = stats.TotalTasks,
            TasksByStatus = WithAllValues(stats.TasksByStatus),
            TasksByPriority = WithAllValues(stats.TasksByPriority),
            CompletedCount = stats.CompletedCount,
            PendingCount = stats.PendingCount,
            OverdueCount = stats.OverdueCount,
            UpcomingDueTasks = stats.UpcomingDueTasks.Select(t => t.ToSummaryDto(today)).ToList(),
        };
    }

    /// <summary>
    /// Puts every enum value in the dictionary, in declaration order. Any value the read model left
    /// out gets 0, so the API shape is stable whatever the source.
    /// </summary>
    private static Dictionary<TEnum, int> WithAllValues<TEnum>(IReadOnlyDictionary<TEnum, int> counts)
        where TEnum : struct, Enum =>
        Enum.GetValues<TEnum>().ToDictionary(v => v, v => counts.GetValueOrDefault(v));
}
