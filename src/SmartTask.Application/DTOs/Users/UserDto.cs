using SmartTask.Domain.Enums;

namespace SmartTask.Application.DTOs.Users;

/// <summary>
/// User details. Returned by <c>GET users/me</c>, <c>PUT users/me</c>, <c>GET users</c>,
/// <c>GET users/{id}</c>, <c>PATCH users/{id}/role</c> and <c>POST auth/register</c>, and nested in
/// <see cref="Auth.AuthResponseDto"/>.
/// </summary>
public sealed record UserDto
{
    public required Guid Id { get; init; }

    public required string FirstName { get; init; }

    public required string LastName { get; init; }

    public required string FullName { get; init; }

    public required string Email { get; init; }

    public required UserRole Role { get; init; }

    public required bool IsActive { get; init; }

    public DateTimeOffset? LastLoginAt { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
