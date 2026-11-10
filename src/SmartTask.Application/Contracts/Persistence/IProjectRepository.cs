using SmartTask.Application.Common.Models;
using SmartTask.Domain.Entities;

namespace SmartTask.Application.Contracts.Persistence;

public interface IProjectRepository : IRepository<Project>
{
    /// <summary>Loads the project with <c>Owner</c> and <c>Members.User</c>. Tracked, so members can be added or removed.</summary>
    Task<Project?> GetDetailsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Paged project list. With <paramref name="visibleToUserId"/> <c>null</c> (admin) every project
    /// is returned; otherwise only projects the user owns or is a member of. Read-only.
    /// </summary>
    Task<PagedResult<ProjectListItem>> GetPagedAsync(
        ProjectQueryParameters parameters,
        Guid? visibleToUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// True if the owner already has a non-deleted project with this name (case-insensitive).
    /// Pass <paramref name="excludeProjectId"/> when renaming, so the project does not clash with itself.
    /// </summary>
    Task<bool> NameExistsForOwnerAsync(
        Guid ownerId,
        string name,
        Guid? excludeProjectId,
        CancellationToken cancellationToken = default);

    Task<bool> IsUserOwnerOrMemberAsync(Guid projectId, Guid userId, CancellationToken cancellationToken = default);
}
