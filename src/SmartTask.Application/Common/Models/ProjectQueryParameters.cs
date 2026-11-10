namespace SmartTask.Application.Common.Models;

/// <summary>Filters for the project list. Default sort: <c>createdAt desc</c>.</summary>
public sealed record ProjectQueryParameters : PageQuery
{
    /// <summary>Sort keys the repository accepts (case-insensitive). Validators reuse this set.</summary>
    public static readonly IReadOnlySet<string> AllowedSortFields = SortFieldSet(
        SortFields.Name, SortFields.StartDate, SortFields.EndDate, SortFields.CreatedAt);

    public Guid? OwnerId { get; init; }

    public DateOnly? StartDateFrom { get; init; }

    public DateOnly? StartDateTo { get; init; }

    public static class SortFields
    {
        public const string Name = "name";
        public const string StartDate = "startDate";
        public const string EndDate = "endDate";
        public const string CreatedAt = "createdAt";
        public const string Default = CreatedAt;
    }
}
