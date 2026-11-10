namespace SmartTask.Domain.Constants;

public static class DashboardConstraints
{
    /// <summary>Default look-ahead, in days from today inclusive, for "upcoming" tasks.</summary>
    public const int DefaultUpcomingDays = 7;

    /// <summary>Smallest look-ahead a client may request; smaller values are clamped up to it.</summary>
    public const int MinUpcomingDays = 1;

    /// <summary>Largest look-ahead a client may request; larger values are clamped down to it.</summary>
    public const int MaxUpcomingDays = 30;

    /// <summary>Maximum number of upcoming tasks returned on the dashboard.</summary>
    public const int UpcomingTaskCount = 5;
}
