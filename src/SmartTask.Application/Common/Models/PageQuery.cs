using SmartTask.Domain.Constants;

namespace SmartTask.Application.Common.Models;

/// <summary>
/// Paging, search and sorting options shared by every list query. <see cref="SortBy"/> is only a
/// key: repositories look it up in a whitelist, and unknown keys fall back to the default sort.
/// </summary>
public abstract record PageQuery
{
    public int PageNumber { get; init; } = 1;

    public int PageSize { get; init; } = PagingConstraints.DefaultPageSize;

    public string? Search { get; init; }

    public string? SortBy { get; init; }

    /// <summary>
    /// <c>null</c> means ascending when <see cref="SortBy"/> is a known key, and the type's default
    /// direction when the default sort is used.
    /// </summary>
    public SortDirection? SortDirection { get; init; }

    protected static IReadOnlySet<string> SortFieldSet(params string[] fields) =>
        new HashSet<string>(fields, StringComparer.OrdinalIgnoreCase);
}
