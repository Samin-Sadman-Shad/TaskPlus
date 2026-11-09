namespace SmartTask.Domain.Common;

/// <summary>
/// Marks an entity as soft-deletable. The persistence interceptor turns deletes into updates of
/// these members, and a global query filter hides deleted rows.
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; }

    DateTimeOffset? DeletedAt { get; }

    Guid? DeletedBy { get; }
}
