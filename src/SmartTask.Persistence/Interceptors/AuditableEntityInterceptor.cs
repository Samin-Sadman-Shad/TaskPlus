using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SmartTask.Application.Contracts.Identity;
using SmartTask.Domain.Common;

namespace SmartTask.Persistence.Interceptors;

/// <summary>
/// Fills in the audit columns of <see cref="AuditableEntity"/>: <c>CreatedAt/CreatedBy</c> on insert
/// and <c>UpdatedAt/UpdatedBy</c> on update. The properties have private setters, so they are written
/// through the change tracker.
/// </summary>
internal sealed class AuditableEntityInterceptor(TimeProvider timeProvider, ICurrentUserService currentUser)
    : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Apply(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        var userId = currentUser.UserId;

        foreach (var entry in context.ChangeTracker.Entries<AuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Property(nameof(AuditableEntity.CreatedAt)).CurrentValue = now;
                    entry.Property(nameof(AuditableEntity.CreatedBy)).CurrentValue = userId;
                    break;

                case EntityState.Modified:
                    entry.Property(nameof(AuditableEntity.UpdatedAt)).CurrentValue = now;
                    entry.Property(nameof(AuditableEntity.UpdatedBy)).CurrentValue = userId;
                    entry.Property(nameof(AuditableEntity.CreatedAt)).IsModified = false;
                    entry.Property(nameof(AuditableEntity.CreatedBy)).IsModified = false;
                    break;
            }
        }
    }
}
