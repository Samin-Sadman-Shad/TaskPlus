using SmartTask.Application.DTOs.Auth;
using SmartTask.Domain.Enums;

namespace SmartTask.Application.DTOs.Users;

/// <summary>
/// Request body for <c>POST users</c> (admin only): creates an account with any role and an initial
/// password. Unlike <see cref="RegisterRequestDto"/> it carries a role, which is why only an admin may
/// send it. <see cref="Role"/> has no default: a missing role fails validation instead of silently
/// becoming <c>TeamMember</c>.
/// </summary>
public sealed record CreateUserDto : IPersonName
{
    public string FirstName { get; init; } = string.Empty;

    public string LastName { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public string ConfirmPassword { get; init; } = string.Empty;

    public UserRole Role { get; init; }

    /// <summary>Hides both passwords, so the DTO can be logged safely.</summary>
    public override string ToString() =>
        $"{nameof(CreateUserDto)} {{ {nameof(FirstName)} = {FirstName}, {nameof(LastName)} = {LastName}, " +
        $"{nameof(Email)} = {Email}, {nameof(Password)} = {AuthRedaction.Mask}, {nameof(ConfirmPassword)} = {AuthRedaction.Mask}, {nameof(Role)} = {Role} }}";
}
