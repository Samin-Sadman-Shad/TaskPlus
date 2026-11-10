using SmartTask.Domain.Entities;

namespace SmartTask.Application.DTOs.Users;

/// <summary>One-way mapping from <see cref="User"/> to the user read DTOs.</summary>
/// <example>Paged list: <c>pagedUsers.Map(u =&gt; u.ToDto())</c>.</example>
public static class UserMappings
{
    public static UserDto ToDto(this User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return new UserDto
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            FullName = user.FullName,
            Email = user.Email,
            Role = user.Role,
            IsActive = user.IsActive,
            LastLoginAt = user.LastLoginAt,
            CreatedAt = user.CreatedAt,
        };
    }

    public static UserSummaryDto ToSummaryDto(this User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return new UserSummaryDto(user.Id, user.FullName, user.Email, user.Role);
    }
}
