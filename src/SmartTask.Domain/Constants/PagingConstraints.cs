namespace SmartTask.Domain.Constants;

public static class PagingConstraints
{
    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 100;
    public const int SearchTermMaxLength = 100;

    /// <summary>Maximum results of the user lookup used by assignee and member pickers.</summary>
    public const int LookupMaxResults = 20;
}
