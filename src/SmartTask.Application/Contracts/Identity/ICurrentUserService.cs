using SmartTask.Domain.Enums;

namespace SmartTask.Application.Contracts.Identity;

/// <summary>The user making the current request, read from the access token by the API layer.</summary>
public interface ICurrentUserService
{
    /// <summary>The user's id, or <c>null</c> for anonymous requests.</summary>
    Guid? UserId { get; }

    /// <summary>The user's role, or <c>null</c> for anonymous requests.</summary>
    UserRole? Role { get; }

    /// <summary>The client's IP address (stored on issued refresh tokens), or <c>null</c> if unknown.</summary>
    string? IpAddress { get; }

    bool IsAuthenticated { get; }
}
