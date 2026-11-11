namespace SmartTask.Application.DTOs.Auth;

/// <summary>
/// Request body for <c>POST users/me/password</c>. The current password is always required, also for a
/// first sign-in with an admin-set password, so a stolen access token alone can't take over the account.
/// </summary>
public sealed record ChangePasswordDto
{
    public string CurrentPassword { get; init; } = string.Empty;

    public string NewPassword { get; init; } = string.Empty;

    public string ConfirmNewPassword { get; init; } = string.Empty;

    /// <summary>Hides every password, so the DTO can be logged safely.</summary>
    public override string ToString() =>
        $"{nameof(ChangePasswordDto)} {{ {nameof(CurrentPassword)} = {AuthRedaction.Mask}, " +
        $"{nameof(NewPassword)} = {AuthRedaction.Mask}, {nameof(ConfirmNewPassword)} = {AuthRedaction.Mask} }}";
}
