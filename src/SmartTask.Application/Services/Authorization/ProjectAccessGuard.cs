using SmartTask.Application.Contracts.Identity;
using SmartTask.Application.Contracts.Persistence;
using SmartTask.Domain.Entities;
using SmartTask.Domain.Enums;

namespace SmartTask.Application.Services.Authorization;

/// <inheritdoc cref="IProjectAccessGuard"/>
public sealed class ProjectAccessGuard(ICurrentUserService currentUser, IProjectRepository projects) : IProjectAccessGuard
{
    public bool IsAdmin => currentUser.Role == UserRole.Admin;

    public bool HasManagementRole => currentUser.Role is UserRole.Admin or UserRole.ProjectManager;

    /// <remarks>
    /// An authenticated non-admin without a user id should not exist. If one does, it is scoped to
    /// <see cref="Guid.Empty"/>, which matches no rows, rather than to everything.
    /// </remarks>
    public Guid? VisibilityScopeUserId => IsAdmin ? null : currentUser.UserId ?? Guid.Empty;

    public async Task<bool> CanViewProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        if (IsAdmin)
        {
            return await projects.ExistsAsync(projectId, cancellationToken);
        }

        return currentUser.UserId is { } userId
            && await projects.IsUserOwnerOrMemberAsync(projectId, userId, cancellationToken);
    }

    public bool CanViewProject(Project project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return IsAdmin || (currentUser.UserId is { } userId && project.HasMember(userId));
    }

    public bool CanManageProject(Project project)
    {
        ArgumentNullException.ThrowIfNull(project);

        // The role is checked as well as ownership: a manager who was demoted to TeamMember loses
        // management rights, even over projects they still own.
        return IsAdmin
            || (currentUser.Role == UserRole.ProjectManager && currentUser.UserId is { } userId && project.IsOwnedBy(userId));
    }

    public bool CanChangeTaskStatus(Project project, TaskItem task)
    {
        ArgumentNullException.ThrowIfNull(task);

        return CanManageProject(project)
            || (currentUser.UserId is { } userId && task.AssigneeId == userId && project.HasMember(userId));
    }
}
