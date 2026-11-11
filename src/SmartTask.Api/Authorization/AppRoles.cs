using SmartTask.Domain.Enums;

namespace SmartTask.Api.Authorization;

/// <summary>
/// Role names for <c>[Authorize(Roles = ...)]</c>, taken from <see cref="UserRole"/> with <c>nameof</c>
/// so a rename breaks the build. A comma-separated value means "any of these roles". Several
/// <c>[Authorize]</c> attributes on the same endpoint must <b>all</b> pass, so an action-level attribute
/// can only narrow the controller's baseline.
/// </summary>
/// <remarks>
/// A <c>const</c> can't be computed from <c>Enum.GetNames</c>, so a unit test keeps <see cref="Any"/> and
/// <see cref="All"/> in sync with <see cref="UserRole"/> when a role is added.
/// </remarks>
public static class AppRoles
{
    public const string Admin = nameof(UserRole.Admin);
    public const string ProjectManager = nameof(UserRole.ProjectManager);
    public const string TeamMember = nameof(UserRole.TeamMember);

    /// <summary>Admin or ProjectManager: create projects, and manage projects and tasks.</summary>
    public const string Management = Admin + "," + ProjectManager;

    /// <summary>Every known role: the baseline for every authenticated endpoint.</summary>
    public const string Any = Admin + "," + ProjectManager + "," + TeamMember;

    /// <summary>The same roles as <see cref="Any"/>, as an array for <c>RequireRole(...)</c>.</summary>
    public static readonly IReadOnlyList<string> All = [Admin, ProjectManager, TeamMember];
}
