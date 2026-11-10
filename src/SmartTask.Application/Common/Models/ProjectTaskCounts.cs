namespace SmartTask.Application.Common.Models;

/// <summary>Task counts for one project, computed in the database (used for <c>ProjectDto</c>).</summary>
public sealed record ProjectTaskCounts(int Total, int Completed)
{
    public static ProjectTaskCounts None { get; } = new(0, 0);
}
