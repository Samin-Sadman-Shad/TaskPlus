namespace SmartTask.Api.Authorization;

/// <summary>
/// Role-based authorization policies (API-CONVENTIONS §5). Every endpoint requires an authenticated
/// user by default (fallback policy). Resource-level checks (owner, member, assignee) are in the services.
/// </summary>
public static class Policies
{
    /// <summary>Role <c>Admin</c>.</summary>
    public const string AdminOnly = "AdminOnly";

    /// <summary>Roles <c>Admin</c> or <c>ProjectManager</c>.</summary>
    public const string ProjectManagement = "ProjectManagement";
}
