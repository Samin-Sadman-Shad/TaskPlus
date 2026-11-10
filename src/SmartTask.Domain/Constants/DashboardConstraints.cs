namespace SmartTask.Domain.Constants;

public static class DashboardConstraints
{
    /// <summary>How many days ahead of today (inclusive) a task counts as "upcoming".</summary>
    public const int UpcomingWindowDays = 7;

    /// <summary>Maximum number of upcoming tasks returned on the dashboard.</summary>
    public const int UpcomingTaskCount = 5;
}
