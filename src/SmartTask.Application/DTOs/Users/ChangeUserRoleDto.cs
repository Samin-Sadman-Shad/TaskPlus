using SmartTask.Domain.Enums;

namespace SmartTask.Application.DTOs.Users;

/// <summary>Request body for <c>PATCH users/{id}/role</c> (admin only). The user id comes from the route.</summary>
public sealed record ChangeUserRoleDto
{
    public UserRole Role { get; init; }
}
