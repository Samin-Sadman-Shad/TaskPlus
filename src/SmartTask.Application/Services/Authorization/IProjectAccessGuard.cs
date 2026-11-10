using SmartTask.Domain.Entities;

namespace SmartTask.Application.Services.Authorization;

/// <summary>
/// Resource-level permission rules (CLAUDE.md §4) for the current user, kept in one place.
/// Services use it like this: <b>not visible → 404</b> (so ids can't be enumerated),
/// <b>visible but not allowed → 403</b>.
/// </summary>
public interface IProjectAccessGuard
{
    bool IsAdmin { get; }

    /// <summary>Admin or ProjectManager: may create projects and look up users.</summary>
    bool HasManagementRole { get; }

    /// <summary>The user id that list and dashboard queries are scoped to; <c>null</c> for an admin, who sees everything.</summary>
    Guid? VisibilityScopeUserId { get; }

    /// <summary>Admin, owner or member. Checks the database, so the caller needs only the id.</summary>
    Task<bool> CanViewProjectAsync(Guid projectId, CancellationToken cancellationToken = default);

    /// <summary>Same rule as <see cref="CanViewProjectAsync"/>, for a project already loaded with its members.</summary>
    bool CanViewProject(Project project);

    /// <summary>
    /// Admin, or the project manager who owns the project: may update or delete it, manage members,
    /// and create, edit, delete or assign its tasks.
    /// </summary>
    bool CanManageProject(Project project);

    /// <summary>Anyone who can manage the project, or the task's assignee while still a project member.</summary>
    bool CanChangeTaskStatus(Project project, TaskItem task);
}
