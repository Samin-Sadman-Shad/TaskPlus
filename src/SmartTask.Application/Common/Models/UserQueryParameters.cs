using SmartTask.Domain.Enums;

namespace SmartTask.Application.Common.Models;

/// <summary>Filters for the admin user list. Default sort: <c>createdAt desc</c>.</summary>
public sealed record UserQueryParameters : PageQuery
{
    /// <summary>Sort keys the repository accepts (case-insensitive). Validators reuse this set.</summary>
    public static readonly IReadOnlySet<string> AllowedSortFields = SortFieldSet(
        SortFields.Name, SortFields.Email, SortFields.CreatedAt);

    public UserRole? Role { get; init; }

    public bool? IsActive { get; init; }

    public static class SortFields
    {
        public const string Name = "name";
        public const string Email = "email";
        public const string CreatedAt = "createdAt";
        public const string Default = CreatedAt;
    }
}
