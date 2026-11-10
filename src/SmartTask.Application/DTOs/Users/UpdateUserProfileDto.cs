namespace SmartTask.Application.DTOs.Users;

/// <summary>Request body for <c>PUT users/me</c>. Email and role cannot be changed here.</summary>
public sealed record UpdateUserProfileDto
{
    public string FirstName { get; init; } = string.Empty;

    public string LastName { get; init; } = string.Empty;
}
