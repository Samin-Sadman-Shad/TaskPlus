namespace SmartTask.Application.DTOs.Auth;

/// <summary>
/// Request body for <c>POST auth/register</c>. There is deliberately no role field: self-registered
/// users are always <c>TeamMember</c>, and only an admin can change roles.
/// </summary>
public sealed record RegisterRequestDto
{
    public string FirstName { get; init; } = string.Empty;

    public string LastName { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public string ConfirmPassword { get; init; } = string.Empty;

    /// <summary>Hides both passwords, so the DTO can be logged safely.</summary>
    public override string ToString() =>
        $"{nameof(RegisterRequestDto)} {{ {nameof(FirstName)} = {FirstName}, {nameof(LastName)} = {LastName}, " +
        $"{nameof(Email)} = {Email}, {nameof(Password)} = {AuthRedaction.Mask}, {nameof(ConfirmPassword)} = {AuthRedaction.Mask} }}";
}
