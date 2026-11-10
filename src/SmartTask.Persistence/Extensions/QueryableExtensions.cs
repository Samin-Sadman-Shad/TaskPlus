using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SmartTask.Application.Common.Models;
using SmartTask.Domain.Common;
using SmartTask.Domain.Constants;

namespace SmartTask.Persistence.Extensions;

internal static class QueryableExtensions
{
    /// <summary>Escape character used in every <c>LIKE</c> pattern built by <see cref="ToContainsPattern"/>.</summary>
    public const string LikeEscapeCharacter = "\\";

    /// <summary>
    /// Runs a count and a page query. <paramref name="pageNumber"/> below 1 becomes 1, and
    /// <paramref name="pageSize"/> is clamped to <see cref="PagingConstraints.MaxPageSize"/>
    /// (non-positive means <see cref="PagingConstraints.DefaultPageSize"/>).
    /// </summary>
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var size = pageSize <= 0 ? PagingConstraints.DefaultPageSize : Math.Min(pageSize, PagingConstraints.MaxPageSize);
        var page = Math.Max(pageNumber, 1);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * size).Take(size).ToListAsync(cancellationToken);

        return new PagedResult<T>(items, page, size, totalCount);
    }

    /// <summary>
    /// Orders by a whitelisted key. <paramref name="sortBy"/> is only used to look up
    /// <paramref name="sortMap"/>; an unknown or empty key falls back to <paramref name="defaultKey"/>
    /// in <paramref name="defaultDirection"/>. <c>Id</c> is always added as a tiebreaker, so paging is stable.
    /// </summary>
    public static IOrderedQueryable<T> ApplySorting<T>(
        this IQueryable<T> query,
        string? sortBy,
        SortDirection? direction,
        IReadOnlyDictionary<string, Expression<Func<T, object?>>> sortMap,
        string defaultKey,
        SortDirection defaultDirection = SortDirection.Desc)
        where T : BaseEntity
    {
        Expression<Func<T, object?>> keySelector;
        SortDirection effectiveDirection;

        if (!string.IsNullOrWhiteSpace(sortBy) && sortMap.TryGetValue(sortBy.Trim(), out var requested))
        {
            keySelector = requested;
            effectiveDirection = direction ?? SortDirection.Asc;
        }
        else
        {
            keySelector = sortMap[defaultKey];
            effectiveDirection = direction ?? defaultDirection;
        }

        return effectiveDirection == SortDirection.Desc
            ? query.OrderByDescending(keySelector).ThenByDescending(e => e.Id)
            : query.OrderBy(keySelector).ThenBy(e => e.Id);
    }

    /// <summary>
    /// Builds a <c>%term%</c> pattern for <c>EF.Functions.Like</c> with the <c>LIKE</c> wildcards
    /// (<c>%</c>, <c>_</c>, <c>[</c>) escaped, so user input is matched literally. The pattern is sent
    /// as a SQL parameter, never concatenated into the SQL text.
    /// </summary>
    public static string ToContainsPattern(string term)
    {
        var escaped = term.Trim()
            .Replace(LikeEscapeCharacter, LikeEscapeCharacter + LikeEscapeCharacter, StringComparison.Ordinal)
            .Replace("%", LikeEscapeCharacter + "%", StringComparison.Ordinal)
            .Replace("_", LikeEscapeCharacter + "_", StringComparison.Ordinal)
            .Replace("[", LikeEscapeCharacter + "[", StringComparison.Ordinal);

        return $"%{escaped}%";
    }
}
