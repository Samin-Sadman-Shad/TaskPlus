namespace SmartTask.Application.Common.Responses;

/// <summary>
/// User-facing messages shared by the services, so the same situation always reads the same way.
/// They never contain internal details.
/// </summary>
public static class ResponseMessages
{
    public const string ValidationFailed = "Validation failed.";
    public const string AuthenticationRequired = "Authentication is required.";

    /// <summary>The same for unknown email, wrong password and inactive user, so login can't be used to find accounts.</summary>
    public const string InvalidCredentials = "Invalid email or password.";
    public const string EmailTaken = "An account with this email already exists.";
    public const string SessionExpired = "Your session has expired. Please sign in again.";

    public const string UserNotFound = "User not found.";
    public const string UserNotFoundOrInactive = "User not found or inactive.";
    public const string AdminOnly = "Only administrators can perform this action.";
    public const string ManagerOnly = "Only administrators and project managers can perform this action.";
    public const string CannotDemoteSelf = "You cannot remove your own administrator role.";
    public const string CannotDeactivateSelf = "You cannot deactivate your own account.";

    public const string ProjectNotFound = "Project not found.";
    public const string ProjectNameTaken = "You already have a project with this name.";
    public const string ProjectModifyForbidden = "You do not have permission to modify this project.";
    public const string MemberNotFound = "The user is not a member of this project.";

    public const string TaskNotFound = "Task not found.";
    public const string TaskModifyForbidden = "You do not have permission to modify this task.";
    public const string TaskStatusForbidden = "You can only change the status of tasks assigned to you.";
    public const string AssigneeNotMember = "Assignee must be a member of the project.";

    public const string AiUnavailable = "The AI service is currently unavailable. Please try again later.";
    public const string AiBusy = "AI service is busy, try again shortly.";
    public const string AiInvalidResponse = "The AI service returned an unusable response. Please try again.";
}
