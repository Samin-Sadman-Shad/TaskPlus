namespace SmartTask.Domain.Common;

/// <summary>
/// Base type for all entities with a single <see cref="Guid"/> identity.
/// Ids are version 7 GUIDs (time-ordered) so clustered indexes stay index-friendly.
/// </summary>
public abstract class BaseEntity
{
    protected BaseEntity()
    {
        Id = Guid.CreateVersion7();
    }

    public Guid Id { get; protected set; }
}
