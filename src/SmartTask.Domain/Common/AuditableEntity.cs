namespace SmartTask.Domain.Common;

/// <summary>
/// Entity with audit columns. The values are written by the persistence interceptor through
/// EF Core's change tracker, never by domain or application code.
/// </summary>
public abstract class AuditableEntity : BaseEntity
{
    public DateTimeOffset CreatedAt { get; private set; }

    public Guid? CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }
}
