using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SmartTask.Application.Common.Models;
using SmartTask.Application.Contracts.Persistence;
using SmartTask.Domain.Entities;
using SmartTask.Domain.Enums;
using SmartTask.Persistence.Extensions;
using SortFields = SmartTask.Application.Common.Models.ProjectQueryParameters.SortFields;

namespace SmartTask.Persistence.Repositories;

internal sealed class ProjectRepository(SmartTaskDbContext context)
    : Repository<Project>(context), IProjectRepository
{
    private static readonly IReadOnlyDictionary<string, Expression<Func<Project, object?>>> SortMap =
        new Dictionary<string, Expression<Func<Project, object?>>>(StringComparer.OrdinalIgnoreCase)
        {
            [SortFields.Name] = p => p.Name,
            [SortFields.StartDate] = p => p.StartDate,
            [SortFields.EndDate] = p => p.EndDate,
            [SortFields.CreatedAt] = p => p.CreatedAt,
        };

    /// <inheritdoc />
    public Task<Project?> GetDetailsAsync(Guid id, CancellationToken cancellationToken = default) =>
        Set
            .Include(p => p.Owner)
            .Include(p => p.Members).ThenInclude(m => m.User)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    /// <inheritdoc />
    public Task<PagedResult<ProjectListItem>> GetPagedAsync(
        ProjectQueryParameters parameters,
        Guid? visibleToUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var query = Set.AsNoTracking().VisibleTo(visibleToUserId);

        if (parameters.OwnerId is { } ownerId)
        {
            query = query.Where(p => p.OwnerId == ownerId);
        }

        if (parameters.StartDateFrom is { } from)
        {
            query = query.Where(p => p.StartDate >= from);
        }

        if (parameters.StartDateTo is { } to)
        {
            query = query.Where(p => p.StartDate <= to);
        }

        if (!string.IsNullOrWhiteSpace(parameters.Search))
        {
            var pattern = QueryableExtensions.ToContainsPattern(parameters.Search);
            query = query.Where(p =>
                EF.Functions.Like(p.Name, pattern, QueryableExtensions.LikeEscapeCharacter) ||
                (p.Description != null && EF.Functions.Like(p.Description, pattern, QueryableExtensions.LikeEscapeCharacter)));
        }

        return query
            .ApplySorting(parameters.SortBy, parameters.SortDirection, SortMap, SortFields.Default)
            .Select(p => new ProjectListItem
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                StartDate = p.StartDate,
                EndDate = p.EndDate,
                OwnerId = p.OwnerId,
                OwnerName = p.Owner.FirstName + " " + p.Owner.LastName,
                MemberCount = p.Members.Count + 1,
                TaskCount = p.Tasks.Count,
                CompletedTaskCount = p.Tasks.Count(t => t.Status == TaskItemStatus.Completed),
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt,
            })
            .ToPagedResultAsync(parameters.PageNumber, parameters.PageSize, cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> NameExistsForOwnerAsync(
        Guid ownerId,
        string name,
        Guid? excludeProjectId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(name);
        var trimmed = name.Trim();

        // Name comparison follows the column collation (case-insensitive by default on SQL Server),
        // the same rule the filtered unique index enforces.
        return Set.AnyAsync(
            p => p.OwnerId == ownerId && p.Name == trimmed && (excludeProjectId == null || p.Id != excludeProjectId),
            cancellationToken);
    }

    public Task<bool> IsUserOwnerOrMemberAsync(Guid projectId, Guid userId, CancellationToken cancellationToken = default) =>
        Set.Where(p => p.Id == projectId).VisibleTo(userId).AnyAsync(cancellationToken);
}
